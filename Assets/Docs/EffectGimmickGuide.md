# Effect / MusicGimmick 사용 및 확장 가이드

> 적용 범위: 현재 ChartMaker Preview와 `DemoPlay` 통합 하네스  
> 데이터 형식: ChartMaker Editor JSON 형식 버전 1 (`.rd` 단일 파일)
> 최종 갱신: 2026-09-09

이 문서는 Effect를 배치하고 안전하게 저장하는 방법, Preview와 실행 시의 공통
규칙, 새 Effect 또는 곡 전용 MusicGimmick을 추가하는 방법을 설명한다.
`DemoPlay`는 최종 ReMind Game 씬이 아니라 공용 실행 계약을 기존 판정·규칙·카메라에
연결해 보는 과도기 통합 하네스다.

## 1. 책임과 데이터 원본

| 정보 | 원본 |
| --- | --- |
| Effect 시각, 동일 시각 순서, 종류, 명령, `effectId` | `.rd`의 Effect 정의 |
| 지속 시간, 위치, 강도, 조건 기준값 | `.rd`의 `eventDictionary[].parameters` |
| 조건, 분기, 상태 변화, 실제 명령 구현 | C# Effect / MusicGimmick |
| 실행 중 상태와 Unity 오브젝트 | 재생 세션; 파일이나 Snapshot에 저장하지 않음 |

Effect 행과 정의는 위치로, 조정값은 같은 `eventDictionary` 항목에 저장한다.
저장 전에 전체 채보와 등록된 파라미터를 검증하고 임시 파일을 거쳐 `.rd`를 교체한다.
복구용 `.rd.bak`이 생길 수 있다. 디스크에 마지막으로 저장된 revision은
Undo/Redo되는 편집 이력과 별도로 관리한다.

## 2. ChartMaker 작업 순서

1. Specials 도구에서 Effect를 선택해 시각 위치에 배치한다.
2. 생성된 Effect가 자동 선택되면 `Effect Type`을 지정한다.
3. `music.call`이면 현재 채보의 `gimmickId`에 등록된 `Command`도 지정한다.
4. 표시된 JSON 수치를 수정하고 `Apply`한다.
5. `.rd`로 저장한다. Effect 파라미터도 같은 파일에 저장된다.
6. 다시 열어 Effect 선택과 수치가 유지되는지 확인하고 Preview를 시작한다.

종류가 비어 있거나 현재 등록되지 않은 기존 Effect도 열기·편집·보존·재저장은 할 수
있다. 다만 의미를 추정하지 않으며, 등록된 종류와 명령을 지정하기 전에는 Preview나
Gameplay 준비를 허용하지 않는다. 이 경우 ChartMaker는 Console 오류로 중단하는 대신
해당 Effect를 선택하고 설정 안내를 표시한다.

Preview 중에는 문서 변경, 저장/열기, 음악 교체를 막는다. 외부에서 `.rd`를 수정한
경우 실행 중 세션에 조용히 반영하지 않고 명시적으로 다시 열거나 다시 불러온다.

## 3. `.rd`의 Effect 파라미터 형식

```json
{
  "format": "REmindChart",
  "formatVersion": 1,
  "musicId": "effect_gameplay_sample",
  "difficultyId": "demo",
  "baseBpm": 120,
  "musicStartCorrectionMs": 0,
  "revision": "stage6_demo_004",
  "notes": ["000|0000|--------|--------|00000000|-1|-|T|-"],
  "eventDictionary": [
    {
      "position": 0,
      "effectId": "fx_stage6_camera",
      "effectTypeId": "camera.offset",
      "commandId": "",
      "order": 0,
      "parameters": {
        "durationMs": 2000,
        "offsetX": 2.5,
        "offsetY": 0.75,
        "rollDegrees": 12,
        "attackMs": 500,
        "releaseMs": 500
      }
    }
  ]
}
```

중복 JSON key, 잘못된 파라미터 타입, NaN/Infinity는 준비 단계에서 거부한다.
설정이 필요 없는 명령은 `parameters`를 생략할 수 있다.

### `camera.offset`

| 필드 | 필수 | 기본값 | 단위 및 범위 |
| --- | --- | --- | --- |
| `durationMs` | 예 | 없음 | ms, finite, `0` 이상 |
| `offsetX` | 아니요 | `0` | 로컬 위치, finite, `-10000`~`10000` |
| `offsetY` | 아니요 | `0` | 로컬 위치, finite, `-10000`~`10000` |
| `rollDegrees` | 아니요 | `0` | degree, finite, `-36000`~`36000` |
| `attackMs` | 아니요 | 기존 파일 `0`, 새 설정 `100` | ms, finite, `0` 이상 |
| `releaseMs` | 아니요 | 기존 파일 `0`, 새 설정 `100` | ms, finite, `0` 이상 |

`attackMs + releaseMs`는 `durationMs` 이하여야 한다. 기존 파라미터에 두 보간 필드가
없으면 이전 동작을 보존하기 위해 둘 다 `0`으로 읽는다. ChartMaker에서 새로 만드는
Camera 설정은 각각 `100ms`를 기본으로 넣는다.

카메라 출력은 절대 chart time으로 평가한 smooth-step Attack → Hold → Release
envelope를 사용한다. 프레임이 늦어져도 그 프레임의 실제 경과 위치를 계산하므로 처음부터
다시 보간하지 않는다. 겹치는 Camera Effect는 세션 소유 offset을 더하며, 종료·재시작·
예외 시 자기 offset만 제거한다. 이 수학과 합성 수명은 Shared이고, ChartMaker와 Game은
계산된 X/Y/roll을 각자의 Transform 계층에 적용한다.

### 개발용 `sample` MusicGimmick

`sample`은 구조 검증용이며 실제 곡 규칙이나 영구 진행을 정의하지 않는다.

| 명령 | 설정 | 의미 |
| --- | --- | --- |
| `begin-section` | `minimumHealth` 0~100, `damageMultiplier` 0~100 | 해당 시각 체력이 기준 이상이면 구간과 피해 배율을 시작 |
| `count-success` | 없음 | 활성 구간의 성공 수를 1 증가 |
| `end-section` | 없음 | 성공 여부를 확정하고 규칙 구간을 해당 시각에 종료 |
| `request-transition` | 비어 있지 않은 `targetMusicId`, `targetDifficultyId` | 성공한 경우 준비된 전환 대상을 요청 |

한 세션의 모든 `music.call`은 같은 MusicGimmick 인스턴스를 공유한다. 명령 호출이
끝났다고 활성 구간 상태가 사라지지 않으며, 새 Play/Restart에는 새 인스턴스를 쓴다.

## 4. 준비와 실행 계약

준비 단계에서만 파일을 읽고 다음을 모두 검사한다.

- `.rd`의 곡·난이도 정보와 revision
- Effect 행과 정의의 1:1 대응, 고유 `effectId`, 동일 시각의 고유 order
- 등록된 Effect 종류, MusicGimmick과 명령
- JSON 구조·타입·필수 값·범위
- 실행에 필요한 카메라, 게임 상태, 규칙, 전환 capability

성공하면 불변 `PlayableChartSnapshot`과 `PreparedEffectPlan`을 만든다. 실행 루프에는
ChartMaker의 holder나 JSON 문자열을 넘기지 않고 준비된 계획만 전달한다.

실행 순서는 다음과 같다.

1. 도달한 Effect를 예약 시각과 명시 order 순으로 한 번씩 시작한다.
2. 해당 시각까지 도착한 입력을 도착 sequence 순으로 판정한다.
3. 자동 판정/Miss를 처리한다. 정확한 마감 시각은 기존 판정 규칙처럼 입력이 먼저다.
4. 활성 Effect와 MusicGimmick을 현재 프레임 chart time으로 한 번 갱신한다.
5. 프레임이 끝난 뒤 승인된 곡 전환 요청을 앱의 안전한 경계에서 처리한다.

예약 시각과 늦게 처리한 현재 시각은 구분한다. 시각 T에 시작/종료된 규칙은 T 이전
입력에 소급 적용하지 않는다. 일시정지는 음악과 같은 공용 곡 시간을 멈추고, Resume은
현재 세션을 유지한다. Restart는 runner, gimmick, rule handle, camera offset,
cancellation과 전환 mailbox를 모두 새 세대로 교체한다.

실행 예외가 난 항목은 이미 소비된 것으로 보며 부작용 명령을 자동 재시도하지 않는다.
실패 또는 취소 시에도 소유한 자원을 모두 정리하고, 실행 실패와 정리 실패가 함께 나면
둘 다 보존해 보고한다.

## 5. Preview와 DemoPlay의 범위

ChartMaker Preview는 같은 Snapshot, Effect 계획, 시간 계산, 카메라 envelope와 세션
수명 규칙을 사용한다. 다만 Preview의 게임 상태는 명시적인 테스트 값이며 규칙 서비스는
handle 수명만 재현한다. 계정, 해금, 영구 진행, 실제 씬 전환을 변경하지 않는다.
상태 이력을 복원할 수 없는 MusicGimmick이 있으면 중간 시각 시작을 거부한다.

`DemoPlay`는 공용 계획을 기존 `NoteJudgementSystem`, 실제 `GameRule`,
`GameplaySessionState`, 카메라 pivot에 연결한 통합 하네스다. 현재 확인할 수 있는 것은
이 연결의 시간·수명·서비스 계약이다. 최종 Game의 곡 선택, 로딩, Player 진입,
씬 전환, 콘텐츠 카탈로그는 아직 구현 대상이 정해지지 않았으며 이 하네스를 최종
Gameplay로 간주하지 않는다.

공용 카메라·배치·판정의 원칙은 다음과 같다.

- chart time/position 변환, 카메라 timeline/easing, Effect 실행 의미는 Shared에 둔다.
- 판정 결과가 같아야 하는 규칙과 시간 경계도 Shared 후보로 분류한다.
- ChartMaker의 편집 UI/Preview 표시와 Game의 실제 입력/Transform 표시는 앱별 adapter다.
- 기존 Long/Scratch 판정을 이 Effect 작업에서 새로 만들거나 바꾸지 않는다.

## 6. 확장 방법

### 새 공용 Effect

1. 순수 parameter 타입과 `Effect` 구현을 Shared 영역에 추가한다.
2. 안정적인 `typeId`, 기본값, 의미 검증, 필요한 capability, seek 지원 여부를
   `EffectRegistration`에 명시한다.
   내장 타입 외의 parameter 객체를 쓰면 `copyParameters`를 등록해 원본과 각
   실행 세션에 서로 다른 독립 복사본을 제공한다. 복사 함수가 없으면 준비를 거부한다.
3. 현재 authoring adapter인 `ChartEffectJsonCodec`에 동일 필드의 decoder와 새 설정의
   기본 JSON을 추가한다.
4. 절대 chart time, 지연 프레임, 종료/취소 정리, 겹침을 Core 테스트로 고정한다.
5. ChartMaker Preview와 Game presenter는 필요한 표시 변환만 각각 연결한다.

### 새 곡 MusicGimmick 또는 명령

1. 세션 상태를 인스턴스 필드에 두는 `MusicGimmick` 구현을 만든다.
2. 파일에 저장할 안정적인 `gimmickId`와 `commandId`를 등록한다. C# 메서드명을
   문자열 reflection으로 호출하지 않는다.
3. 설정이 있는 명령만 parameter 타입, 기본값, 검증, decoder를 등록한다.
   사용자 정의 parameter 타입에는 명령 등록의 `copyParameters`도 제공한다.
4. 필요한 서비스만 `EffectSessionContext`의 제한된 capability로 요구한다.
5. ChartMaker에서 선택 가능한지, 한 세션에서 상태를 공유하는지, Restart/예외 때
   정리되는지 검사한다.

현재 registry와 JSON decoder 등록은 한 곳으로 완전히 통합되지 않은 과도기 경계다.
새 확장에서 중복 등록이 필요하면 두 등록이 같은 parameter 타입과 검증을 사용하도록
테스트하고, 장기적으로는 Shared schema/codec 경계로 이동한다.

## 7. 번들 샘플과 확인 방법

- 채보와 Effect 설정: `Assets/Tests/Fixtures/EffectGameplaySample.rd`
- 실행 패키지: `Assets/Tests/Fixtures/rmp/EffectGameplaySample.rmp.json`
- revision: `stage6_demo_004`
- 모든 노트와 Effect는 최초 로딩과 겹치지 않게 1000ms 이후에 있다.
- Camera Effect는 약 1.733초에 시작해 500ms 동안 진입하고, 약 1초 유지한 뒤
  500ms 동안 복귀한다.

자동 회귀는 Unity가 닫힌 상태에서 프로젝트 루트의 다음 스크립트로 실행한다.

```powershell
.\Tools\Run-EffectBaseline.ps1
```

수동 확인은 `Assets/Scenes/DemoPlay.unity`에서 Auto가 켜진 상태로 Play한다. 초기
1초가 비어 있는지, Camera가 순간이동하지 않고 부드럽게 이동·회전·복귀하는지,
Pause/Resume에서 위치가 튀지 않는지, Reset 후 임시 카메라와 규칙이 남지 않는지,
Console 오류가 없는지 확인한다. Auto는 물리 입력 대신 샘플 Tap을 자동 판정해 Effect
연결을 관찰하기 위한 하네스 옵션이지 자동 테스트 전체를 뜻하지 않는다.

## 8. 문제 해결

- `Temp chart TextAsset is not assigned`: 현재 `DemoPlay`에는 구형 `TempLoader`가 없어야
  한다. 씬에 남은 legacy `Chart Provider`를 제거하고 새 session controller 참조를 쓴다.
- `duplicate ID, invalid order, or no matching Effect row`: `.rd`의 Effect 행과
  `eventDictionary` 위치/ID/order가 1:1인지 확인한다.
- 저장된 파일이 손상된 경우: `.rd.bak`을 확인하고 ChartMaker에서 다시 저장한다.
- 타입 미지정 안내: 데이터를 지우지 말고 해당 Effect의 등록된 Type과 필요한 Command를
  선택한 뒤 Apply한다.
- 카메라가 움직이지 않음: 준비 오류로 Play가 멈추지 않았는지, Effect camera pivot과
  presenter가 연결됐는지, `durationMs`와 offset/roll이 0이 아닌지 확인한다.
- 카메라가 순간이동함: 기존 파일은 호환을 위해 attack/release 기본값이 0이다. 부드러운
  연출이 필요하면 두 값을 명시하고 합이 duration을 넘지 않게 한다.

구조의 장기 경계와 남은 과도기 의존성은 `ARCHITECTURE.md`, `MIGRATION.md`,
현재 진행 상태는 `EffectGimmickWorkLog.md`에서 관리한다.
