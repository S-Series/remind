# Music Content

`data.json` 형식 버전 1은 한 곡의 공통 정보와 난이도 목록을 소유한다. 채보 본문의 노트·시간·
Effect 시점과 조정값은 `.rd` 형식 버전 1이 함께 소유한다.
Game은 ChartMaker가 검증해 출력한 실행 패키지를 받으며 `.rd`를 직접 읽지 않는다.

```text
Assets/Data/Music/i/
  data.json
  audio.mp3
  art.jpg
  hard.rd
  rmp/
    hard.rmp.json       # 카탈로그 갱신 시 생성하는 실행 패키지
```

- `musicId`는 제목이나 폴더 이름과 독립적인 곡 식별자다. 채보의
  `musicId`가 이 값과 일치해야 한다.
- `charts[]`는 `difficultyId`, `level`, `chartAuthor`와 선택적 `chartFile`을 가진다.
  `difficultyId`는 `.rd`의 값과 일치해야 한다. `level: 0`은 테스트용
  미정 표시다. 정식 공개 전에 실제 레벨을 지정한다.
- 원본 곡 데이터와 채보는 곡 폴더 바로 아래에 둔다. 생성된 실행 패키지만
  채보 폴더의 `rmp/` 하위 폴더에 모은다. `data.json`의 선택적 `audioFile`은
  기본 `audio.mp3`, 선택적 `jacketFile`은 기본 `art.jpg`, 각 차트의 선택적
  `chartFile`은 기본 `<difficultyId>.rd`이다. 명시한 값도 같은 곡 폴더의
  파일명만 허용한다.
- `musicVolumeMultiplier`는 곡별 음악 볼륨 배수이며 범위는 `0`~`2`,
  생략 시 `1`이다. AudioSource 음악 볼륨은 `musicVolumeMultiplier × 0.5`로
  계산한다. 따라서 `0`은 무음, `1`은 `0.5`, `2`는 `1`이다. Music Select
  미리듣기와 ChartMaker의 곡 데이터 기반 음원 불러오기에 적용한다.
- `.rd`의 선택적 `jacketFile`은 난이도 전용 재킷이다. 기본 파일명은
  `<difficultyId>.jpg`이다. 기본 파일이 없으면 곡의 `art.jpg`를 사용한다.
  명시한 재킷 파일이 없으면 오류로 처리한다. 파일명에는 폴더 경로를 넣지 않는다.
- 현재 `i`의 제목과 제작자 정보는 테스트 값이다. 사용자 점수·해금·즐겨찾기와
  세션별 판정 상태는 이 파일에 저장하지 않는다.
- ChartMaker는 목록에 등록된 채보를 열거나 저장할 때 식별자와 경로를 확인한다.
  Effect의 정의와 파라미터는 `eventDictionary` 항목 안에서 함께 저장한다.
- Music Select는 `Assets/Data/Music/*/data.json`을 기준으로 만든
  `MusicCatalog.asset`의 곡마다 `Music Track.prefab`을 실행 중 인스턴스화한다.
  곡명·아티스트는 같은 `data.json`에서, 기본 재킷과 음악은 각각 `jacketFile`과
  `audioFile` 참조에서 온다. 카탈로그 생성 시 등록된 채보 파일의 ID와 존재를 확인하고
  난이도 전용 재킷 또는 기본 재킷을 연결하며,
  난이도 ID·레벨을 기록한다. Music Select의 EASY/NORMAL/HARD/CHAOS 버튼은
  현재 곡에 해당 채보가 있을 때만 활성화한다. 선택한 곡의 `previewStartMs`부터
  `previewDurationMs` 구간을 재생하고 반복한다. 반복 끝에서는 0.35초 동안
  페이드아웃하고 시작 지점부터 0.35초 동안 페이드인한다. 실제 구간이 짧으면
  페이드 길이를 구간의 절반 이하로 제한한다. 왼쪽/오른쪽 Shift는 사용 가능한
  난이도 사이를 이동한다. 씬의 `Music Track`은 편집 중 배치 확인용이며 실행 시
  숨긴다.
  `Test-01`~`Test-06`은 비활성 상태로 보존한다.
- 곡 폴더나 파일을 가져오면 Editor의 `MusicCatalogBuilder`가 카탈로그를 다시
  만든다. 등록된 채보마다 채보 폴더의 `rmp/`에 `<채보 파일명>.rmp.json` 실행 패키지를
  출력하고 카탈로그의 난이도 항목에 연결한다. 수동으로는
  `REmind/Music/Rebuild Catalog` 메뉴를 쓴다. Game 빌드 직전에도 재생성하며,
  잘못된 곡 식별자나 누락된 재킷·음악·채보는 오류로 알린다.
  Game 빌드는 파일 시스템의 `Assets/Data/Music`을 직접 순회하지 않는다.
- Music Select의 Play는 선택 ID를 `AppRoot`에 기록하고 Game으로 이동한다.
  Game은 카탈로그의 패키지 ID와 선택 ID를 대조하고 음원·곡 볼륨과 함께 세션을
  준비한 뒤 재생한다. `REmind/Build I Song Runtime Package`는 `i`만 별도로
  출력하는 개발용 명령으로 남아 있다.
- 결과의 곡·난이도별 최고 점수·랭크와 곡 즐겨찾기는
  `Application.persistentDataPath/player-progress-v1.json`에 저장한다.
  저장 파일은 버전 1이며 쓰기 전 임시 파일과 이전 파일 백업을 사용한다.
  Music Select에 다시 진입하면 해당 난이도의 최고 점수와 즐겨찾기를 표시한다.
  Auto Play 결과는 Result에서 표시하되 최고 점수에는 기록하지 않는다.
- `.rd`의 곡·난이도·타이밍 정보가 먼저 나오고, 긴 `notes`와
  `eventDictionary` 배열이 마지막에 나온다. 저장 중 실패에 대비한 `.rd.bak`은
  생길 수 있지만 별도 Effect 본문 파일은 만들지 않는다.
