# ReMind Music Select UI 스프라이트

첨부된 음악 선택 화면을 참고해 **내장 이미지 생성 도구로 개별 생성한** 투명 PNG 에셋이다. 최종 PNG는 생성 결과의 투명 여백만 잘랐다. 배경 장면, 인물 그림, 곡 재킷, 글자와 숫자는 포함하지 않는다.

모든 스프라이트는 `MusicSelectUI` 바로 아래에 둔다. 기존 `Art/Sprites/UI/MusicSelect` 팩의 8개 PNG도 이 폴더로 모았다. 아래 표는 별도로 생성한 장식 세트만 설명하며, 합쳐진 팩에는 `StellarPromise_Jacket.png`와 기존 메뉴 스프라이트도 포함된다.

| 종류 | 파일 | 용도 |
| --- | --- | --- |
| Icons | `compass_rose`, `favorite_star`, `play_chevron`, `butterfly`, `sparkle` | 곡명 나침반, 즐겨찾기, 재생 화살표, 장식 나비와 별빛 |
| Lines | `difficulty_ring`, `tab_underline`, `hairline`, `vertical_glint_rule`, `corner_filigree`, `photo_caption_rule`, `center_star_ray`, `center_star_ray_glow` | 난이도 원, 탭 선택선, 반복 구분선, 선택 광선, 행 모서리, 사진 캡션 장식, 중앙 기준 별과 수평 광선 및 약한 Glow 변형 |
| Containers | `polaroid_frame`, `selected_song_row`, `idle_song_row`, `play_button`, `thumbnail_frame`, `shortcut_keycap`, `score_panel`, `song_list_panel` | 사진·곡 행·버튼·축소 이미지·키캡·점수·곡 목록의 빈 UI 면 |

각 파일은 독립 RGBA PNG이며 Unity `Sprite (2D and UI)` 설정을 사용한다. `polaroid_frame`과 `thumbnail_frame`의 이미지 구멍은 투명하다. 그림과 텍스트는 별도 UI 요소로 배치한다. `hairline`은 길이를 바꾸거나 회전해서 다른 구분선에도 쓸 수 있다. `difficulty_ring`은 기본 테두리로, 선택 색과 추가 발광은 셰이더나 별도 UI 레이어에서 조정할 수 있다. 생성 이미지의 은청색 가장자리 빛은 PNG에도 일부 포함되어 있으므로 셰이더 발광을 더할 때 강도를 확인한다.

고정 비율 장식 그림으로 생성했으며 현재 9-slice Border는 설정하지 않았다. 특히 긴 행과 버튼은 비율을 크게 바꾸면 테두리와 광선이 늘어날 수 있다. 실제 씬에 적용할 때 대상 RectTransform 크기를 보며 조정한다.

## 생성 프롬프트 요약

공통 기준: “첨부된 ReMind 음악 선택 화면을 시각 참고로 삼아 지정된 **한 개의** UI 요소만 생성한다. 남청색·은백색·연보라색의 섬세한 천문 장식과 유리·종이 질감을 살린다. 진짜 투명 배경을 유지하고, 배경 장면·인물·곡 그림·글자·숫자를 넣지 않는다.”

- 아이콘: 팔방 나침반, 윤곽선 즐겨찾기 별, 가는 오른쪽 화살표, 짙은 남색과 얼음빛 날개의 나비, 사방 별빛을 각각 단독 생성.
- 선: 작은 눈금이 있는 비어 있는 난이도 원, 중앙 별빛이 있는 탭 밑줄, 반복 가능한 수평 선, 별빛을 지나는 수직 광선, 우상단 천문 장식, 사진 하단 잉크 장식을 각각 단독 생성.
- 추가 선: 제공된 가로 띠 이미지를 참고해 수평 광선 하나와 정확히 중앙의 별 하나만 생성. 격자·배경·다른 별빛은 제외.
- Glow 변형: 원본 광선을 유지하면서 이미지 생성으로 얻은 발광 변형을 낮은 강도로 섞어 중심 별과 선의 빛을 아주 조금 높였다. 원본과 같은 2147×234 캔버스다.
- 컨테이너: 빈 사진 구멍과 캡션 여백의 폴라로이드, 활성·비활성 곡 행, 장식 없는 연보라 재생 버튼, 빈 재킷 프레임, 빈 키캡, 빈 점수 패널, 곡 목록용 남색 투명 패널을 각각 단독 생성.

이미지 생성 결과에서 투명 여백을 기계적으로 자른 것 외에 도형을 코드로 그리거나 원본 화면을 잘라 붙이지 않았다.
