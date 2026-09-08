# Effect / MusicGimmick 장기 작업 기록

최종 갱신: 2026-09-08 (한국 시간)

이 문서는 장기 작업 중 **완료한 범위, 현재 수정 중인 지점, 검증 결과, 다음 재개 지점**을 계속 기록한다. 설계의 원본은 `C:\Users\inwea\Downloads\REmind_Effect_Gimmick_Codex_Instructions.md`이며, 상세 재개 지침은 `Assets/Docs/EffectGimmickHandoff.md`에 있다.

## 단계 현황

| 단계 | 범위 | 상태 |
| --- | --- | --- |
| 1 | 기존 테스트 기준선과 회귀 대상 확보 | 완료 |
| 2 | 저장 소유권, revision, Undo/Redo, 부분 저장 복구 | 완료 |
| 3 | 에디터와 재생 준비의 설정 검증 통합 | 완료 |
| 4 | 실행 시각, 동일 시각 순서, 세션 수명과 정리 | 완료 |
| 5 | ChartMaker 배치→설정→저장→재열기→Preview 전체 흐름 | 완료 |
| 6 | 실제 Gameplay 씬과 게임 상태·규칙·카메라 연결 | 대기 |
| 7 | 문서·샘플·최종 회귀 검사 | 대기 |

## 완료된 핵심 작업

### 1~3단계

- `.rd`와 Effect JSON의 revision·소유 관계를 검사해 다른 채보의 설정 덮어쓰기를 차단했다.
- 디스크에 저장된 revision과 Undo/Redo되는 편집 메타데이터를 분리했다.
- 두 파일 중 하나만 교체되는 실패를 재현하고 backup pair 복구 또는 명확한 오류 경로를 만들었다.
- JSON 구조/타입 검사와 Effect별 필수 값·범위 검사를 공통화했다.
- 종류가 없거나 현재 등록되지 않은 기존 Effect는 편집·보존할 수 있지만 재생 준비에서는 거부한다.

### 4단계 — 완료

- 지연 프레임에서도 Effect를 예약 시각 순으로 한 번씩 실행하고, 활성 Effect/Gimmick은 실제 프레임 시각으로 한 번만 갱신한다.
- 동일 시각 순서를 `Effect 명시 order → 입력 도착 sequence → 자동 판정/Miss`로 고정했다.
- 시간 역행, 같은 시각 중복 update, runner 재진입, 실행/정리 복합 실패를 검사한다.
- 판정 프레임 시작 시점의 runner 세대를 전달해 이전 세션의 완료 콜백이 새 세션을 건드리지 않게 했다.
- Play/Resume/Restart 준비·활성화·취소 경계를 분리하고, Resume은 기존 세션을 유지하며 Restart는 새 세션으로 교체한다.
- 준비 중인 규칙·카메라 출력은 실제 세션에 노출하지 않고, 폐기된 세션의 핸들이 새 세션을 변경하지 못하게 했다.
- Preview 종료·중단 시 runner, 규칙, 카메라, 전환 mailbox와 이벤트 구독을 정리한다.

### 5단계 — 자동 수용 검증 완료

- 새 Effect를 배치하면 배치 도구를 끝내고 생성된 Effect를 즉시 선택해 설정 창을 바로 연다.
- 저장 성공 시 `.rd` 경로를 최근 문서로 기억하며, 저장→문서 초기화→재열기→컴파일→Effect 준비→Preview 시작/종료 전체 왕복을 실제 ChartMaker 구성 요소로 검사한다.
- 현재 sidecar의 구조는 맞지만 등록된 값 범위가 잘못된 경우에도 유효한 backup pair로 복구한다. backup도 없으면 열린 채보·메타데이터·BPM·보정·저장 대상·최근 파일을 전혀 바꾸지 않는다.
- Preview 중에는 배치·선택·노트 편집·삭제·Undo/Redo·새 문서·열기·저장·BPM/보정·음악 교체를 차단한다. 종료 시 임시 실행 상태를 정리한 뒤 다시 편집할 수 있다.
- Preview 준비/시작 실패와 비동기 음악 로드 실패를 상단 상태에 표시하고, Chart Data 팝업에서 시작한 음악 로드 실패는 팝업에도 표시한다.
- Effect JSON 검증·복사·재로드의 성공 안내는 오류와 다른 색으로 표시한다.
- 실제 `ChartMaker.unity`의 Effect 프리팹, 선택기, 저장/로드, TopMenu, Preview, UXML 필드와 호출 순서를 독립 씬 수용 검사 3개로 고정했다.

## 현재 진행 중인 작업

사용자가 Unity에서 5단계 흐름을 실제로 수행해 정상 작동과 파일 생성을 확인했다. 생성된 `.rd`와 Effect JSON은 같은 문서 소유 정보와 revision을 가지며, 원본 `.rd`도 `.bak`으로 보존됐다. 발견된 문제는 저장 유실이 아니라 **종류를 지정하기 전 Preview를 시도했을 때 의도된 차단이 빨간 Console 오류와 스택으로 남는 UX**였다.

미지정 Effect를 컴파일러에 보내기 전에 찾고, 해당 Effect를 선택한 뒤 `Effect Type` 선택과 `Apply`를 안내하도록 보완했다. 편집·저장은 계속 허용하고 타입 지정 전 재생만 막는 기존 규칙은 유지한다. 상단 안내는 빨간 오류 스타일을 쓰지 않으며, Core가 남기는 일반 경고 한 번으로 중복 로그도 줄였다. 추가 회귀를 포함한 Unity EditMode 68개가 모두 통과해 **5단계는 완료**했다.

다음 재개 지점은 6단계 실제 Gameplay 연결이다. 기존 `GameplayChartEffectController`와 실제 Gameplay 씬의 게임 상태·판정 규칙·카메라·재생 시작/종료 경계를 먼저 대조한다. 곡 선택·로딩·씬 전환 같은 큰 시스템 신설이 필요하면 Effect 연결 범위와 분리해 사용자에게 확인한다.

## 마지막 확인 결과

- 최종 실행: Unity 6000.3.15f1 EditMode **68/68 성공**, 실패 0, 건너뜀 0.
- 결과: `Logs/EffectBaselineResults/20260908-191253-250a100a/results.xml`
- 로그: `Logs/EffectBaselineResults/20260908-191253-250a100a/unity.log`
- 실행 직전 `dotnet restore` 및 `dotnet build remind.slnx --no-restore`: 경고 0, 오류 0.
- Unity 로그에서 C# 컴파일 오류, 실패 테스트, 처리되지 않은 예외가 발견되지 않았다.
- 2026-09-08 수동 확인 산출물 `Assets/Chart/chart.rd`와 `Assets/Chart/effect.untitled.default.json`은 musicId=`untitled`, difficultyId=`default`, revision=`e11177f93356444e9647c06aaa8ff3cb`로 일치한다.
- 최종 저장 Effect는 `camera.offset`이며 JSON 설정도 유효하다. 수동 테스트 중 먼저 발생한 `EFFECT_TYPE` 로그는 타입 적용·저장 전 Preview 시도의 기록이고 최종 파일의 타입 유실이 아니다.
- no-type 안내 보완 후 솔루션 빌드와 원본 프로젝트 전체 Unity 회귀가 모두 통과했다. 앞서 실패한 임시 프로젝트 검사는 라이선스 채널 충돌 때문이었고 임시 복제본은 정리했다.

## 다음 체크포인트

1. 6단계 시작 시 실제 Gameplay 씬과 `GameplayChartEffectController`의 현재 연결 상태를 읽기 전용으로 감사한다.
2. Preview와 같은 Snapshot·설정이 Gameplay 준비 단계에도 전달되는지 확인한다.
3. 실제 게임 상태·규칙·카메라 서비스를 기존 구성 요소에 연결하고, 시작/재시작/중단 정리를 수용 검사로 고정한다.
4. 곡 선택·로딩·씬 전환 시스템 신설이 필요하면 Effect 범위와 분리해 사용자에게 확인한다.

## 진행 로그

### 2026-09-08 19:13 KST — no-type 보완 전체 회귀 통과, 5단계 완료

- 사용자가 원본 Unity를 닫은 뒤 공식 전체 검사 스크립트를 원본 프로젝트에서 다시 실행했다.
- Unity 6000.3.15f1 EditMode **68/68 통과**, 실패 0, 건너뜀 0이다.
- 결과는 `Logs/EffectBaselineResults/20260908-191253-250a100a/results.xml`, 상세 로그는 같은 폴더의 `unity.log`에 보존했다.
- 타입 미지정 Effect의 편집·보존 가능/재생 불가 규칙, 자동 선택과 설정 안내, 빨간 오류 억제를 회귀 대상으로 고정했다.
- 자동 수용과 실제 화면 저장 흐름 확인까지 충족했으므로 5단계를 완료로 확정했다. 다음은 6단계 실제 Gameplay 연결이다.

### 2026-09-08 18:49 KST — 5단계 수동 결과 검토 및 no-type UX 보완

- 사용자가 실제 배치·설정·저장 흐름의 정상 작동과 파일 생성을 확인했다.
- 저장된 `.rd`/sidecar의 소유 정보, revision, effectId가 일치하며 `.rd.bak`이 기존 원본을 보존하는 것을 확인했다.
- 빨간 `EFFECT_TYPE` 스택은 타입이 최종 파일에서 사라진 것이 아니라 타입 지정 전 Preview를 눌렀을 때 `LogCompileIssues`가 사용자 수정 가능 상태를 오류로 기록한 것이었다.
- 미지정 Effect를 Preview 컴파일 전 감지하고 해당 노트를 선택해 `Effect Type`과 `Apply`를 안내하도록 바꿨다. 재생 차단 규칙은 유지하되 Console error와 빨간 상단 상태는 만들지 않는다.
- 회귀 검사 `ChartPreview_UnresolvedEffectUsesSetupGuidance`를 추가했다. 솔루션 빌드는 경고 0, 오류 0이다.
- 열린 원본 Editor를 건드리지 않기 위해 별도 임시 프로젝트에서 전체 검사를 시도했지만 Unity 라이선스 채널 충돌로 테스트 실행 전에 중단했다. 임시 복제본은 삭제했으며 실패 로그는 `Logs/EffectBaselineResults/20260908-184939-c1853a4a-shadow/unity.log`에 남겼다.

### 2026-09-08 13:33 KST — 5단계 자동 수용 검증 완료

- 저장→초기화→재열기→컴파일/Effect 준비→Preview 전체 왕복 검사와 semantic-invalid sidecar 복구/무변경 검사를 추가했다.
- 실제 ChartMaker 씬 및 UXML 연결 검사 3개를 추가했고 모두 통과했다.
- 새 Effect 배치 직후 설정 창 진입, Preview 중 문서/타이밍/음악 변경 잠금, 상단 상태 및 음악 로드 실패 안내, 성공 메시지 색상 구분을 보완했다.
- 최종 솔루션 빌드는 경고 0, 오류 0이며 Unity EditMode는 **67/67 통과**했다.
- Unity 네이티브 창을 현재 자동화 환경에서 직접 조작할 수 없어 포인터/키보드 기반 화면 QA 1회만 남겼다.

### 2026-09-07 — 5단계 시작

- 장기 작업 기록을 5단계 진행 중으로 전환했다.
- ChartMaker UI/씬 연결, 저장·재열기·Preview 경로, 사용자 채보와 분리된 샘플 검증 방법을 병렬로 감사하기 시작했다.
- 실제 화면 검증과 자동 수용 검사를 분리해, UI 결함과 저장/실행 로직 결함을 각각 재현 가능한 형태로 남긴다.

### 2026-09-07 16:00 KST — 4단계 최종 보완 구현

- `GamePlay`에 `PlaybackCommitting` 경계와 시작 재진입 가드를 추가했다. 준비 후 커밋이 실패하면 시작 호출은 실패를 반환하고 준비/임시 활성 세션을 되돌린다.
- Resume의 논리 시작 시각을 sample 반올림값보다 뒤로 보내지 않도록 했다.
- `GameplayChartEffectController`는 비활성화 시 구독을 먼저 해제하고, 커밋이 성공한 세션만 재생 상태 공개 전에 활성화한다.
- 정상 종료 중 발생한 runner cleanup 실패를 세션 정리 실패로 승격하고, 정리 실패 시 곡 전환을 실행하지 않는다.
- Preview는 Effect와 AutoTestNote를 시간순으로 병합하며 동일 시각에는 Effect를 먼저 실행한다.
- 통합 검사 `Gameplay_CommitRejectionBlocksSchedulingAndReentrancy`를 추가했고, 기존 Resume/Restart 검사를 새 트랜잭션 경계에 맞게 갱신했다.
- 솔루션 빌드: 성공(경고 0, 오류 0). Unity 전체 검사는 열린 Editor 때문에 아직 실행하지 못했다.

### 2026-09-07 16:15 KST — 교차 검토 후 추가 보완

- 실패한 `Prepare`가 이전 또는 부분 검증된 실행 계획을 남기지 않게 했고, 실패 직후 시작이 거부되는 회귀 검사를 추가했다.
- Effect 콜백 중 `Dispose`가 재진입하면 콜백 종료까지 실제 정리를 지연하고, 남은 같은 프레임 작업을 중단하며 호출이 실패를 반환하게 했다.
- 비활성화 정리 실패도 `LastError`와 `PlaybackFailed` 경로로 보고한다.
- Preview에서 이전 세션 정리가 실패하면 새 Preview 세션 생성을 중단한다.
- Core 회귀 검사 `ReentrantDispose_IsDeferredAndStopsTheCurrentFrame`을 추가했다. 현재 전체 대상은 60개다.
- 최신 솔루션 빌드: 성공(경고 0, 오류 0). Unity 전체 재실행은 열린 Editor 종료 대기 중이다.

### 2026-09-07 21:03 KST — 4단계 최종 검증 완료

- Preview 시작/종료에도 시도 ID와 활성 세션 ID를 도입해 중첩 시작, 시작 중 취소, 이전 중단 콜백이 새 세션을 정리하는 문제를 차단했다.
- Preview 종료 상태 알림 중 새 시작을 거부하고, 모든 구독자에게 같은 상태 값을 전달하며 수동 종료와 자연 종료의 시간/상태 순서를 보존했다.
- Effect 콜백이 Preview를 즉시 종료해 snapshot과 runner가 정리돼도 이전 프레임 처리를 계속하지 않게 했다.
- 통합 검사 `ChartPreview_StartStopTransactionsAreReentrancySafe`, `ChartPreview_ReentrantEffectStopDoesNotUseClearedSession`을 추가했다.
- 최종 `dotnet build`: 성공(경고 0, 오류 0).
- Unity 6000.3.15f1 전체 EditMode: **62/62 성공, 실패 0, 건너뜀 0**.
- 다음 작업 위치는 5단계 ChartMaker 전체 사용 흐름 검증이다.

## 작업 경계와 주의사항

- 실제 Gameplay 씬 연결 완료로 과장하지 않는다. 이는 6단계다.
- PlayMode 실제 오디오/DSP, 화면, 물리 입력 검증은 EditMode 회귀 검사와 별도다.
- 기존 Long Tap / Scratch 판정 규칙은 이 작업에서 재설계하지 않는다.
- 작업 트리에 이번 작업 이전의 수정·삭제·미추적 파일이 많다. 관련 없는 사용자 변경을 되돌리거나 정리하지 않는다.
- 자동 commit/push는 하지 않는다.
