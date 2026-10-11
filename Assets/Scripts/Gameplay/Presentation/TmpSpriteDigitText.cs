using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace REmind.Gameplay.Presentation
{
    public enum SpriteDigitDisplayMode { Normal, Highscore }

    /// <summary>
    /// Renders plain ASCII digits through the assigned TMP sprite asset while
    /// keeping the authored and runtime text unchanged.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TmpSpriteDigitText : MonoBehaviour, ITextPreprocessor
    {
        [SerializeField] private TMP_SpriteAsset digitSpriteAsset;
        [SerializeField] private SpriteDigitDisplayMode displayMode = SpriteDigitDisplayMode.Normal;
        [SerializeField, Min(0)] private int minimumDigits;
        [SerializeField] private Color32 leadingZeroTint = new(74, 86, 130, 255);
        [SerializeField, Min(1)] private int trailingFiveSize = 32;

        private TextMeshProUGUI label;

        private void OnEnable()
        {
            label = GetComponent<TextMeshProUGUI>();
            if (digitSpriteAsset)
                label.spriteAsset = digitSpriteAsset;
            label.textPreprocessor = this;
            label.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (label && label.textPreprocessor == this)
            {
                label.textPreprocessor = null;
                label.SetVerticesDirty();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!this || !isActiveAndEnabled) return;
                if (!label) label = GetComponent<TextMeshProUGUI>();
                if (digitSpriteAsset) label.spriteAsset = digitSpriteAsset;
                label.SetVerticesDirty();
            };
        }
#endif

        public string PreprocessText(string text)
        {
            switch (displayMode)
            {
                case SpriteDigitDisplayMode.Normal:
                    return ToSpriteTags(text, null);
                case SpriteDigitDisplayMode.Highscore:
                    if (text == "0") return ToSpriteTags(text, null);
                    return ToSpriteTags(InsertTrailingSize(
                        PadNumericScore(text, minimumDigits),
                        trailingFiveSize), leadingZeroTint);
                default:
                    return ToSpriteTags(text, null);
            }
        }

        private static string PadNumericScore(string text, int width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0)
                return text;

            int digitCount = 0;
            bool grouped = false;
            foreach (char character in text)
            {
                if (character >= '0' && character <= '9') digitCount++;
                else if (character == ',') grouped = true;
                else return text;
            }
            if (digitCount == 0 || digitCount >= width)
                return text;

            StringBuilder digits = new(width);
            digits.Append('0', width - digitCount);
            foreach (char character in text)
                if (character >= '0' && character <= '9')
                    digits.Append(character);
            if (!grouped) return digits.ToString();

            StringBuilder formatted = new(width + (width - 1) / 3);
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    formatted.Append(',');
                formatted.Append(digits[i]);
            }
            return formatted.ToString();
        }

        private static string InsertTrailingSize(string text, int size)
        {
            if (string.IsNullOrEmpty(text) || size <= 0) return text;
            bool hasDigit = false;
            foreach (char character in text)
            {
                if (character >= '0' && character <= '9') hasDigit = true;
                else if (character != ',') return text;
            }
            if (!hasDigit) return text;
            int insertionIndex = Math.Max(0, text.Length - 5);
            return text.Insert(insertionIndex,
                "<size=" + size.ToString(CultureInfo.InvariantCulture) + ">");
        }

        private static string ToSpriteTags(string text, Color32? zeroTint)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            int lastDigitIndex = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (TrySkipTag(text, ref i)) continue;
                if (text[i] >= '0' && text[i] <= '9') lastDigitIndex = i;
            }
            string tintHex = zeroTint.HasValue
                ? ColorUtility.ToHtmlStringRGBA(zeroTint.Value) : null;
            bool significantDigitSeen = false;
            StringBuilder result = new(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                int tagStart = i;
                if (TrySkipTag(text, ref i))
                {
                    result.Append(text, tagStart, i - tagStart + 1);
                    continue;
                }

                if (character >= '0' && character <= '9')
                {
                    result.Append("<sprite name=\"digit_").Append(character)
                        .Append('"');
                    if (zeroTint.HasValue && character == '0' &&
                        !significantDigitSeen && i != lastDigitIndex)
                        result.Append(" color=#").Append(tintHex);
                    result.Append('>');
                    if (character != '0') significantDigitSeen = true;
                }
                else
                    result.Append(character);
            }
            return result.ToString();
        }

        private static bool TrySkipTag(string text, ref int index)
        {
            if (text[index] != '<' || index + 1 >= text.Length ||
                !(char.IsLetter(text[index + 1]) ||
                  text[index + 1] == '/' || text[index + 1] == '#'))
                return false;
            int end = text.IndexOf('>', index + 1);
            if (end < 0) return false;
            index = end;
            return true;
        }
    }
}
