# ReMind Tasks

> 상태: Working Document  
> 현재 작업과 바로 다음 작업만 기록한다.

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
  Music Select의 Play에서 선택을 기록하고 기존 `MusicSelected` 48프레임
  애니메이션을 AppRoot 자식의 전체 화면 오버레이로 재생한 후 Game 씬에 진입한다.
  중복 Play와 전환 중 ESC 복귀를 막고 Game 진입 후 오버레이를 닫는다. 실행 패키지와
  재생 세션은 Game 씬이 소유한다.
- `Assets/Data/Music/i`를 `data.json` 형식 버전 1, 음원, 재킷, 단일 채보 파일로
  정리했다. 공용 `SongContentCodec`와 ChartMaker 저장·열기 검증, Music Select
  행의 곡명·아티스트·레벨 읽기를 연결했다. `level: 0`과 `Unknown`은 테스트
  값이므로 정식 콘텐츠 메타데이터를 받으면 교체한다. 파일의 ID·revision·GUID와
  공용 JSON 파서, Unity EditMode 검사를 확인했다.
- Home 메뉴의 씬 배치 버튼에 공통 선택 Scope/Node와 UI 입력 모듈을 연결했다.
  Game의 선택·일시정지·결과·오류 메뉴도 씬의 `GameFlowCanvas`로 옮겼다.
  Home 중앙은 Music으로 바꾸고 Gallery를 ReMind로 교체했다. Story, Music,
  Character, ReMind, Option, Settings에는 각각 독립 임시 씬과 Home 복귀를
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
- Settings 임시 화면을 작동하는 메뉴로 교체했다. 음악 음량 0~100%, 판정 보정
  -200~+200ms(5ms 단위), 10레인 키 변경·초기화를 제공한다. 설정은 진행 기록과
  분리된 `player-settings-v1.json`에 즉시 저장한다. 곡 선택 미리듣기와 Game
  음량은 곡별 볼륨에 사용자 음량을 곱하고, 판정 보정과 키 변경은 다음 세션의
  판정·입력에 적용한다. ChartMaker에는 적용하지 않는다.
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
  ReMind UI PNG 팩의 런타임 스프라이트 44개를 `Assets/Art/Sprites/UI/ReMindUI`에
  가져왔다. Sprite/9-slice 설정은 `.meta`에 기록했다. MusicSelect의 곡 Slot,
  Favorite 아이콘, 재킷 프레임, 분류 탭, 난이도 링, 시작 버튼에 대응하는
  Sprite를 연결했다.
  그중 상태별 중심이 어긋난 19개 PNG의 픽셀 위치를 보정했다. Unity 화면은 미확인이다.
  씬 YAML 참조와 C# 빌드는 확인했고 실제 화면 입력 확인은 남아 있다.

## 미커밋 변경 묶음 (2026-09-28 기준선)

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
- 설정 저장·재로드·백업 복구·값 검증 테스트와 Settings 씬 버튼→Home→
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
- Settings의 실제 표시, 음량·판정 보정 체감, 레인 키 재지정·중복 거부·ESC 취소,
  게임 재실행 뒤 설정 유지와 플레이 반영을 확인한다. 사용자가 나중에 확인한다.
  자동 메서드 호출과 버튼 스모크를 물리 입력 성공으로 간주하지 않는다.
- Music Select→Game의 48프레임 전환이 실제 화면 비율에서 잘리지 않고
  의도한 속도로 보이는지, 전환 중 클릭·ESC와 Game 진입 뒤 화면·음원이
  자연스러운지 직접 확인한다.
- 정식 화면 아트와 곡 레벨·채보 제작자 메타데이터가 준비되면 임시 표시를
  교체하고 Game/ChartMaker 빌드에서 화면·입력을 직접 다시 확인한다.

## Next — 작업 순서

1. 정식 곡 메타데이터·아트와 서사 데이터가 준비되면 임시 표시를 교체한다.
2. 실제 화면·청음·물리 입력으로 판정 창, UI 탐색, Game/ChartMaker 빌드를
   검수한다. 자동 검사와 별도 확인이 필요하다.
3. 실제 하드웨어에서 프레임 시간·GC·메모리와 배포 호환성을 측정한다.
   구형 `ChartLoader`/`NoteData`는 현행 Game 호출부에서 사용되지 않지만
   `ChartLoadService` 내부가 아직 사용하므로 별도 정리에서 제거 여부를 결정한다.

## Notes

Effect 작업 1~7단계는 전역 `ROADMAP.md`의 Phase 번호와 다르다.
