# Music Content

> 현행 곡 카탈로그 계약 — 2026-10-04 검토
> 제작 형식은 [ChartFormat.md](ChartFormat.md), 진행 상태는 [TASKS.md](TASKS.md)를 따른다.

`data.json` 형식 버전 1은 한 곡의 공통 정보만 소유한다. 난이도별 레벨·제작자·
전용 재킷·노트·시간·Effect는 `.rd` 형식 버전 1이 소유한다.
Game은 ChartMaker가 검증해 출력한 실행 패키지를 받으며 `.rd`를 직접 읽지 않는다.

Unity Project 창의 빈 공간에서 우클릭해 `Remind > Music Data`를 선택하면 현재 폴더에서
기본 `data.json`의 이름을 편집하고 생성한다. 폴더를 따로 선택할 필요가 없다.
기존 파일은 덮어쓰지 않으며 충돌 시 `data 1.json` 같은 고유 이름을 사용한다.
카탈로그는 `data.json`만 읽으므로 등록할 곡 폴더에 최종 이름을 `data.json`으로 맞춘다.
Packages 등 `Assets` 외부에는 생성하지 않는다.
`musicId`·`title`·`artist`의 생성 기본값은 `default`, 미리듣기는 시작 `0ms`와 길이 `5000ms`(5초)다.
등록 전 `musicId`를 곡마다 고유하게 바꾸고 채보의 ID와 맞춘다. 기존 곡의 실제 값은 바꾸지 않는다.
`date`는 생성 시 PC 로컬 시각을 `yyyy-MM-dd HH:mm:ss`로 기록하며 자동 갱신하지 않는다.
기존 곡의 날짜는 그대로 유지한다. 날짜는 현재 작성용 메타데이터이며 공개·해금 판정에는 사용하지 않는다.
기본값과 `_guide` 원본은 `Assets/Editor/MusicDataTemplate.json`에 있다.

새 파일은 `metadataStatus: "draft"`라 카탈로그에서 제외된다. 음원·재킷·고정 이름의 채보를 준비한 뒤
`metadataStatus`를 `test`로 변경하면 등록된다. 등록 대상 곡은 `Assets/Data/Music/<곡 폴더>/`에 둔다.
생성 메뉴는 음원·재킷·채보를 만들지 않는다. `draft` 이외의 곡은 기존 콘텐츠 검증을 그대로 수행한다.

```text
Assets/Data/Music/i/
  data.json
  audio.mp3
  art.jpg
  Hard.rd
  rmp/
    Hard.rmp.json       # 카탈로그 갱신 시 생성하는 실행 패키지
```

- `musicId`는 제목이나 폴더 이름과 독립적인 곡 식별자다. 채보의
  `musicId`가 이 값과 일치해야 한다.
- `hiddenType`은 `NotHidden`(일반곡), `Masked`(목록에 존재를 표시하되 재킷·곡명·미리듣기를 가림),
  `Puzzle`(선택 시 퍼즐 조건 안내 팝업), `Invisible`(곡 목록에서 존재를 숨김) 네 문자열이다.
  공용 곡 데이터는 `SongHiddenType`으로 해석한다. 생략하거나 `null`이면 `NotHidden`이며,
  빈 문자열·숫자·다른 이름·잘못된 대소문자는 오류로 처리한다.
  해금된 난이도가 하나도 없는 `Masked`와 `Puzzle`은 제목·아티스트·재킷·미리듣기·난이도 정보·기록을 가린다.
  `Puzzle`의 잠긴 난이도를 직접 선택하면 선택적 `unlockHint` 문자열을 기존 안내 팝업에 표시한다.
  비어 있으면 "해금 조건이 아직 공개되지 않았습니다."를 표시한다. 안내문 자체가 조건을 판정하지는 않는다.
  `Invisible`은 해금 난이도가 없으면 모든 곡 목록과 분류에서 제외한다.
  히든곡은 난이도 하나 이상을 해금하면 곡 정보가 공개되지만, 일반 플레이에는 선택한 난이도의 해금이 필요하다.
- 도전 성공 시 대상 곡의 해당 난이도 이하를 누적 해금한다. 순서는 `Easy → Normal → Hard → Chaos → Extra`다.
  예를 들어 Hard 성공은 easy·normal·hard를, Extra 성공은 다섯 난이도 전체를 해금한다.
  이후 낮은 난이도에서 성공해도 기존 해금은 줄어들지 않는다.
  도전 정의와 전용 규칙은 해금 대상 곡별로 `Assets/Data/Music Unlock/<musicId>/`에 모은다.
  현재 `designant/Config.asset`은 도전 조건, `designant/Rule.asset`은 전용 게이지 규칙이다.
  Game 전용 `Assets/Data/Music Unlock/MusicUnlockLibrary.asset`의 `entries`에 Config를 여러 개
  등록하고 Bootstrap의 `AppRoot.musicUnlockLibrary`에 연결한다. 현재는 기존 designant Config 하나를 등록한다.
  선택한 실제 곡 ID·난이도와 대상 곡의 같은 난이도 해금 상태로 도전 하나를 선택한다.
  해당 난이도가 이미 해금된 도전은 제외하므로 일부 난이도만 해금된 곡은 상위 난이도 도전을 유지한다.
  조건이 둘 이상 동시에 맞으면 우선순위가 없으므로 등록 순서로 선택하지 않고 기존 시작 오류 안내로 차단한다.
  null 목록·누락된 Config·같은 Config 중복 등록·잘못된 규칙도 시작 오류로 처리한다.
  등록된 대상 곡의 잠긴 난이도 직접 시작은 차단한다. 일치하는 도전이 없거나 라이브러리가 미연결/비어 있으면
  기존 장착 캐릭터 선택 경로를 사용한다. 라이브러리는 진행이나 세션 상태를 저장하지 않으며 실행 중 자산을 수정하지 않는다.
  선택한 도전은 캐릭터 없이 특수 Hard를 적용하고, 시작 snapshot의 대상 ID로 기존 결과 저장에서 해금한다.
  Auto Play 결과는 기존처럼 해금하지 않는다. 폴더 경로를 기준으로 Config를 자동 검색하지 않는다.
  공통 조건·실행·저장 코드는 기존 Gameplay에 유지한다. 실제 곡 전용 동작이 필요해지면 해당 곡 폴더에
  스크립트를 추가하되 Gameplay 어셈블리 귀속을 함께 연결한다. 빈 스크립트나 공통 코드 복제본은 두지 않는다.
  일반 Normal·Hard 공용 규칙은 기존 `Assets/Settings/Gameplay/Rules/`에 유지한다.
- `requiresPurchase`는 곡의 이용 권한 정책이다. 참이면 사용자 `isOwned`가 있어야 플레이할 수 있다.
  소유와 해금은 독립적이다. 유료 히든곡은 소유만으로 공개되지 않으며, 해금만 된 경우 정보는 보이지만
  플레이에는 소유가 필요하다. 무료 일반곡은 두 사용자 값이 없어도 기존처럼 플레이할 수 있다.
  동일한 Game 정책을 곡 선택 화면과 선택한 곡의 Game 준비 경계에서 확인한다.
  ChartMaker 제작용 Preview에는 이 이용 제한을 적용하지 않는다.
- 곡 폴더의 채보 파일명은 `Easy.rd`, `Normal.rd`, `Hard.rd`, `Chaos.rd`,
  `Extra.rd` 다섯 개로 고정한다. 존재하는 파일만 해당 난이도로 등록하며 최소
  한 개가 필요하다. 파일명이 난이도 ID를 정한다. ID는 각각
  `easy`·`normal`·`hard`·`chaos`·`extra`이며 `.rd`에는 중복 저장하지 않는다.
- 각 `.rd`는 숫자 `level`과 문자열 `author`를 가진다. `level: 0`은 테스트용
  미정 표시다. `Extra.rd`는 표시용 문자열 `extraName`도 필수로 가진다.
  기록·해금의 난이도 ID는 표시명과 무관하게 `extra`다.
- 원본 곡 데이터와 채보는 곡 폴더 바로 아래에 둔다. 생성된 실행 패키지만
  채보 폴더의 `rmp/` 하위 폴더에 모은다. `data.json`의 선택적 `audioFile`은
  기본 `audio.mp3`, 선택적 `jacketFile`은 기본 `art.jpg`이다. 파일 참조에는
  같은 곡 폴더의 파일명만 허용한다.
- `musicVolumeMultiplier`는 곡별 음악 볼륨 배수이며 범위는 `0`~`2`,
  생략 시 `1`이다. AudioSource 음악 볼륨은 `musicVolumeMultiplier × 0.5`로
  계산한다. 따라서 `0`은 무음, `1`은 `0.5`, `2`는 `1`이다. Music Select
  미리듣기·Game 재생과 ChartMaker의 곡 데이터 기반 음원 불러오기에 적용한다.
  Game에서는 여기에 사용자 BGM 설정(`AppRoot.MusicGain`)을 곱하고 마스터 볼륨은
  AudioListener에서 적용한다. ChartMaker는 Game 사용자 설정을 읽지 않는다.
- `.rd`의 선택적 `jacketFile`은 난이도 전용 재킷이다. 기본 파일명은
  `<difficultyId>.jpg`이다. 기본 파일이 없으면 곡의 `art.jpg`를 사용한다.
  명시한 재킷 파일이 없으면 오류로 처리한다. 파일명에는 폴더 경로를 넣지 않는다.
- 현재 `i`·`designant`는 테스트 콘텐츠이며 레벨·채보 제작자에 미정 값이 있다. 사용자 점수·해금·즐겨찾기와
  세션별 판정 상태는 이 파일에 저장하지 않는다.
- ChartMaker는 곡 폴더의 채보를 열거나 저장할 때 고정 파일명·식별자·채보별
  표시 데이터를 확인한다.
  Effect의 정의와 파라미터는 `eventDictionary` 항목 안에서 함께 저장한다.
- Music Select는 `Assets/Data/Music/*/data.json`을 기준으로 만든
  `MusicCatalog.asset`의 곡마다 `Music Track.prefab`을 실행 중 인스턴스화한다.
  곡명·아티스트는 같은 `data.json`에서, 기본 재킷과 음악은 각각 `jacketFile`과
  `audioFile` 참조에서 온다. 카탈로그 생성 시 고정 이름의 채보 파일을 찾고 ID를 확인하고
  난이도 전용 재킷 또는 기본 재킷을 연결하며,
  난이도 ID·레벨·제작자·Extra 표시명을 기록한다. Music Select는
  EASY/NORMAL/HARD/CHAOS와 Extra를 다섯 Holder에 표시한다. Extra의 이름은
  해당 곡 `Extra.rd`의 `extraName`을 사용한다. 해당 채보가 있는
  Holder만 클릭해 선택할 수 있다. 선택한 곡의 `previewStartMs`부터
  `previewDurationMs` 구간을 재생하고 반복한다. 반복 끝에서는 0.35초 동안
  페이드아웃하고 시작 지점부터 0.35초 동안 페이드인한다. 실제 구간이 짧으면
  페이드 길이를 구간의 절반 이하로 제한한다. 왼쪽/오른쪽 Shift는 사용 가능한
  난이도 사이를 이동한다. 곡 행은 카탈로그에서 실행 중 생성한다.
  곡 행의 기준 위치와 간격은 `Music Track.prefab` 루트 RectTransform의
  Anchored Position과 높이를 사용한다. `Track Slots`가 목록의 화면상 위치를 정한다.
  `Music Track.prefab`의 Slot 이미지는 프리팹에 배치된 그림을 유지하고 선택 시
  색상만 바꾼다. Medal 자리는 메달 기록과 스프라이트가 준비될 때까지 비활성화한다.
  `Test-01`~`Test-06`은 비활성 상태로 보존한다.
- 곡 폴더나 파일을 가져오면 Editor의 `MusicCatalogBuilder`가 카탈로그를 다시
  만든다. 등록된 채보마다 채보 폴더의 `rmp/`에 `<확장자를 뺀 채보 파일명>.rmp.json` 실행 패키지를
  출력하고 카탈로그의 난이도 항목에 연결한다. 수동으로는
  `REmind/Music/Rebuild Catalog` 메뉴를 쓴다. Game 빌드 직전에도 재생성하며,
  잘못된 곡 식별자나 누락된 재킷·음악·채보는 오류로 알린다.
  Game 빌드는 파일 시스템의 `Assets/Data/Music`을 직접 순회하지 않는다.
- Music Select의 Play는 선택 ID를 `AppRoot`에 기록하고 Game으로 이동한다.
  Game은 카탈로그의 패키지 ID와 선택 ID를 대조하고 음원·곡 볼륨과 함께 세션을
  준비한 뒤 재생한다. `REmind/Build I Song Runtime Package`는 `i`만 별도로
  출력하는 개발용 명령으로 남아 있다.
- 결과의 곡·난이도별 최고 점수·랭크·클리어 이력·플레이 횟수·최고 콤보와 곡 즐겨찾기는
  `Application.persistentDataPath/player-progress-v1.json`에 저장한다.
  파일 이름은 유지하고 내부 저장 형식은 버전 5다. `songAccess`에는 곡별 `musicId`, `isOwned`,
  `unlockedDifficultyIds`(해금 난이도 ID 목록)와 기존 전체 해금 값 `isUnlocked`를 저장한다.
  버전 1·2를 읽을 때 기록·즐겨찾기·캐릭터를 보존하며 곡 이용 상태는 빈 목록으로 시작한다.
  버전 3의 `isUnlocked: true`는 버전 4·5에서도 전체 난이도 해금으로 유지한다. 다른 해금을 임의로 부여하지 않는다.
  버전 3 이상에서 필수 곡 상태 목록이 없거나, 버전 4 이상에서 난이도 목록이 누락·손상된 경우 오류로 처리한다.
  버전 1~4의 클리어 기록은 버전 5 이행 시 기억 조각 잔액으로 한 번 옮긴다.
  이후 수량은 `inventory`의 `itemId`·정수 수량으로 저장하고, 버전 5에서 목록이 누락·손상되면 오류로 처리한다.
  쓰기 전 임시 파일과 이전 파일 백업을 사용하며, 소유·해금 변경은 저장 성공 후 메모리에 반영한다.
  `LocalPlayerDataStore.SetSongOwned`는 해금을 보존해 소유만 변경한다. `SetSongUnlocked`는 소유를 유지하며
  전체 해금 또는 해금 초기화를 수행한다. 도전 성공 정산은 기존 해금 목록에 해당 난이도 이하를 추가한다.
  상점·스토리팩 구매로 소유를 획득하는 경로는 아직 구현하지 않았다.
  Music Select에 다시 진입하면 해당 난이도의 최고 점수와 즐겨찾기를 표시한다.
  실패도 플레이 횟수에 포함한다. Auto Play 결과는 Result에서 표시하되 진행에는
  기록하지 않는다. 첫 클리어는 저장 전 이력과 비교해 기억 조각 1개를 기록·해금과
  함께 저장한다. 보유 수량은 저장된 잔액을 읽으며, 소비 후 재클리어해도 재지급하지 않는다.
  소비 API는 있으나 소비 UI와 추가 지급 정책은 아직 연결하지 않았다.
- Game 설정은 `Application.persistentDataPath/player-settings-v1.json`에 별도 저장한다.
  파일 이름과 달리 현행 내부 형식은 버전 2이며 버전 1 설정을 보존해 읽는다.
  곡 metadata나 `.rd`에 사용자 음량·키·판정 보정을 복사해 저장하지 않는다.
- `.rd`의 곡·난이도·타이밍 정보가 먼저 나오고, 긴 `notes`와
  `eventDictionary` 배열이 마지막에 나온다. 저장 중 실패에 대비한 `.rd.bak`은
  생길 수 있지만 별도 Effect 본문 파일은 만들지 않는다.
