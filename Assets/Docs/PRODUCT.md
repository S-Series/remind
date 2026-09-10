# ReMind Product

> 상태: Draft Skeleton  
> 목적: ReMind 프로젝트가 최종적으로 무엇을 완성해야 하는지 정의한다.

## 1. Project Goal

ReMind 프로젝트의 최종 목표는 **완성된 리듬게임 시스템과, 그 게임의 채보를 제작하기 위한 독립 ChartMaker를 끝까지 구현하는 것**이다.

ReMind는 단순한 리듬게임 기술 데모나 Chart Editor 프로젝트가 아니다.

최종적으로 실제 배포 가능한 리듬게임을 완성하고, 해당 게임의 콘텐츠 제작에 사용할 수 있는 ChartMaker를 함께 완성하는 것을 목표로 한다.

---

## 2. Products

ReMind 프로젝트는 하나의 Repository와 공용 시스템을 사용하지만, 최종적으로 두 개의 독립된 Application을 만든다.

### 2.1 ReMind Game

플레이어에게 제공되는 실제 리듬게임이다.

게임 빌드에는 플레이에 필요한 시스템만 포함한다.

ChartMaker의 편집 UI나 제작 도구는 게임에 포함하지 않는다.

최종적으로 다음과 같은 영역을 완성해야 한다.

- Chart Runtime
- Music Playback / Timeline
- Player Input
- Judgement
- Note System
- Long / Scratch
- Score / Combo
- Gauge / Health
- GameRule / Modifier
- Effect
- MusicGimmick
- Camera / Presentation
- Gameplay UI
- Result
- Gameplay Flow
- Settings / Save
- 실제 콘텐츠를 플레이할 수 있는 게임 구조

세부 게임 기획과 시스템 사양은 별도 문서에서 정의한다.

### 2.2 ReMind ChartMaker

ReMind용 채보와 관련 데이터를 제작하고 검증하기 위한 별도의 제작 도구다.

ChartMaker는 Game과 별도로 빌드한다.

ChartMaker의 주요 역할은 다음과 같다.

- Chart 작성
- Chart 수정
- Save / Load
- Undo / Redo
- Timing 작성
- Note 작성
- Long / Scratch 작성
- Camera Event 작성
- Effect 및 관련 데이터 작성
- Chart Validation
- Preview
- Test Play
- 실제 게임에서 사용할 Chart 데이터 출력

ChartMaker는 게임에 포함되는 사용자 기능이 아니라 **콘텐츠 제작 도구**다.

---

## 3. Relationship Between Game and ChartMaker

Game과 ChartMaker는 서로 다른 Application이지만 동일한 리듬게임 의미 체계를 공유해야 한다.

```text
                   Shared Systems
                  /              \
                 /                \
          ReMind Game         ReMind ChartMaker
          Game Build           Maker Build
```

두 Application이 같은 규칙을 독립적으로 다시 구현하는 것은 목표 구조가 아니다.

가능한 경우 다음과 같은 핵심 규칙과 데이터 의미를 공유한다.

- Chart Format
- Chart Validation
- Timing
- Position / Time 변환
- Note 의미와 기본 규칙
- Long / Scratch 규칙
- Runtime Chart 생성 규칙
- Effect의 기본 의미
- Preview와 실제 Gameplay가 동일하게 해석해야 하는 Rule

반면 실행 환경에 종속되는 기능은 각 Application이 독립적으로 가진다.

---

## 4. Game-only Responsibilities

다음과 같은 영역은 기본적으로 Game 전용이다.

- 실제 플레이어 Input
- Gameplay UI
- Result
- 게임 진행
- 사용자 Save
- 플레이어 Settings
- 실제 Gameplay Scene Flow
- 플레이어 콘텐츠 해금/진행 시스템
- 실제 배포 환경에 필요한 기능

---

## 5. ChartMaker-only Responsibilities

다음과 같은 영역은 기본적으로 ChartMaker 전용이다.

- Chart 편집 UI
- 선택 / 배치 도구
- Undo / Redo
- 편집 중 임시 상태
- Authoring Validation UI
- 제작 보조 기능
- 편집용 Timeline 조작
- 제작자가 사용하는 Debug / Preview 도구

---

## 6. Build Separation

Game과 ChartMaker는 별도의 빌드 결과물이어야 한다.

게임 빌드는 ChartMaker에 의존하지 않는다.

ChartMaker 빌드는 Game 전용 사용자 기능에 의존하지 않는다.

한쪽 Application의 전용 코드가 다른 쪽에서도 필요해졌다면, 해당 책임이 Shared 영역으로 분리되어야 하는지 먼저 검토한다.

최종적으로 ChartMaker 전용 코드나 리소스가 Game 빌드에 불필요하게 포함되지 않는 구조를 지향한다.

---

## 7. Final Product Principle

ReMind의 목표는 다음 세 개의 독립적인 리듬게임 구현을 유지하는 것이 아니다.

```text
ChartMaker Rhythm Logic

Preview Rhythm Logic

Gameplay Rhythm Logic
```

최종적으로는 하나의 리듬게임 규칙과 데이터 의미를 기반으로 서로 다른 실행 환경이 이를 소비해야 한다.

```text
                Rhythm / Chart Core
               /                   \
       ChartMaker Runtime       Game Runtime
              |                     |
       Authoring / Preview       Actual Play
```

ChartMaker와 Game의 표현 방식, UI, 입력 방식, 실행 목적은 다를 수 있다.

하지만 동일한 Chart와 동일한 Rule이 서로 다른 의미로 해석되는 구조는 피한다.

---

## 8. Product Completion

ReMind는 ChartMaker만 완성하거나 Gameplay 기술 데모만 동작하는 상태를 최종 완료로 보지 않는다.

최종적으로 다음이 연결되어야 한다.

```text
ChartMaker
    ↓
Chart Data
    ↓
Validation / Runtime Preparation
    ↓
ReMind Game
    ↓
Actual Gameplay
```

ChartMaker에서 제작한 콘텐츠가 실제 Game에서 안정적으로 재생되고, Game에서 요구하는 규칙을 ChartMaker에서도 충분히 제작·검증할 수 있어야 한다.
