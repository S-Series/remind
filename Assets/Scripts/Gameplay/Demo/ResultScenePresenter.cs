using System;
using System.Globalization;
using REmind.Charting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REmind.Gameplay.Demo
{
    /// <summary>Displays a finished Game session without retaining its live systems.</summary>
    public sealed class ResultScenePresenter : MonoBehaviour
    {
        private static readonly string[] DifficultyIds = { "easy", "normal", "hard", "chaos" };

        [SerializeField] private MusicCatalog catalog;
        [SerializeField] private TMP_Text songTitle;
        [SerializeField] private TMP_Text songArtist;
        [SerializeField] private Image jacket;
        [SerializeField] private TMP_Text[] difficultyLabels = new TMP_Text[4];
        [SerializeField] private Image selectedDifficulty;
        [SerializeField] private TMP_Text scoreValue;
        [SerializeField] private TMP_Text rankValue;
        [SerializeField] private TMP_Text rankCaption;
        [SerializeField] private TMP_Text[] judgementCounts = new TMP_Text[5];
        [SerializeField] private TMP_Text[] judgementPercents = new TMP_Text[5];
        [SerializeField] private TMP_Text comboValue;
        [SerializeField] private TMP_Text accuracyValue;
        [SerializeField] private Image accuracyFill;
        [SerializeField] private Button retryButton;
        [Header("Player Progress")]
        [SerializeField] private GameObject newRecordLabel;
        [SerializeField] private GameObject newRecordUnderline;
        [SerializeField] private GameObject memorySection;
        [SerializeField] private TMP_Text memoryTitle;
        [SerializeField] private TMP_Text memorySubtitle;
        [SerializeField] private TMP_Text memoryQuote;
        [SerializeField] private GameObject rewardSection;
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private TMP_Text fragmentCount;
        [SerializeField] private TMP_Text fragmentName;
        [SerializeField] private GameObject otherReward;
        [Header("Content awaiting its own data source")]
        [SerializeField] private GameObject rightQuoteSection;
        [SerializeField] private GameObject songMemo;
        [SerializeField] private GameObject songQuote;

        private float fullAccuracyWidth;

        private void Awake()
        {
            if (accuracyFill) fullAccuracyWidth = accuracyFill.rectTransform.sizeDelta.x;
        }

        private void Start()
        {
            HideUnbackedContent();
            if (AppRoot.Current && AppRoot.Current.TryTakeResult(out GameResultSnapshot result))
                Present(result);
            else
                PresentMissingResult();
        }

        public void Present(GameResultSnapshot result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            HideUnbackedContent();
            bool newRecord = AppRoot.Current &&
                AppRoot.Current.LastResultIsNewRecord;
            if (newRecordLabel) newRecordLabel.SetActive(newRecord);
            if (newRecordUnderline) newRecordUnderline.SetActive(newRecord);
            MusicCatalogEntry entry = catalog ? catalog.FindSong(result.MusicId) : null;
            SongContent song = null;
            if (entry?.SongData)
            {
                try
                {
                    song = SongContentCodec.Parse(entry.SongData.text);
                    if (song.MusicId != result.MusicId) song = null;
                }
                catch (FormatException exception)
                {
                    Debug.LogError("Result song data is invalid: " + exception.Message, this);
                }
            }

            Set(songTitle, song?.Title ?? result.MusicId);
            Set(songArtist, song?.Artist ?? string.Empty);
            if (jacket)
            {
                jacket.sprite = entry?.GetJacket(result.DifficultyId);
                jacket.enabled = jacket.sprite;
            }
            UpdateDifficulty(entry, result.DifficultyId);
            Set(scoreValue, Math.Round(result.Score).ToString("N0", CultureInfo.InvariantCulture));
            Set(rankValue, result.Rank.ToString());
            Set(rankCaption, result.IsAutoPlay ? "AUTO PLAY" :
                result.IsAllPerfect ? "ALL PERFECT" :
                result.IsFailed ? "FAILED" : result.IsCleared ? "CLEAR" : "FINISHED");

            int[] counts = { result.Perfect, result.Great, result.Good, 0, result.Miss };
            for (int index = 0; index < counts.Length; index++)
            {
                SetAt(judgementCounts, index, counts[index].ToString("N0", CultureInfo.InvariantCulture));
                double percent = result.TotalNoteCount > 0
                    ? counts[index] * 100d / result.TotalNoteCount : 0d;
                SetAt(judgementPercents, index,
                    percent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            }
            Set(comboValue, result.MaxCombo.ToString("N0", CultureInfo.InvariantCulture) +
                " / " + result.TotalNoteCount.ToString("N0", CultureInfo.InvariantCulture));
            Set(accuracyValue, (result.ScoreRatio * 100d).ToString("0.0",
                CultureInfo.InvariantCulture) + "%");
            if (accuracyFill)
            {
                Vector2 size = accuracyFill.rectTransform.sizeDelta;
                size.x = fullAccuracyWidth * (float)result.ScoreRatio;
                accuracyFill.rectTransform.sizeDelta = size;
            }
            PresentProgress(result);
            PresentReward(result);
            if (retryButton) retryButton.interactable = true;
        }

        private void PresentReward(GameResultSnapshot result)
        {
            if (!rewardSection || !AppRoot.Current || result.IsAutoPlay ||
                !AppRoot.Current.LastResultWasFirstClear) return;
            Set(rewardLabel, "기억 조각 · 보유 " +
                AppRoot.Current.MemoryFragmentCount);
            Set(fragmentCount, "× 1");
            Set(fragmentName, "첫 클리어");
            Hide(otherReward);
            rewardSection.SetActive(true);
        }

        private void PresentProgress(GameResultSnapshot result)
        {
            if (!memorySection || !AppRoot.Current ||
                !AppRoot.Current.TryGetChartProgress(result.MusicId,
                    result.DifficultyId, out ChartProgressSnapshot progress))
                return;
            Set(memoryTitle, "플레이 기록");
            Set(memorySubtitle, "누적 " + progress.PlayCount +
                "회 · 최고 콤보 " + progress.MaxCombo);
            Set(memoryQuote, result.IsAutoPlay
                ? "AUTO PLAY는 기록에 반영되지 않습니다."
                : AppRoot.Current.LastResultWasFirstClear
                    ? "첫 클리어 기록!"
                    : progress.Cleared ? "클리어 기록 보유" : "클리어 기록 없음");
            memorySection.SetActive(true);
        }

        private void UpdateDifficulty(MusicCatalogEntry entry, string selectedId)
        {
            if (selectedDifficulty) selectedDifficulty.gameObject.SetActive(false);
            for (int index = 0; index < DifficultyIds.Length &&
                 index < difficultyLabels.Length; index++)
            {
                TMP_Text label = difficultyLabels[index];
                if (!label) continue;
                string id = DifficultyIds[index];
                MusicDifficultyEntry difficulty = entry?.FindDifficulty(id);
                bool available = difficulty != null || id == selectedId;
                label.gameObject.SetActive(available);
                if (!available) continue;
                string level = difficulty != null && difficulty.Level > 0
                    ? difficulty.Level.ToString(CultureInfo.InvariantCulture) : "—";
                label.text = id.ToUpperInvariant() + "   " + level;
                bool selected = id == selectedId;
                label.color = selected ? new Color(.91f, .93f, 1f) :
                    new Color(.65f, .71f, .87f);
                if (selected && selectedDifficulty)
                {
                    RectTransform marker = selectedDifficulty.rectTransform;
                    Vector2 position = marker.anchoredPosition;
                    position.x = label.rectTransform.anchoredPosition.x - 14f;
                    marker.anchoredPosition = position;
                    selectedDifficulty.gameObject.SetActive(true);
                }
            }
        }

        private void PresentMissingResult()
        {
            Set(songTitle, "결과 데이터가 없습니다");
            Set(songArtist, string.Empty);
            if (jacket) jacket.enabled = false;
            UpdateDifficulty(null, string.Empty);
            Set(scoreValue, "—");
            Set(rankValue, "—");
            Set(rankCaption, string.Empty);
            for (int index = 0; index < 5; index++)
            {
                SetAt(judgementCounts, index, "—");
                SetAt(judgementPercents, index, "—");
            }
            Set(comboValue, "—");
            Set(accuracyValue, "—");
            if (accuracyFill)
            {
                Vector2 size = accuracyFill.rectTransform.sizeDelta;
                size.x = 0f;
                accuracyFill.rectTransform.sizeDelta = size;
            }
            if (retryButton) retryButton.interactable =
                AppRoot.Current && AppRoot.Current.TryGetSelectedSong(out _);
        }

        private void HideUnbackedContent()
        {
            Hide(newRecordLabel);
            Hide(newRecordUnderline);
            Hide(rewardSection);
            Hide(memorySection);
            Hide(rightQuoteSection);
            Hide(songMemo);
            Hide(songQuote);
        }

        private static void Hide(GameObject value)
        {
            if (value) value.SetActive(false);
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label) label.text = value;
        }

        private static void SetAt(TMP_Text[] labels, int index, string value)
        {
            if (labels != null && index < labels.Length) Set(labels[index], value);
        }
    }
}
