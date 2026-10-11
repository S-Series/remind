# Chart Format

> 현행 계약 검토: 2026-10-08. 시작 전 Step을 포함한 구현·고정 검증본 검사/빌드를 완료했다. 사용자 후속 씬 편집과 수동 검수는 별도다.
> 제작 원본: `REmindChart` JSON `formatVersion: 1`, 중앙 노트 포함 시 `2` (`.rd`).
> Game 전달 파일: Runtime Package `Version: 1`, 중앙 노트 포함 시 `2` (`.rmp.json`).

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
  "level": 11,
  "author": "Unknown",
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
| `musicId` | 소속 곡 ID. 난이도 ID는 `.rd` 파일명에서 결정 |
| `level`, `author` | 채보별 숫자 레벨과 제작자. 테스트 채보의 `level: 0`은 미정 표시 |
| `extraName` | `Extra.rd`에서만 쓰는 필수 표시명. 기록 ID는 항상 `extra` |
| `jacketFile` | 선택적 난이도 재킷 파일명. 경로 규칙은 [MusicContent.md](MusicContent.md) |
| `gimmickId` | 곡별 C# MusicGimmick 등록 ID, 없으면 빈 문자열 |
| `baseBpm` | 0보다 큰 유한한 시작 BPM |
| `musicStartCorrectionMs` | 유한한 밀리초 보정값. 부호는 [RhythmSystem.md](RhythmSystem.md)의 보정값 계약 참조 |
| `revision` | 저장된 편집 문서 식별값 |
| `notes` | 위치순 행 문자열 배열. 노트가 없어도 배열 자체는 필요 |
| `eventDictionary` | Effect 정의와 파라미터 배열. Effect가 없으면 빈 배열 |

새 `.rd`에는 `difficultyId`를 기록하지 않는다. 기존 파일의 `difficultyId`는
이행을 위해 읽을 수 있지만, 곡 폴더에서는 파일명과 다른 값을 거부한다.

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

- `musicId`: 채보가 속한 곡의 ID. 난이도 ID는 파일명에서 결정한다.
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

### 18.3 중앙 Tap / Knob 노드 버전 2

기존 v1의 좌우 Scratch/Long Scratch, 레인 `0`~`9`, 기존 종류 값 `0`~`4`는 유지한다.
`CenterTap`(종류 값 `5`)과 `Knob`(종류 값 `6`)만 중앙 레인 `10`을 사용한다.
새 중앙 노트가 없는 채보는 계속 v1으로 저장한다. v2는 기존 `notes`의 9열과
`eventDictionary`를 유지하고 최상위 `centerNotes` 배열을 추가한다.

```json
{
  "formatVersion": 2,
  "centerNotes": [
    { "position": 0, "kind": "tap", "x": 0 },
    { "position": 4800, "kind": "knobStart", "x": -0.5, "curve": "sin" },
    { "position": 7200, "kind": "knobMiddle", "x": 0.5, "curve": "cos" },
    { "position": 7200, "kind": "knobMiddle", "x": -0.5, "second": true, "curve": "linear" },
    { "position": 9600, "kind": "knobMiddle", "x": 0.5, "curve": "linear" }
  ]
}
```

- `position`은 `measure * 4800 + position`의 절대 정수 위치이며 채보 범위 안이어야 한다.
  Knob의 `x`는 유한한 `[-1, 1]` 값이다. Tap은 중앙 고정이며 `x`를 판정에 사용하지 않는다.
- ChartMaker의 Knob 노드는 **Start / Middle** 두 종류다. 새 저장에는 `knobStart`,
  `knobMiddle`만 기록하며 End는 기록하지 않는다. Start는 이전 노드와 연결하지 않는다.
  Middle은 직전 노드와 연결하고, 해당 노드 이전에 Start가 없으면 선을 그리지 않는다.
  연결되지 않은 Middle과 단독 Start도 저장하며, Preview에는 아이콘만 표시한다.
- Start부터 이후 Middle까지 한 경로다. 다음 Start가 나오거나 노드 목록이 끝나면
  마지막 Middle에서 경로가 끝난다. adapter가 실행용 Start/Mid/End를 만들며, 연결되지
  않은 노드에는 Gameplay 판정 대상을 생성하지 않는다. UI가 종료 규칙을 다시 구현하지 않는다.
- 노드의 `curve`는 **그 노드에서 다음 Middle로 나가는 구간**의 `linear`, `sin`, `cos`다.
  생략하면 `linear`다. 목적지에 incoming 구간을 저장하는 이전 방식과 책임이 다르다.
  Linear는 직선, Sin은 `sin(t * π/2)`, Cos는 `1 - cos(t * π/2)`의 ¼주기다.
  `t`는 두 노드 사이의 작성 position 비율이다. Sin은 처음 빠르고 끝에서 완만하며,
  Cos는 처음 완만하고 끝에서 빠르다. X 방향이 반대여도 같은 진행 비율을 사용한다.
- 곡선은 공용 `KnobPathEvaluator`의 64개 고정 구간으로 선형 근사한다. 편집 Ribbon,
  Game/Preview 표시와 판정은 같은 표본을 사용한다. BPM·Scroll 변경 위치도 표시 정점에
  포함하며, 구간을 나눠도 원래 곡선의 진행 비율을 유지한다.
- 같은 `position`에는 최대 2개를 둔다. 두 번째 항목은 `second: true`로 저장하며,
  First와 다른 X를 가져야 한다. First → Second 순서가 같은 시각의 가로 이동 순서다.
  이 구간은 노드의 곡선 종류와 무관하게 실행용 Step으로 처리하고 기존 ±80ms 방향 Down
  판정을 사용한다. Second의 `curve`는 그 이후 구간에 적용된다. Second를 Start로 바꾸면
  First와 연결하지 않고 새 경로를 시작한다. 중복 Tap, First 없는 Second, 같은 X의 두 지점,
  세 번째 지점은 거부한다.
- 이전 v2의 `knobMid` / `knobEnd`는 읽을 때 Middle로 변환한다. 기존 incoming
  `interpolation: "step"`은 호환용으로 보존한다. 이전 노드의 곡선을 직접 변경하면 다음 Middle의
  호환용 incoming Step을 해제한다. 같은 줄 Second Point의 Step은 유지한다. 새 편집의 곡선 종류에는 Step을 제공하지
  않으며 같은 줄의 가로 이동은 Second Point로 작성한다. 공용 enum은
  `Linear=0`, `Step=1`, `Sin=2`, `Cos=3`이며 Runtime Package의 incoming 구간 값으로 투영한다.
  Step은 기존 v2 및 Second Point의 실행 의미로 유지한다.

ChartMaker의 기존 Eraser 위치에 있는 **Knob** 버튼으로 배치를 시작한다.
첫 Start를 배치하면 이후 배치 종류는 Middle로 전환된다. **Tab**은 Linear → Sin → Cos → Linear,
**Shift+Tab**은 Start ↔ Middle로 전환한다. 선택한 Knob 아이콘이 있으면
그 노드를 수정하고, 없으면 현재 Knob 배치 모드를 바꾼다. 텍스트 입력·팝업·Preview 재생
중에는 기존 입력 차단을 따른다. End 배치와 `Finish Knob Path`는 제공하지 않는다.
Edit 메뉴의 Start/Middle, Linear/Sin/Cos 선택과 중간 노드 삽입, X 스냅은 유지한다.
배치 중 Middle은 이전 노드에서 마우스까지 앞 노드의 곡선으로 연결을 미리 표시한다.

편집 노드는 `Assets/Prefabs/Notes/Preview/KnobPoint.prefab`의 작은 1:1 Sprite 아이콘이다.
SpriteRenderer의 Sprite를 교체하며 현재 이미지는 임시 원형 표시다. 배치·선택·이동·삭제의
단위는 이 아이콘이고 Knob 경로는 저장된 노드를 연결하는 표시다. 같은 줄에 추가하면
Second Point가 되며, 이후 경로는 Second에서 이어진다. First를 삭제하면 남은 Second의
Start/Middle 종류와 곡선은 유지한다. 아이콘 드래그 중에도 선을 갱신하며 저장 좌표는
이동 확정 시에만 바꾸고 취소 시 복원한다. 시간 이동도 First → Second 순서를 유지한다.

Game과 Maker에는 별도의 이동 커서나 방향 화살표가 없다. `knobPathMaterial`을 지정하면
공용 RibbonMesh가 그 Material을 공유하며 텍스처·색상을 덮어쓰지 않는다. 미지정 시 임시
흰색 경로를 표시하며 코드에서 Tint를 지정하지 않는다. 저장·출력·Preview 검사의 범위와
수동 검수는 [TASKS.md](TASKS.md)에 둔다.

Game 중앙 Tap은 논리 좌/우 Shift 입력을 받는다. Space Tap 설정이 ON이면 Space도
추가로 받지만 노브 위치 조작은 계속 논리 좌/우 입력만 사용한다. Space Tap은 기본 OFF이며
Home→Settings→Controls에서 변경하고 다음 플레이부터 적용한다.

위치 추적 Knob의 확정 규칙은 다음과 같다. 기존 Tap의 네 등급 판정 창과 Long Scratch의
End release 규칙은 변경하지 않는다.

- 각 Knob 시작 시 플레이어 위치를 그 Start의 `x`로 지정한다. 단, 아래의 유효한 시작 전
  Step 입력이 있으면 이미 이동한 위치를 유지하고 시작값으로 되돌리지 않는다. 이후 입력 또는
  다음 Knob 시작이 없으면 마지막 위치를 유지한다. 시작 새 Down/종료의 정확한 Up은 요구하지 않는다.
- 수동 이동은 논리 단위로 고정 `5 units/s`, 범위는 `[-2.5, 2.5]`다.
  공간 판정과 사선 추적 보정 범위는 `±0.25`이며 유지 체크 간격은 `100ms`다.
- 사선은 범위 안에서 올바른 방향 키를 유지하면 경로를 따라간다. 해제하면 위치를 유지하고,
  양키를 함께 유지하면 수동 이동·사선 추적은 중립이다. 놓친 뒤 위치를 맞추면 복귀할 수 있다.
- 목표 X가 일정한 세로 구간에서 정상 판정 중이면 어느 키를 눌러도 이탈하지 않는다.
  무입력 위치 0과 목표 0이 일치하는 경우도 정상 판정이다.
- 정상 시작·위치 추적·세로·사선 판정은 `Perfect+`, 실패는 `Miss`다.
  이전 겹침/교대 `150ms`·짧은 해제 `50ms` 유예는 이 모델에 재사용하지 않는다.
- Step 목적지 시각의 `±80ms` 안에서 올바른 방향의 새 Down을 받으면 즉시 목적지 X로
  이동한다. 이 입력은 중앙 Tap보다 먼저 소비하며, 소비하지 않은 Shift Down은 Tap에 쓸 수 있다.
  `±80ms`는 Knob 시작 전에도 양쪽 경계를 포함해 허용한다. 시작 100ms·Step 150ms의
  Down 70ms는 70ms에 목적지로 이동하고 100ms에도 그 위치를 유지한다.
- 조기 Step 입력은 직전 구간의 남은 판정만 `Perfect+`로 예약하고 각각의 원래 timestamp에
  확정한다. 시작 전 입력이면 아직 확정하지 않은 해당 이전 구간의 Start/tick도 포함한다.
  과거 Miss를 되돌리거나 다음 구간의 미래 판정을 일괄 보상하지 않는다.
  늦은 입력은 해당 입력 시각 이후 정렬만 바꾸며, Step Down 자체로 점수를 즉시 추가하지 않는다.
  예약은 해당 Knob의 직전 구간에만 적용된다. 시작 전 snap으로 진행 중인 다른 Knob에서
  위치가 벗어나면 그 Knob은 Miss가 될 수 있으며 다른 Knob까지 성공으로 보정하지 않는다.

`PlayableJudgementSession.PlayerKnobX`는 실제 플레이어 논리 위치다. `PlayerKnobNoteId`는
유효한 시작 전 입력을 인정한 시점부터 해당 Knob을 가리키고 Reset 시 null로 돌아간다.
Game adapter도 이 상태를 노출하지만 별도 cursor는 생성하지 않는다. 기존 cursor 연결
검사는 이전 표시 구조의 기록이며, 화면·물리 입력·청음·최종 밸런스 검수는 별도다.

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

중앙 노트가 없는 Package는 `Version: 1`, CenterTap/Knob을 포함하면 `Version: 2`를
사용한다. `.rd`의 `formatVersion`과는 별개의 계약이며 필드도
`MusicId`, `DifficultyId`, `ChartOffsetMs`, `Notes`, `Effects`,
`Parameters` 등 PascalCase다. 노트 위치·Timing·Line Speed·Camera·Scratch Tilt와
Effect 정의/파라미터를 포함하며, Game 전용 UI나 사용자 진행 데이터를 포함하지 않는다.

v2의 Knob 지점에는 `KnobX`, `KnobDirection`과 선택적 `KnobInterpolation`을 포함한다.
`KnobInterpolation`은 목적지 incoming 구간의 정수 enum이며 생략하면 `Linear=0`이다.
Import 후 공용 컴파일러가 경로를 다시 검증한다. v1 Package에는 중앙 레인/새 종류와 Knob 지점
필드를 허용하지 않는다. 기존 v1 파일을 일괄 재작성하거나 좌우 채보를 중앙으로 바꾸지 않는다.

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
