# Gameplay Structure

> 문서 상태: Draft 0.1  
> 최종 갱신: 2026-07-21  
> 목적: Scene, Field, Prefab과 핵심 시스템의 책임을 분리하고 데이터 흐름을 고정한다.

## 1. 구조 원칙

- 판정 로직은 화면 오브젝트와 분리한다.
- 노트 Prefab이 존재하지 않아도 판정 시스템은 동작해야 한다.
- 곡 시간은 `RhythmClock`만 제공한다.
- 입력은 `InputRouter`만 수집한다.
- 판정 결과는 이벤트 또는 명시적 결과 데이터로 전달한다.
- Scene 오브젝트끼리 직접 탐색하는 `Find` 의존을 만들지 않는다.
- 한 노트마다 독립적인 `Update()`를 사용하지 않는다.
- 노트 표시는 풀링한다.
- 채보 DTO를 그대로 게임 로직에서 사용하지 않고 검증된 런타임 모델로 변환한다.

## 2. 전체 흐름

```text
Chart JSON
  -> ChartLoader
  -> ChartValidator
  -> RuntimeChart
  -> GameplaySession

Input System Event
  -> InputRouter
  -> InputEventQueue
  -> JudgementSystem
  -> JudgementResult
  -> ScoreSystem / Presentation

AudioSettings.dspTime
  -> RhythmClock
  -> JudgementSystem
  -> NotePresenter
```

## 3. Scene 구성

### 3.1 Bootstrap

책임:

- 게임 전역 서비스 초기화
- 설정 파일 로드
- 입력 설정 로드
- 오디오 설정 로드
- 첫 Scene 이동

포함 대상:

- `AppRoot`
- `SceneFlowService`
- `SettingsService`
- `AudioService`
- `SaveService`

규칙:

- 전역 서비스만 `DontDestroyOnLoad`를 사용한다.
- 게임플레이 세션 데이터는 전역 오브젝트에 남기지 않는다.

### 3.2 MainMenu

책임:

- 데모 시작
- 설정 진입
- 종료

첫 데모에서는 SongSelect와 합쳐도 된다.

### 3.3 SongSelect

책임:

- 곡 목록 표시
- 난이도 선택
- 채보 기본 정보 표시
- Gameplay Scene에 넘길 선택값 생성

전달 데이터 예시:

```text
GameplayRequest
- songId
- chartId
- difficultyId
```

### 3.4 Gameplay

책임:

- 채보 로드
- 세션 생성
- 곡 재생
- 입력과 판정
- 점수 표시
- 일시정지와 재시작
- 종료 조건 감지

Gameplay Scene은 아래 Field와 시스템을 조립하는 Composition Root 역할을 한다.

### 3.5 Result

책임:

- 최종 점수
- 정확도
- 최대 콤보
- 판정별 개수
- Early/Late 분포
- 재시도 또는 SongSelect 복귀

Result Scene에는 판정 로직을 두지 않는다.

## 4. Field 정의

`Field`는 Unity 기본 개념이 아니라, 이 프로젝트에서 **특정 화면 영역과 표시 책임을 묶는 프레젠테이션 단위**를 의미한다.

### 4.1 LaneField

책임:

- 10개 레인의 위치와 크기 관리
- 판정선 표시
- 노트 표시 좌표 계산
- 레인별 키 입력 시각 효과

포함:

- `LaneFieldView`
- `LaneView[10]`
- `JudgementLineView`
- `NotePool`

금지:

- 직접 점수 계산
- 직접 판정 구간 계산
- `Time.deltaTime` 누적으로 곡 시간 계산

### 4.2 GameplayHudField

책임:

- 점수
- 콤보
- 정확도
- 현재 판정
- Early/Late
- 진행도

### 4.3 PauseField

책임:

- 일시정지 메뉴 표시
- 재개, 재시작, 나가기 요청 전달

직접 오디오와 판정 시스템을 조작하지 않고 `GameFlowController`에 요청한다.

### 4.4 DebugField

개발 빌드 전용이다.

표시 후보:

- DSP 시각
- 현재 곡 시각
- 입력 이벤트 시각
- 판정 deltaMs
- 입력 큐 길이
- 활성 노트 수
- 프레임 시간
- 적용 중인 오프셋

## 5. 핵심 시스템

### 5.1 GameFlowController

책임:

- 세션 상태 전환
- 시작, 플레이, 일시정지, 재개, 종료, 재시작
- 포커스 이탈 처리
- Scene 이동 요청

상태 예시:

```text
Loading
Ready
Countdown
Playing
Paused
Finished
Restarting
Exiting
```

상태 전환은 한 곳에서만 수행한다.

### 5.2 GameplaySession

한 번의 플레이에만 존재하는 데이터 컨테이너다.

포함:

- `RuntimeChart`
- `RhythmClock`
- `InputEventQueue`
- 노트 런타임 상태
- 점수 상태
- 현재 세션 설정

재시작 시 기존 Session을 초기화해서 재활용하기보다 새 Session을 생성하는 방향을 우선한다.

### 5.3 RhythmClock

책임:

- 예약된 오디오 시작 DSP 시각 보관
- 현재 곡 시각 제공
- 일시정지 시간 보정
- DSP와 Input Event 시간축 변환 지원

외부 공개값 예시:

```text
CurrentSongTimeSec
CurrentSongTimeMs
ScheduledStartDspTime
IsRunning
```

금지:

- 점수 계산
- 노트 검색
- 화면 오브젝트 이동

### 5.4 AudioPlaybackService

책임:

- AudioClip 로드
- `PlayScheduled` 호출
- Pause, Resume, Stop
- 오디오 장치 변경 대응

`RhythmClock`과 시작 시각을 공유하되, 서로의 내부 상태를 임의로 변경하지 않는다.

### 5.5 InputRouter

책임:

- Unity Input System 이벤트 수집
- 키와 레인 매핑
- Down/Up 구분
- 이벤트 시각과 순번 복사
- 입력 이벤트 큐에 전달

입력 이벤트 데이터 예시:

```text
RhythmInputEvent
- lane
- phase: Down | Up
- eventTime
- sequence
```

금지:

- 직접 노트 판정
- 점수 갱신
- 화면 효과 재생

### 5.6 InputEventQueue

책임:

- 입력 이벤트 시간순 보관
- 같은 시각이면 sequence 순으로 정렬
- 일시정지, 재시작 시 비우기

가능하면 GC 할당이 적은 구조를 사용한다.

### 5.7 ChartLoader

책임:

- JSON 읽기
- DTO 역직렬화
- 오류 메시지 생성

### 5.8 ChartValidator

책임:

- `ChartFormat.md` 규칙 검증
- 오류와 경고 분리
- 노트 ID, 레인, 시간, 중첩 검사

검증 실패 시 Gameplay를 시작하지 않는다.

### 5.9 RuntimeChartBuilder

책임:

- 검증된 DTO 정렬
- 레인별 노트 배열 생성
- 초 단위 `double` 값 사전 계산
- 불변 `RuntimeChart` 생성

### 5.10 JudgementSystem

프로젝트의 핵심 순수 로직이다.

입력:

- 현재 곡 시각
- 시간 변환된 입력 이벤트
- 레인별 미판정 노트
- 판정 설정값
- 오프셋 설정값

출력:

- `JudgementResult`
- 노트 상태 변경
- Hold 시작, 완료, 실패 이벤트
- 자동 Miss 결과

가능한 한 Unity 오브젝트에 의존하지 않는 일반 C# 코드로 작성한다.

결과 데이터 예시:

```text
JudgementResult
- noteId
- lane
- grade
- deltaMs
- timing: Early | Exact | Late
- inputSequence
- judgedSongTimeMs
```

### 5.11 ScoreSystem

책임:

- 판정 결과를 점수로 변환
- 콤보
- 최대 콤보
- 정확도
- 판정별 개수

판정 구간을 다시 계산하지 않고 `JudgementResult`만 사용한다.

### 5.12 NoteScheduler

책임:

- 현재 곡 시각과 표시 시간을 기준으로 화면에 필요한 노트 범위 계산
- 노트 View 생성과 반환 요청

판정과 무관한 표시용 시스템이다.

### 5.13 NotePresenter

책임:

- 노트 위치 갱신
- Tap/Hold 모양 표시
- 판정 완료 노트 숨김
- Object Pool 반환

노트 위치는 다음 개념으로 계산한다.

```text
remainingTime = noteTargetTime - visualSongTime
position = ScrollFunction(remainingTime, noteSpeed)
```

### 5.14 ResultBuilder

책임:

- 세션 종료 시 결과 스냅샷 생성
- Result Scene에 전달 가능한 불변 데이터 생성

## 6. Prefab 구성

권장 초기 Prefab:

```text
Prefabs/
├─ Gameplay/
│  ├─ LaneField.prefab
│  ├─ Lane.prefab
│  ├─ JudgementLine.prefab
│  ├─ TapNote.prefab
│  ├─ HoldNote.prefab
│  ├─ JudgementEffect.prefab
│  └─ LaneInputEffect.prefab
└─ UI/
   ├─ GameplayHud.prefab
   ├─ PausePanel.prefab
   ├─ ResultPanel.prefab
   └─ DebugPanel.prefab
```

Prefab 규칙:

- Tap/Hold Prefab에는 판정 로직을 두지 않는다.
- Prefab은 View와 애니메이션 책임만 가진다.
- 필수 참조는 Inspector 직렬화 또는 초기화 메서드로 명시한다.
- 런타임 중 `Resources.FindObjectsOfTypeAll` 같은 전역 탐색을 사용하지 않는다.
- 노트 Prefab은 Object Pool을 통해 재사용한다.

## 7. Gameplay Scene 계층 예시

```text
GameplayScene
├─ GameplayCompositionRoot
├─ Systems
│  ├─ GameFlowController
│  ├─ AudioPlaybackService
│  ├─ InputRouter
│  └─ GameplayRunner
├─ Fields
│  ├─ LaneField
│  ├─ GameplayHudField
│  ├─ PauseField
│  └─ DebugField
├─ Camera
└─ EventSystem
```

`GameplayCompositionRoot`는 시스템을 생성하고 연결한 뒤 Session을 시작한다.

## 8. 한 프레임 처리 순서

`GameplayRunner`가 명시적으로 다음 순서를 유지한다.

```text
1. InputRouter가 수집한 이벤트 큐 전달
2. 이벤트를 DSP 시간축으로 변환
3. JudgementSystem 입력 판정
4. JudgementSystem 자동 Miss / Hold 갱신
5. ScoreSystem 결과 반영
6. NoteScheduler 가시 범위 계산
7. NotePresenter와 HUD 갱신
8. 종료 조건 확인
```

Unity Script Execution Order에만 의존하지 않고 가능한 한 한 Runner 안에서 순서를 보이게 만든다.

## 9. 데이터 소유권

| 데이터 | 소유자 | 읽는 대상 |
|---|---|---|
| 사용자 키 설정 | SettingsService | InputRouter, 설정 UI |
| User Input Offset | SettingsService | RhythmClock/Judgement 구성 |
| Chart DTO | ChartLoader | ChartValidator |
| RuntimeChart | GameplaySession | JudgementSystem, NoteScheduler |
| 입력 큐 | GameplaySession | JudgementSystem |
| 노트 판정 상태 | JudgementSystem 또는 Session | Presenter, ResultBuilder |
| 점수 상태 | ScoreSystem | HUD, ResultBuilder |
| 현재 곡 시각 | RhythmClock | Judgement, Presenter, Flow |

한 데이터의 쓰기 책임자는 하나만 둔다.

## 10. 폴더 구조 초안

```text
Assets/
├─ ReMind/
│  ├─ Runtime/
│  │  ├─ Audio/
│  │  ├─ Chart/
│  │  ├─ Gameplay/
│  │  │  ├─ Flow/
│  │  │  ├─ Input/
│  │  │  ├─ Judgement/
│  │  │  ├─ Score/
│  │  │  └─ Presentation/
│  │  ├─ Settings/
│  │  └─ Shared/
│  ├─ Tests/
│  │  ├─ EditMode/
│  │  └─ PlayMode/
│  ├─ Prefabs/
│  ├─ Scenes/
│  └─ Data/
└─ StreamingAssets/
   └─ Charts/
```

실제 채보 배포 방식이 Addressables로 결정되면 `StreamingAssets` 사용 여부를 다시 정한다.

## 11. 테스트 경계

### EditMode 테스트

- 판정 경계값
- 입력과 노트 매칭
- 동시 입력
- Hold 상태 전환
- Chart Validation
- Score 계산
- Pause 시간 보정 계산

### PlayMode 테스트

- `PlayScheduled` 시작
- Scene 조립
- Input System 연동
- Prefab Pool
- 포커스 이탈
- 재시작
- Gameplay에서 Result 이동

## 12. 금지할 구조

- 노트 Prefab 각자가 입력을 확인하는 구조
- 노트 Prefab 각자가 판정을 계산하는 구조
- AudioSource의 현재 재생 위치만으로 모든 시간을 계산하는 구조
- `Update()`마다 전체 노트 목록을 처음부터 검색하는 구조
- UI가 직접 점수나 콤보를 변경하는 구조
- 포커스 이벤트가 여러 시스템을 각각 직접 초기화하는 구조
- Scene 전환 후 이전 GameplaySession이 남는 구조

## 13. 미정 사항

- DI Container 사용 여부
- Addressables 도입 시점
- Scene 수를 데모에서 축소할지 여부
- Field의 최종 명명 규칙
- RuntimeChart 공유 패키지 구성
- 에디터 저장소와 Schema 코드를 어떤 방식으로 공유할지
- 렌더링 방식이 uGUI, UI Toolkit, Sprite 기반 중 무엇인지
