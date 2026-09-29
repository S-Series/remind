# Chart Core 구조 개편

> 문서 상태: 보관 — 2026-08 구조 개편 설계 및 당시 체크리스트
> 최초 작성: 2026-08-25  
> 목적: 에디터, Preview, 테스트 플레이, 실제 Gameplay가 동일한 채보 해석 결과를 사용하도록 공용 Chart Core의 경계와 이전 절차를 고정한다.

## 현행 대응 — 2026-09-29

현재 이전 상태는 [MIGRATION.md](MIGRATION.md), 목표 경계는
[ARCHITECTURE.md](ARCHITECTURE.md), 작업 순서는 [ROADMAP.md](ROADMAP.md)를 따른다.
아래 Phase 0~7은 이 문서의 옛 단계이며 전역 ROADMAP Phase나 Effect 1~7단계와 다르다.

- Game 판정은 이미 Snapshot과 `PlayableJudgementSession`을 직접 사용한다.
- NoteType/Scratch 규칙, Runtime Package와 codec, 공용 표시 assembly 분리는 반영됐다.
- 편집 원본은 현재 `ChartHolder`이며 `ChartDocument`는 컴파일을 위한 투영이다.
  아래 “ChartDocument가 저장·Undo의 직접 원본” 설명은 목표 설계다.
- `NoteTrajectory`, `TimingTransform`, `JudgementEngine` 등의 이름은 설계 용어다.
  이름을 맞추려고 같은 책임의 새 시스템을 만들지 않는다.
- 시간·보정 부호·확정 판정 창은 [RhythmSystem.md](RhythmSystem.md)를 따른다.
  당시 D-004/D-005를 그대로 미정 상태로 다시 적용하지 않는다.
- 현행 좌표는 4800 units/measure, 양수 Line Speed이며 저장 형식은
  [ChartFormat.md](ChartFormat.md)의 단일 `.rd` 형식 버전 1이다.
- 기존 체크박스는 당시 상태로 보존한다. 미완료 표시만으로 새 작업을 배정하지 않는다.

## 보관된 설계

이하에서 “현재”, “확정”, “결정 필요”는 2026-08 설계 시점을 가리킨다.

## 1. 구조 개편을 진행하는 이유

현재 에디터의 `ChartHolder`, 테스트 플레이의 `ChartTestPlay`, 실제 게임의
`NoteJudgementSystem`이 각자 채보 데이터를 해석한다. 같은 노트라도 시스템마다
다르게 동작할 가능성이 있으며, 다음 계산이 여러 위치에 분산될 수 있다.

- BPM과 박자에 따른 노트 시간 계산
- Timing Group과 스크롤 속도에 따른 화면 위치 계산
- Scratch의 이동 경로 계산
- Long Note의 중간 Tick 생성
- 오프셋과 판정 오차 계산
- 채보 파일을 런타임 데이터로 변환하는 규칙

이 구조에서는 에디터 Preview에서는 정상인 노트가 테스트 플레이에서 다른 위치에
표시되거나, 실제 게임에서만 판정 Tick이 다르게 생성될 수 있다. 기능을 추가할
때도 같은 규칙을 각 시스템에 반복해서 구현해야 한다.

이번 개편의 핵심 이유는 **채보를 해석하는 규칙을 하나로 통합하고, 에디터,
테스트 플레이, 실제 게임이 동일한 컴파일 결과를 사용하게 만드는 것**이다.

## 2. 최종 목표

> 하나의 `ChartDocument`를 공용 컴파일러로 변환하고, 에디터 Preview, 테스트
> 플레이, 실제 Gameplay가 동일한 `PlayableChartSnapshot`을 사용한다.

```text
파일 DTO
  -> 버전 변환 / 역직렬화
  -> ChartDocument
  -> ChartCompiler + Validation
  -> PlayableChartSnapshot
       |- Editor Preview
       |- Chart Test Play
       `- Gameplay
```

`PlayableChartSnapshot`은 Unity Scene이나 GameObject에 의존하지 않는다. Snapshot을
소비하는 각 환경은 표시, 입력, 오디오, 이펙트만 담당한다.

## 3. 핵심 목표

### 3.1 에디터와 게임의 결과 일치

같은 `ChartDocument`를 컴파일했을 때 다음 항목은 모든 환경에서 동일해야 한다.

- 노트의 판정 시각
- 노트의 화면상 위치
- Long/Scratch의 경로
- 중간 판정 Tick의 개수와 시각
- Timing Group과 스크롤 변화
- 판정 결과와 오프셋 처리

에디터에서 확인한 결과가 테스트 플레이와 실제 게임에서도 그대로 재현되어야
한다.

### 3.2 편집 상태와 플레이 상태 분리

`ChartDocument`는 다음과 같은 편집 중간 상태를 표현할 수 있는 mutable 모델이다.

- 끝점이 아직 없는 Long Note
- 연결되지 않은 Scratch 구간
- 시간 순서가 잠시 뒤바뀐 노트
- Undo/Redo를 위한 중간 상태
- 선택 및 편집용 메타데이터

`PlayableChartSnapshot`은 다음 조건을 만족하는 immutable 모델이다.

- 모든 필수 필드 검증 완료
- 노트와 이벤트 정렬 완료
- Long/Scratch 연결 완료
- 판정 시각과 중간 Tick 계산 완료
- TimingMap, ScrollMap, CameraMotionMap, NoteTrajectory 생성 완료
- GameObject, MonoBehaviour, Renderer 참조 없음

유효하지 않은 `ChartDocument`는 편집할 수 있지만 Snapshot으로 컴파일할 수 없다.

### 3.3 시간과 위치의 의미 분리

Arcaea형 리듬게임에서는 판정 시각과 화면 위치가 항상 같은 비율로 진행되지
않는다. 정지, 역주행, 가속 같은 표시 연출이 들어가더라도 판정 시각은 독립적으로
유지될 수 있어야 한다.

```text
MusicalPosition
  <-> TimingMap
  <-> ChartTime

ChartTime
  <-> ScrollMap
  <-> FloorPosition

DspTime
  -> PlaybackClock
  -> SongTimeMs
  -> Offset 적용
  -> ChartTime
```

| 개념 | 의미 | 판정 사용 여부 |
|---|---|:---:|
| `MusicalPosition` | 마디, 박자, pulse상의 편집 위치 | 간접 사용 |
| `ChartTime` | 채보 내부의 논리적 판정 시각 | O |
| `FloorPosition` | 화면상의 진행 위치 | X |
| `DspTime` | 변경하지 않는 오디오 원본 시계 | 간접 사용 |
| `SongTimeMs` | DSP 기준점으로부터 도출한 현재 곡 시각 | O |

규칙:

- BPM과 박자표는 `MusicalPosition <-> ChartTime` 변환에만 관여한다.
- 스크롤 속도와 Timing Group의 표시 규칙은 `ChartTime <-> FloorPosition`에
  관여한다.
- 노트 판정은 `FloorPosition`이나 Unity Transform을 사용하지 않는다.
- 에디터 입력은 `FloorPosition -> ChartTime` 역변환을 사용할 수 있다.
- 오프셋 부호와 적용 순서는 공용 `TimingTransform` 한 곳에서만 정의한다.

#### 확정된 TimingPoint 규칙

ChartMaker의 편집 원본은 정수 `Position`이다. 컴파일러는 각 BPM 변경점에 다음
불변 기준점을 생성한다.

```text
TimingPoint
  - StartTimeMs
  - StartPosition
  - Bpm
```

다음 TimingPoint의 시간은 이전 구간으로부터 한 번만 계산한다.

```text
Next.StartTimeMs = Current.StartTimeMs
  + PosToMs(Next.StartPosition - Current.StartPosition, Current.Bpm)
```

노트의 판정 시각은 노트가 포함된 구간의 기준점으로부터 컴파일한다.

```text
NoteTimeMs = Point.StartTimeMs
  + PosToMs(Note.Position - Point.StartPosition, Point.Bpm)
```

플레이 중 DSP 값은 수정하거나 BPM에 맞춰 보정하지 않는다.

```text
SongTimeMs = ClockOriginSongTimeMs
  + (DspTime - ClockOriginDspTime) * 1000

CameraFloorPosition = ScrollPoint.StartFloorPosition
  + (ChartTimeMs - ScrollPoint.StartTimeMs)
  * ScrollPoint.FloorUnitsPerMs

ScrollPoint.FloorUnitsPerMs
  = MsToPosition(1ms, ScrollPoint.Bpm)
  * FloorUnitsPerPosition
  * ScrollPoint.LineSpeed
```

카메라에는 이전 프레임 이동량이나 BPM 보정량을 누적하지 않는다. 같은
`SongTimeMs`는 탐색, 프레임 드롭, 재계산 여부와 관계없이 항상 같은 절대
`FloorPosition`을 반환해야 한다.

`ScrollMap`은 BPM 경계와 Line Speed 경계를 합친 별도 절대 구간을 사용한다.
`LineSpeed = 1`이 기본이며 현재 확정 범위에서는 양수 유한값만 허용한다. Line
Speed는 `FloorPosition`만 바꾸며 `TimingMap`, 노트의 `NoteTimeMs`, 판정 윈도우에는
관여하지 않는다.

따라서 `BPM 240 × LineSpeed 0.5`와 `BPM 120 × LineSpeed 1`은 같은 시간 동안
같은 화면 거리를 이동한다. 다만 같은 MusicalPosition의 노트 시각은 전자가 두 배
빠르게 컴파일되므로 두 설정의 채보 판정 시각까지 같다는 뜻은 아니다.

시간 판정 윈도우가 예를 들어 `±30ms`라면 Line Speed가 빨라질수록 그 30ms 동안
움직이는 화면 거리가 커져 시각적인 판정 범위가 넓어 보인다. 논리 판정식은 계속
`InputSongTimeMs - UserOffsetMs - NoteTimeMs`이므로 판정 결과는 동일하다.

정지(`0`), 역주행(음수), 독립 Timing Group은 FloorPosition의 역변환과 Long 표시
방향을 함께 결정해야 하므로 아직 지원하지 않는다.

#### 확정된 Camera Note 규칙

Camera Note는 판정 Target이 아니라 표시 전용 `CameraMotionMap` 이벤트다. 이벤트
시점의 라인 중심 X에 Note의 값을 더해 새로운 회전 기준 X로 사용한다.

```text
CameraReferenceX = LineCenterXAtEvent + CameraNote.OffsetX
SpinDurationMs = 175 * 120 / EventBpm
```

- `N`: 회전 없음
- `L`: 반시계 방향 `+360도`
- `R`: 시계 방향 `-360도`
- 기준 X에 따른 기본 Roll: `-CameraReferenceX * 0.6도`
- 회전 진행: EaseInOut

에디터의 Camera Note Edit에서는 공통 `Measure`/`Pos`와 함께 `Offset X`,
`Spin(L/N/R)`을 수정한다. 적용·이동·삭제와 Undo/Redo는 Camera 이벤트 데이터와
세 개의 편집/Preview 표시 오브젝트를 하나의 편집 단위로 처리한다.

Camera 상태는 프레임 이동량을 누적하지 않고 현재 `ChartTimeMs`에서 절대 평가한다.
따라서 탐색하거나 프레임을 건너뛰어도 같은 시각에는 같은 기준 X와 회전각을
반환한다.

### 3.4 Unity 오브젝트와 핵심 로직 분리

다음 로직은 `GameObject`, `MonoBehaviour`, Renderer에 의존하지 않는다.

- 시간 및 위치 변환
- 채보 검증과 컴파일
- Long/Scratch 연결
- NoteTrajectory 계산
- 판정 Target과 중간 Tick 생성
- 입력과 판정 Target 매칭
- 점수와 판정 규칙

이를 통해 다음을 가능하게 한다.

- Unity Play Mode 없이 순수 C# 테스트
- 에디터 Preview와 Gameplay의 재사용
- 렌더링 및 오브젝트 풀 방식과 판정 로직의 독립
- 자동 플레이와 리플레이 입력 재현
- 동일 입력과 Snapshot에 대한 결정적 결과

### 3.5 새로운 노트와 규칙의 확장

```text
편집 노트
  -> Note Compiler
  -> NoteTrajectory + JudgementPlan
  -> PlayableChartSnapshot
  -> Judgement Engine
  -> Presentation Events
```

새 노트 타입은 컴파일 규칙과 판정 계획을 추가해 확장한다. Preview, 테스트
플레이, 실제 게임이 각각 별도의 노트 해석기를 구현하지 않는다.

## 4. 변환 경계

### 4.1 파일과 도메인

```text
Native/JSON DTO
  -> Version Migrator
  -> DTO Validator
  -> ChartDocument
```

- 파일 버전과 필드 인코딩은 Infrastructure 책임이다.
- 런타임 판정 시스템은 native v1~v4나 JSON 필드명을 알지 않는다.
- 파일 저장은 `ChartDocument`를 DTO로 변환한 뒤 수행한다.

### 4.2 편집 상태와 플레이 상태

```text
ChartDocument
  -> ChartCompiler
  -> CompileResult
       |- Snapshot
       |- Errors
       `- Warnings
```

- 컴파일 실패는 예외적인 프로그램 오류가 아니라 명시적인 오류 결과다.
- 테스트 플레이는 성공한 Snapshot이 있을 때만 시작한다.
- 컴파일 중 원본 `ChartDocument`를 변경하지 않는다.

### 4.3 노트와 판정 계획

```text
Authoring Note
  -> JudgementPlanCompiler
       |- discrete JudgementTarget
       `- continuous JudgementSegment
```

- Tap은 단일 Target으로 변환한다.
- Long 계열은 Start, 유지 구간, Tick, End 규칙으로 변환한다.
- Scratch는 경로와 판정 구간을 같은 컴파일 단계에서 생성한다.
- Air는 4개 AirMain 레인의 단일 판정 노트만 지원하며 공중 Long은 만들지 않는다.
- 자동 플레이도 같은 Target과 Segment를 소비한다.

### 4.4 입력과 공용 판정

```text
Unity Input Event
  -> InputClockMapper
  -> RhythmInputEvent
  -> JudgementEngine
  -> JudgementEvent
  -> UI / Effect / Sound / Score
```

- Unity Input 이벤트 포인터를 보관하지 않는다.
- 입력의 control, phase, 시각, sequence, value만 복사한다.
- 판정 엔진은 GameObject를 숨기거나 이펙트를 재생하지 않는다.

### 4.5 분석과 편집

```text
Audio Analyzer
  -> AnalysisSuggestion
  -> User Confirmation
  -> ChartEditCommand
  -> ChartDocument
```

- 오디오 분석기는 채보를 직접 변경하지 않는다.
- 적용된 분석 결과는 일반 편집 명령과 동일하게 Undo할 수 있어야 한다.

## 5. 데이터 소유권

| 데이터 | 쓰기 소유자 | 소비자 |
|---|---|---|
| `ChartDocument` | Authoring/Edit Service | Compiler, File Writer |
| `PlayableChartSnapshot` | ChartCompiler | Preview, Test Play, Gameplay |
| `TimingMap` | ChartCompiler | Preview, Placement, Gameplay |
| `ScrollMap` | ChartCompiler | Preview, Note Presentation |
| `CameraMotionMap` | ChartCompiler | Preview, Test Play, Camera Presentation |
| `NoteTrajectory` | Note Compiler | Preview, Presentation, Continuous Judge |
| 입력 큐 | Play Session | JudgementEngine |
| 판정 상태 | JudgementEngine | Score, Presentation, Result |
| GameObject/View | Presentation Adapter | Unity Scene |

`ChartDocument`와 Snapshot은 GameObject를 소유하지 않는다. View와 데이터의 연결은
Presentation Adapter 또는 View Registry가 별도로 관리한다.

## 6. 단계별 이전 계획

### Phase 0. 규칙과 회귀 기준 고정

- [x] 구조 개편 목적과 변환 경계를 문서화한다.
- [ ] 의미가 불명확한 규칙을 결정 목록에 기록한다.
- [ ] 현재 native 포맷 round-trip fixture를 준비한다.
- [ ] 좌표 변환과 Scratch 경로 회귀 테스트를 추가한다.

완료 조건:

- 이전 과정에서 유지해야 할 현재 동작을 자동 테스트 또는 fixture로 확인할 수
  있다.

### Phase 1. Unity 비의존 Chart Core 도입

- [x] `ChartDocument` 최소 모델
- [x] `PlayableChartSnapshot` 최소 모델
- [x] `CompileIssue`와 `CompileResult`
- [x] 순수 C# Core assembly
- [x] Core EditMode tests

완료 조건:

- Unity 오브젝트 없이 빈 채보와 기본 Tap 채보를 컴파일하고 테스트할 수 있다.

### Phase 2. 현재 에디터 데이터 어댑터

- [x] `ChartHolder -> ChartDocument` 변환
- [x] 변환 오류와 지원하지 않는 데이터 보고
- [ ] 현재 JSON 저장 round-trip 및 legacy native 읽기 회귀 테스트

완료 조건:

- 현재 에디터 채보를 원본 변경 없이 `ChartDocument`로 투영할 수 있다.

### Phase 3. 테스트 플레이 이전

- [x] 테스트 시작 시 Snapshot 컴파일
- [x] `ChartTestPlay`의 직접 `ChartHolder` 순회 제거
- [x] 공용 TimingMap과 JudgementPlan 소비
- [x] 컴파일 오류 로그 표시
- [ ] 컴파일 오류 UI 표시

완료 조건:

- `ChartTestPlay`는 `ChartHolder`의 배열 구조를 알지 않는다.

### Phase 4. Preview 이전

- [x] Preview와 테스트 플레이가 Snapshot의 TimingMap과 ScrollMap 사용
- [ ] Scratch가 공용 NoteTrajectory 사용
- [ ] 편집 중 유효하지 않은 구간의 별도 표시 정책 적용

완료 조건:

- 같은 ChartTime에서 Preview와 테스트 플레이가 같은 FloorPosition과 경로 값을
  생성한다.

### Phase 5. 실제 판정 시스템 이전

- [ ] `NoteJudgementSystem`이 JudgementTarget/Segment 사용
- [ ] 입력 이벤트 시각을 공용 PlaybackClock으로 변환
- [ ] 판정과 GameObject 비활성화 분리
- [ ] 동일 입력 재생의 결정성 테스트

완료 조건:

- 같은 Snapshot과 입력 이벤트 목록은 실행 프레임과 무관하게 같은 판정 이벤트를
  생성한다.

### Phase 6. 파일 경계 정리

- [ ] native reader/writer와 version migrator 분리
- [ ] JSON DTO와 공용 도메인 분리
- [ ] 파일 형식별 통합 fixture

완료 조건:

- 파일 형식을 변경하거나 새 형식을 추가해도 판정 엔진을 수정하지 않는다.

### Phase 7. 레거시 경로 제거

- [ ] 직접 `ChartHolder` 해석 코드 제거
- [ ] `TempLoader` 교체 또는 제거
- [ ] 중복 좌표/시간/경로 계산 제거
- [ ] 문서를 실제 구현 상태로 갱신

## 7. 완료 기준

이번 구조 개편은 다음 조건을 모두 만족해야 완료된다.

1. `ChartTestPlay`가 `ChartHolder`를 직접 해석하지 않는다.
2. 테스트 시작 시 `ChartDocument`를 `PlayableChartSnapshot`으로 컴파일한다.
3. Preview와 테스트 플레이가 같은 `TimingMap`, `ScrollMap`, `NoteTrajectory`를
   사용한다.
4. 판정 시스템은 GameObject가 아닌 `JudgementTarget`을 처리한다.
5. 같은 입력 이벤트와 Snapshot을 주면 항상 같은 판정 결과가 나온다.
6. 유효하지 않은 채보는 플레이 시작 전에 컴파일 오류로 차단된다.
7. 파일 형식이 변경되어도 런타임 판정 로직은 수정하지 않는다.
8. 화면 스크롤을 변경해도 노트 판정 시각은 변하지 않는다.

## 8. 결정이 필요한 항목

Arcaea를 롤모델로 삼되 외부 게임의 규칙을 암묵적으로 복제하지 않는다. 프로젝트의
규칙은 아래 결정 기록으로 명시한 뒤 코드와 테스트로 고정한다.

| ID | 항목 | 현재 상태 | 결정 전 영향 범위 |
|---|---|---|---|
| D-001 | 최종 입력 구조 | **확정**: 4 Main + 4 AirMain + Ground Left/Right = 10 lane. Air Long 없음 | ChartDocument, Snapshot, 입력, 판정 |
| D-002 | 편집 및 재생 시간 기준 | **확정**: 편집 원본은 Position, 컴파일 결과는 TimingPoint의 Position+Ms와 NoteTimeMs, 재생 원본 시계는 DSP에서 도출한 SongTimeMs | TimingMap, 저장 포맷, Placement |
| D-003 | Line Speed와 Timing Group | **부분 확정**: Line Speed는 양수 배율이며 BPM과 곱해 표시 기울기만 변경. 0/음수 및 Group 중첩은 결정 필요 | ScrollMap, NoteTrajectory, Placement |
| D-004 | Long/Scratch의 Tick 간격과 실패 조건 | 결정 필요 | JudgementPlan, Score |
| D-005 | Chart/User/Visual Offset의 부호 | 결정 필요 | TimingTransform, 설정 UI |
| D-006 | Camera Note | **확정**: 기준 X = 이벤트 시점 라인 X + 값. N/L/R, L=+360, R=-360, 시간=175ms*120/BPM, EaseInOut | CameraMotionMap, Preview, Test Play |

결정되지 않은 항목은 공용 API에 임시 의미로 고정하지 않는다. 해당 단계에 도달하면
구현을 중단하고 사용자에게 규칙 확정을 요청한다.

입력 레인과 현재 ChartMaker 표시 이펙트의 대응은 다음과 같다. 판정 레인은 10개로
독립적이지만 기존 표시 이펙트는 같은 물리 위치를 공유한다.

| 판정 레인 | 표시 이펙트 |
|---|---|
| Main 1~4 | Main 1~4 |
| AirMain 1~4 | Main 1~4 위치 공유 |
| Ground Left/Right | Left/Right |

## 9. 한 문장으로 정리

이번 작업의 목적은 단순히 코드를 깔끔하게 나누는 것이 아니라, **에디터에서
제작하고 확인한 채보가 테스트 플레이와 실제 게임에서 완전히 동일하게 동작하도록
채보 해석의 단일 기준을 만드는 것**이다.
