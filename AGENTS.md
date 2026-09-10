# ReMind Development Instructions

> 이 문서는 ReMind 프로젝트에서 Codex 및 기타 AI 코딩 에이전트가 코드를 조사, 수정, 리팩터링, 확장할 때 반드시 따라야 하는 최상위 개발 지침이다.

## 1. Project Goal

ReMind의 최종 목표는 **완성된 리듬게임 시스템과, 그 게임의 채보를 제작하기 위한 독립 ChartMaker를 끝까지 구현하는 것**이다.

프로젝트는 단순한 기술 데모나 Chart Editor 프로젝트가 아니다.

최종적으로 다음 두 개의 독립 빌드 결과물을 만든다.

- **ReMind Game**
  - 플레이어에게 제공되는 실제 리듬게임
- **ReMind ChartMaker**
  - ReMind용 채보와 관련 데이터를 제작하고 검증하기 위한 제작 도구

Game과 ChartMaker는 서로 직접 의존하지 않는다.

두 제품에서 같은 의미로 사용되어야 하는 규칙과 데이터는 Shared 영역으로 분리하는 방향을 우선 검토한다.

---

## 2. Development Priorities

기본 우선순위는 다음과 같다.

1. 동작의 정확성
2. 시스템 책임의 명확성
3. 데이터와 실행 로직의 일관성
4. 유지보수성과 확장성
5. 테스트 가능성
6. 구현 편의성

현재 작업을 빠르게 끝내기 위해 장기적으로 구조를 악화시키지 않는다.

반대로 미래의 모든 가능성을 처리하기 위한 과도한 범용화도 피한다.

---

## 3. Core Development Rules

- 작업 전에 기존 구조와 실제 호출 관계를 조사한다.
- 동일한 게임 규칙을 여러 시스템에서 중복 구현하지 않는다.
- UI와 Presentation에 핵심 게임 규칙을 넣지 않는다.
- 저장 데이터와 Runtime 상태를 구분한다.
- Editor와 Gameplay가 같은 의미의 데이터를 다르게 해석하지 않도록 한다.
- Unity Scene이나 GameObject가 없어도 성립 가능한 핵심 규칙은 가능한 한 순수 C#로 유지한다.
- mutable global/static Gameplay state를 피한다.
- 시스템의 데이터 소유권과 Lifetime을 명확하게 한다.
- 기존 구현이 있다는 이유만으로 무조건 재사용하지 않는다.
- 기존 구현이 오래됐다는 이유만으로 무조건 폐기하지 않는다.
- 새 기능을 추가하기 전에 기존에 같은 책임을 가진 코드가 있는지 조사한다.
- 공용화와 범용화를 혼동하지 않는다.

---

## 4. Product / Build Boundaries

ReMind Game과 ReMind ChartMaker는 별도의 Application이며 별도로 빌드한다.

### ReMind Game

게임 빌드에는 실제 플레이에 필요한 시스템만 포함한다.

ChartMaker 전용 편집 UI와 제작 기능에 의존하지 않는다.

### ReMind ChartMaker

ChartMaker는 채보 제작, 검증, Preview/Test Play를 위한 독립 제작 도구다.

Game 전용 UI, 진행 시스템, 사용자 저장 데이터 등에 의존하지 않는다.

### Shared

양쪽에서 동일한 의미로 사용되어야 하는 기능은 Shared 영역 후보로 본다.

예:

- Chart Format
- Chart Validation
- Timing
- Position / Time 변환
- Note의 의미와 기본 규칙
- Long / Scratch 규칙
- Runtime Chart 생성 규칙
- Effect의 기본 의미
- Preview와 실제 Gameplay가 동일하게 해석해야 하는 GameRule 관련 규칙

다음 의존 방향을 목표로 한다.

```text
               Shared Systems
              /              \
             /                \
      ReMind Game        ReMind ChartMaker
       Game Build          Maker Build
```

다음과 같은 직접 의존은 피한다.

```text
Game -> ChartMaker
ChartMaker -> Game
```

한쪽의 전용 구현이 다른 쪽에서도 필요해졌다면, 해당 책임을 Shared로 분리할 수 있는지 먼저 검토한다.

---

## 5. AI Agent Workflow

### Before Making Changes

1. root `AGENTS.md`를 읽는다.
2. 관련 `Assets/Docs` 문서를 확인한다.
3. `git status`를 확인한다.
4. 사용자의 미커밋/미추적 변경을 확인한다.
5. 관련 구현 파일과 호출부를 조사한다.
6. 유사한 책임을 가진 기존 구현이 있는지 검색한다.
7. 현재 코드와 목표 구조의 차이를 파악한다.

### During Implementation

- 사용자 변경사항을 덮어쓰지 않는다.
- 관련 없는 리팩터링을 동시에 수행하지 않는다.
- 문제를 해결하기 위해 기존 기능을 임의로 삭제하지 않는다.
- 컴파일 오류를 없애기 위해 기존 API나 검증을 무작정 약화시키지 않는다.
- 같은 책임을 가진 새 시스템을 병렬로 만들지 않는다.
- 기존 코드에서 필요한 책임만 추출하는 것이 더 적절한지 검토한다.
- 임시 호환 계층이 필요한 경우 그 목적과 제거 조건을 명확히 한다.
- 로컬 코드가 원격 Repository보다 최신일 수 있음을 항상 고려한다.

### After Implementation

1. diff를 검토한다.
2. 의도하지 않은 변경이 없는지 확인한다.
3. 관련 테스트를 실행한다.
4. 기존 기능의 회귀 가능성을 확인한다.
5. 구조나 책임 경계가 바뀌었다면 관련 문서를 갱신한다.
6. 직접 검증하지 않은 내용을 검증 완료라고 기록하지 않는다.

---

## 6. Existing Code Reuse Rules

기존 구현을 재사용하기 전에 최소한 다음을 확인한다.

1. 현재 실제 사용되는가?
2. 특정 Scene이나 Tool에만 종속된 책임인가?
3. Game과 ChartMaker에서 공용이어야 하는 책임인가?
4. Unity 객체에 불필요하게 결합되어 있는가?
5. 이미 다른 영역에 같은 책임이 존재하는가?
6. 재사용하면 중복 데이터 모델이나 이중 구조를 장기 유지하게 되는가?
7. 알고리즘/규칙만 추출하는 것이 더 적절한가?

**재사용 자체가 목표가 아니다. 올바른 책임 배치가 목표다.**

---

## 7. Architecture Change Rules

- 같은 의미의 규칙을 Game과 ChartMaker에 따로 구현하지 않는다.
- 공용 Core가 특정 Game Scene 또는 ChartMaker UI를 알게 만들지 않는다.
- Adapter는 실행 환경과 공용 Core 사이의 변환 경계로 사용한다.
- 비슷한 데이터 모델이 둘 이상 존재하면 변환기부터 추가하지 말고 장기 Source of Truth를 먼저 조사한다.
- Migration을 위한 임시 Adapter는 허용하지만 영구 구조처럼 확장하지 않는다.
- 구조 개선을 이유로 관련 없는 대규모 리팩터링을 동시에 하지 않는다.

---

## 8. Unity Rules

- 프로젝트의 실제 `ProjectSettings/ProjectVersion.txt`를 Unity 버전 기준으로 사용한다.
- Scene 검색과 숨겨진 전역 의존성을 최소화한다.
- `Find`, `FindObjectOfType`, `FindFirstObjectByType` 등에 핵심 구조가 의존하지 않도록 한다.
- 가능하면 Inspector, 명시적 초기화, Factory, Composition Root 등을 사용해 의존성을 연결한다.
- `Awake`, `OnEnable`, `Start`의 책임을 구분한다.
- 세션별 mutable 상태를 static에 저장하지 않는다.
- ScriptableObject를 무분별한 mutable 전역 상태 저장소로 사용하지 않는다.
- 핵심 판정이나 Timeline 로직이 Unity의 프레임 실행 순서에 우연히 의존하지 않도록 한다.
- 장시간 반복되는 표시 객체는 필요 시 Pool을 검토한다.
- 노트마다 무거운 `Update()`를 두는 구조를 피한다.

---

## 9. Testing and Verification

변경된 기능에 따라 가능한 범위에서 다음을 확인한다.

- Compile
- 관련 Unit / EditMode 테스트
- 기존 기능 회귀
- Chart Save / Load
- Chart Compile
- Editor Preview / Test Play
- 실제 Gameplay 또는 테스트 Harness
- Timing 경계
- 동일 timestamp 이벤트 순서
- Long / Scratch lifecycle
- Effect lifecycle
- Restart / Cleanup

버그 수정에는 가능하면 회귀 테스트를 추가한다.

테스트를 통과시키기 위해 테스트 자체를 삭제하거나 약화하지 않는다.

Unity Editor에서 직접 실행하지 못했다면 실행 성공을 가정하지 않는다.

---

## 10. Forbidden Actions

명시적인 이유와 필요성 없이 다음을 수행하지 않는다.

- 대량 코드 삭제
- 기존 기능 비활성화
- 테스트 삭제 또는 약화
- 데이터 검증 제거
- 새로운 Framework 도입
- 새로운 전역 Singleton 추가
- 동일한 게임 규칙의 중복 구현
- 임시 Compatibility Layer를 영구 구조처럼 확장
- 사용자 미커밋 변경 롤백
- 사용자 작업을 원격 main 상태로 강제 복원
- 자동 commit
- 자동 push

---

## 11. Documentation

문서의 역할을 구분한다.

- `AGENTS.md`
  - AI 개발자가 항상 지켜야 하는 작업 규칙
- `Assets/Docs/PRODUCT.md`
  - 최종적으로 무엇을 만드는가
- `Assets/Docs/ARCHITECTURE.md`
  - 최종적으로 어떤 시스템 구조를 지향하는가
- `Assets/Docs/MIGRATION.md`
  - 현재 구조개편 상태와 레거시/현행 관계
- `Assets/Docs/ROADMAP.md`
  - 완성까지의 큰 개발 순서
- `Assets/Docs/TASKS.md`
  - 현재 진행 중이거나 바로 다음에 수행할 작업

현재 상태에만 해당하는 내용은 가능하면 `AGENTS.md`의 영구 규칙과 분리한다.

---

## 12. Definition of Done

작업은 최소한 다음 조건을 만족해야 완료로 본다.

- 요청된 구현이 완료됨
- 기존 동작에 불필요한 회귀가 없음
- 프로젝트의 책임 경계를 불필요하게 악화시키지 않음
- 관련 테스트/검증 수행
- 불필요한 변경이 없음
- 필요한 문서가 갱신됨
- 남은 위험이나 미검증 사항이 명확히 보고됨
