# Music Content

`data.json` v2는 한 곡의 공통 정보와 난이도 목록을 소유한다. 채보 본문의 노트·시간·
Effect 시점은 `.rd` v8이, Effect 조정값은 같은 폴더의 sidecar가 소유한다.
Game은 ChartMaker가 검증해 출력한 실행 패키지를 받으며 `.rd`를 직접 읽지 않는다.

```text
Assets/Data/Music/i/
  data.json
  audio/music.mp3
  jackets/hard.jpg
  charts/hard.rd
  charts/effect.i.hard.json
```

- `musicId`는 제목이나 폴더 이름과 독립적인 곡 식별자다. 채보와 sidecar의
  `musicId`가 이 값과 일치해야 한다.
- `charts[]`는 `difficultyId`, `level`, `chartAuthor`, `chartFile`을 가진다.
  `difficultyId`는 `.rd`와 sidecar의 값과 일치해야 한다. `level: 0`은 테스트용
  미정 표시다. 정식 공개 전에 실제 레벨을 지정한다.
- `audioFile`, `jacketFile`, `chartFile`은 `data.json` 폴더 기준 상대 경로이며
  `/`를 구분자로 쓴다. 상위 폴더 탈출은 허용하지 않는다.
- 현재 `i`의 제목과 제작자 정보는 테스트 값이다. 사용자 점수·해금·즐겨찾기와
  세션별 판정 상태는 이 파일에 저장하지 않는다.
- ChartMaker는 목록에 등록된 채보를 열거나 저장할 때 식별자와 경로를 확인한다.
  Effect가 있는 채보는 `.rd`와 sidecar의 revision까지 맞는 짝으로 연다.
- Music Select는 `Assets/Data/Music/*/data.json`을 기준으로 만든
  `MusicCatalog.asset`의 곡마다 `Music Track.prefab`을 실행 중 인스턴스화한다.
  곡명·아티스트는 같은 `data.json`에서, 재킷과 음악은 각각 `jacketFile`과
  `audioFile` 참조에서 온다. 카탈로그 생성 시 등록된 채보 파일의 존재를 확인하고
  난이도 ID·레벨을 기록한다. Music Select의 EASY/NORMAL/HARD/CHAOS 버튼은
  현재 곡에 해당 채보가 있을 때만 활성화한다. 선택한 곡의 `previewStartMs`부터
  `previewDurationMs` 구간을 재생하고 반복한다. 왼쪽/오른쪽 Shift는 사용 가능한
  난이도 사이를 이동한다. 씬의 `Music Track`은 편집 중 배치 확인용이며 실행 시
  숨긴다.
  `Test-01`~`Test-06`은 비활성 상태로 보존한다.
- 곡 폴더나 파일을 가져오면 Editor의 `MusicCatalogBuilder`가 카탈로그를 다시
  만든다. 수동으로는 `REmind/Music/Rebuild Catalog` 메뉴를 쓴다. Game 빌드
  직전에도 재생성하며, 잘못된 곡 식별자나 누락된 재킷·음악·채보는 오류로 알린다.
  Game 빌드는 파일 시스템의 `Assets/Data/Music`을 직접 순회하지 않는다.
- `i`의 실행 패키지 출력 명령은 `REmind/Build I Song Runtime Package`다.
  Music Select의 선택 곡을 Game 플레이 데이터로 넘기는 작업은 아직 남아 있다.
