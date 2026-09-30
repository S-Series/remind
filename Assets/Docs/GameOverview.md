# Game Overview

> 상태: 현행 제품 안내
> 검토 기준: 2026-09-29 로컬 코드
> 제품 목표는 [PRODUCT.md](PRODUCT.md), 구현 상태는 [MIGRATION.md](MIGRATION.md)를 따른다.

## 1. 제품과 플레이

ReMind는 10개 판정 레인을 사용하는 리듬게임과 독립 채보 제작 도구 ChartMaker를
함께 만드는 프로젝트다. Game과 ChartMaker는 각각 빌드되며 공용 채보·시간·노트·
Effect 의미를 공유한다. 최종 목표는 배포 가능한 두 제품의 완성이다.

레인은 Main 4개, AirMain 4개, 좌우 Scratch 2개다. 현행 공용 모델에는 Tap, Hold,
Scratch, Long Scratch, Air 계열과 Camera/Effect 이벤트가 있다. AirMain은 단일
판정 지점만 지원한다. 기본 키·판정·Long lifecycle은 [RhythmSystem.md](RhythmSystem.md)를
따른다. 2026-07의 Tap/Hold 전용 데모 제한과 A~; 기본 키 초안은 현재 기준이 아니다.

## 2. 현재 사용자 흐름

```text
Game 실행
  → Bootstrap 표시 완료 → 새 버튼 입력
  → Home
      ├→ Settings 모달 → Home
      └→ MusicSelect → 곡·난이도 선택
          → Game → Pause/Resume/Restart
          → 완주 또는 실패 → Result
              ├→ Retry → Game
              └→ Next / Music Select → MusicSelect
```

Bootstrap의 백분율은 최소 표시 시간에 따른 연출이다. 입력을 기다린 뒤
`AppRoot`에 Home 전환을 요청한다. 일반 씬 이동과 MusicSelect→Game 전환 연출은
[CrystalTransition.md](CrystalTransition.md)에 설명한다.

Result는 점수·랭크·판정 수·최대 콤보·점수 비율 정확도와 저장된 진행을 표시한다.
최고 기록·즐겨찾기·플레이 횟수·클리어 이력·최고 콤보를 로컬에 보존한다.
곡·난이도 첫 클리어마다 기억 조각 1개를 표시하며 보유 수량은 클리어 기록에서
계산한다. Auto Play 결과는 표시하지만 진행에는 저장하지 않는다.

## 3. 제작에서 실행까지

```text
곡 data.json + ChartMaker .rd
  → 편집·저장·검증
  → ChartDocument → 공용 컴파일
      ├→ Snapshot 기반 Preview
      └→ .rmp.json 출력 → Game 준비·검증 → 실제 입력 플레이
```

곡 제목·음원·난이도 목록은 `data.json`, 노트·타이밍·Effect는 `.rd`가 소유한다.
Game은 검증된 실행 패키지를 읽으며 ChartMaker 저장 모델을 직접 사용하지 않는다.
시간은 정수 밀리초로 반올림한 노트 원본이 아니라 편집 위치에서 컴파일한
`double` 밀리초를 사용한다. 형식은 [ChartFormat.md](ChartFormat.md),
곡 구성은 [MusicContent.md](MusicContent.md)를 따른다.

## 4. 구현과 검수의 구분

- 핵심 채보 파이프라인, Game 플레이 흐름, 결과·로컬 기록, 설정과 독립 빌드 경계가 있다.
- 현재 두 곡 `i`·`designant`와 각 `hard` 채보는 테스트 콘텐츠다.
- Story·ReMind·Option·Music 일부 화면은 임시 메뉴다. Character는 Home 위의
  오버레이로 배치됐지만 프로필·카드·스탯은 화면 시안이며 플레이 능력 선택에
  연결되지 않았다. 버튼 연결은 해당 제품 기능 전체의 구현 완료를 뜻하지 않는다.
- ChartMaker 자동 Preview는 공용 Snapshot을 소비한다. 실제 입력을 받는 제작용
  Test Play의 범위와 GameRule 공유는 별도로 확정한다.
- Early/Late 정보는 판정 이벤트에 있다. 전용 분포 그래프·고스팅 검사·Visual Offset
  UI·포커스 이탈 자동 Pause는 구현 완료로 표시하지 않는다.

최신 자동 검사·빌드와 사용자가 직접 확인한 항목은 [TASKS.md](TASKS.md)에 구분해
기록한다. 실제 키 입력·청음·화면 배치·하드웨어 성능은 자동 검사를 통과해도 별도 검수한다.

## 5. 다음 결정과 개발 기준

첫 출시의 메뉴별 기능, 진행·해금·서사, 콘텐츠 공급, 지원 환경과 추가 입력 장치는
[ROADMAP.md](ROADMAP.md)의 D1에서 결정한다. 초기 데모 문서의 제외 목록을 현재
출시 정책으로 승계하지 않는다. 임시 메타데이터와 화면은 정식 콘텐츠가 준비되면 교체한다.

개발은 입력 시각과 DSP 시간의 일관성, Shared 규칙의 단일 구현, 저장 데이터와
세션 상태의 분리, 반복 플레이의 정리 안전성을 우선한다. 구조는
[ARCHITECTURE.md](ARCHITECTURE.md), 실제 클래스 연결은
[GameplayStructure.md](GameplayStructure.md), 전체 작업 순서는
[ROADMAP.md](ROADMAP.md)를 사용한다.
