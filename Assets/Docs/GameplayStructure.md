# Gameplay Structure

> 상태: 현행 구성 안내
> 검토 기준: 2026-09-29 로컬 코드
> 장기 책임 경계는 [ARCHITECTURE.md](ARCHITECTURE.md), 이전 상태는 [MIGRATION.md](MIGRATION.md)를 따른다.

## 1. assembly 경계

| assembly / 폴더 | 책임 |
| --- | --- |
| `REmind.ChartCore` / `ChartCoreDomain` | Unity 비의존 채보 컴파일·Snapshot·시간·판정 lifecycle·Effect |
| `REmind.NoteRules` / `SharedNoteRules` | Unity 비의존 NoteType·Scratch 규칙/경로 |
| `REmind.Common` / `Common` | 공용 Unity 노트 표시·메뉴 내비게이션 등 |
| `REmind.Presentation` / `SharedPresentation` | 공용 타격 표시 `LaneHitEffectPlayer` |
| `REmind.Gameplay` / `Gameplay` | 입력·GameRule·재생·진행·씬/UI |
| `REmind.ChartMaker` / `ChartMaker` | 편집·파일·Preview·패키지 출력 |

Game과 ChartMaker assembly는 서로를 참조하지 않는다. 제품 define은
`REMIND_GAME`과 `REMIND_CHARTMAKER`이며 Editor에서는 양쪽을 사용할 수 있다.
`Assets/Editor`의 빌드·카탈로그·검사 도구가 두 제품을 연결하는 것은 런타임 의존과 구분한다.

## 2. 콘텐츠 준비 경로

```text
ChartMaker: ChartHolder → ChartHolderDocumentAdapter → ChartDocument
  → ChartCompiler → PlayableChartSnapshot → Preview
  → ChartMakerRuntimePackageExporter → .rmp.json

Game: MusicCatalog → 선택한 난이도의 RuntimePackage
  → GameplayChartPreparation → RuntimeChartPackageCodec.Import
  → 공용 컴파일·Effect 파라미터 검증
  → PreparedGameplayChart
  → GameplayChartSessionController
      ├→ NoteJudgementSystem
      ├→ GameplaySessionState
      └→ GameplayChartEffectController
```

`PreparedGameplayChart`는 Snapshot, PreparedEffectPlan, 검증된 ID와 타이밍 metadata를
가진다. Game은 `.rd`/ChartHolder를 읽지 않는다. 준비 실패와 live 세션 게시 경계를
구분하고 구성요소가 준비·활성화되지 않으면 재생을 시작하지 않는다.

`ChartLoader`·`ChartLoadService`·`NoteData`는 구형 경로에 남아 있다.
실제 Game 판정은 Snapshot을 직접 소비한다. 구형 파일끼리의 참조와 씬·프리팹의
직렬화 사용을 조사하기 전에는 “파일이 존재한다” 또는 “Game이 안 쓴다”만으로 삭제하지 않는다.

## 3. 씬과 수명

| 씬/화면 | 구성과 소유권 |
| --- | --- |
| Bootstrap | `AppRoot` 초기화, `BootstrapLoadingController` 표시 완료 후 새 입력 대기 |
| Home | 주 메뉴, `SettingsOverlay.prefab` 모달과 선택 복원 |
| MusicSelect | 카탈로그·곡/난이도 선택·미리듣기·즐겨찾기 |
| Game | 채보 준비·오디오·입력·판정·점수/체력·표시·Pause/Result 전환 |
| Result | 1회 전달된 `GameResultSnapshot`과 카탈로그·로컬 진행 표시 |
| ChartMaker | 독립 제작 제품의 편집/Preview 씬 |
| 테스트 DemoPlay | `Assets/Tests/Fixtures/Scenes/DemoPlay.unity`의 회귀 하네스 |

`AppRoot`와 자식 전환 표시기는 DontDestroyOnLoad 수명을 가진다. 곡 선택,
설정/기록 저장소, 결과 전달은 루트가 소유하고 Game 씬의 판정·Effect·음원 세션은
씬 종료 때 정리한다. Result는 `TryTakeResult`로 결과를 한 번 소비한다.

일반 씬 이동은 `SceneTransitionController`의 투명 Intro/Loop/Outro와 어두운 덮개를,
MusicSelect→Game은 추가 흰 결정 연출을 사용한다. 설정 모달은 씬 이동이 아니다.
전환 편집은 [CrystalTransition.md](CrystalTransition.md)에 설명한다.

## 4. 실제 플레이 구성요소

| 구성요소 | 책임 |
| --- | --- |
| `GameManager` | GamePlay/GameRule 참조와 시작·정지 요청 연결 |
| `GamePlay` + `DspSongClock` | AudioClip 예약 재생, DSP 기반 곡 시간, Pause/Resume/Restart |
| `RhythmInputRouter` | 10레인 Input System press/release와 이벤트 시각 수집 |
| `NoteJudgementSystem` | 입력 큐·Effect·자동 판정의 시간 순서, Game 규칙 adapter, 판정 이벤트/등록 View 연결 |
| `PlayableJudgementSession` | Unity 비의존 단일 노트·Long 구간 상태와 결과 발생 |
| `GameRule` / `DefaultGameRule` | 판정 창·modifier, 점수·체력·콤보·클리어/실패 규칙 |
| `GameplaySessionState` | 한 플레이의 집계 상태와 Effect용 상태 제공 |
| `GameplayChartEffectController` | 준비된 Effect 계획과 Game capability의 세션 연결·정리 |
| `DemoPlayController` | 현행 Game에서도 쓰는 노트/카메라 표시 및 개발 하네스 기능 |
| `GameFlowController` | 로딩·선택·플레이·Pause·오류 화면, 결과 확정·씬 이동 |
| `ResultScenePresenter` / `ResultMenuActions` | 결과 표시와 재시도·곡 선택 요청 |

`Gameplay/Demo` 폴더에는 현행 제품 코드가 있다. 이름만으로 폴더 전체를 레거시로
분류하지 않는다. 현재 `GameManager`의 Singleton 참조와 일부 fallback 탐색,
판정 adapter의 View 등록은 남아 있는 결합이다. 공용 판정 코어는 Unity View를 모른다.

## 5. 입력·Effect·판정 순서

`NoteJudgementSystem.LateUpdate`가 현재 chart time까지 다음 이벤트를 병합한다.
같은 시각에는 Effect order, 입력 sequence, 자동 판정/Miss 순서다. 활성 Effect와
곡 기믹은 프레임 시각으로 한 번 갱신하고 프레임 완료 경계에서 전환 요청을 처리한다.
서로 다른 시각의 이벤트는 시간순이며 미래 규칙을 과거 입력에 소급하지 않는다.

`GamePlay`의 입력 시간 변환과 사용자 보정 부호, 구간별 판정 창은
[RhythmSystem.md](RhythmSystem.md)에 둔다. UI/Presenter에서 재계산하지 않는다.
노트마다 판정용 Update를 두지 않으며 성능은 실제 밀집 채보에서 별도로 계측한다.

## 6. 데이터 소유권

| 데이터 | 쓰기 책임 |
| --- | --- |
| 곡·난이도 콘텐츠 metadata | `data.json` 및 제작 파이프라인 |
| 노트·타이밍·Effect 제작 데이터 | ChartMaker `.rd`와 편집 상태 |
| Snapshot / Effect 계획 | 공용 컴파일·준비 경계 |
| 입력 큐 | Game의 `NoteJudgementSystem` |
| 노트/구간 판정 상태 | `PlayableJudgementSession` |
| 점수·콤보·체력 | `GameplaySessionState`가 GameRule 결과를 반영 |
| 판정 통계·최대 콤보와 결과 확정 | `GameFlowController` → `GameResultSnapshot` |
| 영구 진행·즐겨찾기 | `AppRoot`가 소유한 `LocalPlayerDataStore` |
| 사용자 음량·판정 보정·키 | `AppRoot`가 소유한 `LocalGameSettingsStore` |
| 씬 전환 상태 / 표시 | `SceneTransitionController` / `CrystalTransitionPlayer` |

Resume은 세션을 유지하고 Restart는 성공한 시작 경계에서 판정·Effect·집계를
초기화/교체한다. 이전 generation의 callback/cleanup이 새 세션을 변경하지 않아야 한다.
준비 실패, 취소, 비활성화, 예외의 정리도 검증 대상이다.

## 7. 표시와 UI

Game 메뉴는 uGUI, ChartMaker 편집 메뉴/팝업은 UI Toolkit을 사용한다.
`MenuNavigationController`는 Scope/Node와 모달 Push/Pop으로 선택 범위를 관리하고
이동·Submit은 Input System UI 모듈/Selectable에 맡긴다.

노트의 chart time·floor position·카메라 수학은 Shared 계산을 소비하며,
lane X·prefab·Transform·Animator와 표시 효과는 각 제품이 연결한다.
`LaneHitEffectPlayer`는 공용 표시만 맡고 Game 판정 이벤트 연결은
`GameplayChartSessionController`가 담당한다.

초기 문서의 LaneField·GameplayRunner·ScoreSystem·ResultBuilder 등의 명칭은
설계 예시였다. 현재 구현에 같은 이름의 새 시스템을 추가해야 한다는 요구가 아니다.
노트 View 전체의 풀링·가시 범위 최적화가 완료됐다고 가정하지 않는다.

## 8. 검사와 후속 작업

- Core/EditMode: 컴파일·시간/스크롤·판정·Effect 경계.
- EffectIntegration와 `REmindBaselineChecks`: 실제 파일 왕복, Preview/Game 대조,
  준비 실패와 씬 구성. DemoPlay 검사는 테스트 fixture를 사용한다.
- Gameplay/EditMode: 진행과 설정 저장·검증·복구/이행.
- `PlayableGameSmokeRunner`: Bootstrap·설정·두 곡·결과·재시도 자동 흐름.
- `RuntimeProductBuilds`: Play/Chart 프로필과 상대 제품 assembly 제외 경계.
- 수동/하드웨어: 화면·실제 입력·청음·포커스 정책·성능·배포 환경.

검사 코드가 존재하는 것과 최신 원본에서 통과한 것은 다르다.
실행 결과는 [TASKS.md](TASKS.md), 작업 배분·완료 기준은 [ROADMAP.md](ROADMAP.md)를 따른다.
