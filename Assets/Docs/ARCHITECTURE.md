# ReMind Architecture

> 상태: Living Target  
> 이 문서는 ReMind의 **장기 목표 아키텍처**를 정의한다.  
> 현재 코드 상태와 마이그레이션 세부사항은 `MIGRATION.md`에서 관리한다.

## 1. Architecture Goal

ReMind Game과 ReMind ChartMaker는 독립 Application이다.

둘은 서로 직접 의존하지 않고, 동일한 의미가 필요한 시스템을 Shared 영역을 통해 공유하는 구조를 지향한다.

```text
                    Shared Systems
                   /             \
                  /               \
          ReMind Game       ReMind ChartMaker
```

## 2. Main Boundaries

| 영역 | 책임 | 알면 안 되는 것 |
| --- | --- | --- |
| Shared Domain / Core | Chart 의미, 검증, 시간/위치 변환, 불변 Runtime 데이터, Effect·카메라·판정의 공통 규칙 | Unity Scene, Editor UI, 입력 장치, 계정/저장 진행 |
| Game Runtime | 실제 입력, GameRule 연결, 세션 진행, 콘텐츠/씬/오디오 구성 | ChartMaker UI와 편집 저장 모델 |
| ChartMaker Runtime | 편집, 저장/복구, Preview/Test Play, 제작자용 오류 표시 | Game의 계정, 해금, Player 전용 흐름 |
| Presentation | 공용 계산 결과를 Transform, Animator, UI, Audio에 표시 | 핵심 시간·판정·Effect 규칙의 재구현 |
| Data / Infrastructure | 파일 I/O, importer, 경로와 버전 관리, 앱별 로딩 | 실행 중 mutable session state |

Shared는 두 앱에서 우연히 재사용할 수 있는 모든 코드를 뜻하지 않는다. 두 앱에서
**같은 의미와 결과를 가져야 하는 계약**만 공용화한다. 앱별 표시기는 공용 결과를
소비하는 adapter로 둔다.

## 3. Chart Data Flow

장기 개념 목표:

```text
Stored Chart Data
       ↓
Load / Validation
       ↓
Editable Representation (ChartMaker only)
       ↓
Compile
       ↓
Validated Runtime Package
       ↓
       ├─ ChartMaker Preview / Test
       └─ ReMind Game
```

현재 클래스명을 장기 계약으로 확정하지 않는다.

핵심 목표는 Game과 ChartMaker가 채보의 의미와 실행 규칙을 중복 구현하지 않는 것이다.

## 4. Shared Systems

현재 공용 경계로 검증된 책임:

- `ChartDocument` → `PlayableChartSnapshot` 컴파일과 기본 검증
- `TimingMap`, `ScrollMap`, `CameraMotionMap`의 chart time/position 계산
- Effect 정의, registry, parameter 의미 검증, `PreparedEffectPlan`
- 예약 시각 기반 `EffectRunner`와 세션 취소/정리 계약
- Camera Effect의 절대시간 easing과 additive offset 합성
- Preview용 제한된 상태·규칙 handle·전환 mailbox 계약

공용 후보지만 아직 물리적 경계가 완성되지 않은 책임:

- Note/Long/Scratch 의미와 완전한 Runtime Chart 모델
- 입력과 무관한 판정 시간 경계 및 결과 규칙
- GameRule modifier의 공통 의미
- Camera Note와 Scratch 카메라가 최종 Game에서도 가져야 할 정확한 표시 의미

후보를 곧바로 새 구현으로 만들지 않는다. 현재 ChartMaker와 Game 양쪽의 실제 사용
관계를 조사하고 Source of Truth를 정한 뒤 이동한다.

## 5. Application-specific Systems

### Game

- 실제 입력 장치와 입력 routing
- 공용 Runtime package를 세션에 게시하는 composition root
- 실제 `GameRule`, 체력·점수·콤보·실패/클리어 상태 adapter
- Game 전용 카메라/노트/이펙트/오디오 표시
- 곡 선택, 콘텐츠 로딩, 씬 전환, 결과와 영구 진행

### ChartMaker

- `ChartHolder` 기반 편집 상태, 배치/선택 UI와 Undo/Redo
- `.rd`와 Effect sidecar 저장, backup/복구, 최근 파일
- 설정 panel과 제작자용 validation 안내
- Preview용 Transform/Animator/Audio 표시와 명시적인 테스트 상태

## 6. Runtime Session

파일과 편집 모델은 재생 준비 경계에서 검증된 불변 Runtime package로 변환한다.
실행 중에는 파일이나 JSON을 다시 읽지 않는다. Effect 실행부는 최소한 다음만 받는다.

- 불변 Snapshot과 `PreparedEffectPlan`
- music/difficulty 같은 검증된 식별자
- 공용 chart time
- 카메라, 현재 상태, 규칙, 전환을 위한 제한된 session capability

준비와 live 세션 게시를 분리한다. 준비가 완전히 성공하기 전에는 기존 세션의 카메라,
규칙, 판정 상태를 바꾸지 않는다. Resume은 같은 세션을 유지하고, Play/Restart는
준비·commit이 성공한 뒤 새 세대를 게시한다.

## 7. Time / Judgement

모든 실행 의미는 프레임 수가 아니라 공용 chart time을 기준으로 한다. 예약 시각과
늦은 프레임의 현재 처리 시각을 별도로 보존한다. 한 프레임에서 여러 경계를 지나면:

1. Effect를 예약 시각과 명시 order 순으로 실행한다.
2. 입력을 원래 평가 시각과 도착 sequence 순으로 처리한다.
3. 자동 판정/Miss를 처리한다.
4. 활성 Effect와 곡 기믹을 현재 프레임 시각으로 한 번 갱신한다.

시각 T의 규칙 변경은 T 이전 입력에 소급하지 않는다. 같은 시각에는 Effect가 먼저이며,
정확한 Miss 마감 시각에서는 기존 판정 계약대로 입력을 먼저 처리한다. 시간 역행과
같은 시각 중복 update를 허용하지 않는다.

노트 배치의 Y/시간/scroll 계산은 Snapshot의 Timing/Scroll 결과를 공유한다. X 위치,
prefab, Animator와 물리 입력은 앱별 표현이다. 전체 판정 엔진의 공용화는 Long/Scratch
현행 규칙을 함께 분류한 뒤 진행하며, 부분적인 두 번째 판정 엔진을 만들지 않는다.

## 8. Effect / MusicGimmick

Effect 정의는 실행 시각·order·stable ID·종류·명령만 가진다. 난이도별 sidecar는
수치만, 복잡한 조건과 상태 전이는 C#만 소유한다.

`EffectRunner`는 공용 Effect와 `CallMusicGimmickEffect`를 한 목록에서 처리한다.
한 세션의 명령은 같은 MusicGimmick 인스턴스를 공유하며, 명령 호출 종료와 곡 기믹
상태 종료를 구분한다. 실패한 부작용 명령은 자동 재실행하지 않는다.

카메라 움직임의 envelope와 합성은 Shared에서 절대 chart time으로 계산한다.
ChartMaker와 Game은 계산 결과를 각자의 Transform 계층에만 적용한다. 곡 전환은
Effect 안에서 Scene을 즉시 바꾸지 않고 요청만 제출하며 앱의 프레임 완료 경계에서
처리한다.

## 9. Ownership / Lifetime

- Effect, MusicGimmick, cancellation, camera offset, rule handle, transition request는
  한 재생 세션이 소유한다.
- Effect 종료는 자신이 만든 handle만 해제한다. 겹친 다른 Effect의 출력을 초기화하지
  않는다.
- 이전 세대의 지연 callback이나 Dispose가 새 세대의 presenter를 변경할 수 없어야 한다.
- 실행·취소·예외·컴포넌트 비활성화 모두 같은 멱등 cleanup 경로를 사용한다.
- mutable runtime state를 static singleton이나 설정 asset에 저장하지 않는다.
- Preview capability는 영구 진행이나 실제 Scene/Audio 교체 권한을 갖지 않는다.

## 10. Dependency Rules

목표 의존 방향은 `Game → Shared ← ChartMaker`다. Shared는 Unity 비의존 순수 C#을
우선하며 두 앱의 구체 타입을 참조하지 않는다. Game과 ChartMaker 사이의 직접 타입
참조는 허용된 최종 구조가 아니라 제거 조건이 적힌 migration adapter로만 둔다.

현재 `REmind.ChartCore`만 별도 `noEngineReferences` assembly로 검증되어 있다.
Game/ChartMaker assembly와 독립 Build Profile은 아직 완성되지 않았으므로 폴더 위치만으로
독립성이 보장된다고 보지 않는다. 현재 직접 의존과 제거 순서는 `MIGRATION.md`에서,
작업 우선순위는 `TASKS.md`에서 관리한다.
