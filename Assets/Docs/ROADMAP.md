# ReMind Roadmap

> 상태: Skeleton  
> 최종 리듬게임과 독립 ChartMaker 완성을 위한 큰 개발 순서를 관리한다.

> 이 문서의 **Phase**와 `EffectGimmickWorkLog.md`의 1~7단계는 서로 다른 번호 체계다.
> 현재 Effect vertical slice가 7단계에 도달해도 아래 전역 Phase 5~8이 완료됐다는
> 뜻이 아니다.

## Final Goal

- ReMind Game 완성
- ReMind ChartMaker 완성
- 두 제품의 독립 Build
- Shared Core 안정화
- ChartMaker에서 제작한 Chart를 실제 Game에서 동일한 의미로 실행

## Phase Candidates

### Phase 1. ChartMaker 안정화
편집·저장·복구·Undo/Redo와 실제 제작 흐름을 안정화한다.

### Phase 2. Shared Chart / Runtime Core 정리
Chart format, validation, timing/position, Runtime package의 Source of Truth를 정리한다.

### Phase 3. Actual Gameplay Runtime 재구축
최종 Game scene/composition root가 Shared Runtime package를 직접 소비하게 한다.

### Phase 4. Input / Judgement / GameRule 완성
현행 Long/Scratch를 보존하며 실제 입력, 판정, 규칙과 세션 상태를 완성한다.

### Phase 5. Effect / MusicGimmick 완성
공용 실행 계약과 곡별 등록, 실제 콘텐츠용 capability 및 오류 정책을 완성한다.

### Phase 6. Presentation / Camera / UI
Game과 ChartMaker가 공용 계산을 소비하되 각자의 표시/UI를 독립 구현한다.

### Phase 7. Score / Result / Gameplay Flow
곡 선택부터 플레이, 결과, 다음 화면까지 Player 흐름을 연결한다.

### Phase 8. Build Separation / Content Pipeline
Game/ChartMaker assembly·Build Profile과 검증된 콘텐츠 배포 경계를 확립한다.

### Phase 9. Final Content / Polish / Release Readiness
실제 곡/채보, 성능, 호환성, 접근성, 배포 전 회귀를 마무리한다.

> Phase와 순서는 프로젝트 진행에 따라 조정한다.
