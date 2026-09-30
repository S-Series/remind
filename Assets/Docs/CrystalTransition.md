# Crystal Compass 전환 편집

> 현행 에셋/호출 경계 검토: 2026-09-29
> 아래 렌더·실행 결과는 기존 작업 기록이며 이번 문서 검토에서 재실행하지 않았다.

`Assets/Prefabs/Transitions/CrystalTransition.prefab`는
uGUI 이미지, 앞뒤 궤도 mesh, 부드러운 halo shader와 Animation Clip으로 구성한
단독 기본 길이 1.5초의 화면 전환 에셋이다. Game 전체 씬 전환 시간은 투명
오버레이·비동기 로드·화면 드러내기 단계를 더하므로 이 값과 같지 않다.
별 4개 부품과 모든 파편/광선을 하이어라키에서 수정할 수 있다.

- 미리보기: **REmind → Crystal Transition → Preview**
- 편집: 프리팹의 Inspector / Unity Animation 창
- 씬: `Assets/Scenes/prev/CrystalPreview.unity`
- 애니메이션: `Assets/Art/Animations/ReMind_CrystalTransition.anim`
- 애니메이션은 고정 속성을 프리팹에 두고 불필요한 중간 키를 줄인 상태다.
- 텍스처: `Assets/Art/Transitions/CrystalCompass/Textures`
- 셰이더와 머티리얼: `Assets/Art/Shaders/UI_RadialHalo.shader`, `RadialHalo.mat`
- 사용법과 레이어 설명: [CrystalCompass README](../Art/Transitions/CrystalCompass/README.md)
- 검증: **REmind → Crystal Transition → Validate and render contact frames**

Game 전용 표시기는 `REmind.Gameplay.Presentation.CrystalTransitionPlayer`다.
씬 로드, 선택 상태, 중복 전환 방지는 담당하지 않는다. Bootstrap의
`SceneTransitionController`가 두 표시기를 호출한다. 투명 오버레이는 모든 Game 씬
이동에서 로드 전후로 이어 재생하며, 흰 결정 연출은 Music Select→Game에서만
화면을 덮는다. 일반 씬 이동은 별도 어두운 덮개로 로드 순간을 가린다.
덮개는 전환 시작과 함께 약 0.15초 동안 어두워지고, 새 씬 로드 중 유지되다가
투명 오버레이가 끝나기 직전 약 0.15초 동안 밝아진다.
투명 오버레이는 `Intro → Loop → Outro`로 재생한다. Intro와 Outro는 각각 원본
1.2초 구간을 Game 씬 전환에서 1초로 재생한다. 일반 전환은 Intro 시작 0.25초 뒤
씬 로드를 요청하며, 빠르게 준비되면
Loop를 생략해 기본 길이는 2초다. 오래 걸리면 0.8초 Loop를 반복하고, 현재 반복이
끝나는 자세에서 Outro로 넘어간다.
Bootstrap→Home도 같은 전환을 사용한다. 로고와 추가 로딩을 연결할 때는
`AppRoot`의 자동 Home 진입을 끄고 로딩 완료 뒤 버튼 입력을 받으면
`CompleteBootstrapLoading()`을 호출한다.

프로젝트 안에서 컴파일, Edit Mode의 명시적 시간 진행/정지/재시작/역방향 seek,
2초 길이 변경, 이벤트 1회 발생, 입력 해제, 실제 궤도 pixel 기여와
화이트아웃 경계를 검증했다. URP로 1920×1080 대표 프레임 9장을 렌더했다.
타 기기의 성능은 이 에셋 검증 범위에 포함하지 않는다.

개별 Artwork의 색·텍스처·위치·크기·회전·표시 여부와 궤도 반지름·선 두께는
프리팹 Inspector에서 수정한다. 개별 시점과 이동 경로의 원본은 Animation Clip이다.
Preview 창은 재생과 시간 이동만 담당하며, 편집하거나 프리팹에 값을 저장하지 않는다.

## 배경 없는 별도 전환

- 프리팹: `Assets/Prefabs/Transitions/CrystalOverlayTransition.prefab`
- 애니메이션: `Assets/Art/Animations/ReMind_CrystalOverlay_Intro.anim`,
  `ReMind_CrystalOverlay_Loop.anim`, `ReMind_CrystalOverlay_Outro.anim`
- 이전 통합 원본: `Assets/Art/Animations/ReMind_CrystalOverlay.anim` (런타임에서는 사용하지 않음)
- 데모 씬: `Assets/Scenes/prev/CrystalOverlayPreview.unity`
- 미리보기: **REmind → Crystal Transition → Preview Transparent Overlay** (체크무늬는 투명 영역 표시)
- 검증: **REmind → Crystal Transition → Validate Transparent Overlay**

Intro에서 중앙 별이 형성되고 궤도·파편·가는 십자 광선·작은 별 장식이 나타난다.
Loop에서는 완성된 자세를 유지하면서 궤도가 작게 흔들리며, Outro에서 함께 사라진다.
프리팹 단독으로는 배경 이미지와 전체 화면 덮개가 없으며 뒤 화면과 클릭을 유지한다.
Game 씬 전환에서는 별도 덮개와 호출부의 전환 가드가 입력을 제한하므로,
투명 프리팹의 특성을 전환 중 사용자 입력 허용으로 해석하지 않는다.
데모 카메라의 단색 배경은 프리팹에 포함되지 않는다. 기존 화이트아웃 프리팹과 클립은 별개다.
중앙 별의 크기 값은 0.5이고 앞뒤 Orbit 그룹은 원래 크기의 70%다.
18개 결정 조각은 각도를 유지하면서 중심 거리를 약 350~720 범위로 고정 배치했다.
재생할 때 위치를 다시 뽑지 않으며, Intro·Loop·Outro의 조각 위치가 이어진다.

두 프리팹은 최초 대기 상태에서 보이지 않으며 자동 재생하지 않는다.
프리팹을 씬에 배치하고 흰색 전환은 `CrystalTransitionPlayer.Play()`로 재생한다.
투명 전환의 세 단계는 `SceneTransitionController`가 씬 로드 상태에 맞춰 재생한다. 데모 씬에서는
루트를 선택하고 Animation 창의 **Preview → Play**로 편집 중인 클립을 확인하거나,
Play 모드 진입 후 Inspector의 **Play / Restart**로 재생한다. 데모 씬 인스턴스의 루트 CanvasGroup은
Animation 창에서 보이도록 설정되어 있고, 런타임 시작 시에는 Player가 숨김 상태로 초기화한다.
Animation 창에서 루트의 클립 목록을 열어 Intro, Loop, Outro를 각각 선택한다.
Intro 첫 프레임과 Outro 마지막 프레임은 의도적으로 투명하다. Intro의 끝이나
Loop의 아무 프레임으로 이동하면 별과 궤도를 확인할 수 있다. Game 뷰에는 전체 화면으로 표시되며, Scene 뷰에서는
캔버스가 작으면 루트를 선택하고 `F`로 프레임한 뒤 휠로 확대한다.
`Duration`으로 Loop를 제외한 기본 전환 길이, `Star Size`로 중앙 별 크기,
`Orbit Opacity`와 `Particle Opacity`로 각 요소의 농도를 조절한다.
Outro 완료 시 자동으로 숨겨진다. 각 비반복 단계의 `completed` 이벤트는 한 번씩 발생한다. 화면 전체를 가리지 않으므로
`covered` 이벤트는 발생하지 않는다. 씬 교체 시점은 호출하는 기존 컨트롤러에서 결정한다.

`00_Elements_Envelope` 아래에 별 부품, 궤도 앞/뒤, 파편/미세 입자,
`05_Fine_Cross_Light`, `06_Star_Ornaments`를 분리했다. 개별 Artwork를 Inspector로 수정하고,
출현·반복·소멸 시점과 움직임은 세 `.anim`을 Unity Animation 창에서 각각 편집한다.
처음에는 투명하므로 Inspector의 **Show assembled pose for editing**으로 펼쳐 볼 수 있다.
편집 내용 저장 후 미리보기의 **Reload**로 확인한다.

## 제품 검수와 테스트 씬

`Assets/Scenes/prev/`는 에셋 편집용 미리보기 씬이며 Play/Chart 제품 프로필에 넣지 않는다.
Bootstrap은 표시 완료 뒤 새 버튼 입력으로 한 번 전환한다. 진행 백분율은 현재
최소 표시 시간에 따른 연출이며 실제 리소스 로드량을 측정하지 않는다.
일반 이동의 빠른 로드/Loop 진입, 곡 선택 전용 흰 화면, 중복 요청, 전환 후 입력 복원은
[TASKS.md](TASKS.md)의 A/B 검수 대상으로 관리한다. 옛 48프레임 전환 이력으로
현재 Intro/Loop/Outro 전체 검증을 대신하지 않는다.
