# UI 아트

UI 스프라이트는 화면 또는 역할별 폴더에 모은다. 각 폴더 안에서는 `Icons`, `Lines`, `Containers` 같은 종류별 하위 폴더를 두지 않고 PNG를 바로 배치한다. 이동한 파일의 Unity `.meta` GUID는 유지했다.

- `CommonUI`: 여러 화면에서 공유하는 스프라이트와 공용 도구 아이콘
- `HomeUI`, `GalleryUI`, `MusicSelectUI`, `ResultUI`, `SettingsUI`: 화면별 아트
- `ReMindUI`: 기존 런타임 UI 스프라이트 팩
- `CompassLayers`: UI 이미지로 조합하는 나침반 레이어

화면에 고유한 그림과 여러 화면에서 동일하게 쓰는 그림을 구분해 관리한다.
