# Chart Format

> 문서 상태: Draft 0.1  
> 최종 갱신: 2026-07-21  
> 목적: 에디터와 게임 런타임이 공유하는 채보 JSON 형식과 유효성 검사 규칙을 고정한다.

## 1. 기본 원칙

- 파일 형식은 UTF-8 JSON이다.
- 최상위 `formatVersion`을 반드시 포함한다.
- 레인은 화면 왼쪽부터 `0`~`9`이다.
- 노트의 기준 시간은 정수 밀리초 `timeMs`다.
- `timeMs = 0`은 오디오 파일의 샘플 0이다.
- BPM, beat, tick 정보는 에디터 표시와 재편집을 돕는 메타데이터다.
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

### 8.3 `timeSignatures`

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
