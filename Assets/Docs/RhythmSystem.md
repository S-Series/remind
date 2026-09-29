# Rhythm System

> 상태: 현행 코드의 시간·입력·판정 계약
> 검토 기준: 2026-09-29, Unity 6000.3.15f1
> 구현 변경 시 관련 공용·Game 테스트와 이 문서를 함께 갱신한다.

## 1. 적용 범위와 원본

시간·노트 구간 lifecycle은 `REmind.ChartCore`, 노트/Scratch 의미는
`REmind.NoteRules`가 소유한다. Game의 `NoteJudgementSystem`은 입력·Effect 순서와
`GameRule`을 공용 `PlayableJudgementSession`에 연결한다.
ChartMaker의 자동 Preview는 같은 Snapshot의 target을 사용한다.

판정 시간 창은 `NoteJudgeWindowProfile`과 `GameRuleConfig`를 확인한다.
이 문서는 기본 규칙을 설명하며 RuleModifier가 적용된 결과를 별도 고정값으로
중복 구현하지 않는다. 테스트 통과 이력과 실제 플레이 검수는 [TASKS.md](TASKS.md)에 둔다.

## 2. 레인과 입력

| 판정 레인 | 의미 | 기본 키 | Input System 경로 |
| --- | --- | --- | --- |
| 0~3 | Main 1~4 | Z / X / C / V | `<Keyboard>/z`, `x`, `c`, `v` |
| 4~7 | AirMain 1~4 | M / , / . / / | `<Keyboard>/m`, `comma`, `period`, `slash` |
| 8~9 | Ground Left/Right (Scratch) | Left Shift / Right Shift | `<Keyboard>/leftShift`, `rightShift` |

표의 생략된 경로도 모두 `<Keyboard>/` 접두사를 사용한다.
AirMain에는 단일 판정 노트만 두고 Air Long을 지원하지 않는다.
키 바인딩은 채보가 아니라 Game의 `LocalGameSettingsStore`에 저장한다.
중복 키 배정을 거부하고 Game 진입 시 입력 액션의 런타임 복제본에 적용한다.
ChartMaker 키 설정에는 영향을 주지 않는다.

`RhythmInputRouter`는 `Rhythm/Lane01~Lane10`의 performed/canceled에서 레인,
원래 `context.time`, press/release를 복사한다. `NoteJudgementSystem`이 sequence를
부여하고 chart time·sequence 순서로 정렬한다. 콜백 수신 프레임 시각으로 대체하지 않는다.
아래에서 **양입력은 누름(press), 음입력은 뗌(release)**을 뜻하며 두 키 동시 입력을
뜻하지 않는다.

## 3. 시간과 편집 좌표

- DSP 절대 시각과 Input System 이벤트 시각은 초 단위 `double`이다.
- 곡 시간 `SongTimeMs`, 채보 시간 `ChartTimeMs`, 판정 오차는 밀리초 단위 `double`이다.
- 편집 위치는 한 마디 4800 정수 units다. `TimingMap`이 BPM 구간별 chart time을
  컴파일하며 소수 밀리초를 보존한다. `.rd` 노트를 정수 `timeMs`로 반올림해 저장하지 않는다.
- `ScrollMap`의 Line Speed는 표시용 FloorPosition만 변경한다. BPM은 편집 위치의
  판정 시각을 바꾸므로 BPM 변경과 Line Speed 변경은 같은 의미가 아니다.

`GamePlay`는 `DspSongClock`을 사용한다.

```text
SongTimeMs = OriginSongTimeMs + (dspTime - OriginDspTime) * 1000
```

새 Play/Restart 및 Resume 예약마다 DSP/Input System 시계 차이를 기록하고
`AudioSource.PlayScheduled`를 호출한다. 코드의 예약 여유 기본값은 0.2초이며
직렬화 설정이 우선한다. 시작 예약 시각에 도달하기 전 실제 입력은 받지 않는다.
BPM이나 화면 프레임 이동량으로 DSP 시계를 수정하지 않는다.

## 4. 보정값의 부호

```text
chartOffsetMs = -musicStartCorrectionMs
inputDspTime = inputEventTime + InputTimeToDspOffset
inputSongTimeMs = DspSongClock.SongTimeMsAt(inputDspTime)
inputChartTimeMs = inputSongTimeMs - chartOffsetMs
deltaMs = inputChartTimeMs - userOffsetMs - targetChartTimeMs
```

| 값 | 소유자 | 양수의 의미 |
| --- | --- | --- |
| `musicStartCorrectionMs` | 제작용 `.rd` | 같은 song time에서 chart time을 앞쪽으로 진행시킨다. 노트의 대응 오디오 시각은 빨라진다. |
| `chartOffsetMs` | 내보낸 Runtime Package | `song = chart + offset`. 노트의 대응 오디오 시각이 늦어진다. |
| `userOffsetMs` | Game 사용자 설정 | 계산에서 입력 시각을 그만큼 빼므로 입력을 더 이르게 평가한다. |
| Visual Offset | 후속 요구 | 현행 Game 설정으로 구현됐다고 간주하지 않는다. 추가하더라도 판정과 분리한다. |

예: 목표 chart time 1000ms, chartOffset 0, 입력 song time 1020ms,
사용자 보정 +20ms이면 delta는 0이다. delta가 음수면 Early, 양수면 Late다.
설정 UI의 판정 보정은 -200~+200ms이며 5ms 단위로 조절한다.
이전 초안의 “양수 User Offset은 입력을 늦춘다”는 설명은 현행 코드와 반대다.

## 5. 판정 창과 후보 선택

기본 Game 설정은 `JudgeWindows(50, 50, 100, 100)`이다.

| 대상 | 입력 허용 창 | 결과 |
| --- | --- | --- |
| 일반 단일 노트, Hold 지점, Long Scratch Start | 절대 오차 ≤50ms | Perfect |
| 같은 대상 | 50ms < 절대 오차 ≤100ms | Good |
| Long Scratch Mid의 합류 입력 | 절대 오차 ≤100ms | Perfect |
| Long Scratch End의 release | 절대 오차 ≤75ms | Perfect |

Great는 결과 enum과 규칙 확장에 남아 있지만 기본 창에서 Perfect와 경계가 같아
독립 구간이 없다. Bad 등급은 현행 enum에 없다. 기본 창 밖의 입력은 해당 지점을
소비하지 않는다. 여기서 “간접 판정”은 같은 레인의 50ms 밖~100ms Good 입력을 뜻한다.

단일 노트 미입력은 target + userOffset + MissWindow를 **넘은** 시각에 Miss다.
Long 마지막 End 미입력도 해당 End 창을 넘으면 Miss다. 코드에서는 strictly outside를
표현하기 위해 0.000001ms를 더한다. 정확한 마감 시각에는 입력이 먼저 처리된다.

공용 세션은 Snapshot의 노트 순서(시작 시각, ID)로 같은 레인의 유효 후보를 찾는다.
현행 알고리즘을 “절대 오차가 가장 작은 노트를 검색”하는 것으로 설명하지 않는다.
하나의 press는 최초로 소비된 노트/합류 지점에서 처리를 끝낸다. 같은 레인·같은 시각의
중복 시작은 컴파일러가 거부한다. 모든 구간 중첩 조합의 정책이 확정됐다는 뜻은 아니다.

## 6. Long / Scratch lifecycle

단일 Tap·Scratch·Air는 해당 레인의 press로 처리한다. 공용 Long은 순서 있는
Start/Mid/End 지점과 그 사이 구간을 가진다. Long Tap(Hold)은 Start/End,
Long Scratch는 0개 이상의 Mid를 포함한다.

- Start press가 유효하면 구간 유지 상태에 합류하고 입력 등급을 보관한다.
- Long Scratch Mid에서는 유지 상태로 앞 구간을 확정한다. 놓친 구간은 Miss이며
  Mid의 새 press로 이후 구간에 재합류할 수 있다.
- 마지막 End는 유효 창 안의 release로 끝낸다. 범위 밖 release는 유지 상태를 끊는다.
- 구간 결과는 다음 지점에서 발생한다. Start를 별도 점수 한 번으로 중복 집계하지 않는다.
  마지막 구간은 합류 등급과 End 등급 중 낮은 결과를 사용한다.
- Long Scratch Mid/End의 입력 등급이 Perfect여도 첫 구간에 Start의 Good이
  이어질 수 있다. 입력 등급과 최종 구간 결과를 구분한다.
- Auto Play는 같은 구간 경계를 자동 처리한다. Game은 Auto Play 결과를 진행 저장에서 제외한다.

이전 초안의 Hold “종료 90ms 전부터 자동 성공”과 “시작 실패면 모든 이후 구간 복구 불가”
규칙은 현행 구현 기준이 아니다. Scratch motion은 공용 경로/표시 데이터이며
`N/G/I/R`과 이동량의 파일 표현은 [ChartFormat.md](ChartFormat.md)에 둔다.

## 7. 한 프레임의 처리와 지연 입력

`NoteJudgementSystem.LateUpdate → ProcessFrame`은 다음 경계를 chart time으로 병합한다.

1. 가장 이른 Effect 예약 시각, 입력 시각, 자동 판정 시각을 선택한다.
2. 같은 시각은 **Effect order → 입력 sequence → 자동 판정/Miss** 순서다.
3. 대기 경계를 처리한 뒤 활성 Effect/Gimmick을 현재 프레임 시각으로 한 번 갱신한다.
4. finally의 프레임 완료 경계에서 Effect 전환 요청 등을 처리한다.

“현재까지의 모든 Effect를 먼저 실행한 뒤 과거 입력을 처리”하는 순서가 아니다.
시각 T의 규칙은 T보다 이른 입력에 소급하지 않는다. 프레임이 늦더라도 예약 시각과
실제 처리 시각을 구분한다.

다음 프레임에 늦게 전달된 입력도 원래 시각을 사용한다. 단, 그 시각 이후에 이미
Effect 또는 자동 결과가 확정됐다면 되돌리지 않고 `DiscardedLateInputCount`에 기록한다.
임의로 오래 지연된 입력을 항상 복구한다고 보장하지 않는다. 시간 역행은 오류로 보고
세션 재시작이 필요하다.

## 8. Pause / Resume / Restart와 포커스

`GamePlay.Pause`는 곡 시각을 보존하고 AudioSource를 Stop한다. Resume은 보존 위치의
샘플에서 다시 예약하며 논리 시각이 샘플 반올림 때문에 역행하지 않도록 한다.
판정·Effect 세션은 Resume에 유지되고 성공한 Play/Restart에서 초기화/교체된다.
Resume 시 실제로 놓인 키는 `BreakReleasedHolds`로 유지 상태를 끊는다.
3초 재개 카운트다운이나 모든 키 해제 대기 기능이 있다고 가정하지 않는다.

ESC Pause/Resume에는 같은 프레임 중복 전환 차단이 있다. 세션 정리와 입력 대기열
관리는 앱 연결부의 실제 lifecycle을 따른다. 재시작·비활성화·실패 시 이전 세대의
callback이 새 세션을 변경하지 않아야 한다.

현재 `AppRoot.OnApplicationFocus/OnApplicationPause`는 오디오 음소거/백그라운드
설정을 적용한다. **포커스 이탈 자동 게임 Pause와는 다르다.** 자동 Pause·장치 변경 후
복구 정책은 제품 범위 결정 및 수동 검수 항목이며 구현 완료로 기록하지 않는다.

## 9. 검증과 남은 결정

자동 검사는 TimingMapTests, PlayableJudgementSessionTests, EffectRuntimeTests,
EffectBaselineTests 및 Editor 브리지에 있다. 소수 시각, 판정 경계, 지연 입력,
동일 시각 순서, Long 구간, Resume/Restart를 유지해야 한다.

실제 10키 동시 입력, 고스팅, 청음·보정 체감, 전체 길이 재생, 포커스 전환과
하드웨어 성능은 별도 검수다. 기본 판정 창은 위와 같이 확정되어 있으며,
Visual Offset·입력 검사 화면·추가 입력 장치·포커스 정책 등의 출시 범위는
[ROADMAP.md](ROADMAP.md)의 D1에서 결정한다. 현재 검증 결과는 [TASKS.md](TASKS.md)를 따른다.
