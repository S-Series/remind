# ReMind Tasks

> 상태: Working Document  
> 현재 작업과 바로 다음 작업만 기록한다.

## 현황 검토 — 2026-09-30

기준은 `main`의 `978da3e`(`Game 전환 연출과 설정 오버레이 통합`)다.
검토 시작 시 `a5f6026` 이후 로컬 변경을 조사했고, 마무리 중 새 커밋에 반영된 것을
확인했다. 해당 현황 검토를 마무리한 시점의 미커밋 변경은 TASKS/ROADMAP 두 문서였다.
**제작→출력→Game 플레이→결과의
핵심 경로는 구현되어 있으며, 최신 변경의 통합 검증과 실제 사용 검수, 출시 범위 결정이
다음 단계다.** 아래 이전 검토 및 Verified의 날짜별 기록을 보존한다.

| 이번 확인 대상 | 직접 확인한 근거 | 판정과 다음 작업 |
| --- | --- | --- |
| 최신 C# 컴파일 | `dotnet build remind.slnx --verbosity quiet`: 경고 0, 오류 0 | 컴파일 통과. Unity 실행·제품 빌드 성공을 대신하지 않음 |
| 설정 모달 | `AppRoot.TryOpenSettings`, Bootstrap용 Prefab Builder, 모달 Scope·씬 이동 정리를 검사하는 smoke 코드 | 구현 및 검사 코드 존재. 최신 산출물 대조는 A2 |
| Bootstrap·전환 | 새 버튼 입력 대기, 일반 전환 Intro 시작 0.25초 뒤 로드, 곡 선택 전용 흰 결정 경로 | 구현 확인. 실제 입력·느린 로드·실패/중단 검수는 A2/B1 |
| UI 에셋 재배치 | 커밋 직전 삭제된 `.meta` 359개와 신규 `.meta`의 GUID 대조: 333개 대응, 중복 대응 0 | 대규모 삭제 표시를 기능 삭제로 단정하지 않음. GUID 대응은 파일 내용·Unity import 검증과 별개 |
| 대응 없는 메타 | 폴더 24개, `score_compass.png`·`header_compass.png` 2개 | 두 이미지의 기존 GUID는 현재 Assets 텍스트 참조 검색에서 발견되지 않음. 삭제 의도·대체 이미지와 import 결과는 A1/A2에서 확인 |
| 미리보기 씬 경로 | 실제 `Assets/Scenes/prev/CrystalPreview.unity`; 생성기 `CrystalTransitionBuilder.ScenePath`와 문서는 `ReMind_CrystalPreview.unity` | 경로 불일치 확인. A1에서 기존 씬의 의도와 GUID를 확인하고 코드·문서 기준 통일 |
| 테스트/제품 씬 경계 | DemoPlay fixture, 별도 `prev/DemoPlay`, Play의 Game 씬 10개·Chart의 ChartMaker 씬 1개 | 제품 목록에 테스트/prev/Settings 씬 없음. 두 DemoPlay의 역할과 보존 기준은 A1에서 명시 |
| 검증 기록의 출처 | 로컬 `Logs`의 최신 test-run XML은 `GameFlowTests5/results.xml`: 2026-09-26, 90/90 | 문서의 09-29 114개 및 09-30 smoke는 격리 복사본 이력. 원본 산출물·대상 파일 대응은 아직 미확인이며 테스트 감소/회귀로 해석하지 않음 |

이번에는 Unity Test Runner, PlayMode, 제품 빌드, 화면/청음/물리 입력 검수를
재실행하지 않았다. 위 GUID 검사는 삭제→신규 경로 대응 조사이며 프로젝트 전체의
Missing Reference나 중복 GUID 검사를 통과했다는 의미가 아니다.

### 최신 커밋의 변경 묶음과 통합 경계

| 묶음 | 주요 파일/영역 | 함께 확인할 경계 |
| --- | --- | --- |
| 설정·오디오 | AppRoot, SettingsMenuController, LocalGameSettingsStore, HitSoundPlayer, SettingsOverlay Prefab/Builder | v1→v2 저장 이행, 영속 Canvas, 모달 닫기·씬 변경·입력 복원 |
| 시작·씬 전환 | BootstrapLoadingController, SceneTransitionController, Bootstrap, Crystal 표시기·애니메이션·셰이더 | 새 입력 1회 처리, Intro/Loop/Outro, 흰 화면, 실패·중단 정리 |
| 에셋·씬 재배치 | `Assets/Art/UI`, `Assets/Art/TransitionFX`, `Assets/Scenes/prev`, 테스트 fixture | GUID·문자열 경로·import·생성기·제품 프로필 |
| 회귀·제품 구성 | PlayableGameSmokeRunner, REmindBaselineChecks, RuntimeProductBuilds, Play/Chart 프로필 | 위 변경과 동일한 원본에서 검사·빌드한 근거 |
| 문서 | PRODUCT/ARCHITECTURE/MIGRATION, 현행 계약, 보관 기록, ROADMAP/TASKS | 현행·목표·과거 기록 구분, 검증 범위와 다음 작업 |

위 묶음은 최신 커밋을 검수할 책임 범위다. 후속 작업에서 서로 같은 파일을
공유하는 변경은 통합 순서를 맞춘다. 특히 AppRoot·Bootstrap·smoke는 설정과 전환
양쪽이 수정하므로 동시 편집 대상으로 나누지 않는다.

### 바로 다음 작업의 상태

- **A1 경로·대상 고정:** 미리보기 경로와 DemoPlay fixture 역할을 정리하고 격리
  복사본의 해시를 확인했다. 삭제 이미지의 시각적 대체 의도는 별도 검수 대상이다.
- **A2 자동 회귀 실행:** 고정 복사본에서 EditMode 114/114와 두 곡 smoke가 통과했다.
  Unity Editor Search 시작 색인 예외가 반복되며 느린 로드·실패/중단·물리 입력 검수는 남았다.
- **A3 고정본 빌드 완료:** 같은 고정본에서 Game/ChartMaker Windows 빌드와 제품별
  DLL 경계를 확인했다. 이후 추가된 능력·정산 코드는 별도 기준선으로 검사한다.
- **D1 준비 가능:** 출시 필수 메뉴/진행, 수동 Test Play, 설정·포커스·지원 환경을
  결정할 목록은 PRODUCT에 있다. 기획 결정 없이 구현 범위를 임의로 확장하지 않는다.
- **B/C 준비 가능, 수동 수용 대기:** 실제 입력·청음·제작 검수 절차와 복합 채보를
  준비하고 A3 이후 같은 빌드로 확인한다. E2 측정 조건도 D1과 함께 정한다.

세부 선행 조건·완료 기준은 [ROADMAP.md](ROADMAP.md)의 A~F를 사용한다.

### 통합 기준선 실행 기록 — 2026-09-30

- A1의 미리보기 씬·생성기·문서 경로를 실제 `prev/CrystalPreview.unity`로 맞췄다.
  Tests의 DemoPlay fixture와 `prev` 보관본은 줄바꿈을 제외하면 같은 내용이며 GUID는
  다르다. 두 제품 프로필에는 어느 쪽도 포함되지 않는다.
- `Logs/IntegrationBaseline/20260930-0948/`에 Unity 원본 1,560개 파일과 격리
  복사본의 SHA-256 manifest를 남겼다. 복사 직후 경로·크기·해시 차이는 0개다.
  이후 다른 담당의 작업과 이 문서 갱신은 그 시점의 manifest에 포함되지 않는다.
- 과거 SettingsOverlay 격리 복사본의 PlayMode smoke 로그 2개에서 시작·통과 표시를
  직접 확인했다. 현재와 복사본의 관련 파일 8개 해시는 일치하지만 실행 당시 전체
  의존 파일 manifest는 없으므로 최신 A2 통과로 승계하지 않는다. 로그·해시 대조는
  같은 기준선 폴더의 `historical-settings/`에 보존했다.
- `dotnet build remind.slnx -m:1 --verbosity quiet`는 경고 0, 오류 0으로 통과했다.
  Unity EditMode와 두 제품 빌드는 Editor 라이선스 부재로 실행 전 종료 코드 198을
  반환했다. 이번 실행의 테스트 XML과 새 제품 바이너리는 없다. A2/A3는 미완료다.
  명령·로그 경로와 정적 검사의 범위는 같은 폴더의 `report.md`에 기록했다.

### 통합 기준선 재실행 — 2026-09-30

승인된 실행에서 Unity Personal 사용 권한이 확인되어 고정 복사본의 EditMode
**114/114**, 두 곡 smoke 2회, Play·Chart 독립 빌드를 완료했다. 두 smoke의
`PASSED`와 별개로 Unity Editor Search 색인 예외가 반복됐다. 자동 검사 밖의 일반
전환 느린 로드와 실패/중단, 화면·청음·물리 입력은 검증하지 않았다.
Game에는 ChartMaker DLL이, ChartMaker에는 Gameplay DLL이 없고 프로필에는
Tests/`prev` 씬이 없다. 대상 1,562개 파일 manifest, XML, smoke·빌드 로그,
제품 208/184개 파일 manifest와 Unity가 갱신한 설정 5개 이력은
`Logs/IntegrationBaseline/20260930-A2A3/report.md`에 연결했다.

### 캐릭터 능력·정산·성능 확장 검토 — 2026-09-30

사용자가 제시한 능력 구성을 기존 작업 트리의 D1/D2와 E2로 나누었다.
아래 표를 작성한 시점에는 코드 경계 조사와 계획 문서 작성까지였다. 이후의 예시
능력·정산 계약 구현과 Unity 결과는 바로 아래 실행 기록으로 구분한다.

| 영역 | 현재 확인한 경계 | 후속 작업 |
| --- | --- | --- |
| 시작 능력·게이지 | Game의 `GameRule`/`IRuleModifier`에 최대 HP·체력 증감 등 보정점이 있다. 게이지 enum만으로 규칙이 바뀌지 않으며 최대 HP 증가도 기본 초기 HP를 늘리지 않는다. | D1에서 중첩·충돌·최대/초기 HP 정책을 결정하고 D2a에서 장착·곡·월드·전체 게이지 규칙을 세션에 고정 |
| 조건부 능력 | `RuleContext`에 체력·콤보·노트 종류·평가 시각이 있으나 현재 Fever/MissGuard는 false다. 횟수·쿨다운 상태와 명시적 발동 경계는 추가 필요하다. | D2b에서 후보 조회와 결과 확정/소비를 분리. 방어→체력/콤보, 콤보 갱신→회복, 실패 확정 전 생존 순서 검증 |
| 판정 경로 차이 | Long Scratch Mid/End는 별도 판정이며 자동 Miss는 공용 세션이 직접 만든다. modifier 조회는 후보 탐색·실패 조건 검사에서 반복될 수 있다. | 일반 판정 modifier만 연결해 완료로 보지 않음. 자동 Miss·Long 구간과 반복 조회의 1회 발동 정책을 D2b에서 검증 |
| 보상·월드 | `MemoryFragmentCount`는 클리어한 곡·난이도 기록 수이며 임의 배수를 저장하는 잔액 모델이 아니다. 캐릭터 보유/장착·월드 진행은 Game 책임이다. | D1에서 지급 자격·잔액·반올림·초과 진행 정책 결정. D2c에서 순수 정산 계산과 저장 적용을 분리하고 한 판의 변경을 정확히 한 번 반영 |
| 판정 탐색 비용 | 입력 후보·다음 자동 시각·자동 처리 대상이 전체 노트 목록을 순회한다. E2a 순수 Core 반복 측정에서 밀집 2,000노트 순회 17,028,000항목을 기록했다. Unity Player 병목은 아직 측정하지 않았다. | D2 통합 뒤 기준선 갱신→E2b 레인/커서/활성 Long·deadline 관리→E2c 의미 동등성 회귀·재측정→E2d 표시·곡 선택·HUD 개선 |

#### 캐릭터 예시 계약 검증 — 2026-09-30

Game 전용 신규 파일에 능력 종류 네 개를 정의했다. 월드 진행 +20%와 보상 +50%만
시작 snapshot과 순수 decimal 계산에 연결했고, 나머지 두 종류는 의미·값을 정하지
않은 자리다. 이 1,577개 파일 검사 당시 예시는 Hard 게이지 종류만 요구했고
실제 Hard 규칙 자산, 최대/초기 HP·실패 정책은 아직 없었다. 종료 자격, 실제 지급,
중복 방지, 월드 진행 저장은 아직 연결하지 않았다.
별도 1,577개 파일 고정본의 Unity EditMode는 **122/122 통과**했다. 기존 114개에
신규 능력·정산 테스트 8개가 추가됐으며 실패·건너뜀은 0개다. XML·로그·해시는
`Logs/IntegrationBaseline/20260930-abilities/report.md`에 기록했다.

#### D2a 시작 조건·Hard 세션 통합 검증 — 2026-09-30

선택 API가 캐릭터 정의와 전체 게이지 규칙을 함께 받으며, Game 시작 때 채보 ID와
규칙을 검증하고 독립 복사본을 세션에 적용한다. 시작값은 `GameAttemptStartSnapshot`에
복사되어 Result까지 전달된다. Pause/Resume은 같은 시도와 HP를 유지하고,
성공한 Retry는 새 시도와 선택된 규칙을 적용한다. 거부된 시작/Retry는 기존
재생·규칙을 보존하며 Game의 오류 패널 또는 재생 중 안내로 이유를 표시한다.
Auto Play를 시작 뒤 사용했다가 꺼도 그 시도는 기록·진행·첫 클리어 보상에서
제외한다. 개발용 Hard 규칙은 최대/초기 HP 100, Perfect/Great +1, Good -5,
Miss -20, 클리어 HP 1, HP 0 즉시 실패/계속 불가로 동작했다.

실행 시점 최종 격리본은 1,587개 대상 파일 해시가 원본과 일치했다. 런타임·테스트 파일이
같은 직전 본에서 EditMode **130/130**, 기존 두 곡 Normal smoke가 통과했고,
최종 본의 `RunWithHard`도 종료 코드 0으로 통과했다. 같은 최종 본의 Game Windows
빌드는 Unity BuildReport `Success`, 출력 208개 파일이며 Gameplay DLL만 포함하고
ChartMaker DLL은 제외했다. 이전 독립 ChartMaker 빌드 이후 ChartMaker/Shared/
assembly·빌드 프로필에는 변경이 없어 해당 빌드는 재실행하지 않았다.
Editor Search 시작 예외와 스모크의 강제 시간 진행 로그는 남아 있다. 실행·해시·
로그·자동 종료 누락에 따른 배치 Editor 정리는
`Logs/IntegrationBaseline/20260930-D2a-r4/report.md`에 기록했다.

이는 D2a의 **선택 입력·게이지/시도 고정 경계**를 검증한 범위다. 캐릭터 장착의
정식 UI와 월드 대상 고정, 조건부 발동·횟수·수명, 종료 자격과 실제 지급·중복
방지·저장 이행, 최종 Hard 밸런스는 완료로 계산하지 않는다. 실제 화면·청음·
물리 입력과 빌드 실행도 별도 검수 대상이다.

#### E2a 판정 비용 기준선 — 2026-09-30

순수 ChartCore 판정 재생을 간섭 프로세스 없는 조건에서 두 번 측정했다. 밀집 2,000노트
전체 재생 중앙값은 각각 68.915ms/66.098ms, 전체 순회는 17,028,000항목,
측정 구간의 현재 스레드 할당은 0B다. 이전 기준선↔첫 통제 실행, 첫 실행↔둘째 실행의
입력·판정 파일은 각 10개씩 바이트 단위로 일치했다. 조건·다른 채보 결과·한계와
로그 경로는 `Tools/Performance/README.md`에 둔다. 이 값은 전체 재생 시간이며
Unity Player의 프레임 시간이나 최적화 전후 개선율이 아니다. D2 통합 후 비교
기준선을 갱신해 E2b 탐색 개선과 E2c 동등성 검증으로 이어간다.

정책 미결정 목록과 D2a~D2d/E2a~E2d의 완료 기준은
[ROADMAP.md](ROADMAP.md#d1에서-결정할-캐릭터-능력정산-정책)에 둔다.
이전 Hard/+20%/+30%는 설명용 수치였고, 현재의 Hard 요구·월드 +20%·보상 +50%도
제품 적용 전 테스트 예시다. 재시도 시 조건 재선택,
원판정/최종 판정 기준, 효과 만료와 같은 시각 이벤트 순서도 D1에서 결정한다.
`EffectRunner`는 채보 시간표 책임을 유지한다. 두 제품에서 동일한 의미가 필요한 순수
규칙만 Shared 후보로 검토하며 ChartMaker가 Game의 캐릭터·사용자 저장에 의존하지 않게 한다.
능력 담당과 최적화 담당은 같은 판정 핵심 파일을 동시 편집하지 않는다. 초기 계측·회귀
설계는 병행하되 D2a~D2d 통합·회귀 후 갱신한 기준선을 E2b 담당에게 인계한다.

## 이전 현황 검토 — 2026-09-29

기준 커밋은 `a5f6026`이며 그 이후의 로컬 변경을 포함해 조사했다.
현재 평가는 **핵심 제작→Game 실행 경로와 기본 사용자 기능을 구현했고,
최신 UI/전환 통합 및 실제 사용 검수를 진행해야 하는 상태**다.
전체 제품 완료율을 테스트 개수나 연결된 메뉴 수로 환산하지 않는다.

| 영역 | 코드에서 확인한 상태 | 남은 확인/작업 |
| --- | --- | --- |
| Shared / 제품 분리 | 공용 컴파일·시간·판정·Effect와 별도 assembly, Play/Chart Build Profile | 최신 변경과 두 제품 빌드의 검증 대상 일치 확인 |
| ChartMaker / 콘텐츠 | 단일 `.rd` 저장, 복구·Preview·패키지 출력, 복합 채보 자동 검사 | 실제 제작 UI와 Game 화면/청음 대조, 수동 Test Play 요구 확정 |
| 판정 | 일반/Start ±50ms Perfect·±100ms Good, Mid ±100ms·End ±75ms Perfect | 수치 재결정이 아니라 실제 입력·표시·청음 검수 |
| 결과 / 진행 | 최고 기록·즐겨찾기·횟수·클리어·최고 콤보, 첫 클리어 기억 조각 | 반복 플레이·재실행 검수, 추가 진행·서사 정책 |
| 설정 | AppRoot 영속 모달, 저장 형식 v2/v1 이행, 음량·판정 보정·10레인 키 설정 | 물리 키·오디오·화면 검수; Voice 소스와 빈 카테고리는 미완성 |
| Bootstrap / 전환 | 일반 Intro/Loop/Outro와 곡 선택 전용 흰 결정 연출 | 최신 전환 회귀 및 실패/중단 경계 검수; 시작 백분율은 시간 기반 연출 |
| 최종 제품 범위 | 두 곡 테스트 콘텐츠와 일부 임시 메뉴 | 정식 콘텐츠, 메뉴별 출시 범위, 성능·배포 환경 검수 |

2026-09-29 검토에서 `dotnet build remind.slnx --verbosity quiet`를 실행해 **경고 0, 오류 0**을
확인했다. 먼저 수행한 `--no-restore`는 Temp의 테스트 프로젝트 복원 산출물 부재로
실패했고, 일반 빌드가 복원 후 통과했다. 코드 수정으로 오류를 숨기지 않았다.
Unity Test Runner·PlayMode·제품 빌드·수동 조작은 해당 검토에서 재실행하지 않았다.
아래 Verified의 114개 통과 등은 기존 작업의 검증 기록이며, 해당 조사에서는 그 실행의
XML/로그 원본과 최신 변경 전체의 일치까지 확인하지 않았다. 다음 A 작업에서 연결한다.

이전 제안의 **실패 테스트 4개 복구, 복합 채보 검사, 판정 창 확정, 설정·기본 보상,
Build Profile 추가는 이미 반영됐다.** 이를 신규 구현으로 중복 배정하지 않는다.
다만 최신 수정에 영향받는 검사는 다시 실행한다.

현재 미커밋 변경은 설정 모달/오디오, Bootstrap/씬 전환/결정 아트,
DemoPlay의 테스트 fixture 이동, 이에 따른 프로필·회귀 검사·문서로 묶는다.
사용자의 씬·아트 변경을 보존하고 같은 파일의 동시 수정을 피한다.
장기 작업 ID·선행 조건·완료 기준은 [ROADMAP.md의 실행 작업 트리](ROADMAP.md#실행-작업-트리)를 따른다.

## Current

- `Result` 씬에 참고 이미지 기준의 곡 정보, 난이도, 점수·랭크, 판정 통계,
  콤보·정확도, 기억 조각, 보상과 하단 메뉴를 uGUI로 배치했다. 배경과 재킷은
  기존 이미지를 임시로 연결했다. 씬에 저장된 수치는 편집용 예시 값이며
  첫 클리어 기억 조각 보상을 연결했다. `RETRY`는 Game으로,
  `NEXT`와 `MUSIC SELECT`는 MusicSelect로 이동하도록 연결했고 Game 빌드
  씬 목록에 Result를 추가했다. 화면 요소는 배경·헤더·곡 정보·점수·랭크·
  판정·성능·기억·문구·보상·푸터·버튼 영역별로 묶었다. 자켓과 아이콘 자리
  10곳은 정사각형 Image 슬롯으로 두어 Sprite를 교체할 수 있다.
- Game 완주·실패 시 세션에서 점수, 랭크, 판정 수, 최대 콤보를 결과 요약으로
  확정해 Result 씬으로 전달한다. Result는 곡 카탈로그의 제목·아티스트·재킷·
  난이도와 함께 이 값을 표시한다. 정확도는 최대 점수 대비 점수 비율로 표시한다.
  신기록은 로컬 최고 점수 갱신 시 표시한다. 기억 영역에는 저장된 플레이 기록을
  표시한다. 첫 클리어 보상은 표시하며 곡별 서사 문구는 데이터가 없어 숨긴다.
- 곡 데이터에 `musicVolumeMultiplier`를 추가했다. `0`~`2`를 검증하고
  `1`을 AudioSource 볼륨 `0.5`로 해석한다. Music Select의 미리듣기는
  구간 끝에서 페이드아웃한 뒤 시작점에서 페이드인하며, ChartMaker가 곡
  데이터로 음원을 열 때도 같은 볼륨을 쓴다. Game은 선택한 곡의 볼륨을 같은
  `data.json`에서 읽는다. Unity 청음 확인은 남아 있다.
- `Data/Music/i`와 `designant`의 음원·재킷·채보 파일을 곡 폴더
  바로 아래로 정리했다. `data.json` 형식 버전 1은 `audio.mp3`, `art.jpg`,
  `<difficultyId>.rd`를 기본 파일명으로 사용한다. 채보는 전용 재킷 파일명을
  선택적으로 소유하며 기본 `<difficultyId>.jpg`가 없으면 공통 재킷을 쓴다.
  ChartMaker 저장·재열기 및 Music Catalog의 난이도별 재킷 연결을 수정했다.
  자동 저장·재열기 검사는 통과했고 실제 UI 화면 확인은 남아 있다.
- Game 전용 `Bootstrap` 씬과 하이어라키의 `AppRoot`를 추가하고 Game 빌드의
  첫 씬으로 지정했다. 루트는 Home 진입, 곡/난이도 선택, 결과 요약의 1회 전달을 맡는다.
  Bootstrap→Home을 포함한 일반 씬 이동에는 투명 결정 오버레이의 1초 Intro,
  Intro 시작 0.25초 뒤 씬 로드 요청,
  로딩 중 0.8초 반복 Loop, 1초 Outro와 어두운 덮개를 사용한다. 로딩이 빠르면
  Loop를 생략해 기본 전환은 2초다. Bootstrap의 배경 이미지 없는 UI 계층은
  HomeUI/ResultUI 장식을 재사용해 로고·문구·진행 표시를 보여준다.
  자동 진입을 끄고 씬 전용 컨트롤러가 로딩 완료 후 새 키·클릭·터치·게임패드
  버튼 입력을 받으면 `CompleteBootstrapLoading()`을 호출한다.
  Music Select→Game에만 별도의 흰 결정 연출을 사용한다. 실행 패키지와 재생
  세션은 Game 씬이 소유한다.
- `Assets/Data/Music/i`를 `data.json` 형식 버전 1, 음원, 재킷, 단일 채보 파일로
  정리했다. 공용 `SongContentCodec`와 ChartMaker 저장·열기 검증, Music Select
  행의 곡명·아티스트·레벨 읽기를 연결했다. `level: 0`과 `Unknown`은 테스트
  값이므로 정식 콘텐츠 메타데이터를 받으면 교체한다. 파일의 ID·revision·GUID와
  공용 JSON 파서, Unity EditMode 검사를 확인했다.
- Home 메뉴의 씬 배치 버튼에 공통 선택 Scope/Node와 UI 입력 모듈을 연결했다.
  Game의 선택·일시정지·결과·오류 메뉴도 씬의 `GameFlowCanvas`로 옮겼다.
  Home 중앙은 Music으로 바꾸고 Gallery를 ReMind로 교체했다. Story, Music,
  Character, ReMind, Option에는 각각 독립 임시 씬과 Home 복귀를
  연결했다. Music의 샘플 버튼은 Game으로, Game 곡 선택의 Cancel은 Music으로,
  Home의 Exit는 종료로 연결한다.
  이번 UI 변경은 C# 빌드와 씬 YAML 구조를 확인했다. Unity Editor에서
  Home과 Music의 Canvas 표시도 확인했으며 실제 메뉴 입력·씬 전환은 아직
  확인하지 않았다.
- Game의 선택→플레이→일시정지/재개→결과→재시도 흐름을 연결했다.
  현재 `i`와 `designant`는 각 곡의 `.rd`에서 출력한 실행 패키지를 사용한다.
- 플레이 중 ESC로 연 일시정지 메뉴를 다시 ESC로 닫도록 연결했다. UI Cancel과
  게임 입력이 같은 프레임에 들어와도 Pause/Resume이 중복 전환되지 않게 했다.
  두 곡 PlayMode 스모크에서 같은 프레임 차단과 다음 프레임 재개를 확인했고,
  실제 ESC 키 입력도 확인했다.
- Settings를 Bootstrap `AppRoot` Canvas에서 Home 위에 뜨는
  `SettingsOverlay.prefab`으로 옮겼다. Home은 AppRoot를 통해 모달을 열고,
  씬 변경 시 오버레이의 탐색 참조를 정리한다. 이미지의
  Audio 페이지를 마스터/BGM/SFX/Voice 볼륨, 히트 사운드 스타일, 시스템 기본
  출력 장치 링크, 공간 음향, 비활성화·배경 재생 옵션으로 구성했다. Rhythm에는
  판정 보정 -200~+200ms(5ms 단위), Controls에는 10레인 키 변경·초기화를 둔다.
  값은 진행 기록과 분리된 `player-settings-v1.json`의 형식 버전 2에 즉시 저장한다.
  기존 버전 1 데이터는 설정값을 보존해 읽는다. BGM은 미리듣기·Game에,
  마스터는 AudioListener에, SFX/히트 스타일은 판정 효과음에 적용한다.
  Voice 음원은 아직 없으며 나머지 카테고리는 탐색용 자리다. ChartMaker에는
  Game 플레이어 설정을 적용하지 않는다.
- Result의 기존 기억 영역에 곡·난이도별 누적 플레이 횟수, 최고 콤보, 클리어 이력과
  첫 클리어 표시를 연결했다. 실패한 플레이도 횟수에 포함하고 Auto Play는 저장하지
  않는다. `player-progress-v1.json`의 기록만 사용한다. 곡·난이도 첫 클리어마다
  기억 조각 1개를 획득하며 보유 수량은 클리어 기록에서 계산한다. Result의 보상
  칸에는 첫 클리어 시에만 `×1`을 표시한다. 기존 클리어 기록도 보유 수량에 포함한다.
- Game 판정은 공용 `PlayableChartSnapshot`과 `PlayableJudgementSession`을 직접
  사용한다. `NoteData` 변환 경로와 옛 레인 큐는 제거했다.
- Long Scratch Start 양입력, Mid 유지 확인과 양입력 재합류, End 음입력 및
  구간별 점수·콤보·체력 처리를 구현했다. 노트 종류별 시간 창 파일은 만들었으나
  판정 창은 일반/Start ±50ms Perfect, 50ms 밖~100ms Good, Mid ±100ms
  Perfect, End ±75ms Perfect로 확정했다. End 미입력은 +75ms 뒤 Miss다.
  기존 구간 점수에서 Start 입력 등급이 첫 구간에 이어지는 규칙은 유지한다.
- Game과 ChartMaker는 별도 assembly와 별도 Windows 빌드로 유지한다.
- Home의 MUSIC 버튼을 `MusicSelect`로 연결했다. 현재 두 곡 행을 생성하며,
  분류·정렬·즐겨찾기·난이도 선택과 Home 복귀는 저장된
  씬 하이어라키와 버튼 이벤트로 구성했다. 기존 `Music` 임시 씬은 유지하며
  새 화면의 곡 선택·Game 진입·결과 복귀 및 로컬 최고 기록 저장을 연결했다.
  첫 곡 행은 씬의 `Music Track`을 사용한다. `Slot`을 선택 버튼으로 연결하고
  재킷·곡명·아티스트·즐겨찾기 표시를 행 데이터와 연결했다. 곡명과 아티스트의
  넘침 애니메이션도 이 행의 Viewport 하이어라키에 연결했다.
- `MusicSelect`는 배경 그림 없이 Home의 남색·세리프 서체·나침반 장식에 맞춰
  왼쪽 곡 정보, 중앙 폴라로이드형 재킷 카드, 오른쪽 곡 목록을 디자인했다.
  `Stellar Promise`의 재킷 이미지를 별도 에셋으로 넣었다. 난이도 링, 탭 밑줄,
  선택 행 강조와 썸네일은 씬 하이어라키에 배치했다. 곡 선택 시 재킷과 행의
  상태 이미지는 직렬화된 참조로 갱신한다.
  Track 1의 곡명·아티스트에는 넘칠 때만 이동하고 끝에서 페이드 후 처음으로
  돌아오는 TMP 표시 영역을 하이어라키에 연결했다. Unity 화면 동작은 미확인이다.
  ReMind UI PNG 팩의 런타임 스프라이트 44개를 `Assets/Art/UI/ReMindUI`에
  가져왔다. Sprite/9-slice 설정은 `.meta`에 기록했다. MusicSelect의 곡 Slot,
  Favorite 아이콘, 재킷 프레임, 분류 탭, 난이도 링, 시작 버튼에 대응하는
  Sprite를 연결했다.
  그중 상태별 중심이 어긋난 19개 PNG의 픽셀 위치를 보정했다. Unity 화면은 미확인이다.
  씬 YAML 참조와 C# 빌드는 확인했고 실제 화면 입력 확인은 남아 있다.

## 이전 기준선 변경 묶음 (2026-09-28 이력)

아래는 당시의 변경 분류다. 다수는 `a5f6026`에 반영됐으며 현재 미커밋 목록으로 사용하지 않는다.

- 곡 콘텐츠와 카탈로그: `i`·`designant`의 `data.json`, 음원·재킷·`.rd` 경로,
  `rmp/` 실행 패키지, 카탈로그 참조.
- 제작 및 공용 데이터: ChartMaker 저장·재열기·Effect 내장 형식, 공용 곡 데이터와
  채보 변환·검증, 패키지 출력.
- Game 흐름과 로컬 진행: Bootstrap, Music Select, Game, Result 연결,
  판정·세션·결과, 최고 기록·즐겨찾기 저장.
- 화면과 아트: Home·ReMind·Result 등 씬 및 UI 에셋과 이미지. 이 묶음에는
  기준선 작업 이전부터 존재한 사용자 변경도 포함된다.
- 검사와 빌드: EditMode/PlayMode 검사, DemoPlay 전용 fixture,
  제품별 빌드 설정·도구·문서.

## Verified

- 2026-09-30 `SettingsOverlay`를 Bootstrap의 영속 `AppRoot` Canvas에 연결한 뒤
  `dotnet build remind.slnx` 경고·오류 0개와 격리된 Unity Game PlayMode 전체
  스모크를 확인했다. Home 열기·설정 저장·닫기, 열린 상태의 씬 이동 뒤
  모달 비활성화와 탐색 참조 해제를 검사했다. 실제 화면 클릭과 비율은 미확인이다.
- 일반 화면 전환의 씬 로드 요청을 Intro 시작 0.25초 뒤로 옮긴 뒤
  `dotnet build remind.slnx`와 격리된 Unity Game PlayMode 전체 스모크가
  통과했다. 실제 화면의 끊김 감소는 수동 확인이 필요하다.
- 2026-09-29 격리된 Unity 6000.3.15f1 복사본에서 Bootstrap 로딩 완료 뒤
  입력 전 자동 전환이 없고 `TryContinue()` 호출 뒤 Home 전환이 시작되는 것을
  전체 Game PlayMode 스모크로 확인했다. 실제 키·마우스·터치·게임패드 입력은
  직접 조작해 확인하지 않았다.
- 2026-09-29 격리된 Unity 6000.3.15f1 프로젝트에서 Settings Prefab 생성과
  당시 Home 인스턴스 연결, EditMode 114개, 두 곡 PlayMode 스모크, Game·ChartMaker
  Windows 빌드가 통과했다. 오버레이를 열어 값 변경·저장, 키 변경 취소,
  닫은 뒤 Home 복귀를 검사했다. 제거된 DemoPlay 런타임 씬의 기존 검사 규칙은
  `Assets/Tests/Fixtures/Scenes/DemoPlay.unity`에서 유지한다.
- Music Select에서 두 곡 각각 Play→48프레임 전환→Game→Result→Music Select를
  격리된 Unity PlayMode 스모크로 확인했다. 프레임 진행, 중복 Play 차단,
  Game 진입 뒤 오버레이 정리도 검사했다. Unity EditMode 113개와 Game Windows
  빌드가 통과했다. 실제 화면 크기·체감 속도·입력은 수동 확인 목록에 남긴다.
- `designant`와 `i` 각각 Music Select→Game 판정·점수 집계→Result 실제 표시→
  Music Select 복귀를 격리된 Unity PlayMode 스모크로 확인했다. Auto Play 결과는
  최고 점수에 저장되지 않는다. 로컬 최고 점수·즐겨찾기 저장과
  백업 복구 EditMode 테스트 2개가 통과했다. 전용 Game Windows 빌드에 Result 씬이
  포함되고 ChartMaker DLL이 빠진 것도 확인했다. ChartMaker Windows 빌드에도
  Gameplay DLL이 없다. 화면에서의 실제 키 입력과
  곡 전체 길이 재생은 별도 수동 확인이 필요하다.
- 2026-09-28 기준 격리된 Unity 6000.3.15f1 프로젝트에서 EditMode 113개가
  모두 통과했다. 기존 실패 4개를 현행 단일 `.rd` 샘플과 `rmp/` 패키지로 복구했다.
  추가된 복합 채보 검사는 Tap·Hold·Scratch·Long Scratch·BPM·스크롤·Effect를
  편집 상태에서 저장→재열기→Preview→패키지 출력→Game 구성요소 실행까지
  통과시키고 노트 지점·시간·바닥 위치·Effect 설정을 양쪽에서 대조한다.
- 설정 저장·재로드·백업 복구·값 검증 테스트와 Home Settings 오버레이→Home→
  두 곡 Game 스모크가 통과했다. 저장한 음량·판정 보정·레인 키가 Game에
  반영되는 것을 확인했다. 새 Settings 화면의 실제 표시·물리 키 변경은 수동
  확인이 남아 있다.
- 진행 기록 EditMode 검사는 실패→첫 클리어→재도전 후 플레이 횟수·클리어·
  최고 점수·최고 콤보 재로드를 확인했다. 두 곡 PlayMode 스모크는 Result의
  기록 표시, 첫 클리어 보상과 디스크 저장을 확인했다. Game·ChartMaker
  Windows 빌드도 다시 통과했다.
- 같은 격리 복사본에서 Game·ChartMaker Windows 빌드와 두 곡 PlayMode 자동
  스모크가 통과했다. 첫 곡 실패→Result→재시도→완주, 두 번째 곡 완주,
  Pause/Resume/Restart, 결과 복귀, 최고 점수·즐겨찾기 디스크 재로드를 확인했다.
  제품 빌드의 Managed DLL에는 상대 제품 assembly가 없다.
- `Play`·`Chart` Build Profile의 제품별 씬 목록·define을 구성했다. 프로필을
  직접 사용한 Windows 빌드가 통과했고 상대 제품 DLL도 포함되지 않는다. 임시
  Story·Character·ReMind·Option·Music 씬의 표시용 UI와 Home 복귀를 자동
  스모크로 확인했다. ChartMaker 빌드 뒤 Editor 활성 프로필을 원래대로 복원하여
  이어지는 Game PlayMode도 통과했다. 정식 콘텐츠 메타데이터·아트는 아직
  제공되지 않아 임시 표시다.
- Gameplay의 프레임 콜백을 정적 조사했다. 판정은 `NoteJudgementSystem`의
  단일 `LateUpdate`에서 처리하며 노트별 `Update`는 없다. 실제 프레임 시간·GC·
  메모리와 OS 호환성은 배치 스모크로 판정할 수 없어 하드웨어 계측 대상으로 남긴다.
- `dotnet build remind.slnx` 경고 0, 오류 0.
- 실제 화면, 청음, 물리 키 입력과 복합 채보의 화면 배치는 자동 검사의 범위 밖이다.

## 수동 확인 목록 — 사용자 확인 시 재개

- 두 곡과 ChartMaker UI의 화면·청음·물리 키 입력을 수동 검수한다.
  10레인 입력, 음원·판정 표시 동기화, Long/Scratch 길이·깊이·레인,
  Effect 카메라, 일시정지·재시도·결과, Music Select 미리듣기와 UI 탐색을 확인한다.
- Settings Prefab의 실제 화면 비율·겹침, 오디오 항목의 청음, 판정 보정 체감,
  레인 키 재지정·중복 거부·ESC 취소,
  게임 재실행 뒤 설정 유지와 플레이 반영을 확인한다. 사용자가 나중에 확인한다.
  자동 메서드 호출과 버튼 스모크를 물리 입력 성공으로 간주하지 않는다.
- 일반 씬의 Intro/Loop/Outro·어두운 덮개와 Music Select→Game의 흰 결정 연출이
  실제 화면 비율에서 잘리지 않고 의도한 속도로 보이는지, 전환 중 클릭·ESC와
  Game 진입 뒤 화면·음원이 자연스러운지 직접 확인한다.
- 정식 화면 아트와 곡 레벨·채보 제작자 메타데이터가 준비되면 임시 표시를
  교체하고 Game/ChartMaker 빌드에서 화면·입력을 직접 다시 확인한다.

## Next — 작업 순서

1. **A1~A3: 최신 통합 기준선.** 설정 모달·Bootstrap·전환·UI 에셋/fixture 이동이 포함된
   대상 파일을 고정한다. 미리보기 씬과 생성기 경로를 맞추고 GUID·참조를 확인한 뒤
   EditMode, 두 곡 PlayMode smoke, Play/Chart 빌드를 확인한다.
   일반 Intro/Loop/Outro와 흰 결정 경로를 구분한다. 예전 48프레임 스모크의 통과를
   최신 전환 전체 검증으로 간주하지 않는다. 로그·XML·대상 파일 해시를 연결해 남긴다.
2. **D1: 출시 범위 결정.** 임시 메뉴, 빈 설정 카테고리·Voice, 수동 Test Play,
   진행·해금·서사, 목표 하드웨어/지원 환경을 출시 필수와 후속으로 구분한다.
   능력 중첩·게이지/초기 HP·발동/만료·재시도·정산 자격과 월드 진행 정책을 확정하고
   D2a 시작 조건→D2b 조건부 발동→D2c 종료 정산→D2d 캐릭터 한 명 검증으로 배분한다.
   사용자 기획 결정은 임의로 확정하지 않으며 A와 병행해 준비한다.
3. **B/C: 실제 Game·ChartMaker 검수.** 사용자 수동 확인 일정에 맞춰 위 확인 목록과
   복합 채보 제작→Game 대조를 수행한다. 정식 아트가 없어도 테스트용 콘텐츠로 진행한다.
   ESC 확인 이력은 보존하되 전체 물리 입력 검수로 확대 해석하지 않는다.
4. **E2 후속 측정·최적화.** E2a의 순수 Core 판정 기준선은 기록했다. 합의한 하드웨어의
   Unity Player 프레임 시간·GC·메모리·전환 비용은 아직 계측해야 한다. D2 통합 후
   기준선을 갱신해 E2b/E2c로 판정 탐색을 줄이고 동등성을 검증한다. 표시·곡 선택·HUD는
   E2d에서 측정에 따라 개선한다.
   최종 콘텐츠가 들어오면 영향을 받은 구간을 다시 측정한다.

이후의 기능·콘텐츠·레거시 정리·배포 작업은 ROADMAP의 D2~F에서 관리한다.
`ChartLoader`/`NoteData`는 `ChartLoadService` 내부 참조와 직렬화 사용까지 확인한 뒤
별도 작업으로 정리한다. 위 계획 작성은 구현·배포·commit/push 실행을 뜻하지 않는다.

## Notes

Effect 작업 1~7단계는 전역 `ROADMAP.md`의 Phase 번호와 다르다.
