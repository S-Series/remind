# ReMind Architecture Migration

> 상태: Living Document  
> 이 문서는 현재 Repository의 과도기 구조와 구조개편 진행 상황을 기록한다.  
> 완료된 마이그레이션 내용은 계속 갱신하거나 제거할 수 있다.

## 1. Current State

### 중앙 Floor / FloorLong 제작·판정 확장, EditMode 통합·독립 빌드 확인 (2026-10-11)

새 Floor 단타는 `CenterTap`, FloorLong은 `CenterHold`로 중앙 레인 `10`을 사용한다.
기존 v1 좌우 Scratch/Long Scratch는 변환 없이 읽기·편집·저장하며 기존 일반 Hold도 유지한다.
Maker의 Floor/Floor Long 메뉴를 중앙 배치로 연결했고 `.rd` v2 `centerNotes`에
`holdStart`/`holdEnd` 쌍을 저장한다. 순서 오류·미완성 쌍·단타와 롱 Start의 같은 위치는
codec/adapter에서 거부하며 공용 컴파일러도 같은 Start timestamp 충돌을 거부한다.
롱 도중/End의 단타는 허용한다. Runtime v2는 Kind `7`, Lane `10`, 기존 Start/End 두 점을 쓰며
새 DTO 필드는 없다. 세부 계약은 [ChartFormat.md](ChartFormat.md) 18.3절과 20절을 따른다.

판정은 공용 세션에 추가했다. 새 Start Down 뒤 좌/우 Shift와 ON인 Space 중 하나라도
유지하면 End에서 Start 등급으로 1회 자동 확정한다. 같은 timestamp의 키 교체는 허용하며
양수 시간의 해제 유예·재합류·tick과 End의 정확한 Up 요구는 없다. Knob Step의 새 Down
우선 소비·Space의 Knob 이동 금지와 기존 Hold/Long Scratch의 End release는 유지한다.
양제품의 중앙 단타·롱 끝점·구간은 기존 Floor 프리팹의 Mesh·재질을 사용하고
`FloorNoteGeometry`의 bounds 계산을 공유한다. 표시 계산은 판정을 소유하지 않는다.

사전 순수 C# 검사는 **128/128** 통과했다. 독립 검토에서 확인한 Maker 롱 끝점의 배율 갱신
누락 수정 후 고정본에서 Unity EditMode 전체 **413/413**, 집중 제품 소스 **149/149**,
수정 씬 연결 probe **2/2**와 v2 export→Prepare→실제 Game 표시 생성 probe **2/2**가 통과했다.
Game/ChartMaker 독립 Windows 빌드와 상대 제품 DLL 제외를 확인했다. 집중 151건 실행은
별도 probe의 잘못된 SpriteRenderer 가정으로 150통과·1실패였으며 제품 소스 실패가 아니다.
해당 probe를 MeshRenderer 계약에 맞춰 수정한 뒤 2/2를 확인했다. 횟수는 중복 합산하지 않는다.

실행 고정본은 [source manifest](../../Logs/IntegrationBaseline/20261011-center-floor/source-manifest.json)의
1,981파일 기준이다. 격리 Unity가 설정·폰트 7개를 재직렬화했고 원본에는 복사하지 않았다.
이번 네 문서의 후속 갱신은 실행 manifest의 문서 해시와 다르다. 원본 전체와 바이트 단위로
동일한 빌드로 해석하지 않는다. 카메라 픽셀·물리 입력·곡 전체 재생·청음·최종 외관은 미검수다.
세부 검사·로그와 차이는 [Floor 통합 보고서](../../Logs/IntegrationBaseline/20261011-center-floor/README.md)
및 [TASKS.md](TASKS.md)를 따른다. 아래 Knob/인벤토리의 이전 결과와 별개의 실행 기록이다.

### Knob Start/Middle 및 outgoing 곡선으로 편집 계약 변경 (2026-10-09)

ChartMaker와 `.rd`는 Start/Middle만 다루고 `curve: linear/sin/cos`를 노드의 다음 구간에
저장한다. End는 adapter가 마지막 Middle에서 생성하는 실행 데이터다. Start 없는 Middle과
단독 Start는 저장·Preview 아이콘을 유지하되 선과 Gameplay 판정 대상을 만들지 않는다.
Tab은 곡선 종류, Shift+Tab은 Start/Middle을 선택된 노드 또는 현재 배치 모드에서 전환한다. ¼주기 곡선의 64개 고정
구간 계산을 표시와 판정이 공유하며, 같은 줄의 Second Point는 실행용 Step으로 유지한다.
기존 v2의 Mid/End 및 incoming Step은 읽기 호환을 유지한다. 세부 계약은
[ChartFormat.md](ChartFormat.md) 18.3절을 따른다.

당시 공용 Knob/Long Scratch .NET 검증은 65/65 통과했고 관련 C# 컴파일을 확인했다.
후속 2026-10-11 통합 검증에서는 최신 Start/Middle·outgoing 곡선·Second Point와
Game/Maker의 공유 Material·cursor 제거를 포함한 집중 검사 89/89, Pause/Retry/실패 수명
검사 15/15, 격리 전용 계약 probe 3/3과 두 제품 독립 빌드·상대 제품 DLL 제외를 확인했다.
Game 씬 Main Camera의 1280×720 대표 경로 렌더도 확인했다. 전체 suite, 실제 곡 플레이·
물리 키·청음·Pause/Retry UI·최종 외관 승인은 미검수이며 과거 횟수를 최신 검사에 합산하지 않는다.
최종 원본 대조의 1,954파일은 manifest와 일치했으나 격리 Unity가 설정 5개를 자동
재직렬화한 뒤 빌드했다. 원본 settings까지 완전히 일치한 빌드로 기록하지 않는다.
이번 문서 마무리는 검증 후 수정이다. 상세 범위와 실행 근거는 [TASKS.md](TASKS.md) 및
[2026-10-11 통합 보고서](../../Logs/IntegrationBaseline/20261011-knob-integration/README.md)에 둔다.

ReMind는 현재 구조 개편 중이다.

### 과거 기록: 중앙 Tap / Knob v2 시작 전 Step 포함 구현·고정 검증본 검사 완료 (2026-10-08)

기존 v1 좌우 채보와 레인 `0`~`9`는 무변환 보존한다. 새 중앙 레인 `10`의
`CenterTap`/`Knob`을 포함한 경우에만 `.rd`와 Runtime Package가 각각 v2를 사용한다.
`.rd`는 기존 9열을 유지하고 `centerNotes`를 추가한다. 상세 계약은
[ChartFormat.md](ChartFormat.md)의 18.3절을 따른다.

- ChartMaker의 holder·파일 codec·adapter·배치 도구와 Edit 메뉴에 새 타입 연결을 추가했다.
  Edit 메뉴의 `Draw Knob Path`→`Finish Knob Path`, 지점 삽입·Vertical/Linear/Step·X 스냅을
  제공하고 `Place Center Tap`, `Place Knob Start/Mid/End` 개별 배치도 유지한다.
- Knob 방향 Hold를 위치 추적으로 교체했다. 목적지 incoming `KnobInterpolation`
  (Linear/Step), `KnobPathEvaluator.TargetX`, 실제 위치 `PlayerKnobX`를 공용 계약으로 쓴다.
  첫 동일 X 구간을 허용하며 시작 위치 지정·정상 Perfect+/실패 Miss·Step ±80ms와
  원래 timestamp의 예약 확정을 적용한다. 세부 수치는 ChartFormat 18.3절에 둔다.
  기존 Tap 창과 Long Scratch의 End release 의미는 유지한다.
- Game은 좌/우 독립 입력을 재사용하고 선택적 Space Tap을 중앙 Tap에만 추가한다.
  설정 내부 v3은 기존 v1/v2를 Space Tap OFF로 읽는다. Space의 레인 중복 할당은 거부한다.
- Game/ChartMaker의 공유 RibbonMesh·실제 cursor 및 한 경로 지점 편집을 연결했다.
  후속 사용자 아트 교체와 수동 화면·물리 입력·청음·밸런스 검수는 남아 있다.

새 위치 추적 모델의 시작 전 Step 수정 후 집중 EditMode 87/87, 전체 316/316와
Game/ChartMaker 빌드·DLL 분리를 고정 검증본에서 확인했다. 전체에는 시작 전 독립
probe 1개·Maker 경로 probe 1개·Game 표시 probe 2개가 포함되며 중복 합산하지 않는다.
기존 76/304 통과 및 추가 시작 전 probe 0/1은 수정 전 기록이다. 시작 100ms·Step 150ms의
Down 70ms 즉시 이동·시작 시 위치 유지와 미확정 Start/tick의 원래 시각 예약 확정을
검증했다. 유효한 조기 입력이 없는 일반 시작은 초기값을 지정한다. `PlayerKnobNoteId`는
조기 입력부터 실제 cursor의 소유 경로를 지정하고 Reset 시 null이 된다.
예약은 해당 Knob의 직전 구간에만 적용하며 이동으로 벗어난 다른 Knob은 Miss가 될 수 있다.
당시 사용자 후속 Game 씬 편집으로 이 10/08 기록은 당시 원본 씬 전체의 빌드 근거가 아니었다.
현재 snapshot의 빌드·설정 차이 범위는 위 10/11 기록을 따른다.
자세한 증거·고정 snapshot 범위와 수동 검수는 [TASKS.md](TASKS.md)를 따른다.

아래는 **이전 방향 Hold 모델**의 기록이며 새 위치 추적 규칙의 통과로 승계하지 않는다.
당시 집중 EditMode 64/64와 이를 포함한 전체 EditMode 299/299, 두 제품 독립 빌드와
상대 제품 DLL 부재를 확인했다. Maker의 저장·출력·Preview 자동 큐와 Game 입력·상태는
각 검사 경계에서 검증했다. 실제 Game 씬 probe 1/1은 v2 준비 후 중앙 Tap·Knob 경로와
cursor 생성을 확인했지만, 격리된 빈 noteField를 사용해 런타임 Cleanup/Retry를 증명하지 않는다.
단일 PlayMode Maker→Game 전체 세션, 실제 화면·물리 입력·청음·Pause/Retry 실플레이,
개발값 밸런스와 새 아트는 후속 검수다. 원본 동기화 목록과 XML·빌드 로그는
[TASKS.md](TASKS.md)의 중앙 Tap·노브 항목에 기록한다.

### 기존 제작·플레이 연결

`Assets/Data/Music/i/data.json`은 형식 버전 1 곡 목록과 `hard` 채보 참조를 가진다.
ChartMaker의 파일 경계는 목록의 곡·난이도 ID와 경로를 검사한다. `i`와
`designant`의 Effect 파라미터는 이제 각 `.rd` 안에 있다. Music Select의 행은 곡명·아티스트·
레벨을 이 목록에서 읽는다. 카탈로그 생성은 등록된 각 `.rd`를 검증해 난이도별
채보 폴더의 `rmp/`에 `.rmp.json`을 출력하고 Unity 에셋 참조를 연결한다.
Game 빌드의 첫 씬은 `Bootstrap`이다. 씬 전용 `BootstrapLoadingController`가
로고와 진행 표시가 끝나고 새 버튼 입력을 받으면 Home 전환을 요청하며, `AppRoot`가 Music Select에서 고른
곡/난이도 ID를 보유한다. Game 씬은 카탈로그에서 해당
실행 패키지와 음원을 찾아 ID·곡 데이터를 확인한 뒤 세션을 준비한다.
Bootstrap→Home을 포함한 Game 씬 이동은 AppRoot 자식의
`SceneTransitionController`를 통한다. 일반 이동에서는 투명
`CrystalOverlayTransition`을 로드 전후로 이어 재생하고 어두운 덮개로 로드 순간을
가린다. Music Select→Game은 `CrystalTransition`의 흰 화면 연출만 재생한다.
흰 화면이 덮인 뒤 씬을 로드·활성화하고 새 씬에서 화면을 드러내며, 이 경로에서
투명 오버레이의 Intro/Loop/Outro와 어두운 덮개는 실행하지 않는다.
기존 `MusicSelected` Animator/스프라이트는 실행 시 비활성화한다.
전환 표시와 씬 로드는 Game 전용이며 판정·재생 세션은 Game 씬에 남는다.

현재 존재하는 코드의 폴더명이나 클래스명만으로 장기 Source of Truth를 판단하지 않는다.

## 2. Current Functional Reference

현재 제작 흐름은 ChartMaker, 실제 플레이 흐름은 Game이 기준이다. 두 제품의 공통 의미는
Shared 계약과 교차 검사로 확인한다. ChartMaker 내부 구현 전체를 최종 아키텍처로 간주하지 않는다.

Effect vertical slice는 ChartMaker Preview와 기존 `DemoPlay`에서 같은 실행 의미를
검증한다. `DemoPlay`는 최종 Game 씬이 아니라 레거시 구성요소를 연결한 과도기 통합
하네스다. 씬은 `Assets/Tests/Fixtures/Scenes/DemoPlay.unity`로 옮겼고 제품 빌드에
넣지 않는다. `Gameplay/Demo`의 클래스는 Game에서도 사용되므로 폴더 전체를 레거시로
분류하지 않는다. 별도 `Game.unity`는 `i`와 `designant`의 선택·플레이·일시정지·
실패·결과·재시도 흐름을 연결한다. 로컬 최고 점수·클리어·플레이 횟수·최고 콤보와
즐겨찾기를 저장하고, Result에 기록 기반 진행을 표시한다. Game 설정도 별도 파일에
저장한다. 첫 클리어 기억 조각은 현행 v5 인벤토리에 지급·저장한다. 정식 콘텐츠와 최종 UI
시각 검수는 후속 작업이다.

`Assets/Scenes/prev/DemoPlay.unity`는 이동 전 씬을 보관한 복사본이다. 현재 회귀
검사와 `GameplayStage6SceneSetup`은 Tests 아래 fixture만 연다. 두 씬은 줄바꿈을
제외한 YAML 내용이 같지만 GUID가 다르므로, `prev` 씬을 테스트 대상으로 혼용하지 않는다.

## 3. Legacy Gameplay

DemoPlay 및 기존 Gameplay의 일부는 레거시 또는 과도기 코드다.

전체를 그대로 신규 구조의 기준으로 삼지 않는다.

동시에 Gameplay 폴더 전체를 일괄 폐기하지도 않는다.

재사용 가치가 있는 알고리즘과 책임은 클래스/책임 단위로 조사한다.

## 4. Current Shared-core Direction

현재 구현된 제작·실행 경계:

```text
ChartHolder (편집 원본)
        ↓ ChartHolderDocumentAdapter
ChartDocument → ChartCompiler → Snapshot → Preview
        ↓ ChartMakerRuntimePackageExporter
.rmp.json → GameplayChartPreparation → Snapshot + Effect plan → Game
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
  구독하지 않는다. Game/테스트 하네스의 `GameplayChartSessionController`가 판정 이벤트를
  표시기에 연결하고 ChartMaker는 표시기만 호출한다.
- `REmind.ChartMaker`, `REmind.Gameplay`, `REmind.Common` assembly를 추가했다.
  공용 assembly와 두 제품 assembly는 서로의 제품 구현을 참조하지 않는다.
- ChartMaker는 Effect 파라미터를 포함한 `.rd`를 검증한 후 버전이 있는 `.rmp.json`
  실행 패키지를 내보낸다. Game은 이 패키지를 다시 컴파일·검증해 Snapshot과
  Effect 계획을 준비한다. DemoPlay의 통합 검사 샘플은
  `Assets/Tests/Fixtures/EffectGameplaySample.rd`와 같은 폴더의
  `rmp/EffectGameplaySample.rmp.json`을 사용한다. 샘플 Effect 설정은 `.rd`에
  포함되며 별도 설정 JSON은 필요하지 않다.
- 제품별 빌드 명령은 Bootstrap으로 시작하는 Game 씬 목록과 ChartMaker 씬을 각각 선택하고 전용
  assembly define을 지정한다. 두 Windows 빌드는 컴파일에 성공했으며 각
  산출물의 Managed 폴더에 상대 제품 assembly가 없음을 확인했다.
- `NoteType`과 Scratch motion/rules/path를 `REmind.NoteRules` 순수 C# assembly로
  이동했다. `GameplayChartPreparation`과 `NoteJudgementSystem`은 Snapshot을
  사용한다. 호출·직렬화 참조가 없던 구형 `ChartLoadService`/`NoteData` 경로는 제거했다.
- 입력 라우터가 press와 release를 발행한다. 공용 판정 세션은 단일 노트 입력,
  Long의 Start→Mid→End 유지/해제, Mid의 재합류와 구간별 결과를 처리한다.
- Long Scratch 입력 규칙은 Start=양입력, Mid=유지 중인지 확인, End=음입력으로
  정했다. 앞 구간을 놓치면 그 구간은 Miss로 확정하며 각 Mid의 양입력으로 이후
  구간에 다시 합류할 수 있다. 구간마다 점수·콤보·체력을 개별 집계한다.
  노트 종류별 `NoteJudgeWindowProfile` 파일을 사용한다. 일반 노트와 Long
  Scratch Start는 같은 레인 입력에서 ±40ms Perfect+, ±80ms Perfect,
  ±130ms Great, ±175ms Miss를 적용한다. 조기 입력 -175ms도 Miss다.
  Long Scratch Mid는 ±100ms,
  End는 ±75ms에서 Perfect 입력만 받는다. End 미입력은 +75ms를 넘으면 Miss다.
  공용 판정 세션은 지점 인덱스를 Game 규칙에 전달해 구간별 창을 적용한다.
- `Game.unity`는 선택한 곡/난이도의 카탈로그 패키지와 음원을 준비한다. 직접
  Game 씬을 여는 개발 흐름에서는 `i/hard`를 기본 선택으로 사용한다. 원본 `.rd`는
  실행 중 읽지 않는다.
- 복합 채보 EditMode 검사는 ChartMaker 저장·재열기·Preview·패키지 출력 후
  Game 구성요소에서 같은 Tap/Long/Scratch, BPM·스크롤·Effect 시각과 설정으로
  준비·실행되는지 확인한다. PlayMode smoke는 두 곡의 선택, 음원·볼륨·노트
  표시 등록, 실패→결과→재시도, 완주·결과 복귀와 로컬 기록을 확인한다.
  실제 화면·청음·물리 키 입력은 별도 수동 검수 대상이다.
- Game 플레이어 설정은 진행 기록과 별도 `player-settings-v1.json`에 저장한다.
  사용자 음악 음량은 곡별 볼륨에 곱하고, 판정 보정은 차트 준비 전에 판정
  세션에, 레인 키 경로는 Game 입력 액션의 런타임 복제본에 적용한다.
  ChartMaker의 입력 액션과 데이터는 이 설정을 읽지 않는다.
  파일 내부 형식은 버전 3이며 기존 버전 1/2의 음량·보정·키 설정을 보존하고
  Space Tap은 OFF로 읽는다. 중앙 입력 확장의 검증 상태는 이 문서 첫 절을 따른다.
  설정 UI는 `AppRoot`의 영속 Canvas 아래 `SettingsOverlay.prefab` 모달로
  이동했다. Home은 AppRoot를 통해 열고 씬 이동 시 모달 탐색 참조를 정리한다.
  Character도 같은 영속 Canvas 아래 `CharacterOverlay.prefab`으로 배치했다.
  Home은 오버레이를 열고 BEGIN/닫기에서 Home으로 돌아온다. 현 단계의 캐릭터
  카드·프로필·스탯·스킬은 화면 시안이며 플레이 조건에 적용되지 않는다.
  마스터 볼륨은 AudioListener, BGM은 곡/미리듣기, SFX와 히트 사운드 스타일은
  판정 효과음에 반영한다. 출력 장치는 OS 기본 장치를 사용한다. 음성 소스는 아직
  없어 Voice 볼륨과 음성 중 BGM 감소는 저장되며 음성 재생을 연결할 때 적용한다.
- Game의 `player-progress-v1.json`은 곡·난이도별 최고 점수·랭크,
  클리어 이력, 플레이 횟수, 최고 콤보와 곡 즐겨찾기를 소유한다. 실패한 플레이도
  횟수에 포함하고 첫 클리어는 저장 전 기록과 비교한다. Auto Play는 저장하지 않는다.
  Result의 기억 영역은 이 사실 기반 기록을 표시한다. 첫 클리어마다 기억 조각
  1개를 기록·해금과 함께 저장한다. v1~v4의 클리어 이력은 v5 잔액으로 한 번
  이행하고, 이후 보유 수량은 인벤토리에서 읽는다. 소비 API는 있으나 소비 UI,
  그 외 보상과 곡별 서사 데이터는 아직 없다.

2026-10-11 인벤토리 통합은 [검증 기록](../../Logs/IntegrationBaseline/20261011-item-inventory/README.md)의
동일 소스 고정본에서 Unity EditMode 전체 **355/355**, 별도 통합 probe **2/2**,
Game Windows 빌드 성공을 확인했다. 집중 검사 87개는 전체 355개에 포함된다.
이 문서의 현행화는 검증 뒤 수행했으며 실행 원본의 문서 해시에는 포함되지 않는다.
실제 사용자 저장 파일은 수정하지 않았다. 격리본의 설정 파일 5개는 Unity가
재직렬화했으므로 빌드 설정은 원본과 바이트 단위로 동일하지 않다. Bootstrap의
실제 사용자 저장 경로를 사용하는 Awake, 소비 UI, 곡 전체 플레이는 검증하지 않았다.

## 5. Known Transitional Risks

현재 확인/관리해야 할 대표 위험:

- ChartHolder 편집 상태와 ChartDocument 변환 경계의 장기 소유권
- Preview 자동 표시와 Game 수동 판정의 통합 범위
- 레거시 API를 신규 구조가 따라가게 되는 문제
- 같은 규칙이 서로 다른 폴더에 중복되는 문제
- 임시 Adapter가 영구 구조로 굳어지는 문제
- 곡별 서사·정식 콘텐츠와 최종 UI 시각 검수가 남은 문제

### 현재 남은 동작 경계

1. **Preview 자동 표시와 실제 입력**
   Preview Auto Test는 Snapshot target 순서로 소리와 표시를 진행한다. 실제 입력을
   받는 Game은 공용 판정 세션을 사용한다. Preview에 수동 입력 테스트가 생기면
   같은 공용 세션을 연결해야 한다.
2. **카메라 표시 차이**
   Effect offset/easing은 공용이지만 Camera Note의 기준 X, lane/prefab과 실제 Transform
   구성은 `Game.unity`에서 정적 연결까지만 검증했다. 실제 화면·입력 수동 대조가 남았다.
3. **연속 도전과 선택형 제약**
   `SampleMusicGimmick`의 성공 횟수는 명령으로 증가하는 개발 예제이며 실제 노트
   판정이나 해금에 연결되지 않는다. 공용 transition mailbox와
   `GameplayChartEffectController.TransitionRequested`는 있지만 현재 Game의
   `CanTransitionTo` 설정과 이벤트 구독자가 없어 제품 플레이에서 요청이 승인되지
   않는다. `GameFlowController`의 결과와 `LocalPlayerDataStore`의 기록은 한 곡
   기준이며 도전 전체 결과·제약 조합·히든곡 해금을 저장하지 않는다. `GameRule`의
   수정자는 등록 순서대로 적용되어 제약 합성 순서도 아직 계약이 없다. 목표 경계와
   미결정 정책은 [ARCHITECTURE.md](ARCHITECTURE.md)의 연속 도전·제약 절을 따른다.

## 6. Current Classification

이 섹션은 작업하면서 계속 갱신한다.

### CURRENT
- ChartMaker의 실제 편집, 저장/복구, Undo/Redo, Preview 흐름
- `PlayableChartSnapshot`, Effect runtime과 카메라 공용 계산
- Game의 카탈로그·재생·공용 판정 연결·결과·설정·로컬 기록
- Bootstrap/씬 전환/Settings 모달의 최신 로컬 변경과 검수

### SHARED
- `REmind.ChartCore`의 chart compiler, timing/scroll/camera map
- Effect 계약, registry, 준비 계획, runner와 session context
- Effect 카메라 envelope/mixer, Preview capability, transition mailbox
- Runtime chart package codec, Effect parameter decoder, 공용 노트 지점과 구간
- PlayableJudgementSession, NoteType과 Scratch motion/rules/path
- LaneHitEffectPlayer의 Unity 표시 전용 책임

### SHARED candidates
- GameRule의 판정 등급/수치·modifier 중 Preview 수동 입력에도 필요한 부분

### LEGACY
- 테스트 fixture의 DemoPlay 하네스 구성. 해당 Presenter/Controller 전체가 폐기 대상은 아님
- 남은 `TempLoader` 경로. DemoPlay fixture의 TempLoader는 제거됨

### UNKNOWN / Mixed
- 실제 키 입력·청음에서 확정 판정 창의 체감 검증
- ChartMaker의 static 편집 상태와 fallback 씬 탐색 의존
- 포커스 이탈 자동 Pause·Visual Offset·수동 Test Play의 제품 범위

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
4. 공용 판정 세션과 Game의 Snapshot 입력 연결 — **완료**. 지점별 판정 창도
   연결했다. 실제 입력·청음 수동 검증은 후속 작업이다.
5. Game/ChartMaker assembly로 직접 의존을 컴파일 단계에서 막는다 — **완료**.
   `Play`·`Chart` Build Profile에 제품별 씬·define을 저장하고 두 Windows 빌드의
   상대 제품 DLL 제외를 확인했다.

### 과도기 adapter 제거 조건

- `TempLoader`: 남은 호출·직렬화 사용과 제거 조건을 별도로 확인한다.
- `SampleMusicGimmick`: 실제 곡 registry가 준비되면 개발 전용 등록을 테스트/샘플 범위로
  격리하거나 Player 등록에서 제외한다.

세부 우선순위와 검증 이력은 [TASKS.md](TASKS.md), 작업 의존관계는
[ROADMAP.md](ROADMAP.md)에서 관리한다. 기본 판정 창은 확정되어 있으며
실제 입력·청음 검수와 추가 제품 정책을 구분한다.
