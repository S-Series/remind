# Common UI 스프라이트

화면 사이에서 동일한 이미지로 사용하는 UI 장식을 둔다. 기존 공용 파일은 이 폴더 바로 아래에 두며, 각 스프라이트의 Unity `.meta` GUID를 유지했다. 여러 요소로 구성된 새 패널 세트는 하위 폴더로 묶는다.

- `logo_compass.png`: Home 로고 및 Bootstrap. Result의 동일한 점수 나침반 이미지도 이 원본을 사용한다.
- `header_compass.png`: Gallery와 Settings의 동일한 헤더 나침반.
- `nav_option_astrolabe.png`: Home에서 제작하고 Music Select에서도 사용하는 설정 메뉴 아이콘.
- `hairline_horizontal.png`, `marker_star_flare.png`, `orbit_ring.png`: Home에서 제작하고 Bootstrap에서 사용하는 장식.
- `selected_nav_underline.png`: Home에서 제작하고 Music Select에서도 사용하는 선택 밑줄.
- `footer_star_rule.png`, `progress_fill.png`, `vertical_star_rule.png`: Result에서 제작하고 Bootstrap에서 사용하는 장식.
- `Eraser.png`: 기존 UI 편집 도구 스프라이트.
- `frame_outer.png`, `frame_portrait_ring.png`, `badge_*.png` 등: 별과 달 테마의 공용 패널을 구성하는 독립 스프라이트.
- `frame_background_filled.png`: 외곽 프레임과 남색 유리 배경을 함께 남기고 내부 UI 요소를 제거한 빈 패널.
- `profile_plate_empty_matte.png`: 프로필 카드의 내부 요소를 제거한 남색 플레이트. 바깥쪽은 투명하고 발광 효과가 없는 테두리를 사용한다.
- `CelestialDigits/`: 공용 패널 색감에 맞춰 다시 디자인한 0–9 숫자 스프라이트와 전체 시트.

이름이 같더라도 이미지나 역할이 다른 화면 전용 스프라이트는 해당 화면 폴더에 둔다. 에디터 코드에서 경로로 로드하는 경우에는 이 폴더의 경로를 사용한다.
