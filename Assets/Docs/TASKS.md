# ReMind Tasks

> 상태: Working Document  
> 현재 작업과 바로 다음 작업만 기록한다.

## Current

- `Assets/Data/Music/i`를 `data.json` v2, 음원, 재킷, 채보·Effect pair로
  정리했다. 공용 `SongContentCodec`와 ChartMaker 저장·열기 검증, Music Select
  행의 곡명·아티스트·레벨 읽기를 연결했다. `level: 0`과 `Unknown`은 테스트
  값이므로 정식 콘텐츠 메타데이터를 받으면 교체한다. 이번 변경은 C# 빌드와
  파일의 ID·revision·GUID 및 공용 JSON 파서까지 확인했다. Unity 리로드와
  Test Runner는 사용자 요청에 따라 실행하지 않았다.
- Home 메뉴의 씬 배치 버튼에 공통 선택 Scope/Node와 UI 입력 모듈을 연결했다.
  Game의 선택·일시정지·결과·오류 메뉴도 씬의 `GameFlowCanvas`로 옮겼다.
  Home 중앙은 Music으로 바꾸고 Gallery를 ReMind로 교체했다. Story, Music,
  Character, ReMind, Option, Settings에는 각각 독립 임시 씬과 Home 복귀를
  연결했다. Music의 샘플 버튼은 Game으로, Game 곡 선택의 Cancel은 Music으로,
  Home의 Exit는 종료로 연결한다.
  이번 UI 변경은 C# 빌드와 씬 YAML 구조를 확인했다. Unity Editor에서
  Home과 Music의 Canvas 표시도 확인했으며 실제 메뉴 입력·씬 전환은 아직
  확인하지 않았다.
- 첫 곡 `Chroma I`를 `Game.unity`에서 선택→플레이→일시정지/재개→결과→재시도
  흐름으로 연결했다. ChartMaker 초안에서 검증·출력한 302 Tap 실행 패키지를 번들로 쓴다.
- Game 판정은 공용 `PlayableChartSnapshot`과 `PlayableJudgementSession`을 직접
  사용한다. `NoteData` 변환 경로와 옛 레인 큐는 제거했다.
- Long Scratch Start 양입력, Mid 유지 확인과 양입력 재합류, End 음입력 및
  구간별 점수·콤보·체력 처리를 구현했다. 노트 종류별 시간 창 파일은 만들었으나
  최종 수치는 미정이라 기존 기본값으로 초기화했다.
- Game과 ChartMaker는 별도 assembly와 별도 Windows 빌드로 유지한다.
- Home의 MUSIC 버튼을 `MusicSelect`로 연결했다. 현재 첫 곡 행을 배치했으며,
  분류·정렬·즐겨찾기·난이도 선택과 Home 복귀는 저장된
  씬 하이어라키와 버튼 이벤트로 구성했다. 기존 `Music` 임시 씬은 유지하며
  새 화면의 실제 곡 재생과 결과 저장은 후속 작업이다.
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
  씬 YAML 참조와 C# 빌드는 확인했고 Unity 화면 확인은 리로드 후 진행한다.

## Verified

- Unity EditMode 전체 90/90 통과. 공용 Long Scratch 구간/재합류/마감과
  Game 장면·패키지 연결 회귀를 포함한다.
- `dotnet build remind.slnx` 경고 0, 오류 0.
- Game과 ChartMaker Windows 빌드 성공. 상대 제품 DLL은 각 Managed 폴더에 없다.
- Game 빌드의 무화면 시작 로그에서 초기화 오류를 보지 못했다. 실제 화면·키 입력은
  직접 조작해 검증하지 않았다.
- `Game.unity` PlayMode smoke에서 302개 노트 표시 객체, 마지막 노트 이후까지의
  재생 시간, Play/Pause/Resume/Restart/Stop 전이를 확인했다.

## Next

- `i`의 실행 패키지를 Unity에서 출력한 뒤 Game용 곡 목록 및 곡 선택→플레이
  연결을 완성한다. 현재 Game은 기존 `chroma-i` 샘플 패키지를 유지한다.
- Unity에서 나머지 임시 씬과 Game의 Canvas를 열어 초기 선택, 방향 이동,
  Submit, 포인터 호버·클릭, Cancel, Home↔각 씬 이동, Music→Game 이동,
  Pause/Resume, Result 재시도를 직접 확인한다. 이번에는 Home과 Music의
  화면만 확인했고 컴퓨터 조작 도구의 클릭 입력이 우클릭으로 전달되어 실제
  전환은 확인하지 못했다. Test Runner는 별도 요청 후 진행한다.
- `Game.unity`를 화면에서 실행해 10레인 입력, 노트 위치·속도, 판정 표시,
  Pause/Resume/Restart, 곡 종료와 결과를 수동 확인한다.
- ChartMaker에서 초안 편집·저장·재열기·Preview 및 게임용 패키지 재출력을
  실제 UI로 확인한다. 원본 초안 파일은 자동 변경하지 않는다.
- Long Scratch 전용 Start/Mid/End 허용 창과 Miss 마감 수치를 확정하고
  `NoteJudgeWindowProfile`별 값을 조정한다. 일반 노트 간접 판정 정책도 확정한다.
- Long/Scratch의 표시 길이·깊이·lane 배치를 수동 검증하고 필요한 프리팹과
  연출을 보완한다. 현재 첫 곡의 패키지에는 Tap만 있다.
- Music Select 곡 목록은 `Data/Music`에서 생성한 카탈로그와 기존 Track Prefab으로
  구성한다. Unity Play 모드에서 `i` 곡의 슬롯 1개, 제목·아티스트와 재킷 표시를
  확인했다. 미리듣기 구간 반복, 곡 목록 이동·재킷 전환 애니메이션, 채보가 있는
  난이도만 선택하는 UI와 좌우 Shift 입력을 파일에 연결했다. 새 기능의 Unity 화면·
  청음 검증은 남아 있다. 선택한 곡의 실행 패키지를 Game에 넘기는 연결도 남았다.
- 진행 저장, 결과 UI 다듬기와 배포용 Build Profile을 추가한다.
- 사용하지 않는 구형 `ChartLoader`/`NoteData` 파일을 별도 정리한다.

## Notes

Effect 작업 1~7단계는 전역 `ROADMAP.md`의 Phase 번호와 다르다.
