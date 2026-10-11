# Celestial Digits

기존 0–9 숫자 시트를 공용 패널의 푸른 유리·별빛 테두리와 어울리도록 다시 디자인한 이미지 생성 결과입니다. 기존의 큰 내부 별과 굵은 흰색 금속 표현을 줄이고, 더 가는 세리프 획과 절제된 청백색 하이라이트를 사용했습니다.

- `digits_sheet.png`: 전체 숫자 디자인을 확인할 수 있는 0–9 시트.
- `digit_0.png`–`digit_9.png`: 각각 독립적으로 배치할 수 있는 투명 PNG. 모두 480×380 픽셀의 같은 캔버스에 기준선을 맞췄습니다.

숫자 형태는 이미지 생성 도구로 만들었고, 개별 PNG는 생성 결과에서 셀별로 분리해 투명 캔버스에 정렬한 것입니다. 각 `.meta`는 Unity `Sprite (2D and UI)` 설정입니다. 원본 PNG와 `.meta`는 파생 에셋을 다시 만들 때도 수정하지 않습니다.

## TMP 숫자 스프라이트

`Generated/CelestialDigitsSprite.asset`과 `.mat`은 `digits_sheet.png`를 사용해 정리한 TMP 스프라이트 에셋입니다. `digit_0`부터 `digit_9`까지 이름이 등록돼 있습니다. 이 에셋과 시트의 배치·메트릭은 수작업으로 조정했으므로 이전 자동 생성기로 다시 만들지 않습니다.

태그 없이 쓰려면 `TextMeshProUGUI`에 `TmpSpriteDigitText`를 추가하고 **Digit Sprite Asset**에 `CelestialDigitsSprite.asset`을 지정합니다. 텍스트에는 `1`, `00123`, `SCORE 00123`처럼 입력하면 됩니다. Inspector의 원문과 코드에서 설정한 문자열은 그대로 유지하고, 표시할 때만 ASCII 숫자 `0`–`9`를 해당 `<sprite name="digit_N">` 태그로 변환합니다. 기존 Rich Text 태그 내부의 숫자는 건드리지 않습니다.

**Display Mode**는 `Normal`과 `Highscore`로 구분합니다. `Normal`은 원본 숫자 색을 쓰고, `Highscore`는 **Minimum Digits**에 맞춰 앞에 `0`을 채운 다음 첫 유효 숫자 앞의 `0`만 **Leading Zero Tint**로 어둡게 표시합니다. 점수가 0일 때는 마지막 `0`을 원래 색으로 남깁니다. 쉼표가 있는 점수는 자리 채움 뒤 세 자리 구분을 다시 맞춥니다. MusicSelect의 `Best Score`는 현재 최고점 1,000,000점에 맞춰 7자리 `Highscore` 모드로 설정했습니다.

`Highscore`는 **Trailing Five Size** 값으로 마지막 5글자의 크기를 지정합니다. 예를 들어 값이 `32`이면 `09845178`은 표시할 때 `098<size=32>45178`처럼 처리됩니다. 원문에 이미 Rich Text 태그가 있으면 크기 태그를 추가하지 않습니다.

코드에서는 `SetHighscore(10_000_000, new Color32(74, 86, 130, 255))`으로 최고점과 채움 숫자의 tint를 설정하고, TMP 텍스트에 `9,860,000`을 넣으면 됩니다. `SetMode(SpriteDigitDisplayMode.Normal)`으로 원래 색의 일반 숫자 표시로 되돌릴 수 있습니다. 이후 다른 표시 모드는 `PreprocessText`의 모드 분기에 추가합니다.

컴포넌트를 사용하지 않는 텍스트에는 `CelestialDigitsSprite.asset`을 **Sprite Asset**으로 지정하고 `<sprite name="digit_1">` 태그를 직접 사용할 수도 있습니다. 기본 TMP 폰트에서 일반 문자열 `123`만 입력하면 스프라이트로 자동 대체되지는 않습니다.
