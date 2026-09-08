# Effect / MusicGimmick 작업 재개 메모

최종 갱신: 2026-09-08 (한국 시간)

## 지금 멈춘 위치 — 다음 작업자는 먼저 읽을 것

- 사용자와 합의한 **1~4단계(기준선, 저장/Undo 안전성, 설정 검증 통합, 실행 시각·세션 수명)를 완료**했다.
- Unity 6000.3.15f1 EditMode 최종 실행은 **68/68 통과**했다. 결과와 로그는 아래 경로에 기록돼 있다.
- 5단계 ChartMaker UI와 Preview의 구현·자동 수용 검증 및 사용자 실제 화면 확인을 완료했다. 타입 지정 전 Preview가 빨간 오류 스택을 남기던 UX도 안내형 차단으로 보완했고 전체 Unity 회귀가 통과했다. 다음 작업은 6단계 실제 Gameplay 씬 연결이다.
- 작업 트리에는 이전부터 수정·삭제·미추적 파일이 매우 많다. 모두 이번 변경이라고 간주하지 말고, 사용자 변경을 보존한다. 자동 commit/push 금지.

## 원본 지침과 변하지 않는 경계

권위 있는 원본: `C:\Users\inwea\Downloads\REmind_Effect_Gimmick_Codex_Instructions.md`

재개 시 원본 전체와 관련 프로젝트 문서, AGENTS.md 유무, git status를 먼저 확인한다. 아래는 원본을 대체하는 새 설계가 아니라 재개용 요약이다. 원본이 없으면 이 메모만으로 미확정 게임 규칙을 결정하지 말고 사용자에게 원본을 요청한다.

- Effect의 시간·순서·종류·명령·effectId는 채보 정의가 원본이다.
- `effect.{musicId}.{difficultyId}.json`에는 난이도별 조정 수치만 저장한다. 복잡한 분기와 상태 전이는 C#에 둔다.
- ChartDocument / PlayableChartSnapshot에는 수동 데이터만 둔다. 실행 객체와 Unity 참조를 넣지 않는다.
- 플레이 준비 단계에서 설정을 읽고 등록된 타입·명령을 검증한다. 이벤트 실행 시 파일을 읽지 않는다.
- 한 세션의 Effect들이 동일한 MusicGimmick 인스턴스를 공유한다. 재시작 시 새 상태를 사용한다.
- 이동·정렬은 ID 유지, 복사는 새 ID와 독립된 설정, 삭제·Undo/Redo는 정의와 설정을 함께 복원한다.
- 채보와 수치 파일은 같은 revision의 한 문서다. 임시 파일·백업·불일치 감지를 사용하고 두 파일 교체를 통째로 원자적이라고 가정하지 않는다. 별도 관리용 manifest는 만들지 않는다.
- 공용 곡 시간과 명시적인 입력/판정 루프를 사용한다. 과거 입력에 이후 규칙을 소급 적용하지 않는다.
- 효과 종료는 자신이 소유한 카메라·규칙·구독 핸들만 해제한다. 실패한 부작용 명령을 자동 재실행하지 않는다.
- 곡 전환은 요청을 제출한 뒤 플레이 제어기의 안전한 경계에서 처리한다.
- Preview는 영구 진행 정보를 바꾸지 않는다. 상태 이력이 필요한 기믹의 중간 시작은 지원 제한을 명시한다.
- 기존 Long Tap / Scratch 판정 규칙을 이 작업에서 재설계하지 않는다. Unity와 의존성 업그레이드 금지.
- 모호함이 기존 게임 규칙에 영향을 주면 사용자에게 구체적으로 질문한다.

## 합의한 후속 작업 순서

아래 번호는 최근 점검 후 정한 보완 작업 순서다. 원본 지침의 12절에 있는 최초 구현 순서 번호와 구별한다.

1. 현재 테스트 기준선과 회귀 테스트 정리 — **완료**.
2. 저장 충돌 방지, 디스크 revision과 편집 이력 분리, 저장 실패 복구 검증 — **완료**.
3. 에디터와 런타임 설정 검증 통합 — **완료**.
4. 실행 시각·입력 경계·세션 수명·중단/전환 상세 검증 — **완료**.
5. ChartMaker UI와 Preview 전체 사용 흐름 검증/보완 — **완료**.
6. 실제 Gameplay 씬의 구성과 서비스 연결.
7. 샘플·사용 설명·최종 회귀 검증.

## 이번에 남긴 테스트 기반

- `Assets/Editor/REmindBaselineChecks.cs`: 실제 ChartMaker 저장/이력 및 Gameplay 판정 구성에 접근하는 Editor 테스트 브리지.
- `Assets/Tests/EditMode/EffectIntegration/EffectBaselineTests.cs`: 브리지를 호출하는 통합/수용 검사. no-type 안내 회귀를 추가해 현재 전체 Unity 대상은 68개다.
- 같은 폴더의 `ChartMakerSceneFlowTests.cs`: 실제 ChartMaker 씬 연결, UXML/동적 Effect 패널, 배치 후 자동 선택과 Preview 잠금 호출을 검사하는 3개.
- 같은 폴더의 `REmind.EffectIntegration.Tests.asmdef`: Editor 테스트 어셈블리.
- `Tools/Run-EffectBaseline.ps1`: 설치된 프로젝트 버전의 Unity로 EditMode를 실행하고 매번 새 결과 폴더를 만든다. 열린 Unity를 종료하지 않으며, 열려 있으면 실행을 거절한다.
- 기존/추가 Core 테스트: `Assets/Tests/EditMode/ChartCoreDomain/TimingMapTests.cs`, `EffectRuntimeTests.cs`.

브리지 구조를 둔 이유: asmdef 테스트 어셈블리는 기본 `Assembly-CSharp`를 직접 참조할 수 없다. 테스트 때문에 프로덕션 전체 어셈블리 경계를 바꾸지 않고 `Assembly-CSharp-Editor`의 브리지를 리플렉션으로 호출한다.

테스트 데이터는 `Temp/EffectBaselineFixtures/{guid}`의 독립된 임시 경로를 사용한다. 사용자 채보를 수정하지 않는다. 이력 테스트는 빈 편집 상태를 요구하고 합성 상태를 정리한다.

### 실제 확인한 실행 결과

- Unity 버전: **6000.3.15f1**. 설치 경로: `C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe`.
- 최종 전체 EditMode 실행: **총 68개, 성공 68, 실패 0, 건너뜀 0**.
- 결과: `Logs/EffectBaselineResults/20260908-191253-250a100a/results.xml`.
- 로그: `Logs/EffectBaselineResults/20260908-191253-250a100a/unity.log`.
- 실행 직전 `dotnet build remind.slnx --no-restore`도 경고 0, 오류 0으로 통과했다.
- PlayMode, 실제 오디오/DSP, 화면, 사용자 입력을 이용한 수동 검증은 이번 기준선 실행에 포함되지 않는다.
- 테스트 픽스처는 `Temp`를 사용하지만 실행 로그/XML은 Unity 종료 정리에서 사라지지 않도록 git-ignored `Logs`에 둔다.

### 재개 첫 실행

Unity를 닫은 상태에서 프로젝트 루트의 PowerShell에서 실행:

```powershell
.\Tools\Run-EffectBaseline.ps1
```

Core만 확인할 때는 `-CoreOnly`를 붙인다. 결과는 `Logs/EffectBaselineResults/{새 실행 ID}/results.xml` 및 `unity.log`에 남는다. 실패가 있으면 스크립트는 성공으로 감추지 않고 비정상 종료 코드를 반환한다. Windows에서는 Unity 라이선스 프로세스를 기다리며 멈추지 않도록 Editor 프로세스 자체의 종료만 기다린다.

우선 테스트 하네스 자체의 컴파일/기대값 오류와 실제 제품 결함을 구분한다. 기존 동작과 다르다는 이유만으로 제품 코드를 테스트에 맞춰 수정하지 않는다.

## 1~3단계에서 완료한 보완

- sidecar가 현재 파일 또는 `.bak`으로 존재하면 대상 `.rd`의 current/backup revision과 실제로 짝이 맞는지 확인한다. 다른 채보가 소유한 `effect.{musicId}.{difficultyId}.json`은 기존 `.rd`에 Save As하더라도 덮어쓰지 않는다.
- 최초 저장에서 JSON 교체 뒤 `.rd` 교체가 실패하면 새로 만든 orphan JSON만 롤백해 재시도할 수 있다. 기존 문서 갱신 중 실패하면 마지막 matching backup pair를 보존한다.
- current `.rd`가 손상되거나 없어도 유효한 `.rd.bak`과 JSON backup 짝을 `FileToChart` 실제 로드 경로에서 복구한다. 복구 상태는 저장 전까지 dirty로 유지한다.
- `ChartEffectDocumentState.EditableMetadata`를 분리해 Music/Difficulty/Gimmick은 Undo/Redo되지만 마지막 디스크 `Revision`은 되돌아가지 않는다.
- JSON codec은 구조·타입·finite 변환을 맡고, 허용 범위와 필수 의미 값은 `EffectRegistry.ValidateParameters`를 에디터와 재생 준비가 공유한다.
- 알려진 등록의 잘못된 설정은 저장 전에 차단한다. 타입이 비어 있거나 현재 등록을 사용할 수 없는 기존 Effect는 원문 보존·재저장이 가능하지만 재생 준비는 계속 실패한다.
- Music/Difficulty ID의 UI 검사와 실제 sidecar 파일명 검사는 같은 ASCII 1~80자 규칙을 사용한다.

## 4단계에서 완료한 보완

- `EffectRunner.TriggerThrough(cutoffTimeMs, currentTimeMs)`로 예약 이벤트의 인과 순서와 늦은 프레임의 실제 처리 시각을 분리했다. 판정 루프는 Effect를 예약 시각 순으로 트리거한 뒤 활성 Effect와 MusicGimmick을 현재 프레임 시각에 한 번만 갱신한다.
- 같은 시각의 순서는 **Effect의 명시 order → 입력의 도착 sequence → 자동 판정/Miss**로 고정했다. 자동 Miss 경계는 기존처럼 strictly outside라 정확한 마감 시각의 입력이 먼저 처리된다.
- 같은 곡 시각 재호출은 진행 Update를 반복하지 않는다. 시작 시각보다 이른 실제 시각, trigger watermark 역행, runner 재진입은 명시적으로 거부한다. 새 Effect는 기본적으로 seek 불가이며 절대 시각 복원이 가능한 Camera만 opt-in한다.
- 실행 항목은 콜백 전에 소비해 예외 뒤 자동 재시도하지 않는다. 실행 실패와 정리 실패를 함께 보존하며 Effect, Gimmick, session 정리는 여러 번 호출해도 한 번만 수행한다.
- 판정 프레임은 구독자 예외나 시간 역행에서도 frame-completion 경계를 실행한다. 프레임이 시작할 때의 runner generation을 전달해 이전 세션의 완료 콜백이 새 세션을 처리하지 않는다.
- `GamePlay` 시작 요청에 Play/Resume/Restart 이유와 prepare/commit/abort 경계를 추가했다. Resume은 기존 세션을 유지하고 Restart는 fresh runner/Gimmick으로 교체하며, 뒤 validator가 거부하면 준비 중 세션만 폐기한다.
- 준비 중 Gameplay rule modifier와 카메라 출력은 commit 전까지 실제 세션에 노출되지 않는다. 이전 세션의 취소 토큰·scoped handle·폐기된 camera mixer는 새 세션을 변경하지 못한다.
- Preview 수동 종료는 세션을 먼저 정리한 뒤 0ms로 되감아 역행 재진입을 막는다. `ChartTestPlay` 비활성화와 시작 validator 실패에서도 이벤트 구독, Effect, 규칙, 카메라, 전환 mailbox를 끝까지 정리한다.
- 실패한 Gameplay 준비 계획은 재생 가능 상태로 남기지 않으며, 시작은 prepare→commit→confirm 경계를 거친다. 커밋 거부, 상태 알림/cleanup 중 중첩 시작, sample 경계 Resume 역행을 차단한다.
- 실행 콜백 안에서 runner Dispose가 요청되면 콜백 종료까지 정리를 지연하고 남은 같은 프레임 실행을 중단한다. 정상 종료 중 발생한 cleanup 실패도 Gameplay/Preview 실패 경로에 전달한다.
- Preview 시작 시도와 활성 세션에 ID를 부여해 이전 abort/stop이 새 세션을 정리하지 않게 한다. 종료 상태 알림 중 재시작을 거부하고 Effect 콜백 중 종료 후에는 정리된 snapshot을 다시 사용하지 않는다.
- 경계 테스트에는 지연 프레임 scheduled/current 구분, 동일 시각 전체 순서, 규칙 구간 시작/끝, 같은 시각 update 멱등성, 재진입 Dispose, 복합 cleanup 실패, 중복 전환, 판정 콜백 예외, Resume/Restart 세션 교체, Gameplay/Preview 시작·종료 재진입이 포함된다.

## 5단계에서 완료한 보완

- 새 Effect 배치가 끝나면 배치 도구를 해제하고 새 오브젝트를 선택해 Effect 설정 패널을 바로 연다.
- 저장 성공 경로도 최근 `.rd`로 기억하며, 저장→초기화→재열기→컴파일→Effect 준비→Preview 시작/종료 전체 왕복을 실제 구성 요소로 검사한다.
- load 후보는 sidecar를 구조적으로 적용한 뒤 공용 의미 검증까지 통과해야 열린 문서를 교체할 수 있다. 잘못된 current는 유효한 backup으로 복구하고, 전부 실패하면 열린 채보·메타데이터·타이밍·저장/최근 경로를 유지한다.
- Preview 중 문서 편집, 배치/선택, 저장/열기, BPM/시작 보정, 음악 교체를 UI와 핵심 API 양쪽에서 차단한다. 오래된 재생 이벤트가 와도 실제 Core 상태로 잠금을 결정한다.
- Preview 준비/시작 오류와 음악 로드 실패를 상단 상태로 노출하고, Chart Data 팝업에도 비동기 음악 실패를 전달한다. Effect 작업의 성공 메시지는 오류 색상과 구분한다.
- `ChartMakerSceneFlowTests`는 씬 직렬화 참조, 실제 UXML/동적 패널 요소, Effect 자동 선택 및 Preview 잠금 호출 경로를 고정한다.

### 5단계 실제 화면 확인 및 완료 상태

사용자가 Unity에서 실제 흐름을 수행했고 `Assets/Chart/chart.rd`, `effect.untitled.default.json`, `chart.rd.bak`이 생성됐다. `.rd`/JSON의 musicId, difficultyId, revision, effectId가 일치하고 backup이 기존 원본을 보존하므로 저장 유실은 없다. 최종 Effect Type은 `camera.offset`이며 수동 세션의 빨간 `EFFECT_TYPE` 로그는 적용·저장 전에 타입 미지정 상태로 Preview를 시도한 기록이다.

이를 사용자 오류가 아닌 설정 안내로 처리하도록 `ChartTestPlay`에 Preview 사전 검사를 추가했다. 미지정 Effect는 자동 선택되고 `Effect Type` 선택 후 `Apply`하라는 안내가 표시되며, Console error와 빨간 상단 상태는 쓰지 않는다. 타입이 없으면 재생할 수 없다는 데이터 규칙은 유지한다. 추가 회귀 `ChartPreview_UnresolvedEffectUsesSetupGuidance`를 포함한 전체 68개 검사가 통과했다.

앞서 원본 Editor를 유지한 채 시도한 임시 프로젝트 검사는 라이선스 채널 충돌로 테스트 전에 중단됐지만 임시 복제본은 안전하게 삭제했다. 이후 원본 Editor를 닫고 공식 스크립트를 다시 실행해 68/68 통과를 확인했다. 자동 수용과 사용자 실제 흐름 확인을 모두 충족했으므로 5단계는 완료다. 다음 즉시 작업은 6단계 실제 Gameplay 씬과 `GameplayChartEffectController` 연결 감사다.

## 기존 구현 탐색 지도와 과장하면 안 되는 부분

- `Assets/Scripts/ChartCoreDomain/EffectContracts.cs`, `EffectRegistry.cs`, `EffectRunner.cs`, `BuiltInChartEffects.cs`: 공용 실행 계약, 준비/등록, 시계 기반 실행, 공용 카메라 및 상태 공유 샘플 기믹.
- `Assets/Scripts/Gameplay/Effects/ChartEffectPreparation.cs`: 편집 정의/설정에서 준비 계획으로 변환.
- 같은 폴더의 `ChartEffectServices.cs`: 카메라 합성, 시간 구간 규칙, 테스트 상태 서비스.
- `GameplayChartEffectController.cs`: 실제 게임 연결을 위한 구성 요소/API는 있으나 **실제 Gameplay 씬 및 호출자 연결 완료로 간주하지 않는다**.
- `ChartTestPlay.cs`, `ChartScroll.cs`: Preview 실행과 카메라 합성 경계. 전체 UI/시각 검증은 별도로 필요하다.
- `NoteJudgementSystem.cs`, `RuleContext.cs`: 입력/효과/자동 판정의 시간 경계. 지연 입력과 규칙 소급 적용 방지 테스트 대상이다. 실제 입력 장치 검증을 대체하지 않는다.
- 정상 정의는 EffectRunner → CallMusicGimmickEffect → 세션의 MusicGimmick 명령으로 전달한다. 샘플 조건값은 실제 곡 규칙이 아니다.
- `.rd`의 기존 문자열 배열 노트 구조를 유지하며 Effect 정의와 수치 파일 연결을 확장한 상태다. 임의로 기존 형식을 다시 설계하지 않는다.
- 관련 기존 문서: `Assets/Docs/ChartCoreRefactoring.md`, `Assets/Docs/ChartFormat.md`.

다음 작업 보고에는 실제 변경 범위, 실행한 테스트 수/실패 이름, 미확인 동작, 다음 단계 하나를 분리해서 기록한다. 완료 상태가 달라지면 이 메모도 함께 갱신한다.
