# Chart Format

> 현행 계약 검토: 2026-09-29
> 제작 원본: `REmindChart` JSON `formatVersion: 1` (`.rd`).
> Game 전달 파일: Runtime Package `Version: 1` (`.rmp.json`).

현행 형식은 18~20절에 모았다. 기존 문서의 절 번호를 유지하며, 1~17절의 초기
설계 초안은 문서 끝의 부록으로 이동했다. 부록의 JSON은 현행 파일 예제로 사용하지 않는다.

곡 공통 정보·오디오·재킷 경로는 [MusicContent.md](MusicContent.md), 시간·입력·판정의
실행 의미는 [RhythmSystem.md](RhythmSystem.md)를 따른다.

## 18. ChartMaker Editor JSON 형식 버전 1

ChartMaker의 기본 저장 형식은 UTF-8 JSON이며 전용 확장자는 `.rd`이다. 편집 원본은
`measure`와 `position`을 유지하고, 테스트 플레이와 Gameplay에 전달하기 전에 공용
`ChartCompiler`가 `ChartTime`과 `FloorPosition`을 계산한다. 반복되는 채보 본문은
기존 Native TXT 한 줄을 그대로 JSON 문자열 배열에 담아 간결하게 유지한다.

```json
{
  "format": "REmindChart",
  "formatVersion": 1,
  "musicId": "demo_song",
  "difficultyId": "normal",
  "gimmickId": "",
  "baseBpm": 120.0,
  "musicStartCorrectionMs": 0.0,
  "revision": "example_revision",
  "notes": [
    "000|0000|LF------|--------|00000000|-1|-|F|-",
    "000|1200|--------|----NT16|00010000|240|0.5|T|N:-5*"
  ],
  "eventDictionary": [
    {
      "position": 1200,
      "effectId": "fx_example",
      "effectTypeId": "camera.offset",
      "commandId": "",
      "order": 0,
      "parameters": {
        "durationMs": 400,
        "offsetX": 0,
        "offsetY": 0,
        "rollDegrees": 0
      }
    }
  ]
}
```

현재 최상위 필드는 다음과 같다. 이 표는 저장 계약이며 아래 부록의 초기 DTO와
다르다. 버전 1 파서는 허용하지 않은 최상위 필드를 거부한다.

| 필드 | 현재 의미 |
| --- | --- |
| `format`, `formatVersion` | `REmindChart`, 정수 `1` |
| `musicId`, `difficultyId` | 소속 곡과 난이도 ID |
| `jacketFile` | 선택적 난이도 재킷 파일명. 경로 규칙은 [MusicContent.md](MusicContent.md) |
| `gimmickId` | 곡별 C# MusicGimmick 등록 ID, 없으면 빈 문자열 |
| `baseBpm` | 0보다 큰 유한한 시작 BPM |
| `musicStartCorrectionMs` | 유한한 밀리초 보정값. 부호는 [RhythmSystem.md](RhythmSystem.md)의 보정값 계약 참조 |
| `revision` | 저장된 편집 문서 식별값 |
| `notes` | 위치순 행 문자열 배열. 노트가 없어도 배열 자체는 필요 |
| `eventDictionary` | Effect 정의와 파라미터 배열. Effect가 없으면 빈 배열 |

곡 제목·아티스트·오디오 경로는 [MusicContent.md](MusicContent.md)의 `data.json`에서
관리한다. 현재 `.rd`는 노트마다 정수 `timeMs`를 저장하지 않는다.
공용 컴파일 결과의 시간은 `double` 밀리초다.

- 각 `notes` 문자열의 열은
  `measure|position|main notes|scratch notes|air notes|target BPM|line speed|effect|camera`
  순서다. 이름은 `notes`지만 BPM·속도·Effect·Camera 행도 함께 담는다.
- 행은 `(measure, position)` 오름차순이며 같은 위치를 중복할 수 없다.
- `position`은 한 마디 안의 `0000`~`4799` 정수다.
- Main Note의 line은 `1`~`4`, hand는 `L` 또는 `R`이다.
- Tap의 `point`는 `Tap`, Long Tap은 `Start`와 `End`를 순서대로 사용한다.
- 일반 Tap과 Long Tap에는 Powered 속성이 없다.
- Scratch side는 `Left` 또는 `Right`다. 단 Scratch는 `Tap`, Long Scratch는
  `Start`, 0개 이상의 `Mid`, `End` 순서로 닫힌다.
- Scratch motion은 `N`(None), `G`(Gradual), `I`(Instant), `R`(Release),
  amount는 `00`~`99`다. `R`은 Long Scratch의 `Mid`와 `End`에만 사용할 수 있다.
- Air Note는 Main 1~4에 대응하며 값은 `00`~`99`다.
- 변경 없는 BPM은 `-1`, Line Speed와 Camera는 `-`, Effect는 `F`로 쓴다.
- Camera는 `spin:offsetX`이며 spin은 `L`, `N`, `R` 중 하나다.
- Marker가 있는 행은 별도 열을 추가하지 않고 Camera 열 뒤, 즉 문자열 마지막에
  `*`를 하나 붙인다. Marker는 판정·스크롤·카메라에 영향을 주지 않는 위치 표식이다.

행 문자열의 실제 토큰은 다음과 같다. 위의 `line`, `hand`, `point`는 의미를
설명하는 명칭이며 버전 1의 개별 JSON 프로퍼티가 아니다.

| 열 | 고정 길이와 인코딩 |
| --- | --- |
| Main | 8문자: Main 1~4 순서의 2문자 토큰. `--` 또는 손 `L/R` + `F`(Tap), `S`(Start), `E`(End) |
| Scratch | 8문자: Left, Right 순서의 4문자 토큰. `----` 또는 motion `N/G/I/R` + 지점 `T/S/M/E` + 두 자리 amount |
| Air | 8문자: Main 1~4 순서의 두 자리 값. `00`은 없음 |

예제의 `NT16`은 오른쪽 단일 Scratch다. `NS16`으로 쓰면 닫는 End가 필요한
Long Scratch Start를 뜻한다.

형식 버전 1은 실행 가능한 Effect의 정체성과 파라미터를 행 문자열과 분리해
`eventDictionary`에 저장한다. 곡·난이도·타이밍 정보 뒤에 긴 `notes`와
`eventDictionary` 배열을 둔다. `eventDictionary`는 현재 Effect 정의만 담는 배열이다.

- `musicId`, `difficultyId`: 채보와 난이도의 저장 소유자.
- `gimmickId`: `music.call`이 사용할 곡별 C# MusicGimmick 등록 ID. 없으면 빈 문자열이다.
- `revision`: 저장된 편집 문서의 식별값이다.
- `eventDictionary`: Effect가 `T`인 각 행과 정확히 하나씩 대응한다.
- `position`: `measure * 4800 + position`으로 계산한 절대 chart position이다.
- `effectId`: 문서 안에서 고유한 stable ID다.
- `effectTypeId`: 등록된 공용 Effect 종류다. `music.call`은 곡 기믹 명령 호출을 뜻한다.
- `commandId`: 일반 Effect는 빈 문자열, `music.call`은 등록된 명령 ID다.
- `order`: 같은 시각 Effect끼리의 0 이상 실행 순서이며 같은 시각에는 고유해야 한다.
- `parameters`: 해당 Effect의 조정값 객체다. 설정이 없는 명령은 생략할 수 있다.

Effect를 이동·정렬할 때 `effectId`를 유지하고, 복사할 때는 새 ID를 만든다. v7의
의미가 지정되지 않은 Effect는 로드 시 ID만 만들어 보존하며 종류를 추정하지 않는다.
종류가 지정되기 전까지 편집/재저장은 가능하지만 컴파일과 재생 준비는 실패한다.

Line Speed는 표시 이동량만 변경하고 판정 시각은 바꾸지 않는다. 예를 들어
행의 Line Speed 열 값 `0.5`는 그 지점부터 같은 BPM의 기본 표시 이동량을 절반으로 만든다.

Camera Note가 처리되는 순간의 기준값은 다음과 같다.

```text
CameraReferenceX = LineCenterXAtEvent + OffsetX
SpinDurationMs = 175 * 120 / EventBpm
```

`Left`는 반시계 `+360도`, `Right`는 시계 `-360도`, `None`은 회전 없음이다.
회전 진행은 EaseInOut으로 평가하며 BPM 120에서는 175ms, BPM 240에서는
87.5ms가 걸린다.

Preview와 Test Play 카메라는 회전감을 강조하기 위해 회전 방향의 반대쪽으로 X를
함께 보정한다. 360도 Spin은 시작과 끝에서 `0`, 회전 중간에서 최대 `3.5`만큼
이동한다. Camera Note가 정한 기준 X의 기울기는 현재 각도에 비례하여 최대 `2`만큼
이동한다. Preview Camera는 Line 중심을 추적하며, Powered Scratch의 X는 Line 추적
계산 이후 독립 변수로 최대 `7.5`만큼 추가 적용한다. 오른쪽 `-10도` 롤은 카메라 `-7.5 X`, 왼쪽
`+10도` 롤은 카메라 `+7.5 X`가 된다. Powered 롤에 비례한 Y 회전도 추가하여
`Z +10도`에서는 `Y -2.5도`, `Z -10도`에서는 `Y +2.5도`를 적용한다.
Z 롤이 복귀하면 X 이동도 같은 곡선으로 `0`에 복귀한다.

Scratch 이동 거리는 Amount `10`당 `7.5 horizontal units`, 즉 Amount `1`당
`0.75`다. Long Scratch의 `End`는 `None` 또는 `Release`를 사용할 수 있다. End까지
이어지는 이동은 앞선 `Start` 또는 `Mid`의 Motion을 사용하고, End가 `Release`라면
End부터 별도의 역방향 이동을 시작한다.

`Instant` Scratch는 저장 타입과 판정 시각은 그대로 유지하되 화면 경로를 노트
위치부터 `1/32마디` 동안 선형 보간한다. 현재 좌표계에서는 `150 position units`에
해당하며, Line Speed가 적용된 Preview에서는 같은 두 ChartPosition의
FloorPosition 구간을 사용한다.

`Release`는 `Instant`와 같은 `1/32마디` 선형 보간을 사용하되, 지정한 Amount만큼
반대 방향으로 이동한다. Long Scratch의 `Mid`와 `End`에만 저장할 수 있다.

Instant Scratch가 처리되면 카메라는 이동 방향의 반대쪽으로 최대 `10도` 기울었다가
원래 자세로 돌아온다. 전체 지속시간은 `200 * 120 / EventBpm` 밀리초다. 처음 10%의
시간에 EaseOut으로 빠르게 최대 각도에 도달하고, 남은 90% 동안 감쇠하며 복귀한다.
BPM 120에서는 약 20ms에 최대 각도에 도달하고 180ms 동안 돌아온다. 좌우 Instant
Scratch가 동시에 처리되면 서로 상쇄하고, 겹친 연출의 최종 기울기는
`-10도`~`10도`로 제한한다. 이 연출은 표시 전용이므로 판정 시각에는 영향을 주지 않는다.
`Release`는 같은 지속시간과 곡선을 사용하며 기울기 방향만 `Instant`의 반대로 적용한다.

Long Scratch의 `Gradual`이 시작되면 BPM과 관계없이 고정 `100ms` 동안 Linear로
이동 방향의 반대쪽 `5도`까지 기울고, 해당 구간이 끝날 때까지 각도를 유지한다.
다음 `Mid`의 Motion이 다시 `Gradual`이면 Attack을 재시작하지 않고 그대로 유지한다.
`Instant`이면 그 위치부터 Instant 충격 곡선을 사용하며, `None`이면 해제를 시작한다.
Long Scratch의 `Mid` 또는 `End`에서 `Release`를 만나면 유지 중인 Gradual 기울기를
고정 `20ms` 동안 원래 각도로 복귀시키는 동시에 역방향 Instant 충격 곡선을 적용한다.
End의 `None`은 Gradual 기울기만 해제한다. Gradual의 Attack과 해제도 표시 전용이며
판정에는 영향을 주지 않는다.

에디터에서 Tap 계열 노트를 선택하고 `Tab`을 누르면 Left/Right Hand가 전환되고,
`Shift+Tab`을 누르면 Tap/Long Tap이 전환된다.
Scratch 계열 노트를 선택한 경우 `Tab`은 Powered를 전환하고, `Shift+Tab`은
Scratch/Long Scratch를 전환한다. Powered를 켤 때 일반 Scratch는 `Instant`, Long
Scratch의 Start/Mid는 `Gradual`, End는 `Release`를 기본값으로 사용한다.

노트 배치 도구 단축키는 `Q` Tap, `W` Scratch, `E` Eraser, `R` Air,
`T` Specials다. Tap 또는 Scratch 도구가 활성화된 상태에서 같은 단축키를 다시
누르면 일반형과 Long형을 전환한다. Specials는 `Speed`, `Effect`, `Camera`,
`Marker` 순서로 순환한다.
Speed 이벤트를 배치하면 기본 배율은 `1`이며, 선택한 뒤 편집 창에서 양수 배율과
위치를 수정할 수 있다. Line Speed는 노트 판정 시각에는 영향을 주지 않는다.

같은 Main 라인에 닫히지 않은 Long Tap 시작점이 있으면 일반 Tap 도구로 배치한
다음 Tap도 그 Long Tap의 End로 처리한다. 편집 입력만 Tap 도구를 공유하며 저장
데이터는 기존과 동일하게 `LongTap End`이므로 파일 형식과 컴파일 규칙은 바뀌지 않는다.
마찬가지로 같은 Scratch 라인에 닫히지 않은 Long Scratch가 있으면 일반 Scratch
도구로 배치한 다음 Scratch를 `LongScratch End`로 처리한다.

Long Tap과 Long Scratch 프리팹은 일반 노트와 구분할 수 있도록 짧은 반투명 Ribbon
Stub을 포함한다. 배치 미리보기와 아직 닫히지 않은 Start/Mid에서만 Stub을 표시하며,
구간이 닫히면 시작점의 Stub은 실제 Ribbon으로 교체하고 End에서는 숨긴다.

저장 시 닫히지 않은 Long Note는 저장을 실패시키지 않고 자동 정규화한다.

- 짝이 없는 Long Tap은 일반 Tap으로 변환한다.
- End가 없는 Long Scratch의 Start/Mid는 각각 일반 Scratch Tap으로 변환한다.
- Start가 없는 Long Scratch Mid/End도 일반 Scratch Tap으로 변환한다.
- 정상적으로 닫힌 Long Tap과 Long Scratch는 변경하지 않는다.

저장 성공 후 에디터의 데이터와 표시 오브젝트에도 같은 변환을 적용하므로 바로
Test Play를 시작해도 저장 파일과 동일한 결과를 사용한다.

### 18.1 이전 형식 읽기 호환

ChartMaker의 파일 열기와 최근 파일 복원은 `.rd` 확장자만 허용한다. `.json`과 `.txt`는
선택창에 표시하지 않으며 경로가 직접 전달되어도 로드 전에 차단한다. 포맷 변환을 위한
기존 compact JSON v8/v7, JSON 객체형 v6, Native v1~v6, 구버전 병렬 배열 파서는 내부
호환 코드로만 유지한다.

```text
#REmindChart|6
#BPM|120
#MUSIC_START_CORRECTION_MS|0
measure|position|main notes|scratch notes|air notes|target BPM|line speed|effect|camera
```

구버전 Powered Tap `T`는 일반 Tap으로 정규화한다. v1~v5 Camera `T`는
`spin: None`, `offsetX: 0`으로 변환한다. v1~v4 파일에는 Line Speed 이벤트가
없는 것으로 처리하여 기본 배율 `1`을 사용한다.

### 18.2 좌표 호환성

native 포맷 v3~v6의 위치 좌표는 다음 규칙을 사용한다.

- 한 마디의 리듬 기준은 `240 pulses`다.
- 한 pulse는 저장 정밀도 `20 position units`를 가진다.
- 따라서 한 마디는 `4800 position units`이며 유효한 마디 내부 위치는 `0`~`4799`다.
- 화면상의 한 마디 높이는 이전과 동일한 `160 world units`다.
- 좌표 변환은 `30 position units = 1 world unit`이다.
- 3분할은 `1600`, 5분할은 `960`, 16분할은 `300` position units 간격이므로 모두 정수 좌표다.

native v1, v2와 버전 헤더가 없는 파일은 기존 `1600 units/measure`로 해석한 뒤
로드 시 정확히 3배하여 현재 좌표로 변환한다. v3 파일은 위치 좌표를 그대로 읽고,
별도 Scratch motion 필드를 v4 토큰으로 합친다. 레거시 이동 거리는 `00`~`99`로
정규화하며, powered가 아니었던 Scratch는 이동량을 보존한 `N` motion으로 변환한다.
이전 정수 좌표는 전부 손실 없이 변환되며 화면 위치와 BPM 기반 재생 시각은 변하지 않는다.

레거시 `TempChartData` JSON의 `NotePos`도 기존 1600 단위로 해석한다. 이 경로는
불러오기 호환용이며 저장할 때는 현재 ChartMaker Editor JSON으로 변환한다.

## 19. Effect 파라미터

Effect의 조정값은 같은 `.rd`의 `eventDictionary[].parameters` 객체에 둔다.
Effect 행·ID·종류·순서와 함께 검증한 다음 `.rd` 한 파일을 저장한다.
설정이 없는 명령은 `parameters`를 생략할 수 있다. `camera.offset`에서
`attackMs`/`releaseMs`가 없으면 0ms로 읽고, 새 설정은 각각 100ms를 기본으로 쓴다.

Effect별 필드, 범위, ChartMaker 작업 순서, 실행·정리·Preview 제한은
[EffectGimmickGuide.md](EffectGimmickGuide.md)를 따른다.

## 20. Game 전달용 Runtime Package

`.rd`는 ChartMaker의 제작 원본이며, Game은 내보낸 `.rmp.json`을 사용한다.
`ChartMakerRuntimePackageExporter`가 편집 데이터에서 공용 `ChartDocument`를
구성하고 `RuntimeChartPackageCodec.Export`로 컴파일·직렬화한다.

현재 Package는 `Version: 1`을 사용한다. `.rd`의 `formatVersion: 1`과는 별개의
계약이며 필드도 `MusicId`, `DifficultyId`, `ChartOffsetMs`, `Notes`, `Effects`,
`Parameters` 등 PascalCase다. 노트 위치·Timing·Line Speed·Camera·Scratch Tilt와
Effect 정의/파라미터를 포함하며, Game 전용 UI나 사용자 진행 데이터를 포함하지 않는다.

Game의 `GameplayChartPreparation`은 Package를 Import할 때 공용 문서를 복원하고
다시 컴파일한다. 이후 공용 Effect 파라미터와 실행 준비를 검증하여
`PreparedGameplayChart`를 만든다. 저장된 계산 결과를 검증 없이 신뢰하지 않는다.
원본 수정 후에는 Package를 다시 내보내며, 두 파일을 독립적인 편집 원본으로 관리하지 않는다.

파서·컴파일러·재생 준비는 서로 다른 검증 경계다. 파일을 열거나 저장할 수 있다는
사실만으로 실행 가능한 채보임을 보장하지 않는다. 전체 흐름과 책임은
[GameplayStructure.md](GameplayStructure.md), 현재 수동 검수는 [TASKS.md](TASKS.md)를 따른다.

## 부록: 이전 설계 초안 (1~17절)

아래는 초기 데모 설계 기록이다. semver 문자열 버전, 정수 `timeMs`, 메타데이터
중복 소유, Air 레인의 Hold 예제 및 별도 `timing` 객체는 현행 `.rd` 계약이 아니다.
“미정 사항”도 당시 상태이며 현재 제품 결정은 [ROADMAP.md](ROADMAP.md)에서 관리한다.
호환 파서가 이 초안 전체를 지원한다고 해석하지 않는다.

## 1. 기본 원칙

- 파일 형식은 UTF-8 JSON이다.
- 최상위 `formatVersion`을 반드시 포함한다.
- 레인은 `0`~`9`다. `0~3`은 Ground Main 1~4, `4~7`은 AirMain 1~4,
  `8~9`는 Ground Left/Right다.
- AirMain에는 단일 판정 노트만 저장하며 공중 Long Note는 지원하지 않는다.
- 노트의 기준 시간은 정수 밀리초 `timeMs`다.
- `timeMs = 0`은 오디오 파일의 샘플 0이다.
- BPM, beat, tick은 에디터 표시와 재편집 및 화면 스크롤 계산에 사용한다.
- Line Speed는 화면 스크롤에만 사용하며 판정 시각을 변경하지 않는다.
- 런타임 판정은 최종 `timeMs`를 사용한다.
- 실제 키 바인딩은 채보에 저장하지 않는다.

## 2. 파일 예시

```json
{
  "formatVersion": "0.1.0",
  "chartId": "demo-song-normal",
  "songId": "demo-song",
  "title": "Demo Song",
  "artist": "Unknown",
  "charter": "S-Series",
  "difficulty": {
    "id": "normal",
    "name": "Normal",
    "level": 5
  },
  "laneCount": 10,
  "audioFile": "demo-song.ogg",
  "chartOffsetMs": 0,
  "preview": {
    "startMs": 30000,
    "durationMs": 15000
  },
  "timing": {
    "baseBpm": 120.0,
    "bpmChanges": [
      {
        "timeMs": 0,
        "bpm": 120.0
      }
    ],
    "lineSpeedChanges": [
      {
        "timeMs": 0,
        "multiplier": 1.0
      }
    ],
    "timeSignatures": [
      {
        "timeMs": 0,
        "numerator": 4,
        "denominator": 4
      }
    ]
  },
  "notes": [
    {
      "id": "n000001",
      "type": "tap",
      "lane": 0,
      "timeMs": 1000
    },
    {
      "id": "n000002",
      "type": "tap",
      "lane": 9,
      "timeMs": 1000
    },
    {
      "id": "n000003",
      "type": "hold",
      "lane": 4,
      "timeMs": 2000,
      "durationMs": 1000
    }
  ]
}
```

## 3. 최상위 필드

| 필드 | 타입 | 필수 | 의미 |
|---|---|:---:|---|
| `formatVersion` | string | O | 채보 형식 버전. 초기값 `0.1.0` |
| `chartId` | string | O | 채보 고유 ID |
| `songId` | string | O | 곡 고유 ID |
| `title` | string | O | 표시용 곡 제목 |
| `artist` | string | O | 표시용 아티스트 |
| `charter` | string | O | 채보 제작자 |
| `difficulty` | object | O | 난이도 정보 |
| `laneCount` | integer | O | 현재 버전에서는 반드시 `10` |
| `audioFile` | string | O | 채보 파일 기준 상대 오디오 경로 또는 파일명 |
| `chartOffsetMs` | integer | O | 채보 전체 시간 보정값 |
| `preview` | object | X | 곡 미리듣기 구간 |
| `timing` | object | O | BPM과 박자표 메타데이터 |
| `notes` | array | O | 노트 목록 |

## 4. 버전 규칙

`formatVersion`은 `MAJOR.MINOR.PATCH` 문자열을 사용한다.

- `MAJOR`: 기존 런타임이 안전하게 읽을 수 없는 구조 변경
- `MINOR`: 하위 호환 가능한 필드 또는 노트 종류 추가
- `PATCH`: 의미 변경 없는 문서, 검증, 직렬화 수정

초기 정책:

- 런타임은 지원하지 않는 `MAJOR` 버전을 즉시 거부한다.
- 더 높은 `MINOR` 버전은 알 수 없는 필드가 있어도 핵심 필드가 유효하면 경고 후 로드를 시도할 수 있다.
- 필수 필드를 알 수 없거나 지원하지 않는 노트 타입이 있으면 로드를 거부한다.
- 에디터는 저장 시 자신이 지원하는 최신 버전으로 명시적으로 마이그레이션한다.

## 5. ID 규칙

### 5.1 `chartId`

- 저장소 또는 배포 단위에서 고유해야 한다.
- 권장 형식: `{songId}-{difficultyId}`
- 영문 소문자, 숫자, 하이픈 사용을 권장한다.

예시:

```text
demo-song-normal
```

### 5.2 Note `id`

- 채보 파일 내부에서 고유해야 한다.
- 노트 정렬 순서와 관계없이 유지되어야 한다.
- 에디터에서 노트를 이동해도 가능하면 같은 ID를 유지한다.
- 런타임 판정 결과와 디버그 로그는 이 ID를 기준으로 연결한다.

권장 형식:

```text
n000001
n000002
```

## 6. 난이도

```json
{
  "id": "normal",
  "name": "Normal",
  "level": 5
}
```

| 필드 | 타입 | 필수 | 규칙 |
|---|---|:---:|---|
| `id` | string | O | 시스템 식별자 |
| `name` | string | O | 화면 표시 이름 |
| `level` | number | O | 난이도 수치. 최종 범위는 미정 |

## 7. 시간과 오프셋

### 7.1 `timeMs`

- 정수만 허용한다.
- 음수를 허용하지 않는다.
- 오디오 파일의 샘플 0부터 계산한 시각이다.
- Chart Offset을 적용하기 전의 원본 노트 시각이다.

최종 목표 시각:

```text
judgementTargetMs = note.timeMs + chartOffsetMs
```

### 7.2 `chartOffsetMs`

- 정수 밀리초다.
- 양수는 모든 노트 판정을 늦춘다.
- 음수는 모든 노트 판정을 앞당긴다.
- 사용자 장치 보정값은 여기에 포함하지 않는다.

### 7.3 정밀도

- 에디터 내부에서 beat/tick 또는 더 높은 정밀도를 사용해도 된다.
- JSON으로 내보낼 때 가장 가까운 정수 밀리초로 반올림한다.
- 반올림으로 인한 오차는 최대 `0.5ms`다.
- 같은 위치의 노트는 반올림 후 같은 `timeMs`를 가질 수 있다.

## 8. Timing 메타데이터

```json
{
  "baseBpm": 120.0,
  "bpmChanges": [
    {
      "timeMs": 0,
      "bpm": 120.0
    }
  ],
  "timeSignatures": [
    {
      "timeMs": 0,
      "numerator": 4,
      "denominator": 4
    }
  ]
}
```

### 8.1 `baseBpm`

- `0`보다 큰 유한한 숫자여야 한다.
- 첫 BPM 이벤트와 같은 값을 권장한다.

### 8.2 `bpmChanges`

| 필드 | 타입 | 규칙 |
|---|---|---|
| `timeMs` | integer | `0` 이상 |
| `bpm` | number | `0`보다 큰 유한값 |

규칙:

- `timeMs` 오름차순으로 저장한다.
- 같은 `timeMs`에 BPM 이벤트를 두 개 둘 수 없다.
- 첫 이벤트는 `timeMs = 0`이어야 한다.
- BPM 정보가 잘못되어도 이미 저장된 노트 `timeMs`가 자동으로 달라지면 안 된다.

### 8.3 `lineSpeedChanges`

| 필드 | 타입 | 규칙 |
|---|---|---|
| `timeMs` | integer | `0` 이상 |
| `multiplier` | number | `0`보다 큰 유한값 |

- `multiplier = 1`이 기본 표시 속도다.
- Line Speed는 `FloorPosition`만 변경하고 노트 `timeMs`와 판정 윈도우에는
  관여하지 않는다.
- 같은 `timeMs`에 Line Speed 이벤트를 두 개 둘 수 없다.
- 정지(`0`)와 역주행(음수)은 현재 형식에서 허용하지 않는다.

### 8.4 `timeSignatures`

| 필드 | 타입 | 규칙 |
|---|---|---|
| `timeMs` | integer | `0` 이상 |
| `numerator` | integer | `1` 이상 |
| `denominator` | integer | `1`, `2`, `4`, `8`, `16` 중 하나 |

박자표는 에디터 그리드와 마디 표시를 위한 정보다.

## 9. 노트 공통 필드

| 필드 | 타입 | 필수 | 의미 |
|---|---|:---:|---|
| `id` | string | O | 채보 내부 고유 ID |
| `type` | string | O | `tap` 또는 `hold` |
| `lane` | integer | O | `0`~`9` |
| `timeMs` | integer | O | 노트 시작 시각 |

공통 규칙:

- `lane < 0` 또는 `lane >= laneCount`인 노트는 오류다.
- `timeMs < 0`인 노트는 오류다.
- 같은 레인과 같은 시각에 동일 종류 노트를 여러 개 둘 수 없다.
- 서로 다른 레인은 같은 시각을 사용할 수 있다.
- 노트 배열은 `(timeMs, lane, id)` 순으로 저장하는 것을 권장한다.
- 런타임은 파일 순서를 신뢰하지 않고 로드 후 정렬한다.

## 10. Tap

```json
{
  "id": "n000001",
  "type": "tap",
  "lane": 2,
  "timeMs": 1500
}
```

규칙:

- `durationMs`를 가지지 않는다.
- `KeyDown` 이벤트 하나로 판정한다.

## 11. Hold

```json
{
  "id": "n000002",
  "type": "hold",
  "lane": 4,
  "timeMs": 2000,
  "durationMs": 1000
}
```

| 필드 | 타입 | 필수 | 의미 |
|---|---|:---:|---|
| `durationMs` | integer | O | Hold 유지 시간 |

규칙:

- `durationMs > 0`이어야 한다.
- 종료 시각은 다음과 같다.

```text
endTimeMs = timeMs + durationMs
```

- 같은 레인에서 Hold 구간과 다른 노트가 겹치는 배치는 첫 데모에서 금지한다.
- Hold 종료점의 별도 ID는 만들지 않는다.
- Hold 중간 tick은 현재 포맷에 저장하지 않는다.

## 12. Preview

```json
{
  "startMs": 30000,
  "durationMs": 15000
}
```

| 필드 | 타입 | 규칙 |
|---|---|---|
| `startMs` | integer | `0` 이상 |
| `durationMs` | integer | `0`보다 큼 |

- 오디오 길이를 넘어가면 런타임에서 가능한 구간으로 제한하거나 오류를 표시한다.
- 데모에서 곡 선택 미리듣기를 구현하지 않으면 이 필드는 무시할 수 있다.

## 13. 유효성 검사

### 오류: 로드 거부

- JSON 파싱 실패
- 지원하지 않는 `formatVersion`의 Major 버전
- 필수 필드 누락
- `laneCount != 10`
- 중복 `chartId` 또는 노트 ID
- 지원하지 않는 노트 타입
- 레인 범위 초과
- 음수 `timeMs`
- Hold의 `durationMs <= 0`
- 같은 레인에서 금지된 노트 구간 중첩
- 유한하지 않은 BPM 또는 난이도 값

### 경고: 로드 가능

- 노트 배열이 정렬되지 않음
- `baseBpm`과 첫 BPM 이벤트 값이 다름
- 알 수 있지만 무시 가능한 선택 필드 존재
- Preview 구간이 오디오 길이를 벗어남
- 노트가 오디오 길이보다 뒤에 있음

### 자동 수정 가능

에디터에서는 사용자 확인 후 다음 항목을 자동 수정할 수 있다.

- 노트 정렬
- 누락된 노트 ID 생성
- Preview 구간 제한
- `baseBpm`과 첫 BPM 이벤트 동기화

런타임은 원본 파일을 자동으로 덮어쓰지 않는다.

## 14. 직렬화 규칙

- UTF-8, BOM 없음 권장
- 들여쓰기 2칸
- 개행은 저장소 규칙에 맞추되 LF 권장
- 필드 순서는 문서 예시 순서를 권장
- 시간값은 정수로 저장
- BPM과 난이도 수치는 JSON number로 저장
- `null` 대신 선택 필드를 생략하는 것을 권장
- 노트 배열은 `(timeMs, lane, id)` 오름차순

## 15. 런타임 로드 결과

JSON을 직접 판정 시스템에 넘기지 않고 다음 단계로 변환한다.

```text
JSON Text
  -> Chart DTO
  -> Validation
  -> Normalization / Sorting
  -> Immutable RuntimeChart
  -> Judgement System
```

런타임 데이터는 최소한 다음 값을 미리 계산한다.

- 노트 시작 시간 초 단위 `double`
- Hold 종료 시간 초 단위 `double`
- 레인별 노트 배열
- 전체 노트 수
- 판정 가능한 마지막 시각

## 16. 호환성 원칙

- 에디터와 게임은 가능한 한 같은 DTO 또는 Schema 패키지를 공유한다.
- 공유가 어렵다면 동일한 JSON Schema와 테스트 채보 파일을 양쪽 저장소에서 사용한다.
- 에디터에서 저장한 샘플 채보를 게임 CI에서 로드하는 통합 테스트를 둔다.
- 게임에서 읽지 못하는 채보를 에디터가 저장해서는 안 된다.

## 17. 미정 사항

- 곡 메타데이터를 채보 파일과 분리할지 여부
- `audioFile`을 상대 경로, Addressables 키, GUID 중 무엇으로 저장할지
- 난이도 level의 최종 범위
- Hold 중첩 허용 여부
- 추가 노트 타입
- BPM 이벤트의 에디터 내부 tick 표현
- JSON Schema 파일의 위치와 자동 생성 방식
