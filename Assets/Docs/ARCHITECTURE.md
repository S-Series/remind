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
| 곡 `data.json` | `jacketFile`, `jacketIllustrator`, `charts[]` | 공통 재킷과 난이도 목록. 재킷 기본값 `art.jpg` |
| `charts[]` 항목 | `difficultyId`, `level`, `chartAuthor`, `chartFile` | 난이도 표시 정보와 제작용 채보 참조. 채보 기본값 `<difficultyId>.rd` |
| 난이도별 채보 | `jacketFile` | 선택적 전용 재킷. 기본 `<difficultyId>.jpg`, 파일이 없으면 공통 재킷 사용 |
| 난이도별 채보 | `musicId`, `difficultyId` | 곡·난이도 연결을 검증하는 식별자 |
| 난이도별 채보 | `baseBpm`, `bpmChanges`, `musicStartCorrectionMs` | 채보의 시간 계산과 음원 정렬 |
| 난이도별 채보 | `backgroundId`, `backgroundLayerId`, `notes`, `eventDictionary` | 해당 채보의 노트·연출과 실행 규칙. Effect 파라미터도 `eventDictionary`에 포함 |

이 표는 목표 소유권과 이름이다. 현행 `.rd`와 로더에 없는 필드를 이미 지원한다고
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

### Game

- Game 빌드는 `Bootstrap` 씬에서 시작한다. 하이어라키의 `AppRoot`만
  `DontDestroyOnLoad`로 유지하며 Home을 연다. 이 루트는 곡/난이도 선택처럼
  씬 사이의 선택·결과 요약과 로컬 플레이어 기록 저장소를 소유한다. 그 자식의
  `SceneTransitionController`는 Bootstrap→Home을 포함한 Game의 씬 이동을 맡는다.
  투명 `CrystalOverlayTransition`의 Intro를 시작하고, 씬 로딩이 길어지면
  Loop를 반복한 뒤 Outro로 새 화면을 드러낸다. 일반 이동은
  어두운 덮개로 로드 순간을 가린다. Music Select→Game에서만
  `CrystalTransition`의 흰 화면을 사용한다. `GameManager`, 채보·판정·Effect·오디오
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
  요청하고 현재 `MenuNavigationController`의 모달 Scope로 Push/Pop한다.
  씬이 바뀌면 오버레이와 씬 소유 탐색 참조를 정리한다. 설정 데이터는
  `LocalGameSettingsStore`가 소유하며 Prefab은 표시와 입력만 맡는다.
  기존 Settings 씬은 빌드 목록에서 제외했다.
- Home의 MUSIC 버튼은 하이어라키에 저장된 `MusicSelect` 곡 선택 화면을 연다.
  Play를 누르면 선택한 곡 ID와 난이도 ID를 `AppRoot`에 기록하고 Game 씬으로
  이동한다. Music Select→Game에서는 흰 결정 연출이 화면을 덮은 뒤 Game을
  로드하고, 새 씬 위에서 투명 오버레이의 후반부를 이어 재생한다. 전환 중
  중복 Play와 Music Select의 ESC 복귀를 막는다. Game 씬은
  `MusicCatalog`에서 해당 ID의 검증된 실행 패키지와 음원을
  찾아 패키지 ID를 다시 확인한 뒤 세션을 준비한다. 음원 로드가 끝나면 재생을
  시작한다. 실패하면 Game 오류 화면으로 이동한다. 패키지와 재생 세션은 Game 씬이
  소유하며 `AppRoot`에는 남기지 않는다. Game에서 나가면 `MusicSelect`로 복귀한다.
  플레이 종료 시 Game은 세션의 점수·판정·콤보와 GameRule의 랭크를 불변 결과
  요약으로 확정한다. `AppRoot`는 이 요약을 Result 씬으로 한 번 전달하고 즉시
  비운다. Result 씬은 곡 카탈로그에서 제목·아티스트·재킷·난이도를 읽어 표시한다.
  곡·난이도별 최고 점수, 클리어 이력, 플레이 횟수, 최고 콤보와 곡 즐겨찾기는
  Game 전용 로컬 JSON에 저장한다. Music Select는 최고 점수·즐겨찾기를,
  Result는 확정된 결과와 저장된 진행 기록을 읽는다. Auto Play는 진행에 반영하지
  않는다. 곡·난이도 첫 클리어마다 기억 조각 1개를 지급하며, 보유 수량은 클리어
  기록에서 계산한다. Result는 첫 클리어 보상만 표시하고 곡별 서사는 숨긴다.
  기존 `Music` 임시 씬은 샘플 진입용으로 보존한다.

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

## 7. Time / Judgement

모든 실행 의미는 프레임 수가 아니라 공용 chart time을 기준으로 한다. 예약 시각과
늦은 프레임의 현재 처리 시각을 별도로 보존한다. 한 프레임에서 여러 경계를 지나면:

1. 다음 Effect·입력·자동 판정 경계 중 가장 이른 시각을 선택한다.
2. 같은 시각이면 Effect의 명시 order, 입력의 sequence, 자동 판정/Miss 순으로 처리한다.
3. 현재 프레임 시각까지 위 병합을 반복한다. 미래 Effect를 과거 입력보다 먼저 적용하지 않는다.
4. 활성 Effect와 곡 기믹을 현재 프레임 시각으로 한 번 갱신한다.

시각 T의 규칙 변경은 T 이전 입력에 소급하지 않는다. 같은 시각에는 Effect가 먼저이며,
정확한 Miss 마감 시각에서는 기존 판정 계약대로 입력을 먼저 처리한다. 프레임 시간 역행과
같은 시각의 중복 Effect 갱신은 막는다. 다음 프레임에 늦게 도착한 입력의 허용/폐기 경계와
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

## 9. Ownership / Lifetime

- Effect, MusicGimmick, cancellation, camera offset, rule handle, transition request는
  한 재생 세션이 소유한다.
- Effect 종료는 자신이 만든 handle만 해제한다. 겹친 다른 Effect의 출력을 초기화하지
  않는다.
- 이전 세대의 지연 callback이나 Dispose가 새 세대의 presenter를 변경할 수 없어야 한다.
- 실행·취소·예외·컴포넌트 비활성화 모두 같은 멱등 cleanup 경로를 사용한다.
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
