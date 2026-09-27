# ReMind Architecture Migration

> 상태: Living Document  
> 이 문서는 현재 Repository의 과도기 구조와 구조개편 진행 상황을 기록한다.  
> 완료된 마이그레이션 내용은 계속 갱신하거나 제거할 수 있다.

## 1. Current State

ReMind는 현재 구조 개편 중이다.

`Assets/Data/Music/i/data.json`은 v2 곡 목록과 `hard` 채보 참조를 가진다.
ChartMaker의 파일 경계는 목록의 곡·난이도 ID와 경로를 검사하고, `i`의 Effect
sidecar를 원래 revision에 맞춰 복구했다. Music Select의 기존 행은 곡명·아티스트·
레벨을 이 목록에서 읽는다. Game의 현재 샘플은 여전히 별도 번들 실행 패키지를
사용하며, `i` 패키지 출력 및 곡 선택에서 플레이로 넘어가는 연결은 남아 있다.

현재 존재하는 코드의 폴더명이나 클래스명만으로 장기 Source of Truth를 판단하지 않는다.

## 2. Current Functional Reference

현재 ChartMaker가 실제 개발과 기능 구현의 주요 기준이다.

다만 ChartMaker의 내부 구현 전체를 최종 아키텍처로 간주하지 않는다.

Effect vertical slice는 ChartMaker Preview와 기존 `DemoPlay`에서 같은 실행 의미를
검증한다. `DemoPlay`는 최종 Game 씬이 아니라 레거시 구성요소를 연결한 과도기 통합
하네스다. 별도 `Game.unity`는 첫 곡의 선택·플레이·일시정지·결과·재시도
흐름을 연결한다. 이후 여러 곡, 영구 진행과 최종 UI는 별도 작업이다.

## 3. Legacy Gameplay

DemoPlay 및 기존 Gameplay의 일부는 레거시 또는 과도기 코드다.

전체를 그대로 신규 구조의 기준으로 삼지 않는다.

동시에 Gameplay 폴더 전체를 일괄 폐기하지도 않는다.

재사용 가치가 있는 알고리즘과 책임은 클래스/책임 단위로 조사한다.

## 4. Current Shared-core Direction

현재 중요한 공용화 흐름 후보:

```text
ChartMaker storage/model
        ↓
Adapter
        ↓
Editable shared chart model
        ↓
Compile
        ↓
Validated runtime chart
        ↓
Preview / Game
```

현재 구체 타입과 API가 장기적으로 확정됐다는 의미는 아니다.

### Effect vertical slice에서 완료한 이동

- `REmind.ChartCore`에 Effect 계약, registry, 준비 계획, runner와 세션 context가 있다.
- `EffectSessionServices.cs`로 Unity 비의존 `EffectCameraMixer`, Preview 상태/규칙
  capability, transition mailbox를 이동했다.
- Camera Effect attack/hold/release와 Camera Note/Scratch가 쓰는 smooth-step 계산을
  공용 chart-time easing으로 통합했다.
- `GameplayChartEffectController`는 이제 `PreparedEffectPlan`과 원시 metadata만 받으며
  `ChartHolder`나 Effect JSON을 알지 않는다.
- ChartMaker Preview는 Gameplay의 실제 `EffectRuleService`를 빌려 쓰지 않고 공용
  Preview capability를 사용한다.
- 이전 `Gameplay/Effects/ChartEffectPreparation.cs`는 제거했고, 현재 authoring 변환은
  `ChartEffectJsonCodec.PreparePlan` 경계 한 곳에서 수행한다.

### 공용 노트 지점과 첫 Game 플레이 흐름

- `ChartDocumentNote.Points`와 `PlayableNoteSnapshot.Points`에 단일 노트와 Long의
  Start/Mid/End 순서, 지점별 chart time·floor position, Scratch motion·move amount를
  보존한다. ChartMaker adapter가 기존 Long Scratch Mid를 이 계약으로 투영한다.
- 공용 컴파일러는 지점 순서와 시작/종료 일치를 검증하고, Long Scratch Mid를
  `JudgementTarget`과 구간별 `JudgementSegment`로 컴파일한다. 같은 레인·시각의
  중복 시작도 공용 컴파일 경계에서 거부한다. Preview 자동 테스트는 이 공용 타깃
  순서를 사용한다. Game은 같은 Snapshot으로 공용 `PlayableJudgementSession`을
  구성해 실제 입력을 처리한다.
- `PreparedEffectPlan`은 읽기 전용 엔트리와 파라미터 원본 복사본을 가진다. 내장
  파라미터는 매 세션에 별도 복사되며 확장 파라미터는 복사 함수를 등록해야 한다.
- `LaneHitEffectPlayer`는 공용 Unity 표시 assembly로 이동해 판정 시스템을 더는
  구독하지 않는다. DemoPlay의 `GameplayChartSessionController`가 판정 이벤트를
  표시기에 연결하고 ChartMaker는 표시기만 호출한다.
- `REmind.ChartMaker`, `REmind.Gameplay`, `REmind.Common` assembly를 추가했다.
  공용 assembly와 두 제품 assembly는 서로의 제품 구현을 참조하지 않는다.
- ChartMaker는 `.rd`와 Effect sidecar를 검증한 후 버전이 있는 `.rmp.json`
  실행 패키지를 내보낸다. Game은 이 패키지를 다시 컴파일·검증해 Snapshot과
  Effect 계획을 준비한다. DemoPlay의 번들 샘플도 실행 패키지로 바꿨다.
- 제품별 빌드 명령은 `Game.unity`와 ChartMaker 씬을 각각 선택하고 전용
  assembly define을 지정한다. 두 Windows 빌드는 컴파일에 성공했으며 각
  산출물의 Managed 폴더에 상대 제품 assembly가 없음을 확인했다.
- `NoteType`과 Scratch motion/rules/path를 `REmind.NoteRules` 순수 C# assembly로
  이동했다. 기존 `NoteData` 파서와 자료형은 호출되지 않는 레거시 파일로 남지만,
  `GameplayChartPreparation`의 변환과 `NoteJudgementSystem`의 입력에서는 제거했다.
- 입력 라우터가 press와 release를 발행한다. 공용 판정 세션은 단일 노트 입력,
  Long의 Start→Mid→End 유지/해제, Mid의 재합류와 구간별 결과를 처리한다.
- Long Scratch 입력 규칙은 Start=양입력, Mid=유지 중인지 확인, End=음입력으로
  정했다. 앞 구간을 놓치면 그 구간은 Miss로 확정하며 각 Mid의 양입력으로 이후
  구간에 다시 합류할 수 있다. 구간마다 점수·콤보·체력을 개별 집계한다.
  노트 종류별 `NoteJudgeWindowProfile` 파일을 만들었다. 전용 수치가 미정이므로
  현재 모든 파일은 기존 기본값 30/60/100/150ms로 시작한다. Long Scratch의
  최종 수치와 일반 노트 간접 판정 정책은 정해진 뒤 설정·테스트를 갱신해야 한다.
- `Game.unity`는 초안 채보에서 검증·출력한 `ChromaIPlay.rmp.json`과 기존
  `I.mp3`를 번들로 읽는다. 302개 Tap의 점수/체력과 결과를 Game 세션이 관리한다.
  원본 초안 `.rd`는 변경하지 않았다.
- Game 장면의 PlayMode smoke는 채보 준비, 302개 표시 객체, Play/Pause/Resume/
  Restart/Stop과 마지막 노트 이후 종료 시간을 확인했다. 실제 키 입력과 화면 결과는
  아직 수동 검증하지 않았다.

## 5. Known Transitional Risks

현재 확인/관리해야 할 대표 위험:

- Editor와 Gameplay의 이중 데이터 모델
- Preview와 향후 Game runtime의 중복 실행 로직
- 레거시 API를 신규 구조가 따라가게 되는 문제
- 같은 규칙이 서로 다른 폴더에 중복되는 문제
- 임시 Adapter가 영구 구조로 굳어지는 문제
- 여러 곡 콘텐츠·진행 저장과 최종 UI가 아직 없는 문제
- 사용하지 않는 구형 `ChartLoader`/`NoteData` 파일의 정리 여부

### 현재 남은 동작 경계

1. **Preview 자동 표시와 실제 입력**
   Preview Auto Test는 Snapshot target 순서로 소리와 표시를 진행한다. 실제 입력을
   받는 Game은 공용 판정 세션을 사용한다. Preview에 수동 입력 테스트가 생기면
   같은 공용 세션을 연결해야 한다.
2. **카메라 표시 차이**
   Effect offset/easing은 공용이지만 Camera Note의 기준 X, lane/prefab과 실제 Transform
   구성은 `Game.unity`에서 정적 연결까지만 검증했다. 실제 화면·입력 수동 대조가 남았다.

## 6. Current Classification

이 섹션은 작업하면서 계속 갱신한다.

### CURRENT
- ChartMaker의 실제 편집, 저장/복구, Undo/Redo, Preview 흐름
- `PlayableChartSnapshot`, Effect runtime과 카메라 공용 계산
- 기존 Gameplay 판정/GameRule 구성요소 중 Demo 하네스로 검증한 부분

### SHARED
- `REmind.ChartCore`의 chart compiler, timing/scroll/camera map
- Effect 계약, registry, 준비 계획, runner와 session context
- Effect 카메라 envelope/mixer, Preview capability, transition mailbox
- Runtime chart package codec, Effect parameter decoder, 공용 노트 지점과 구간

### SHARED candidates
- GameRule의 판정 등급/수치 중 Preview 수동 입력에도 필요한 부분
- `NoteType`, Scratch motion/rules/path의 물리적 이동 — **완료**
- Runtime chart package와 codec/validation 경계 — **완료**

### LEGACY
- `DemoPlay` 전용 하네스 UI
- `TempLoader` 및 구형 임시 chart 경로. 현재 DemoPlay 씬에서는 제거됨

### UNKNOWN / Mixed
- GameRule 수치와 Long Scratch 전용 시간 창의 최종값
- `LaneHitEffectPlayer`의 공용 presenter와 Gameplay event binding 책임

## 7. Migration Goal

최종적으로:

```text
               Shared Core
              /           \
             /             \
         Game            ChartMaker
```

Game과 ChartMaker가 서로 직접 의존하지 않고,
공통 게임 규칙과 Chart 의미를 Shared 영역에서 공유하는 구조를 목표로 한다.

## 8. Current Migration Tasks

1. Effect vertical slice의 문서/자동 회귀와 Preview↔DemoPlay 수동 대조 — **완료**.
2. Shared Runtime Package 입력 계약을 정하고 `GameplayChartPreparation`의
   ChartMaker 저장 의존을 제거한다 — **완료**.
3. `LaneHitEffectPlayer`를 공용 Unity presenter와 앱별 판정 event adapter로 분리한다 — **완료**.
4. 공용 판정 세션과 Game의 Snapshot 입력 연결 — **완료**. 전용 수치와 실제
   입력 수동 검증은 후속 작업이다.
5. Game/ChartMaker assembly로 직접 의존을 컴파일 단계에서 막는다 — **완료**.
   첫 곡 Game/ChartMaker 빌드까지 확인했다. 배포용 Build Profile은 후속 작업이다.

### 과도기 adapter 제거 조건

- 구형 `ChartLoader`/`NoteData`: 실행·편집 호출부가 없음을 재확인하고 별도
  정리 작업에서 삭제 여부를 결정한다. 실제 Game 판정 경로의 변환은 제거했다.
- `SampleMusicGimmick`: 실제 곡 registry가 준비되면 개발 전용 등록을 테스트/샘플 범위로
  격리하거나 Player 등록에서 제외한다.

세부 우선순위와 아직 결정되지 않은 수치는 `TASKS.md`에서 관리한다.
