using System;
using System.Globalization;
using REmind.Charting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REmind.Gameplay.Demo
{
    public enum MusicCategory { Story, Character, Event }

    // The UI hierarchy is authored in the Music Track prefab; song data is supplied by the catalog.
    public sealed class MusicTrackRow : MonoBehaviour
    {
        [Header("Song Data")]
        [SerializeField] private TextAsset songData;
        [SerializeField] private string difficultyId = "hard";
        [SerializeField] private string title;
        [SerializeField] private string artist;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField] private string rank = "-";
        [SerializeField] private string bestScore = "-";
        [SerializeField] private MusicCategory category;
        [SerializeField] private Image slotImage;
        [SerializeField] private Sprite normalBackgroundSprite;
        [SerializeField] private Sprite selectedBackgroundSprite;
        [SerializeField] private Sprite artwork;
        [SerializeField] private Image jacketArtwork;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text artistLabel;
        [SerializeField] private Image favoriteOffImage;
        [SerializeField] private Image favoriteOnImage;

        private static readonly Color NormalText = new(0.93f, 0.95f, 1f, 1f);
        private static readonly Color NormalSecondary = new(0.69f, 0.76f, 0.91f, 1f);
        private static readonly Color SelectedText = new(0.97f, 0.98f, 1f, 1f);
        private static readonly Color SelectedSecondary = new(0.84f, 0.91f, 1f, 1f);
        private MusicCatalogEntry catalogEntry;

        public string Title => title;
        public string MusicId => catalogEntry?.MusicId;
        public string DifficultyId => difficultyId;
        public string Artist => artist;
        public string Description => description;
        public int Level => level;
        public string Rank => rank;
        public string BestScore => bestScore;
        public MusicCategory Category => category;
        public bool Favorite { get; private set; }
        public Sprite Artwork => artwork;
        public bool HasSongError { get; private set; }
        public AudioClip PreviewAudio => catalogEntry?.AudioClip;
        public double PreviewStartMs { get; private set; }
        public double PreviewDurationMs { get; private set; }
        public float PreviewVolume { get; private set; } = 0.5f;

        public bool TryGetLevel(string id, out int chartLevel)
        {
            if (catalogEntry != null) return catalogEntry.TryGetLevel(id, out chartLevel);
            chartLevel = 0;
            return false;
        }

        public bool SelectDifficulty(string id)
        {
            if (!TryGetLevel(id, out int chartLevel)) return false;
            difficultyId = id;
            level = chartLevel;
            artwork = catalogEntry.GetJacket(id);
            return true;
        }

        public bool Configure(MusicCatalogEntry entry)
        {
            HasSongError = false;
            try
            {
                if (entry == null || !entry.SongData)
                    throw new FormatException("Song catalog entry has no data.json asset.");
                if (!entry.AudioClip || entry.Difficulties.Count == 0)
                    throw new FormatException("Song catalog entry has no audio or available chart.");
                catalogEntry = entry;
                songData = entry.SongData;
                artwork = entry.Jacket;
                SongContent song = SongContentCodec.Parse(songData.text);
                if (!string.Equals(song.MusicId, entry.MusicId, StringComparison.Ordinal))
                    throw new FormatException("Catalog musicId does not match data.json: " + entry.MusicId);
                PreviewStartMs = song.PreviewStartMs;
                PreviewDurationMs = song.PreviewDurationMs;
                PreviewVolume = song.PlaybackVolume;
                title = song.Title;
                artist = song.Artist;
                if (!SelectDifficulty(difficultyId))
                    SelectDifficulty(entry.Difficulties[0].DifficultyId);
                return true;
            }
            catch (Exception exception)
            {
                HasSongError = true;
                Debug.LogError("Music Track song data is invalid: " + exception.Message, this);
                return false;
            }
        }

        public void SetSelectAction(Action action)
        {
            if (slotImage == null || !slotImage.TryGetComponent(out Button button))
                throw new InvalidOperationException("Music Track prefab needs a Button on Slot.");
            button.onClick.RemoveAllListeners();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
        }

        public void Display(bool selected, Color normal, Color highlight)
        {
            if (slotImage != null)
            {
                if (normalBackgroundSprite != null && selectedBackgroundSprite != null)
                {
                    slotImage.sprite = selected ? selectedBackgroundSprite : normalBackgroundSprite;
                    slotImage.color = Color.white;
                }
                else slotImage.color = selected ? highlight : normal;
            }
            if (jacketArtwork != null) jacketArtwork.sprite = artwork;
            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.color = selected ? SelectedText : NormalText;
            }
            if (artistLabel != null)
            {
                artistLabel.text = artist;
                artistLabel.color = selected ? SelectedSecondary : NormalSecondary;
            }
            if (favoriteOffImage != null) favoriteOffImage.gameObject.SetActive(!Favorite);
            if (favoriteOnImage != null) favoriteOnImage.gameObject.SetActive(Favorite);
        }

        public void ToggleFavorite() => Favorite = !Favorite;

        public void ApplyPlayerData(bool favorite, bool hasRecord,
            double score, RankGrade grade)
        {
            Favorite = favorite;
            rank = hasRecord ? grade.ToString() : "-";
            bestScore = hasRecord ? Math.Round(score).ToString("N0",
                CultureInfo.InvariantCulture) : "-";
        }
    }
}
