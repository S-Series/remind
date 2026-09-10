# ReMind Tasks

> 상태: Working Document  
> 현재 작업과 바로 다음 작업만 기록한다.

## Current

- Effect / MusicGimmick 작업 1~7단계 완료 상태 유지
- 다음 독립 migration 범위를 사용자와 선택하기 전에는 최종 Game 구조를 임의로 확장하지 않음

## Recently Completed

- Effect 작업 7단계의 사용/확장/JSON v8/sidecar 문서 작성
- `DemoPlay`를 최종 Gameplay가 아닌 통합 하네스로 문서와 완료 기준에서 재분류
- Camera smooth-step, 중첩 합성, JSON 호환/검증을 포함한 Unity EditMode 75/75
- sample pair `stage6_demo_004` 정적 검증과 솔루션 빌드 경고 0·오류 0
- 사용자 수동 확인: 초기 안전 구간, 두 Tap, smooth camera, Pause/Resume,
  Reset/Restart, Console 무오류

## Next

- Shared Runtime Package 입력 경계를 설계해 `GameplayChartPreparation`의 ChartMaker
  저장 타입 의존 제거
- `LaneHitEffectPlayer`를 공용 표시기와 Gameplay 판정 구독 adapter로 분리
- `NoteType`·Scratch 규칙과 현행 판정 구성요소의 Shared/Legacy 분류
- 최종 Game scene/composition root가 생기면 Snapshot·판정·규칙·카메라 연결 재검증
- Game/ChartMaker 별도 assembly와 Build Profile로 직접 의존 차단

## Blocked / Need Decision

- 최종 Gameplay의 곡 선택, 콘텐츠 로딩, 씬 전환, Player 진입 흐름은 아직 제작 전이다.
  실제 Game 연결을 시작할 때 범위와 composition root를 결정해야 한다.
- 실제 곡용 MusicGimmick과 전환 대상은 콘텐츠 규칙이 확정돼야 한다. 현재 `sample`은
  개발 검증용으로만 유지한다.

## Notes

이 문서는 자주 변경될 수 있다.
장기 설계 원칙은 `AGENTS.md` 또는 `ARCHITECTURE.md`에 기록한다.

Effect 작업 단계 6/7은 전역 `ROADMAP.md`의 Phase 6/7과 같은 뜻이 아니다. 현재
자동 검증은 최종 독립 Game 빌드나 Player 진입 흐름의 완료를 증명하지 않는다.
