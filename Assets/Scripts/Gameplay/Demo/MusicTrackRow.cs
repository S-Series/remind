using System;
using System.Collections.Generic;
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
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField] private string bestScore = "0";
        [SerializeField] private MusicCategory category;
        [SerializeField] private Image slotImage;
        [SerializeField] private Sprite artwork;
        [SerializeField] private Image jacketArtwork;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text artistLabel;
        [SerializeField] private Image favoriteOffImage;
        [SerializeField] private Image favoriteOnImage;

        private static readonly Color NormalText = new(0.93f, 0.95f, 1f, 1f);
        private static readonly Color NormalSecondary = new(0.69f, 0.76f, 0.91f, 1f);
        private static readonly Color NormalPanel = new(1f, 1f, 1f, 0.78f);
        private static readonly Color SelectedText = new(0.97f, 0.98f, 1f, 1f);
        private static readonly Color SelectedSecondary = new(0.84f, 0.91f, 1f, 1f);
        private MusicCatalogEntry catalogEntry;

        public SongContent Content { get; private set; }
        public SongAccessPolicy Access { get; private set; }
        public SongAccessPolicy GetDifficultyAccess(string id) =>
            Content != null && AppRoot.Current
                ? AppRoot.Current.GetSongAccess(Content, id) : Access;
        public string UnlockHint => string.IsNullOrWhiteSpace(Content?.UnlockHint)
            ? "해금 조건이 아직 공개되지 않았습니다." : Content.UnlockHint;
        public string Title => Access.IsMasked ? "???" : title;
        public string MusicId => catalogEntry?.MusicId;
        public IReadOnlyList<MusicDifficultyEntry> Difficulties =>
            catalogEntry?.Difficulties ?? Array.Empty<MusicDifficultyEntry>();
        public MusicDifficultyEntry FindDifficulty(string id) => catalogEntry?.FindDifficulty(id);
        public string DifficultyId => difficultyId;
        public string Artist => Access.IsMasked ? "???" : artist;
        public int Level => Access.IsMasked ? 0 : level;
        public string BestScore => Access.IsMasked ? "—" : bestScore;
        public MusicCategory Category => category;
        public bool Favorite { get; private set; }
        public Sprite Artwork => Access.IsMasked ? null : artwork;
        public bool HasSongError { get; private set; }
        public AudioClip PreviewAudio => Access.IsMasked ? null : catalogEntry?.AudioClip;
        public double PreviewStartMs { get; private set; }
        public double PreviewDurationMs { get; private set; }
        public float PreviewVolume { get; private set; } = 0.5f;

        public bool TryGetLevel(string id, out int chartLevel)
        {
            if (catalogEntry != null && !Access.IsMasked)
                return catalogEntry.TryGetLevel(id, out chartLevel);
            chartLevel = 0;
            return false;
        }

        public bool SelectDifficulty(string id)
        {
            if (catalogEntry == null || !catalogEntry.TryGetLevel(id, out int chartLevel)) return false;
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
                Content = song;
                RefreshAccess();
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

        public void Display(bool selected)
        {
            if (slotImage) slotImage.color = selected ? Color.white : NormalPanel;
            if (jacketArtwork != null)
            {
                jacketArtwork.sprite = Artwork;
                jacketArtwork.enabled = Artwork != null;
            }
            if (titleLabel != null)
            {
                titleLabel.text = Title;
                titleLabel.color = selected ? SelectedText : NormalText;
            }
            if (artistLabel != null)
            {
                artistLabel.text = Artist;
                artistLabel.color = selected ? SelectedSecondary : NormalSecondary;
            }
            if (favoriteOffImage != null) favoriteOffImage.gameObject.SetActive(!Favorite);
            if (favoriteOnImage != null) favoriteOnImage.gameObject.SetActive(Favorite);
        }

        public void ToggleFavorite() => Favorite = !Favorite;

        public void RefreshAccess()
        {
            if (Content == null) return;
            Access = AppRoot.Current ? AppRoot.Current.GetSongAccess(Content)
                : new SongAccessPolicy(Content, false, false);
        }

        public void ApplyPlayerData(bool favorite, bool hasRecord, double score)
        {
            Favorite = favorite;
            bestScore = hasRecord ? Math.Round(score).ToString("0",
                CultureInfo.InvariantCulture) : "0";
        }
    }
}
