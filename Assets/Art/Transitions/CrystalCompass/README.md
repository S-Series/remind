# ReMind Crystal Transition — Unity 편집용

1920×1080 기준, 60fps / 1.5초. 전달받은 PNG를 사용하는 **실시간 uGUI 연출**이다.
동영상이나 전체 화면 프레임 시퀀스를 재생하지 않는다. 추가 패키지는 필요하지 않다.

## 직접 수정하는 가장 쉬운 방법

1. `CrystalTransition.prefab`을 더블클릭해 연다.
2. 루트 Inspector의 **Show assembled pose for editing (frame 39)**를 눌러 조립된 별을 표시한다.
3. 원하는 레이어 아래 `Artwork`를 선택하고 Inspector에서 위치·크기·회전·색상·이미지를 수정한다.
4. 전체 길이와 별 크기, 궤도/입자 불투명도는 루트 `CrystalTransitionPlayer`에서 조정한다.
5. 개별 등장 시점·가속·이동 경로는 **Window → Animation → Animation**에서 수정한다.
6. 프리팹/클립을 저장하고 Preview 창의 **Reload**로 결과를 확인한다.

기본 Unity Inspector와 Animation 창으로 직접 편집하며, 별도 전용 편집 패널은 없다.
궤도 반지름을 바꾸면 앞뒤와 Glow/Crisp를 동일하게 맞추고, 궤도 위 점의 이동 키프레임도
Animation 창에서 맞춰야 한다.

## 바로 미리보기

- Unity 메뉴 **REmind → Crystal Transition → Preview** (Ctrl+Alt+T).
- `Play`, `Pause`, `Restart`, `Loop`, 시간 슬라이더를 사용한다.
- 별도 preview scene에서 실제 URP로 렌더하며, 열려 있는 Test 씬의 수정사항을 건드리지 않는다.
- Prefab/Clip을 수정하고 저장했다면 **Reload**로 다시 불러온다.
- `Assets/Scenes/prev/CrystalPreview.unity`를 열고 Unity Play를 눌러도 1회 재생된다.
  완료 후 흰 화면을 유지한다. 루트 Inspector의 Play / Restart로 다시 재생한다.
- 오디오 장치와 무관하며 `Time.timeScale = 0`에서도 동작한다.

## 편집 구조

프리팹: `Assets/Prefabs/Transitions/CrystalTransition.prefab`

| 계층 | 수정할 내용 |
|---|---|
| `00_Background` | 어두운 배경색, 전체 화면 입력 차단 |
| `01_Orbit_Back` | 별 뒤의 궤도 세 개와 각 궤도의 선/광원 |
| `02_Star_Size/Animated_Star` | 중심, 세로, 가로, 대각선의 **별도 PNG 4개**, Halo |
| `03_Orbit_Front` | 별 앞의 궤도 세 개와 이동 광원 |
| `04_Particles_And_Warp` | 결정 파편 18개, 별가루 44개, 광선 12개 |
| `05_White_Bloom` | 확장하는 부드러운 광원 |
| `06_White_Cover` | 최상단 완전 불투명 화이트아웃 |

`*_Motion`은 키프레임을 받는다. 그 아래 `Artwork`의 RectTransform, 색상,
RawImage Texture는 별도로 수정할 수 있다. 이 하위 Artwork는 재생 때 덮어쓰지 않는다.
레이어를 끄려면 해당 오브젝트를 비활성화한다. 클립의 경로 바인딩 때문에
**애니메이션을 받는 오브젝트의 이름이나 부모를 바꾸면 클립도 함께 수정해야 한다.**

궤도 `CrystalOrbitGraphic`의 Radius / Line Width / Start Degrees / Sweep Degrees를
수정할 수 있다. 앞뒤 궤도는 동일한 Radius와 회전으로 맞춘다. Crisp Arc와 Glow는 별도다.

루트 `CrystalTransitionPlayer`:

- `Duration`: 전체 재생 시간. 기본 1.5초이며 클립을 비례 리타이밍한다.
- `Star Size`: 별 4개와 별 Halo의 크기.
- `Orbit Opacity`: 앞뒤 궤도와 궤도상의 광원 밝기.
- `Particle Opacity`: 파편·별가루·광선 밝기.

## 키프레임 수정

프리팹을 열거나 씬에 배치하고 루트를 선택한 뒤 **Window → Animation → Animation**을 연다.
처음에는 런타임 대기 상태라 숨겨져 있다. 루트 Inspector의 **Show assembled pose for editing (frame 39)**를
누르면 별이 조립된 상태로 표시되어 Artwork를 배치하거나 Animation 창에서 편집하기 쉽다. Undo로 되돌릴 수 있다.
루트의 비활성 `Animation` 컴포넌트는 클립을 편집 창에 노출하기 위한 것이다.
`CrystalOverlayPreview` 씬에서는 Animation 창의 Preview 재생을 위해 해당 컴포넌트와
루트 CanvasGroup 표시가 씬 인스턴스에만 켜져 있다. 게임에서 재생은
`CrystalTransitionPlayer`가 담당한다.
`Assets/Art/Animations/ReMind_CrystalTransition.anim`의 Transform / CanvasGroup Alpha / Orbit Reveal 곡선을 수정한다.
클립은 60fps 기준 프레임에 맞춘 키프레임이며, 값이 고정된 회전·스케일은
프리팹의 기본값에 두었다. 불필요한 중간 키는 줄였고 남은 커브는 직접 편집할 수 있다.

기본 타이밍:

| 프레임 | 동작 |
|---|---|
| 0–13 | 작은 중심 결정 등장 |
| 10–29 | 세로 → 가로 → 대각선 전개 |
| 18–42 | 앞뒤 궤도 그리기, 궤도상의 광원 이동 |
| 25–51 | 결정 파편과 별가루 등장 |
| 44–75 | 방사형 가속, 광선 확장 |
| 66–79 | 흰 화면으로 덮기 |
| 79–89 | RGB 255 완전 흰 화면 유지 |

## 게임 연결 경계

두 프리팹은 **표시만 담당**한다. Bootstrap의 `SceneTransitionController`가
씬 이동과 중복 전환 방지를 맡는다. 투명 오버레이는 모든 Game 씬 이동의
로드 전후에 이어 재생하고, 흰 결정 연출은 Music Select→Game에서만 사용한다.
일반 이동의 로드 순간은 별도 어두운 덮개가 가린다.
`completed`는 연출 시간 종료 이벤트이며, 씬 로딩 완료 이벤트가 아니다.
두 전환 프리팹과 데모 씬의 `playOnStart`는 기본 false다. 최초에는 숨김 상태로 대기하며,
`Play()` 또는 Play 모드 Inspector의 **Play / Restart**로 재생한다.

`Play()` 재시작, `Pause()` 정지, `Resume()` 계속 재생, `Seek(seconds)` 임의 시점 표시,
`Hide()` 숨김·입력 해제 API를 제공한다. 마지막 흰 화면은 Hide를 호출할 때까지 유지된다.
소리, 타이틀, 로고는 없다. PNG 내부의 면은 래스터 이미지이므로 개별 폴리곤 편집 대상이 아니다.

## 검증 및 재생성

**REmind → Crystal Transition → Validate and render contact frames**는 현재 프리팹을 읽고
`Library/CrystalTransitionQA`에 실제 URP 프레임과 결과를 저장한다. 프리팹은 수정하지 않는다.
시간 정지, 재시작, seek 결정성, 길이 변경, 이벤트 중복, 입력 해제와 흰 화면을 검사한다.

Builder의 seed는 280926. `Create editable assets`는 최초 생성용이며, 파일이 이미 있으면
수동 편집 보존을 위해 중단한다. 기존 파일을 재생성으로 덮어쓰지 않는다.
원본 광선/파편은 `Assets/Art/TransitionFX/GuidedWarp60/Sprites`를 참조한다.
별 4개 PNG는 전달받은 원본을 이 폴더의 Textures에 그대로 복사했다.

URP preview render request API: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/camera/submitrenderrequest
