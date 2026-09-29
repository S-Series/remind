# Effect / MusicGimmick 장기 작업 기록

> 상태: 보관 — 2026-09-07~09 Effect 작업과 검증 이력
> 현행 문서 연결 검토: 2026-09-29

이 문서는 당시 구현·수정·수동 수용 기록을 보존한다. 아래 “현재/최신/다음”과
테스트 개수, sidecar·NoteData·Game→ChartMaker 의존 설명은 기록 당시의 상태다.
현행 단일 `.rd`·Runtime Package·공용 판정·독립 빌드 기준으로 읽지 않는다.

최신 진행은 [TASKS.md](TASKS.md), 이전 상태는 [MIGRATION.md](MIGRATION.md),
현행 재개 안내는 [EffectGimmickHandoff.md](EffectGimmickHandoff.md)를 따른다.
당시 출처인 개인 Downloads 문서는 이 역사 기록의 참고이며 현재 작업의 필수 의존이 아니다.
아래 검증 이력은 이번 문서 검토에서 재실행하지 않았다.

## 단계 현황

| 단계 | 범위 | 상태 |
| --- | --- | --- |
| 1 | 기존 테스트 기준선과 회귀 대상 확보 | 완료 |
| 2 | 저장 소유권, revision, Undo/Redo, 부분 저장 복구 | 완료 |
| 3 | 에디터와 재생 준비의 설정 검증 통합 | 완료 |
| 4 | 실행 시각, 동일 시각 순서, 세션 수명과 정리 | 완료 |
| 5 | ChartMaker 배치→설정→저장→재열기→Preview 전체 흐름 | 완료 |
| 6 | `DemoPlay` 통합 하네스에서 Snapshot·판정·규칙·카메라 연결 | 완료 |
| 7 | 문서·샘플·최종 회귀 검사 | 완료 |

## 완료된 핵심 작업

### 1~3단계

- `.rd`와 Effect JSON의 revision·소유 관계를 검사해 다른 채보의 설정 덮어쓰기를 차단했다.
- 디스크에 저장된 revision과 Undo/Redo되는 편집 메타데이터를 분리했다.
- 두 파일 중 하나만 교체되는 실패를 재현하고 backup pair 복구 또는 명확한 오류 경로를 만들었다.
- JSON 구조/타입 검사와 Effect별 필수 값·범위 검사를 공통화했다.
- 종류가 없거나 현재 등록되지 않은 기존 Effect는 편집·보존할 수 있지만 재생 준비에서는 거부한다.

### 4단계 — 완료

- 지연 프레임에서도 Effect를 예약 시각 순으로 한 번씩 실행하고, 활성 Effect/Gimmick은 실제 프레임 시각으로 한 번만 갱신한다.
- 동일 시각 순서를 `Effect 명시 order → 입력 도착 sequence → 자동 판정/Miss`로 고정했다.
- 시간 역행, 같은 시각 중복 update, runner 재진입, 실행/정리 복합 실패를 검사한다.
- 판정 프레임 시작 시점의 runner 세대를 전달해 이전 세션의 완료 콜백이 새 세션을 건드리지 않게 했다.
- Play/Resume/Restart 준비·활성화·취소 경계를 분리하고, Resume은 기존 세션을 유지하며 Restart는 새 세션으로 교체한다.
- 준비 중인 규칙·카메라 출력은 실제 세션에 노출하지 않고, 폐기된 세션의 핸들이 새 세션을 변경하지 못하게 했다.
- Preview 종료·중단 시 runner, 규칙, 카메라, 전환 mailbox와 이벤트 구독을 정리한다.

### 5단계 — 자동 수용 검증 완료

- 새 Effect를 배치하면 배치 도구를 끝내고 생성된 Effect를 즉시 선택해 설정 창을 바로 연다.
- 저장 성공 시 `.rd` 경로를 최근 문서로 기억하며, 저장→문서 초기화→재열기→컴파일→Effect 준비→Preview 시작/종료 전체 왕복을 실제 ChartMaker 구성 요소로 검사한다.
- 현재 sidecar의 구조는 맞지만 등록된 값 범위가 잘못된 경우에도 유효한 backup pair로 복구한다. backup도 없으면 열린 채보·메타데이터·BPM·보정·저장 대상·최근 파일을 전혀 바꾸지 않는다.
- Preview 중에는 배치·선택·노트 편집·삭제·Undo/Redo·새 문서·열기·저장·BPM/보정·음악 교체를 차단한다. 종료 시 임시 실행 상태를 정리한 뒤 다시 편집할 수 있다.
- Preview 준비/시작 실패와 비동기 음악 로드 실패를 상단 상태에 표시하고, Chart Data 팝업에서 시작한 음악 로드 실패는 팝업에도 표시한다.
- Effect JSON 검증·복사·재로드의 성공 안내는 오류와 다른 색으로 표시한다.
- 실제 `ChartMaker.unity`의 Effect 프리팹, 선택기, 저장/로드, TopMenu, Preview, UXML 필드와 호출 순서를 독립 씬 수용 검사 3개로 고정했다.

### 6단계 — `DemoPlay` 통합 하네스 자동 연결 및 수용 검증 완료

- 과도기 `GameplayChartPreparation`이 `.rd`와 matching Effect JSON을 같은 소유 정보·revision으로 검사하고, ChartMaker와 같은 `ChartDocument → PlayableChartSnapshot` 경로를 사용한다. 최종 Game loader가 아니라 번들 샘플용 호환 adapter다.
- Snapshot의 노트를 기존 `NoteJudgementSystem` 입력으로 변환하고, ChartMaker의 음악 시작 보정을 동일한 부호 규칙으로 적용한다. 실행 중에는 파일이나 JSON을 다시 읽지 않는다.
- `GameplayChartSessionController`가 완전히 검증된 Snapshot·노트·설정을 기존 판정, 세션 상태, Effect 컨트롤러에 함께 전달한다. 번들 샘플은 chart `musicId`와 실제 `AudioClip`도 명시적으로 묶어 잘못된 음원 조합을 막는다.
- 재생 시작 guard는 채보 존재뿐 아니라 `GameManager`, `GamePlay`, 판정, 실제 세션 상태, Effect 컨트롤러가 모두 활성·준비됐는지 확인한다. 어느 하나가 비활성화되거나 준비가 해제되면 불완전한 세션을 시작하지 않는다.
- `GameplaySessionState`가 실제 `GameRule`을 사용해 체력·콤보·점수·실패·클리어를 갱신하고 `IEffectGameState`로 곡 기믹에 현재 체력을 제공한다. Resume은 상태를 유지하고 새 Play/Restart는 성공한 시작 경계에서 초기화한다.
- Snapshot의 소수 밀리초 노트 시각을 `NoteData`에 그대로 보존하고 판정 큐가 그 값을 사용한다. 같은 chart 위치의 Effect와 노트가 정수 반올림으로 갈라지지 않으며 동일 시각 Effect 우선 계약을 유지한다.
- 판정 이벤트에 실제 평가 시각을 전달해 지연 입력·자동 Miss의 체력/점수 계산에도 그 시각의 Rule Modifier가 적용된다.
- `DemoPlay.unity`의 카메라를 `Camera Base Motion → Camera Effect Pivot → Main Camera`로 분리했다. 기본 채보 이동과 Effect 카메라 출력이 서로의 Transform을 덮어쓰지 않는다.
- 개발 샘플 `EffectGameplaySample.rd`와 `effect.effect_gameplay_sample.demo.json`을 연결했다. 공용 Camera Effect 하나와 같은 세션 상태를 공유하는 sample MusicGimmick 명령 3개를 포함한다.
- `.rd`를 복제 파일 없이 Player 직렬화 가능한 `TextAsset`으로 가져오는 전용 importer와, 씬 연결을 재현하는 Editor 메뉴를 추가했다. 메뉴는 다른 열린 씬의 미저장 변경을 먼저 확인하고 생성/참조 변경을 Undo로 되돌릴 수 있게 한다.
- Effect 카메라의 Attack/Hold/Release를 공용 절대 chart time smooth-step으로 계산한다. 카메라 합성, Preview 테스트 상태/규칙 handle, 전환 mailbox도 Unity 비의존 Shared Core로 이동했고 ChartMaker와 Demo 하네스는 표시 adapter만 가진다.
- `GameplayChartEffectController`는 이제 `PreparedEffectPlan`과 식별자만 받는다. 다만 최종 독립성은 아직 아니며, 번들 파일 준비 adapter의 Game→ChartMaker 저장 의존과 `ChartTestPlay`의 `LaneHitEffectPlayer` 표현 의존이 남아 있다.

## 당시 완료 상태 (2026-09-09)

첫 사용자 수동 확인에서 `DemoPlay` 카메라의 트리거·이동·복귀는 작동했지만 움직임이 순간적으로 바뀌는 결함이 발견됐다. 원인은 공용 `CameraEffect`가 지속시간 내내 고정 pose를 즉시 적용/해제하던 것이며, Shared absolute-time smooth-step Attack/Hold/Release로 수정했다.

수정 뒤 사용자가 Preview/DemoPlay 실행을 다시 확인했다. 초기 안전 구간, 노트 처리, 부드러운 카메라 이동·회전·복귀, Pause/Resume, Reset/Restart가 모두 정상이고 Console에도 별도 오류가 없음을 확인했다. 샘플 pair는 revision `stage6_demo_004`이며 Camera는 2000ms 중 Attack 500ms, Hold 1000ms, Release 500ms다. 7단계 사용/확장/JSON/구조 문서, 최신 전체 Unity EditMode **75/75**, 솔루션 빌드 경고 0·오류 0, 정적 sample pair 검증도 완료했다.

`DemoPlay`는 Build Settings/Home에서 들어가는 최종 Player Gameplay가 아니다. 최종 씬, 곡 선택·로딩·씬 전환과 독립 Game/ChartMaker build는 후속 범위로 남기며 이번 Effect 7단계 완료를 그 범위의 완료로 과장하지 않는다.

## 당시 마지막 확인 결과

- 최신 전체 실행: Unity 6000.3.15f1 EditMode **75/75 성공**, 실패 0, 건너뜀 0.
- 결과: `Logs/EffectBaselineResults/20260909-190759-5157b189/results.xml`
- 로그: `Logs/EffectBaselineResults/20260909-190759-5157b189/unity.log`
- 최신 Camera/Shared 분리 직후 Core EditMode **36/36 성공**: `Logs/EffectBaselineResults/20260909-184650-7ba5a584/results.xml`. 이후 Camera mixer 중첩 검사 하나를 더해 전체 실행의 Core 대상은 37개다.
- 최신 `dotnet build remind.slnx`: 경고 0, 오류 0. `.rd`/정의 위치·revision `stage6_demo_004`·1000ms 초과와 Camera 500/1000/500ms envelope 정적 검증도 통과했다.
- Unity 로그에서 C# 컴파일 오류, 실패 테스트, 처리되지 않은 예외가 발견되지 않았다.
- 2026-09-08 수동 확인 산출물 `Assets/Chart/chart.rd`와 `Assets/Chart/effect.untitled.default.json`은 musicId=`untitled`, difficultyId=`default`, revision=`e11177f93356444e9647c06aaa8ff3cb`로 일치한다.
- 최종 저장 Effect는 `camera.offset`이며 JSON 설정도 유효하다. 수동 테스트 중 먼저 발생한 `EFFECT_TYPE` 로그는 타입 적용·저장 전 Preview 시도의 기록이고 최종 파일의 타입 유실이 아니다.
- no-type 안내 보완 후 솔루션 빌드와 원본 프로젝트 전체 Unity 회귀가 모두 통과했다. 앞서 실패한 임시 프로젝트 검사는 라이선스 채널 충돌 때문이었고 임시 복제본은 정리했다.

## 당시 후속 작업 시작 지점 — 현행 상태는 MIGRATION 참조

Effect 작업 1~7단계는 완료됐다. 다음 작업은 현재 범위를 자동으로 확대하지 않는다.

1. 최종 Game scene/composition root와 곡 선택·로딩·씬 전환 범위를 결정한다.
2. Shared Runtime Package 입력 경계로 과도기 `GameplayChartPreparation` 의존을 제거한다.
3. `LaneHitEffectPlayer`의 공용 표시와 Gameplay 판정 구독 책임을 분리한다.
4. 현행 Long/Scratch를 보존하면서 판정과 Note 데이터의 장기 Shared 경계를 정한다.
5. Game/ChartMaker 별도 assembly와 Build Profile로 독립 빌드를 검증한다.

## 진행 로그

### 2026-09-09 20:42 KST — 6·7단계 최종 수동 수용 완료

- 사용자가 요청된 수동 항목 전체가 정상 작동하고 Console에도 별도 오류가 없음을 확인했다.
- 초기 1초 안전 구간, 두 Tap, 500/1000/500ms smooth camera, Pause/Resume, Reset/Restart와 세션 정리를 수동 수용 완료로 기록했다.
- 자동 Unity EditMode 75/75, 빌드 경고 0·오류 0, sample pair 정적 검증과 문서화를 합쳐 Effect 작업 1~7단계를 최종 완료 처리했다.
- `DemoPlay`는 계속 과도기 통합 하네스이며, 최종 Game 씬/Player 흐름과 독립 빌드는 별도 후속 migration이다.

### 2026-09-09 18:47 KST — 공용 카메라 보간·세션 경계 및 7단계 문서 보완

- 사용자 수동 결과를 “카메라 트리거·좌측 이동·종료 복귀 확인, 부드러움 실패”로 기록했다. 확인하지 않은 오디오·두 Tap·Pause/Resume·Reset/Restart·Console 상태를 통과로 추정하지 않는다.
- `CameraEffectParameters`에 `attackMs`와 `releaseMs`를 추가하고 공용 absolute-time smooth-step envelope를 적용했다. 구 JSON은 두 값이 없으면 0/0 즉시형으로 보존하고 새 Camera 설정은 100/100ms를 기본으로 쓴다.
- 샘플은 duration 2000ms, attack/release 각 500ms와 revision `stage6_demo_004`로 갱신했다.
- 카메라 합성, Preview rule handle, 테스트 상태, transition mailbox를 `REmind.ChartCore`로 이동했다. ChartMaker와 Demo 하네스가 같은 계산을 소비하고 Transform 적용만 각각 담당한다.
- `GameplayChartEffectController`에서 ChartMaker holder/model 입력을 제거하고 검증된 `PreparedEffectPlan`만 받게 했다. 남은 직접 의존과 제거 조건은 `MIGRATION.md`에 기록했다.
- `EffectGimmickGuide.md`를 추가하고 `ARCHITECTURE.md`, `MIGRATION.md`, `ROADMAP.md`, `TASKS.md`, `ChartFormat.md`를 실제 v8/sidecar/세션 경계에 맞게 갱신했다.
- 공용 Camera 곡선의 비선형 1/4 지점, X/Y/roll, 지연 프레임 종료, 중도 취소, 중첩 합성과 JSON 구버전/경계/default 검사를 보강했다.
- 첫 전체 실행은 의도적 null 준비 실패 로그의 줄바꿈을 예상식이 끝까지 매칭하지 못해 74/75였다. 제품 오류가 아니었으며 검증을 약화하지 않고 첫 오류 문장에 안정적으로 맞춘 뒤 재실행했다.
- 최종 전체 Unity EditMode는 **75/75 성공**(`Logs/EffectBaselineResults/20260909-190759-5157b189/results.xml`), 빌드는 경고 0·오류 0, 정적 sample pair 검사도 통과했다.
- Unity 배치 종료가 `Temp`의 생성된 NuGet assets를 정리해 직후 `--no-restore` 확인만 실패했으나, 일반 restore 포함 빌드를 다시 실행해 경고 0·오류 0을 최종 확인했다.

### 2026-09-09 13:32 KST — 최초 로드용 1초 안전 구간 확보

- 최초 로드 직후의 준비 지연과 샘플 이벤트가 겹치지 않도록 모든 샘플 노트와 Effect를 `+6000` chart unit(225 BPM에서 약 1333.333ms)만큼 함께 이동했다.
- 가장 이른 노트는 약 1333.333ms, 가장 이른 Effect는 1600ms이며 Camera Effect는 약 1733.333ms부터 2초 동안 유지된다. 상대적인 순서와 간격은 그대로다.
- `.rd` Effect 행과 정의 위치를 각각 `7200, 7800, 8400, 9600`으로 맞추고 당시 pair revision을 `stage6_demo_003`으로 함께 갱신했다. 현재 smooth camera 샘플은 `stage6_demo_004`다.
- 씬 수용 검사에 모든 샘플 노트와 Effect가 1000ms를 초과한다는 조건을 추가했다. 같은 위치 Effect→노트 소수 시각 회귀 검사도 새 1866.667ms 위치를 사용한다.
- 정적 pair 검증에서 행 위치, Effect 정의 대응, 고유 ID, order, 양쪽 revision, 모든 실행 시각의 1000ms 초과를 확인했다. 솔루션 빌드는 경고 0·오류 0이며 전체 Unity 73개 재실행은 열린 Editor 종료 후 진행한다.

### 2026-09-09 13:23 KST — Play Mode 구형 로더 오류와 카메라 수동 확인 보완

- 사용자 Play Mode 로그를 확인해 비활성 `TempLoader`도 `Awake`가 호출되고, 삭제된 구형 TextAsset 참조가 null로 풀려 빨간 오류를 만든다는 원인을 확정했다.
- `DemoPlay`에서 더 이상 쓰지 않는 `Chart Provider/TempLoader`를 완전히 제거했다. 씬 재연결 메뉴도 남아 있는 구형 로더를 Undo 가능한 방식으로 제거하며, legacy loader의 초기 로드는 `Awake`가 아니라 활성 상태에서만 호출되는 `Start`에서 수행한다.
- Console의 Error Pause가 이 오류에서 멈추면 `DemoPlayController.Start`와 Effect 세션이 시작되지 않아 카메라도 움직이지 않는다. 실제 로그에는 그 외 Gameplay/Effect 오류가 없었고 카메라 계층과 참조는 정상이다.
- 수동 확인 중 놓치지 않도록 샘플 Camera Effect를 곡 시각 약 0.4초에 시작해 2초 유지하고 offset/roll도 더 분명하게 조정했다. 씬 수용 검사는 구형 로더 0개와 관찰 가능한 샘플 시작/지속 시간을 고정한다.
- 첫 타이밍 수정에서 Camera 정의만 옮겨 발생한 `no matching Effect row`를 즉시 바로잡아, 0 위치의 첫 Tap은 일반 행으로 남기고 1800 위치에 전용 Effect 행을 추가했다. 현재 Effect 행과 정의 위치는 `1200, 1800, 2400, 3600`, ID는 4/4 고유하고 pair revision은 `stage6_demo_002`로 일치한다.
- 보완 후 솔루션 빌드는 경고 0·오류 0이다. Unity Editor가 열려 있어 전체 73개 회귀 재실행은 Editor 종료 후 수행한다.

### 2026-09-09 13:08 KST — 불완전한 Gameplay 구성 시작 차단

- 마지막 수명주기 교차 검토에서 Effect·판정·세션 상태 구성 요소가 비활성화돼도 chart만 남아 있으면 시작 guard가 통과할 수 있는 틈을 찾았다.
- Effect의 실제 준비 상태와 세션 상태의 이벤트 연결 상태를 공개하고, chart 시작 guard가 전체 구성 요소의 활성·초기화·준비 상태를 함께 확인하도록 보완했다.
- 판정, 실제 상태, Effect 중 하나라도 비활성화되면 시작을 거부하는 `GameplayChartSession_DisabledServiceRejectsStart` 수용 검사를 추가했다.
- 솔루션 빌드는 경고 0·오류 0, Unity 6000.3.15f1 EditMode는 **73/73 통과**했다. 결과는 `Logs/EffectBaselineResults/20260909-130845-c274a8b8/`에 보존했다.

### 2026-09-09 08:07 KST — 6단계 `DemoPlay` 통합 하네스 자동 연결·검증

- `.rd`/sidecar를 공용 Snapshot으로 준비해 기존 판정 큐와 `GameplayChartEffectController`에 함께 전달하는 `GameplayChartPreparation`, `GameplayChartSessionController`를 추가했다.
- 실제 `GameRule` 기반 체력·콤보·점수·실패·클리어 상태인 `GameplaySessionState`를 만들고, MusicGimmick의 `IEffectGameState` 입력으로 연결했다.
- 판정 이벤트의 평가 시각을 상태 계산에 전달해 지연 프레임에서도 이후 규칙이 과거 판정에 소급되지 않게 했다.
- Snapshot 노트의 소수 밀리초 시각을 보존해 같은 위치의 Effect·노트가 반올림으로 재정렬되지 않게 했다. 샘플의 533.333…ms 위치도 실제 판정 루프에서 Effect→노트 순서임을 검사한다.
- 채보 교체가 live service 변경을 시작한 뒤 실패하면 CurrentChart, 기존 Effect plan, 판정 초기화, 게임 상태를 함께 폐기한다. 준비되지 않은 상태의 Play도 시작 guard가 거부한다.
- `DemoPlay`를 공용 Snapshot, 실제 상태, 전용 Effect 카메라 pivot에 연결하고 개발용 `.rd`/JSON 샘플을 저장했다. chart `musicId`와 실제 `I.mp3` AudioClip도 번들 설정으로 묶었다.
- 씬 설정 메뉴는 열린 씬의 미저장 변경 확인과 Undo를 지원하도록 보완했다.
- 새 수용 검사는 pair 소유권·보정·소수 시각 순서, 실패한 교체의 전체 폐기, 실제 상태/Resume/Restart, DemoPlay Snapshot·음원·서비스·카메라 연결을 고정한다.
- 당시 솔루션 빌드는 경고 0·오류 0, Unity EditMode는 **72/72 통과**했다. 이후 비활성 구성요소 시작 차단 검사까지 추가한 최신 결과는 위 13:08 항목의 73/73이다.
- 자동화로 확인하지 않은 것은 실제 Play 화면의 오디오/DSP, 물리 입력, 짧은 카메라 움직임이다. `DemoPlay`의 Build Settings/Home 진입 연결도 큰 곡 선택 흐름과 함께 별도 결정한다.

### 2026-09-08 19:13 KST — no-type 보완 전체 회귀 통과, 5단계 완료

- 사용자가 원본 Unity를 닫은 뒤 공식 전체 검사 스크립트를 원본 프로젝트에서 다시 실행했다.
- Unity 6000.3.15f1 EditMode **68/68 통과**, 실패 0, 건너뜀 0이다.
- 결과는 `Logs/EffectBaselineResults/20260908-191253-250a100a/results.xml`, 상세 로그는 같은 폴더의 `unity.log`에 보존했다.
- 타입 미지정 Effect의 편집·보존 가능/재생 불가 규칙, 자동 선택과 설정 안내, 빨간 오류 억제를 회귀 대상으로 고정했다.
- 자동 수용과 실제 화면 저장 흐름 확인까지 충족했으므로 5단계를 완료로 확정했다. 당시 다음 작업으로 부른 “Gameplay 연결”은 이후 최종 씬이 아닌 `DemoPlay` 통합 하네스 연결로 재분류했다.

### 2026-09-08 18:49 KST — 5단계 수동 결과 검토 및 no-type UX 보완

- 사용자가 실제 배치·설정·저장 흐름의 정상 작동과 파일 생성을 확인했다.
- 저장된 `.rd`/sidecar의 소유 정보, revision, effectId가 일치하며 `.rd.bak`이 기존 원본을 보존하는 것을 확인했다.
- 빨간 `EFFECT_TYPE` 스택은 타입이 최종 파일에서 사라진 것이 아니라 타입 지정 전 Preview를 눌렀을 때 `LogCompileIssues`가 사용자 수정 가능 상태를 오류로 기록한 것이었다.
- 미지정 Effect를 Preview 컴파일 전 감지하고 해당 노트를 선택해 `Effect Type`과 `Apply`를 안내하도록 바꿨다. 재생 차단 규칙은 유지하되 Console error와 빨간 상단 상태는 만들지 않는다.
- 회귀 검사 `ChartPreview_UnresolvedEffectUsesSetupGuidance`를 추가했다. 솔루션 빌드는 경고 0, 오류 0이다.
- 열린 원본 Editor를 건드리지 않기 위해 별도 임시 프로젝트에서 전체 검사를 시도했지만 Unity 라이선스 채널 충돌로 테스트 실행 전에 중단했다. 임시 복제본은 삭제했으며 실패 로그는 `Logs/EffectBaselineResults/20260908-184939-c1853a4a-shadow/unity.log`에 남겼다.

### 2026-09-08 13:33 KST — 5단계 자동 수용 검증 완료

- 저장→초기화→재열기→컴파일/Effect 준비→Preview 전체 왕복 검사와 semantic-invalid sidecar 복구/무변경 검사를 추가했다.
- 실제 ChartMaker 씬 및 UXML 연결 검사 3개를 추가했고 모두 통과했다.
- 새 Effect 배치 직후 설정 창 진입, Preview 중 문서/타이밍/음악 변경 잠금, 상단 상태 및 음악 로드 실패 안내, 성공 메시지 색상 구분을 보완했다.
- 최종 솔루션 빌드는 경고 0, 오류 0이며 Unity EditMode는 **67/67 통과**했다.
- Unity 네이티브 창을 현재 자동화 환경에서 직접 조작할 수 없어 포인터/키보드 기반 화면 QA 1회만 남겼다.

### 2026-09-07 — 5단계 시작

- 장기 작업 기록을 5단계 진행 중으로 전환했다.
- ChartMaker UI/씬 연결, 저장·재열기·Preview 경로, 사용자 채보와 분리된 샘플 검증 방법을 병렬로 감사하기 시작했다.
- 실제 화면 검증과 자동 수용 검사를 분리해, UI 결함과 저장/실행 로직 결함을 각각 재현 가능한 형태로 남긴다.

### 2026-09-07 16:00 KST — 4단계 최종 보완 구현

- `GamePlay`에 `PlaybackCommitting` 경계와 시작 재진입 가드를 추가했다. 준비 후 커밋이 실패하면 시작 호출은 실패를 반환하고 준비/임시 활성 세션을 되돌린다.
- Resume의 논리 시작 시각을 sample 반올림값보다 뒤로 보내지 않도록 했다.
- `GameplayChartEffectController`는 비활성화 시 구독을 먼저 해제하고, 커밋이 성공한 세션만 재생 상태 공개 전에 활성화한다.
- 정상 종료 중 발생한 runner cleanup 실패를 세션 정리 실패로 승격하고, 정리 실패 시 곡 전환을 실행하지 않는다.
- Preview는 Effect와 AutoTestNote를 시간순으로 병합하며 동일 시각에는 Effect를 먼저 실행한다.
- 통합 검사 `Gameplay_CommitRejectionBlocksSchedulingAndReentrancy`를 추가했고, 기존 Resume/Restart 검사를 새 트랜잭션 경계에 맞게 갱신했다.
- 솔루션 빌드: 성공(경고 0, 오류 0). Unity 전체 검사는 열린 Editor 때문에 아직 실행하지 못했다.

### 2026-09-07 16:15 KST — 교차 검토 후 추가 보완

- 실패한 `Prepare`가 이전 또는 부분 검증된 실행 계획을 남기지 않게 했고, 실패 직후 시작이 거부되는 회귀 검사를 추가했다.
- Effect 콜백 중 `Dispose`가 재진입하면 콜백 종료까지 실제 정리를 지연하고, 남은 같은 프레임 작업을 중단하며 호출이 실패를 반환하게 했다.
- 비활성화 정리 실패도 `LastError`와 `PlaybackFailed` 경로로 보고한다.
- Preview에서 이전 세션 정리가 실패하면 새 Preview 세션 생성을 중단한다.
- Core 회귀 검사 `ReentrantDispose_IsDeferredAndStopsTheCurrentFrame`을 추가했다. 현재 전체 대상은 60개다.
- 최신 솔루션 빌드: 성공(경고 0, 오류 0). Unity 전체 재실행은 열린 Editor 종료 대기 중이다.

### 2026-09-07 21:03 KST — 4단계 최종 검증 완료

- Preview 시작/종료에도 시도 ID와 활성 세션 ID를 도입해 중첩 시작, 시작 중 취소, 이전 중단 콜백이 새 세션을 정리하는 문제를 차단했다.
- Preview 종료 상태 알림 중 새 시작을 거부하고, 모든 구독자에게 같은 상태 값을 전달하며 수동 종료와 자연 종료의 시간/상태 순서를 보존했다.
- Effect 콜백이 Preview를 즉시 종료해 snapshot과 runner가 정리돼도 이전 프레임 처리를 계속하지 않게 했다.
- 통합 검사 `ChartPreview_StartStopTransactionsAreReentrancySafe`, `ChartPreview_ReentrantEffectStopDoesNotUseClearedSession`을 추가했다.
- 최종 `dotnet build`: 성공(경고 0, 오류 0).
- Unity 6000.3.15f1 전체 EditMode: **62/62 성공, 실패 0, 건너뜀 0**.
- 다음 작업 위치는 5단계 ChartMaker 전체 사용 흐름 검증이다.

## 작업 경계와 주의사항

- `DemoPlay` Editor 씬 연결과 Player Build Settings/Home 진입 흐름을 같은 완료 항목으로 과장하지 않는다. 후자는 아직 연결하지 않았다.
- PlayMode 실제 오디오/DSP, 화면, 물리 입력 검증은 EditMode 회귀 검사와 별도다.
- 기존 Long Tap / Scratch 판정 규칙은 이 작업에서 재설계하지 않는다.
- 작업 트리에 이번 작업 이전의 수정·삭제·미추적 파일이 많다. 관련 없는 사용자 변경을 되돌리거나 정리하지 않는다.
- 자동 commit/push는 하지 않는다.
