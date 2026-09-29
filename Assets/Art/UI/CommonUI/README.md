# Common UI 스프라이트

화면 사이에서 동일한 이미지로 사용하는 UI 장식을 둔다. 파일은 이 폴더 바로 아래에 두며, 각 스프라이트의 Unity `.meta` GUID를 유지했다.

- `logo_compass.png`: Home 로고 및 Bootstrap. Result의 동일한 점수 나침반 이미지도 이 원본을 사용한다.
- `header_compass.png`: Gallery와 Settings의 동일한 헤더 나침반.
- `nav_option_astrolabe.png`: Home에서 제작하고 Music Select에서도 사용하는 설정 메뉴 아이콘.
- `hairline_horizontal.png`, `marker_star_flare.png`, `orbit_ring.png`: Home에서 제작하고 Bootstrap에서 사용하는 장식.
- `selected_nav_underline.png`: Home에서 제작하고 Music Select에서도 사용하는 선택 밑줄.
- `footer_star_rule.png`, `progress_fill.png`, `vertical_star_rule.png`: Result에서 제작하고 Bootstrap에서 사용하는 장식.
- `Eraser.png`: 기존 UI 편집 도구 스프라이트.

이름이 같더라도 이미지나 역할이 다른 화면 전용 스프라이트는 해당 화면 폴더에 둔다. 에디터 코드에서 경로로 로드하는 경우에는 이 폴더의 경로를 사용한다.
