using System;

namespace REmind.Data
{
    public sealed class SongData
    {
        public string SongId { get; }
        public int SongSeason { get; }
        public string Title { get; }
        public string Artist { get; }
        public string DisplayBpm { get; }
        public double MaxBpm { get; }
        public double CommonBpm { get; }
        public double MinBpm { get; }
        public double PreviewStartMs { get; }
        public double PreviewDurationMs { get; }
        public SongHiddenType HiddenType { get; }
        public bool RequiresPurchase { get; }

        public SongData(
            string songId,
            int songSeason,
            string title,
            string artist,
            string displayBpm,
            double minBpm,
            double commonBpm,
            double maxBpm,
            double previewStartMs,
            double previewDurationMs,
            SongHiddenType hiddenType,
            bool requiresPurchase)
        {
            SongId = RequireText(songId, nameof(songId));
            SongSeason = Math.Max(0, songSeason);
            Title = RequireText(title, nameof(title));
            Artist = RequireText(artist, nameof(artist));
            DisplayBpm = RequireText(displayBpm, nameof(displayBpm));

            ValidateBpmRange(minBpm, commonBpm, maxBpm);
            MinBpm = minBpm;
            CommonBpm = commonBpm;
            MaxBpm = maxBpm;

            if (!IsFinite(previewStartMs) || previewStartMs < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(previewStartMs));
            }

            if (!IsFinite(previewDurationMs) || previewDurationMs <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(previewDurationMs));
            }

            if (!Enum.IsDefined(typeof(SongHiddenType), hiddenType))
            {
                throw new ArgumentOutOfRangeException(nameof(hiddenType));
            }

            PreviewStartMs = previewStartMs;
            PreviewDurationMs = previewDurationMs;
            HiddenType = hiddenType;
            RequiresPurchase = requiresPurchase;
        }

        private static void ValidateBpmRange(
            double minBpm,
            double commonBpm,
            double maxBpm)
        {
            if (!IsFinite(minBpm) ||
                !IsFinite(commonBpm) ||
                !IsFinite(maxBpm) ||
                minBpm <= 0d ||
                minBpm > commonBpm ||
                commonBpm > maxBpm)
            {
                throw new ArgumentException(
                    "BPM values must be positive and ordered Min <= Common <= Max.");
            }
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty value is required.",
                    parameterName);
            }

            return value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public enum SongHiddenType
    {
        Unknown = 0,
        NotHidden = 1,
        VisibleHidden = 2,
        InvisibleHidden = 3
    }
}
