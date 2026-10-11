# ReMind Architecture

> 상태: Living Target  
> 이 문서는 ReMind의 **장기 목표 아키텍처**를 정의한다.  
> 현재 코드 상태와 마이그레이션 세부사항은 [MIGRATION.md](MIGRATION.md)에서 관리한다.
> 검토: 2026-09-29. 현행 구현 설명과 장기 목표를 구분한다.

## 1. Architecture Goal

ReMind Game과 ReMind ChartMaker는 독립 Application이다.

둘은 서로 직접 의존하지 않고, 동일한 의미가 필요한 시스템을 Shared 영역을 통해 공유하는 구조를 지향한다.

```text
                    Shared Systems
                   /             \
                  /               \
          ReMind Game       ReMind ChartMaker
```

## 2. Main Boundaries

| 영역 | 책임 | 알면 안 되는 것 |
| --- | --- | --- |
| Shared Domain / Core | Chart 의미, 검증, 시간/위치 변환, 불변 Runtime 데이터, Effect·카메라·판정의 공통 규칙 | Unity Scene, Editor UI, 입력 장치, 계정/저장 진행 |
| Game Runtime | 실제 입력, GameRule 연결, 세션 진행, 콘텐츠/씬/오디오 구성 | ChartMaker UI와 편집 저장 모델 |
| ChartMaker Runtime | 편집, 저장/복구, Preview/Test Play, 제작자용 오류 표시 | Game의 계정, 해금, Player 전용 흐름 |
| Presentation | 공용 계산 결과를 Transform, Animator, UI, Audio에 표시 | 핵심 시간·판정·Effect 규칙의 재구현 |
| Data / Infrastructure | 파일 I/O, importer, 경로와 버전 관리, 앱별 로딩 | 실행 중 mutable session state |

Shared는 두 앱에서 우연히 재사용할 수 있는 모든 코드를 뜻하지 않는다. 두 앱에서
**같은 의미와 결과를 가져야 하는 계약**만 공용화한다. 앱별 표시기는 공용 결과를
소비하는 adapter로 둔다.

## 3. Chart Data Flow

곡별 폴더의 `data.json`은 여러 난이도가 공유하는 곡 식별자, 제목, 아티스트,
음원, 선택 화면 미리듣기 구간, 공통 재킷 등 곡 카탈로그 정보를 소유한다.
곡 파일의 난이도 목록은 난이도/레벨, 채보 제작자와 채보 경로를 소유한다.
난이도별 채보는 BPM·박자·시간 보정, 노트와 Effect·연출 규칙을 소유한다.
재킷이 난이도마다 다르면 해당 `.rd`의 선택적 `jacketFile`로 지정한다.
지정하지 않으면 `<difficultyId>.jpg`를 먼저 찾고, 없으면 곡의 `art.jpg`를
사용한다. 난이도별 배경 metadata 확장은 별도 계약을 정한 뒤 도입하며,
플레이 결과·점수·즐겨찾기는 별도 사용자 저장 데이터다.
플레이어 음악 음량·판정 보정·Game 레인 키는 Game 전용 설정 데이터로
진행 기록과 분리한다. ChartMaker Preview는 이 플레이어 설정을 읽지 않는다.
동일 값의 복사본을 두 파일에 독립적으로 편집하지 않고, 필요할 때 검증된
실행 패키지로 조합한다.

목표 필드명은 약어 대신 의미를 드러내도록 한다.

| 소유 파일 | 필드명 | 의미 |
| --- | --- | --- |
| 곡 `data.json` | `musicId`, `title`, `artist`, `audioFile` | 곡 식별자와 공통 표시·음원 정보. 음원 기본값 `audio.mp3` |
| 곡 `data.json` | `musicVolumeMultiplier` | 곡 음악 볼륨 배수 `0`~`2`. 기본값 `1`은 AudioSource 볼륨 `0.5` |
| 곡 `data.json` | `previewStartMs`, `previewDurationMs` | 곡 선택 화면의 미리듣기 구간 |
| 곡 `data.json` | `jacketFile`, `jacketIllustrator` | 공통 재킷과 일러스트레이터. 재킷 기본값 `art.jpg` |
| 난이도별 채보 | `level`, `author`, `extraName` | 숫자 레벨과 채보 제작자. `extraName`은 `Extra.rd`의 표시명 |
| 난이도별 채보 | `jacketFile` | 선택적 전용 재킷. 기본 `<difficultyId>.jpg`, 파일이 없으면 공통 재킷 사용 |
| 난이도별 채보 | `musicId`와 고정 파일명 | 곡 ID는 파일 내부, 난이도 ID는 `Easy.rd`·`Normal.rd`·`Hard.rd`·`Chaos.rd`·`Extra.rd` 파일명에서 결정 |
| 난이도별 채보 | `baseBpm`, `bpmChanges`, `musicStartCorrectionMs` | 채보의 시간 계산과 음원 정렬 |
| 난이도별 채보 | `backgroundId`, `backgroundLayerId`, `notes`, `eventDictionary` | 해당 채보의 노트·연출과 실행 규칙. Effect 파라미터도 `eventDictionary`에 포함 |

이 표는 목표 소유권과 이름이다. 고정 채보 파일명과 `level`·`author`·`extraName`은
현행 계약에 반영됐다. 나머지 목표 필드 중 `.rd`와 로더에 없는 필드는 이미 지원한다고
간주하지 않는다. 선택 화면의 BPM 표기는 채보의 시간 데이터에서 산출하며, 곡 파일에
독립된 BPM 원본을 중복 저장하지 않는다. 버전은 파일 계약마다 따로 관리한다.
현재 곡 `data.json`과 `.rd`는 `formatVersion`, Runtime Package는 `Version`을 쓴다.
현행 곡 목록 계약과 샘플의 파일 배치는 [MusicContent.md](MusicContent.md),
제작/실행 파일 계약은 [ChartFormat.md](ChartFormat.md)에 기록한다.

장기 개념 목표:

```text
Stored Chart Data
       ↓
Load / Validation
       ↓
Editable Representation (ChartMaker only)
       ↓
Compile
       ↓
Validated Runtime Package
       ↓
       ├─ ChartMaker Preview / Test
       └─ ReMind Game
```

현재 클래스명을 장기 계약으로 확정하지 않는다.

핵심 목표는 Game과 ChartMaker가 채보의 의미와 실행 규칙을 중복 구현하지 않는 것이다.

## 4. Shared Systems

현재 공용 경계로 검증된 책임:

- `ChartDocument` → `PlayableChartSnapshot` 컴파일과 기본 검증
- `TimingMap`, `ScrollMap`, `CameraMotionMap`의 chart time/position 계산
- Effect 정의, registry, parameter 의미 검증, `PreparedEffectPlan`
- 예약 시각 기반 `EffectRunner`와 세션 취소/정리 계약
- Camera Effect의 절대시간 easing과 additive offset 합성
- Preview용 제한된 상태·규칙 handle·전환 mailbox 계약
- Runtime chart package codec과 공용 Effect parameter decoder
- Note/Long/Scratch 지점·구간, `PlayableJudgementSession`의 입력/유지/재합류 lifecycle
- `REmind.NoteRules`의 NoteType과 Scratch motion/rules/path

추가 공용화 또는 제품 간 대조가 필요한 책임:

- Preview 수동 Test Play에 필요한 GameRule의 등급·수치·modifier 의미
- Camera Note/Scratch 공용 계산을 적용하는 앱별 lane/prefab/Transform의 화면 일치
- 현재 ChartHolder 편집 상태와 공용 ChartDocument 사이의 소유권 정리

후보를 곧바로 새 구현으로 만들지 않는다. 현재 ChartMaker와 Game 양쪽의 실제 사용
관계를 조사하고 Source of Truth를 정한 뒤 이동한다.

## 5. Application-specific Systems

### 중앙 Tap / Knob 확장 경계 (2026-10-08)

기존 레인 `0`~`9`와 좌우 Scratch/Long Scratch는 보존하며, 별도 `CenterTap`/`Knob`만
중앙 레인 `10`에 추가한다. 제작 형식과 실행 패키지는 새 종류가 있을 때만 v2를 쓴다.
저장 필드와 위치 추적 규칙은 [ChartFormat.md](ChartFormat.md)의 18.3절을 따른다.

방향 Hold 모델을 위치 추적으로 교체했다. 공용 `ChartCompiler`는 목적지 지점의
`KnobInterpolation`(Linear/Step)과 판정 타깃을 만들고, `KnobPathEvaluator.TargetX`가
작성 position 보간을 논리 목표 X로 변환한다. `PlayableJudgementSession`과 내부
`KnobJudgementSession`은 플레이어 위치·입력 소비·보정·예약 결과를 소유한다.
일반 시작은 시작 위치로 초기화하되 유효한 시작 전 Step 이동이 있으면 그 위치를 유지한다.
`PlayerKnobX`와 `PlayerKnobNoteId`로 실제 cursor와 소유 경로를 표시한다. 유효한 조기 입력부터
소유 Knob을 지정하며 Reset 시 소유 ID를 null로 되돌린다.

논리 범위 ±2.5, 속도 5 units/s, 공간 허용·사선 보정 ±0.25, 체크 간격 100ms를 쓴다.
정상 위치 판정은 Perfect+, 실패는 Miss다. Step의 새 방향 Down ±80ms는 위치를 옮기고,
조기 입력은 직전 구간의 남은 판정만 원래 timestamp에 Perfect+로 확정하도록 예약한다.
과거 Miss나 다음 구간에 소급/일괄 보상하지 않는다. 이 입력은 Tap보다 먼저 소비한다.
예약은 해당 Knob의 직전 구간에만 적용하므로 조기 이동으로 벗어난 다른 Knob은 Miss가
될 수 있다. 다른 Knob까지 성공 보정을 확장하지 않는다.
이전 150ms/50ms 유예는 폐기하고 기존 Tap 창·Long Scratch End release는 유지한다.

Game 입력 adapter는 좌/우 논리 입력의 Down/Up·시각을 독립적으로 전달한다.
Space Tap은 기본 OFF인 Game 설정이며 ON일 때 중앙 Tap에 Space를 추가한다.
설정 변경은 다음 플레이부터 적용한다. Space는 노브 위치를 조작하지 않는다.
다른 레인에 Space가 할당돼 있으면 ON을
거부하고, ON 상태의 레인 키 변경에서도 Space 중복을 막는다. 설정 내부 버전은 3이며
v1/v2를 읽을 때 기존 값을 보존하고 Space Tap은 OFF로 이행한다.

공유 표시의 RibbonMesh·실제 cursor 및 Maker 한 경로 지점 편집을 연결했다.
경로 표시와 판정 계산을 분리하며 사용자 아트 교체는 표시 책임 안에서 진행한다.
Game의 기존 좌우 위치 ±15와 중앙 Tap X=0은 유지한다. 이전 EditMode 299/299·두 제품
빌드·씬 probe는 방향 Hold 모델의 기록이며 새 위치 추적 모델의 검증 결과가 아니다.
시작 전 Step 수정 후 고정 검증본의 집중 EditMode 87/87, 전체 316/316와 두 제품 빌드를
확인했다. 전체에는 시작 전 독립 probe 1개·Maker 경로 probe 1개·Game 표시 probe 2개가
포함된다. 이전 0/1 재현은 해결했고 조기 이동 유지·Start/tick 예약과 실제 cursor를 검증했다.
사용자 후속 Game 씬 편집이 계속돼 현재 원본 씬 전체의 빌드는 미검증이다. 실제 화면·물리 입력·
청음·전체 세션·밸런스 검수는 남아 있다. 원본 범위와 증거는 [TASKS.md](TASKS.md)에 기록한다.

### Game

- Game 빌드는 `Bootstrap` 씬에서 시작한다. 하이어라키의 `AppRoot`만
  `DontDestroyOnLoad`로 유지하며 Home을 연다. 이 루트는 곡/난이도 선택처럼
  씬 사이의 선택·결과 요약과 로컬 플레이어 기록 저장소를 소유한다. 그 자식의
  `SceneTransitionController`는 Bootstrap→Home을 포함한 Game의 씬 이동을 맡는다.
  일반 이동은 투명 `CrystalOverlayTransition`의 Intro를 시작하고, 씬 로딩이 길어지면
  Loop를 반복한 뒤 Outro로 새 화면을 드러내며 어두운 덮개로 로드 순간을 가린다.
  Music Select→Game은 `CrystalTransition`의 흰 화면만 사용하고 투명 오버레이의
  세 단계와 어두운 덮개를 생략한다. `GameManager`, 채보·판정·Effect·오디오
  세션은 계속 Game 씬이 소유하고 씬 종료 시 정리한다.
  Bootstrap의 씬 전용 Canvas는 로고·문구·장식과 진행 표시를 보여준다.
  `BootstrapLoadingController`가 표시 시간을 마친 뒤 새 키·클릭·터치·게임패드
  버튼 입력을 기다린다. 입력을 받으면 `AppRoot.CompleteBootstrapLoading()`을
  호출해 기존 전환으로 Home을 연다.
- 실제 입력 장치와 입력 routing
- 공용 Runtime package를 세션에 게시하는 composition root
- 실제 `GameRule`, 체력·점수·콤보·실패/클리어 상태 adapter
- Game 전용 카메라/노트/이펙트/오디오 표시
- 곡 선택, 콘텐츠 로딩, 씬 전환, 결과와 영구 진행
- uGUI 메뉴는 씬에 배치한 `NavigationScope`와 `NavigationNode`로 선택 범위를
  정의한다. `MenuNavigationController`는 활성 범위와 선택 복원만 맡고,
  방향 이동·Submit은 `InputSystemUIInputModule`/`Selectable`에 맡긴다.
  실제 플레이 중 Pause 진입만 Gameplay 입력에서 처리한다.
- Settings는 별도 씬을 열지 않는다. `SettingsOverlay.prefab` 인스턴스는
  Bootstrap의 영속 `AppRoot` Canvas 아래에 둔다. Home은 `AppRoot`에 열기를
  요청하고 Option도 같은 Settings 모달을 연다. 현재 `MenuNavigationController`의
  모달 Scope로 Push/Pop한다.
  씬이 바뀌면 오버레이와 씬 소유 탐색 참조를 정리한다. 설정 데이터는
  `LocalGameSettingsStore`가 소유하며 Prefab은 표시와 입력만 맡는다.
  기존 Settings 씬은 제거했다.
- Character 선택 화면은 `CharacterOverlay.prefab`으로 Bootstrap의 영속 `AppRoot`
  Canvas 아래에 둔다. Home의 Character 버튼은 현재 메뉴 탐색 범위 위에 모달
  Scope를 연다. 두 모달의 `OverlayPresentation`은 비축척 시간으로 패널의
  alpha/scale Intro·Outro를 진행한다. Intro 동안 모달 입력을 막고 Outro가 끝난
  뒤 Home Scope와 선택을 복원하며, 씬 이동 시 진행 중 표시와 탐색 참조를 정리한다.
  `CharacterCatalog`는 Game 전용 정의 ID·버전·게이지·raw stat을
  소유하고, `LocalPlayerDataStore` v2는 보유·장착 ID를 곡 기록/즐겨찾기와 함께
  저장한다. 오버레이의 카드 선택은 미리보기만 바꾸며 닫기는 장착을 바꾸지 않는다.
  BEGIN은 보유·정의·저장 성공을 확인한 뒤 장착하고 오버레이를 닫는다. 최초
  NON 지급과 잘못된 장착 ID 복구는 저장소와 카탈로그 경계에서 처리한다. 기존
  기존 Character 씬은 제거했다.
  NON의 월드 해금·통상 재화·히든 해금 raw stat 50과 basis 50은 중립 배율 100%를
  뜻하는 조정 가능한 개발값이다. 네 번째 stat은 미정이며 스킬·성장·실제 지급은
  아직 연결하지 않았다. 기본 게이지는 시작 0/최대 100이다.
- Home의 MUSIC 버튼은 하이어라키에 저장된 `MusicSelect` 곡 선택 화면을 연다.
  Play를 누르면 선택한 곡·난이도와 현재 장착한 캐릭터의 정의·게이지 규칙을
  `AppRoot`에 기록하고 Game 씬으로
  이동한다. Music Select→Game에서는 흰 결정 연출이 화면을 덮은 뒤 Game을
  로드하고, 새 씬 위에서 흰 화면을 걷는다. 전환 중
  중복 Play와 Music Select의 ESC 복귀를 막는다. Game 씬은
  `MusicCatalog`에서 해당 ID의 검증된 실행 패키지와 음원을
  찾아 패키지 ID를 다시 확인한 뒤 세션을 준비한다. 음원 로드가 끝나고
  `AppRoot.IsTransitioning`이 해제되면 한 번만 재생을 시작한다. 재생 준비 오류,
  Effect 실행 오류, 필수 Effect·판정 구성요소의 비정상 중단은 해당 시도를
  무효화하고 Game 오류 화면으로 이동한다. 오류 화면에서 Music Select로 복귀할
  수 있으며 이 오류 시도는 결과·진행 저장을 만들지 않는다. 정상 규칙에 따른
  플레이 실패 결과는 별도의 종료 경계로 처리한다.
  패키지와 재생 세션은 Game 씬이
  소유하며 `AppRoot`에는 남기지 않는다. Game에서 나가면 `MusicSelect`로 복귀한다.
  플레이 종료 시 Game은 세션의 점수·판정·콤보와 GameRule의 랭크를 불변 결과
  요약으로 확정한다. `AppRoot`는 이 요약을 Result 씬으로 한 번 전달하고 즉시
  비운다. Result 씬은 곡 카탈로그에서 제목·아티스트·재킷·난이도를 읽어 표시한다.
  곡·난이도별 최고 점수, 클리어 이력, 플레이 횟수, 최고 콤보와 곡 즐겨찾기는
  Game 전용 로컬 JSON에 저장한다. 저장 v1을 읽을 때 기존 곡 기록과 즐겨찾기를
  보존해 v2 보유·장착 필드를 채운다. 미지원 미래 버전이나 복구 불가 저장은
  새 빈 데이터로 덮어쓰지 않고 시작 오류를 표시한다. Music Select는 최고 점수·즐겨찾기를,
  Result는 확정된 결과와 저장된 진행 기록을 읽는다. Auto Play는 진행에 반영하지
  않는다. 곡·난이도 첫 클리어마다 기억 조각 1개를 지급하며, 보유 수량은 클리어
  기록에서 계산한다. Result는 첫 클리어 보상만 표시하고 곡별 서사는 숨긴다.
  기존 `Music` 임시 씬은 제거했고 Home의 MUSIC 버튼은 `MusicSelect`를 연다.

- 해금 도전 선택은 Game 전용 `MusicUnlockLibrary`가 맡는다. Bootstrap의
  `AppRoot.musicUnlockLibrary`는 `Assets/Data/Music Unlock/MusicUnlockLibrary.asset`을
  참조하고, 라이브러리의 `entries`에는 곡별 `Config.asset`을 명시적으로 등록한다.
  현재 designant Config는 기존 전용 Rule 참조를 유지한다. 실제 선택 곡·난이도와
  대상 곡의 선택 난이도 해금 여부로 후보를 정하며, 둘 이상 맞으면 기존 시작 오류
  안내로 차단한다. 등록 순서는 우선순위가 아니다. null·중복 Config·잘못된 규칙과
  등록된 대상의 잠긴 난이도 직접 시작도 차단한다. 미연결/빈 라이브러리와 조건
  불일치는 기존 장착 캐릭터 경로를 유지한다. 특수 Hard 도전은 캐릭터 없이 시작하고,
  대상 ID를 시작 snapshot에 고정한다. 기존 결과 저장은 성공한 수동 플레이에서
  해당 난이도 이하를 누적 해금하며 Retry·저장 재시도·Auto Play 정책은 유지한다.
  라이브러리는 작성 데이터만 소유하며 런타임에 자산을 수정하거나 폴더를 검색하지 않는다.
  공통 선택·실행·저장 코드는 Gameplay에 유지하고 ChartMaker에는 의존을 추가하지 않는다.

### ChartMaker

- `ChartHolder` 기반 편집 상태, 배치/선택 UI와 Undo/Redo
- Effect 파라미터를 포함한 `.rd` 저장, backup/복구, 최근 파일
- 설정 panel과 제작자용 validation 안내
- Preview용 Transform/Animator/Audio 표시와 명시적인 테스트 상태

## 6. Runtime Session

파일과 편집 모델은 재생 준비 경계에서 검증된 불변 Runtime package로 변환한다.
실행 중에는 파일이나 JSON을 다시 읽지 않는다. Effect 실행부는 최소한 다음만 받는다.

- 불변 Snapshot과 `PreparedEffectPlan`
- music/difficulty 같은 검증된 식별자
- 공용 chart time
- 카메라, 현재 상태, 규칙, 전환을 위한 제한된 session capability

준비와 live 세션 게시를 분리한다. 준비가 완전히 성공하기 전에는 기존 세션의 카메라,
규칙, 판정 상태를 바꾸지 않는다. Resume은 같은 세션을 유지하고, Play/Restart는
준비·commit이 성공한 뒤 새 세대를 게시한다.

Game의 현재 시작 경계에서는 `AppRoot`가 다음 플레이의 곡·난이도와 장착 캐릭터의
정의 버전·raw stat·게이지 규칙 입력을 보관한다. Game 씬의 `GameFlowController`가 준비된 채보 ID와
입력을 확인하고 규칙 자산을 세션 전용으로 복사한 뒤, 재생 시작 시 불변
`GameAttemptStartSnapshot`을 확정한다. 실패한 시작은 규칙 변경을 되돌리고,
Pause/Resume은 같은 시도를 유지하며 성공한 Retry만 새 시도를 만든다. 종료
결과는 그 시작 snapshot을 Result에 전달한다. 자동 판정을 시도 중 한 번이라도
사용하면 이후 꺼도 해당 결과는 로컬 기록·진행에 반영하지 않는다. Easy·Normal
게이지는 체력 0으로 시작하거나 플레이 중 0을 유지해도 실패하지 않으며, 채보를
끝까지 완료하고 체력이 70 이상이면 클리어한다. Hard와 특수 Hard는 체력 0에서
즉시 실패하고 이후 판정으로 부활하지 않는다. 체력이 0보다 큰 채로 끝까지
완료하면 클리어하며 0.1처럼 소수 체력도 인정한다. 현재 `clearHealth` 자산 값은
이 게이지별 종료 판정에 사용하지 않는다.
이 경계는 Game 소유이며 ChartMaker에 캐릭터/사용자 저장 의존을 추가하지 않는다.
향후 장착·조건부 능력·정산을 연결하기 전에 `GameFlowController`의 시작 조건과
시도 진행 책임을 Game 전용 소유자로 분리할지 검토한다.

## 7. Time / Judgement

모든 실행 의미는 프레임 수가 아니라 공용 chart time을 기준으로 한다. 예약 시각과
늦은 프레임의 현재 처리 시각을 별도로 보존한다. 한 프레임에서 여러 경계를 지나면:

1. 다음 Effect·입력·자동 판정 경계 중 가장 이른 시각을 선택한다.
2. 같은 시각이면 Effect의 명시 order, 입력의 sequence, 자동 판정/Miss 순으로 처리한다.
3. 현재 프레임 시각까지 위 병합을 반복한다. 미래 Effect를 과거 입력보다 먼저 적용하지 않는다.
4. 활성 Effect와 곡 기믹을 현재 프레임 시각으로 한 번 갱신한다.

시각 T의 규칙 변경은 T 이전 입력에 소급하지 않는다. 같은 시각에는 Effect가 먼저이며,
정확한 Miss 마감 시각에서는 기존 판정 계약대로 입력을 먼저 처리한다. 프레임 시간 역행과
같은 시각의 중복 Effect 갱신은 막는다. 같은 시각에 여러 자동 Miss가 생겨도
종료 실패가 확정되면 해당 자동 처리 묶음의 뒤 판정과 규칙 적용을 멈춘다.
다음 프레임에 늦게 도착한 입력의 허용/폐기 경계와
보정 부호는 [RhythmSystem.md](RhythmSystem.md)를 따른다.

노트 배치의 Y/시간/scroll 계산은 Snapshot의 Timing/Scroll 결과를 공유한다. X 위치,
prefab, Animator와 물리 입력은 앱별 표현이다. `PlayableJudgementSession`은
Snapshot 지점과 구간을 공용 시간축에서 처리한다. Game의 `NoteJudgementSystem`은
입력 시각, 노트별 시간 창, `GameRule` 점수와 Effect 순서를 연결한다. Preview의
자동 표시는 Snapshot target을 사용하며, 수동 입력을 도입할 때 공용 세션을 쓴다.

## 8. Effect / MusicGimmick

난이도별 `.rd`의 `eventDictionary`는 Effect 위치·order·stable ID·종류·명령과
수치 파라미터를 가진다. 복잡한 조건과 상태 전이는 C#만 소유한다.

`EffectRunner`는 공용 Effect와 `CallMusicGimmickEffect`를 한 목록에서 처리한다.
한 세션의 명령은 같은 MusicGimmick 인스턴스를 공유하며, 명령 호출 종료와 곡 기믹
상태 종료를 구분한다. 실패한 부작용 명령은 자동 재실행하지 않는다.

카메라 움직임의 envelope와 합성은 Shared에서 절대 chart time으로 계산한다.
ChartMaker와 Game은 계산 결과를 각자의 Transform 계층에만 적용한다. 곡 전환은
Effect 안에서 Scene을 즉시 바꾸지 않고 요청만 제출하며 앱의 프레임 완료 경계에서
처리한다.

### 연속 도전과 히든곡 분기 — 목표 계약

한 곡의 재생 세션과 여러 곡에 걸친 도전은 수명이 다르다. Game 전용 도전 진행자가
시작 시 선택한 캐릭터·스킬·제약·콘텐츠/규칙 버전을 확정하고, 곡별 세션의 성과와
분기 이력을 도전 전체에 모은다. 곡별 Effect/MusicGimmick은 검증된 대상 ID로
**전환 의도**만 제출한다. 도전 진행자는 대상 콘텐츠 확인, 다음 음원·패키지 준비,
기존 세션 정리, 새 세션 시작, 최종 결과와 해금 저장을 소유한다. Shared는 두 제품이
같이 해석해야 하는 분기 조건·시각·데이터 의미만 소유한다. ChartMaker Preview에는
Game의 영구 진행이나 실제 해금 권한을 주지 않는다.

분기 전환은 A곡의 일반 완주·실패·중단 결과로 확정하지 않는다. 곡별 세션 종료 사유와
도전 전체 종료 사유를 별도로 기록하고, B곡을 시작할 때 판정·점수·기믹 세션을 새로
만든다. 게이지·스킬을 이월할지는 제품 정책으로 결정한다. 재요청·취소·준비 실패에도
한 번의 분기만 확정하고, 실패한 준비가 해금이나 일반 최고 기록을 만들지 않게 한다.
해금은 도전 전체의 확정된 성공 조건을 Game의 사용자 진행 저장소에 적용한다.

전환 지점 T와 같은 시각의 노트를 조건에 포함할지 명시해야 한다. 현재 시간 계약은
**Effect → 입력 → 자동 판정**이므로 T에서 실행하는 Effect는 T의 입력 판정을 아직
보지 못한다. 마지막 대상 판정 뒤에 조건을 평가하도록 채보 시각을 정하거나 별도의
평가 단계를 설계해야 한다. 이 정책을 정하기 전에는 T의 판정을 포함한다고 가정하지
않는다. 끊김을 줄이려면 전환 전에 대상 음원·채보·연출 자산을 준비하고, 새 곡 시작을
가리는 연출 구간을 확보한다. 정확히 이어지는 음악은 별도의 오디오 예약 시각 계약이
필요하다.

### 선택형 제약 — 목표 계약

도전 시작 전에 제약의 안정적인 ID·버전·단계·점수·충돌 관계를 검증하고 선택 집합을
확정한다. 도전 도중 UI 선택 순서에 따라 효과가 달라지지 않도록 합성 순서를 계약으로
고정한다. 체력·판정·실패·클리어처럼 GameRule에 해당하는 효과는 규칙 수정자로,
캐릭터/스킬 제한은 시작 검증과 스킬 경계로, 시야/노트 표시는 Presentation 경계로
보낸다. 일시적인 곡 Effect 수정자와 도전 전체 제약 수정자의 소유자·적용 순서·해제
시점도 명시한다. 같은 의미의 판정 규칙이 ChartMaker Test Play에 필요하면 Shared
계약을 먼저 검토한다.

도전 기록에는 도전 정의/규칙 버전, 확정한 제약 ID·단계, 곡별 분기·성과, 전체 종료
사유를 남긴다. 일반 곡 최고 기록과 제약 조합별 기록의 인정 범위는 별도로 정한다.
현재 곡별 기록 파일의 형식을 그대로 확장하기 전에 새 저장 계약과 기존 데이터 이전을
설계한다.

### 구현 전 제품 정책

| 항목 | 정할 내용 |
| --- | --- |
| 곡 사이 상태 | 게이지·스킬·캐릭터 상태의 초기화 또는 이월 |
| 기록과 보상 | A곡 중간 기록, B곡 일반 기록, Auto Play 인정 여부 |
| 해금과 재시도 | B곡 도달/완주/클리어 중 성공 기준, 재시도 시작 지점 |
| 시간 경계 | 분기 시각과 같은 시각의 마지막 판정 포함 여부 |
| 제약 합성 | 수정자 순서, 중복 단계 충돌, 회복 금지와 회복 능력의 우선순위 |
| 콘텐츠 노출 | 목록 표시 조건과 설치 파일/메타데이터 노출 범위 |

첫 구현 단위는 A곡 조건 달성 → B곡 준비·전환 → B곡 성공 → 해금 저장의 한 경로와
체력·판정 제약의 작은 조합으로 잡는다. 분기 실패·중단·재시도·동일 시각 판정과
저장 복구까지 검증한 뒤 콘텐츠를 늘린다. 첫 출시 포함 여부는 제품 정책 결정이다.

## 9. Ownership / Lifetime

- Effect, MusicGimmick, cancellation, camera offset, rule handle, transition request는
  한 재생 세션이 소유한다.
- Effect 종료는 자신이 만든 handle만 해제한다. 겹친 다른 Effect의 출력을 초기화하지
  않는다.
- 이전 세대의 지연 callback이나 Dispose가 새 세대의 presenter를 변경할 수 없어야 한다.
- 실행·취소·예외·컴포넌트 비활성화 모두 같은 멱등 cleanup 경로를 사용한다.
  실행 중 Effect 또는 판정 컴포넌트가 독립적으로 비활성화되면 실패를 한 번만
  보고하고 시도를 무효화한다. 정상적인 씬 종료·정리 과정은 새 실패를 만들지 않는다.
- mutable runtime state를 static singleton이나 설정 asset에 저장하지 않는다.
- Preview capability는 영구 진행이나 실제 Scene/Audio 교체 권한을 갖지 않는다.

## 10. Dependency Rules

목표 의존 방향은 `Game → Shared ← ChartMaker`다. Shared는 Unity 비의존 순수 C#을
우선하며 두 앱의 구체 타입을 참조하지 않는다. Game과 ChartMaker 사이의 직접 타입
참조는 허용된 최종 구조가 아니라 제거 조건이 적힌 migration adapter로만 둔다.

`REmind.ChartCore`, `REmind.NoteRules`는 Unity 비의존 assembly이고,
`REmind.Gameplay`와 `REmind.ChartMaker`는 별도 제품 assembly다. Game/ChartMaker
Windows 빌드의 상대 제품 DLL 제외 검증 이력은 [TASKS.md](TASKS.md)에 둔다. `Play`와 `Chart`
Build Profile은 제품별 씬과 define을 소유한다. 플랫폼별 추가 설정과 수동 실행
검증은 `MIGRATION.md`와 `TASKS.md`에서 관리한다.
