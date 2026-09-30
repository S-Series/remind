# Celestial panel UI sprites

두 참고 이미지의 구성 요소를 이미지 생성 도구로 각각 분리해 만든 투명 PNG입니다. 글자와 배경 그림은 포함하지 않았습니다. Unity `Sprite (2D and UI)` 설정은 각 PNG의 `.meta`에 저장했습니다.

## CelestialPanelA (첫 번째 참고 이미지)

| 파일 | 용도 |
| --- | --- |
| `frame_outer.png` | 바깥 테두리 |
| `frame_artwork.png` | 왼쪽 정사각형 아트 테두리 |
| `orbit_artwork.png` | 아트 영역의 동심원과 십자선 |
| `slot_header.png` | 긴 상단 슬롯 |
| `slot_secondary.png` | 중간 슬롯 |
| `button_selected.png` | 선택 상태 버튼 |
| `button_idle.png` | 기본 상태 버튼 |
| `ornament_crescent.png` | 오른쪽 초승달 |
| `ornament_star.png` | 재사용 가능한 네 갈래 별 |
| `line_star_divider.png` | 별이 있는 가로 구분선 |
| `line_orbit_arc.png` | 초승달 주변 궤도 호 |

## CelestialPanelB (두 번째 참고 이미지)

| 파일 | 용도 |
| --- | --- |
| `frame_outer.png` | 다른 윤곽의 바깥 테두리 |
| `ornament_crescent.png` | 왼쪽 초승달 |
| `slot_header.png` | 상단 슬롯 |
| `slot_secondary.png` | 가는 중간 슬롯 |
| `button_idle.png` | 아래쪽 3개에 재사용할 버튼 |
| `line_orbit_dots.png` | 초승달 주위 점선 원 궤도 |
| `line_star_chain.png` | 세로 점선 별 장식 및 버튼 사이 구분선 |

PNG는 생성 결과에서 투명 여백만 잘라 저장했습니다. 각 요소를 별도 `Image`로 배치하고 색·밝기·유리 느낌을 머티리얼에서 조절할 수 있습니다. 생성 결과는 참고 이미지와 픽셀 단위로 일치하지 않으며, 특히 가는 궤도선과 빛 번짐에는 미세한 생성 잡티가 남을 수 있습니다.
