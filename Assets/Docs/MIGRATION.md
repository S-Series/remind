# ReMind Architecture Migration

> 상태: Living Document  
> 이 문서는 현재 Repository의 과도기 구조와 구조개편 진행 상황을 기록한다.  
> 완료된 마이그레이션 내용은 계속 갱신하거나 제거할 수 있다.

## 1. Current State

ReMind는 현재 구조 개편 중이다.

현재 존재하는 코드의 폴더명이나 클래스명만으로 장기 Source of Truth를 판단하지 않는다.

## 2. Current Functional Reference

현재 ChartMaker가 실제 개발과 기능 구현의 주요 기준이다.

다만 ChartMaker의 내부 구현 전체를 최종 아키텍처로 간주하지 않는다.

Effect vertical slice는 ChartMaker Preview와 기존 `DemoPlay`에서 같은 실행 의미를
검증한다. `DemoPlay`는 최종 Game 씬이 아니라 레거시 구성요소를 연결한 과도기 통합
하네스다. 현재 Repository에는 최종 Gameplay composition root와 Player 진입 흐름이 없다.

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
Preview / Future Gameplay
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

## 5. Known Transitional Risks

현재 확인/관리해야 할 대표 위험:

- Editor와 Gameplay의 이중 데이터 모델
- Preview와 향후 Game runtime의 중복 실행 로직
- 레거시 API를 신규 구조가 따라가게 되는 문제
- 같은 규칙이 서로 다른 폴더에 중복되는 문제
- 임시 Adapter가 영구 구조로 굳어지는 문제
- Game/ChartMaker assembly와 독립 Build Profile이 아직 없는 문제
- 폴더는 Gameplay에 있지만 양쪽에서 쓰는 `NoteType`·Scratch 규칙의 물리적 위치

### 현재 남은 직접 의존

1. **Game → ChartMaker storage**  
   `GameplayChartPreparation`이 `ChartFileCodec`, `ChartEffectFileStore`, `ChartHolder`,
   `ChartHolderDocumentAdapter`, `ChartEffectJsonCodec`을 사용한다. 이는 번들 `.rd` 샘플을
   `DemoPlay`에 넣기 위한 호환 브리지다.
2. **ChartMaker → Gameplay presentation**  
   `ChartTestPlay`가 `LaneHitEffectPlayer`를 직렬화/탐색한다. 공용 규칙이 아니라 현재
   Animator presenter 재사용이지만 독립 assembly를 막는다.
3. **판정 모델 이중 경계**  
   ChartMaker Auto Test는 Snapshot judgement target을 소비하고, Demo 하네스는 이를
   `NoteData[]`로 변환해 기존 `NoteJudgementSystem`에 넣는다. Long/Scratch를 포함한
   장기 Source of Truth가 아직 정해지지 않았다.
4. **카메라 표시 차이**  
   Effect offset/easing은 공용이지만 Camera Note의 기준 X, lane/prefab과 실제 Transform
   구성은 최종 Game 씬에서 아직 대조하지 않았다.

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

### SHARED candidates
- Note/Long/Scratch runtime 의미와 판정 시간 경계
- `NoteType`, Scratch motion/rules/path의 물리적 이동
- Runtime chart package와 codec/validation 경계

### LEGACY
- `DemoPlay` 전용 scene orchestration과 임시 표시 흐름
- `TempLoader` 및 구형 임시 chart 경로. 현재 DemoPlay 씬에서는 제거됨

### UNKNOWN / Mixed
- `NoteJudgementSystem`, `GameRule`, 기존 Long/Scratch 구성요소의 최종 분할 단위
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
2. 최종 Game loader를 시작할 때 Shared Runtime Package 입력 계약을 정하고
   `GameplayChartPreparation`의 ChartMaker 저장 의존을 제거한다.
3. `LaneHitEffectPlayer`를 공용 Unity presenter와 앱별 판정 event adapter로 분리한다.
4. 현행 Long/Scratch를 포함해 판정 Source of Truth를 조사한 뒤 공용화 범위를 정한다.
5. Game/ChartMaker assembly와 Build Profile을 추가해 직접 의존을 컴파일 단계에서 막는다.

### 과도기 adapter 제거 조건

- `GameplayChartPreparation`: 최종 Game의 content loader가 ChartMaker 편집 타입 없이
  검증된 Shared Runtime Package를 공급할 때 제거한다.
- `ChartTestPlay → LaneHitEffectPlayer`: 공용 presenter가 Gameplay 판정 시스템을 직접
  구독하지 않고 앱별 adapter를 받을 때 제거한다.
- `SampleMusicGimmick`: 실제 곡 registry가 준비되면 개발 전용 등록을 테스트/샘플 범위로
  격리하거나 Player 등록에서 제외한다.

위 작업은 현재 Effect 기능을 통과시키기 위해 Long/Scratch 판정을 새로 만드는 방식으로
확대하지 않는다. 세부 우선순위와 결정 대기는 `TASKS.md`에서 관리한다.
