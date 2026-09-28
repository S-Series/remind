using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.Gameplay.Demo
{
    // Static UI lives in the scene; song rows come from the data catalog and authored prefab.
    public sealed class MusicSelectController : MonoBehaviour
    {
        [Header("Song List")]
        [SerializeField] private MusicCatalog catalog;
        [SerializeField] private MusicTrackRow trackPrefab;
        [SerializeField] private RectTransform trackContainer;
        [SerializeField] private RectTransform scenePreviewRow;
        [SerializeField, Min(1f)] private float rowSpacing = 85f;
        [SerializeField] private Button[] categoryButtons;
        [SerializeField] private Sprite categoryNormalSprite;
        [SerializeField] private Sprite categorySelectedSprite;
        [SerializeField] private Button[] difficultyButtons;
        [SerializeField] private Image[] difficultyRings;
        [SerializeField] private TMP_Text[] difficultyLevelLabels;
        [SerializeField] private Sprite difficultyNormalSprite;
        [SerializeField] private Sprite difficultySelectedSprite;
        [Header("Selection Presentation")]
        [SerializeField] private AudioSource previewAudioSource;
        [SerializeField, Min(0.01f)] private float previewFadeSeconds = 0.35f;
        [SerializeField] private RectTransform jacketContainer;
        [SerializeField] private CanvasGroup jacketCanvasGroup;
        [SerializeField, Min(0.01f)] private float listTransitionSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float jacketFadeSeconds = 0.16f;
        [SerializeField] private TMP_Text selectedTitle;
        [SerializeField] private TMP_Text selectedArtist;
        [SerializeField] private TMP_Text selectedDescription;
        [SerializeField] private TMP_Text selectedLevel;
        [SerializeField] private TMP_Text selectedRank;
        [SerializeField] private TMP_Text selectedBestScore;
        [SerializeField] private TMP_Text artworkCaption;
        [SerializeField] private Image selectedArtwork;
        [SerializeField] private GameObject[] categoryIndicators;
        [SerializeField] private TMP_Text sortLabel;
        [SerializeField] private TMP_Text filterLabel;
        [SerializeField] private TMP_Text favoriteLabel;
        [SerializeField] private GameObject playNotice;
        [SerializeField] private Color rowNormal = new(0.05f, 0.08f, 0.17f, 0.78f);
        [SerializeField] private Color rowSelected = new(0.77f, 0.83f, 0.98f, 0.98f);
        [SerializeField] private Color optionSelected = new(0.20f, 0.29f, 0.47f, 0.98f);

        private MusicTrackRow[] tracks = Array.Empty<MusicTrackRow>();
        private readonly List<int> visibleOrder = new();
        private int selected;
        private int category; // All, story, character, event, favorites.
        private int selectedDifficulty = 1;
        private int levelFilter;
        private int sort;
        private int presentedTrack = -1;
        private Sprite presentedArtwork;
        private int presentedPosition = -1;
        private int previewStartSample;
        private int previewEndSample;
        private float previewTargetVolume;
        private Vector2 listRestPosition;
        private Vector2 jacketRestPosition;
        private Vector3 jacketRestScale;
        private Coroutine listTransition;
        private Coroutine jacketTransition;
        private static readonly string[] DifficultyIds = { "easy", "normal", "hard", "chaos" };
        private static readonly string[] Filters = { "모두", "Lv.8 이하", "Lv.10 이하", "Lv.11 이상" };
        private static readonly string[] Sorts = { "기본", "제목", "레벨" };

        private void Awake()
        {
            if (trackContainer) listRestPosition = trackContainer.anchoredPosition;
            if (jacketContainer)
            {
                jacketRestPosition = jacketContainer.anchoredPosition;
                jacketRestScale = jacketContainer.localScale;
            }
            if (previewAudioSource)
            {
                previewAudioSource.playOnAwake = false;
                previewAudioSource.loop = false;
            }
            if (scenePreviewRow) scenePreviewRow.gameObject.SetActive(false);
            if (!catalog || !trackPrefab || !trackContainer)
            {
                Debug.LogError("Music Select needs a catalog, track prefab and row container.", this);
                return;
            }

            var created = new List<MusicTrackRow>();
            foreach (MusicCatalogEntry entry in catalog.Songs)
            {
                MusicTrackRow row = Instantiate(trackPrefab, trackContainer, false);
                row.name = "Music Track - " + entry.MusicId;
                if (!row.Configure(entry))
                {
                    Destroy(row.gameObject);
                    continue;
                }
                int index = created.Count;
                row.SetSelectAction(() => SelectTrack(index));
                created.Add(row);
            }
            tracks = created.ToArray();
        }

        private void Start()
        {
            if (playNotice) playNotice.SetActive(false);
            if (AppRoot.Current && AppRoot.Current.TryGetSelectedSong(
                    out AppRoot.SongSelection previous))
            {
                for (int index = 0; index < tracks.Length; index++)
                    if (string.Equals(tracks[index].MusicId, previous.MusicId,
                            StringComparison.Ordinal) &&
                        tracks[index].TryGetLevel(previous.DifficultyId, out _))
                    {
                        selected = index;
                        for (int difficulty = 0;
                             difficulty < DifficultyIds.Length; difficulty++)
                            if (DifficultyIds[difficulty] == previous.DifficultyId)
                            {
                                selectedDifficulty = difficulty;
                                break;
                            }
                        break;
                    }
            }
            Refresh();
        }

        private void OnEnable()
        {
            if (tracks.Length > 0) Refresh();
        }

        private void Update()
        {
            if (AppRoot.Current && AppRoot.Current.IsTransitioning) return;
            UpdatePreviewLoop();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (playNotice.activeSelf) CloseNotice(); else Back();
                return;
            }
            if (playNotice.activeSelf) return;
            if (keyboard.rKey.wasPressedThisFrame) CycleSort();
            if (keyboard.tKey.wasPressedThisFrame) CycleLevelFilter();
            if (keyboard.qKey.wasPressedThisFrame) SetCategory((category + 4) % 5);
            if (keyboard.eKey.wasPressedThisFrame) SetCategory((category + 1) % 5);
            if (keyboard.upArrowKey.wasPressedThisFrame) MoveSelection(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame) MoveSelection(1);
            if (keyboard.leftShiftKey.wasPressedThisFrame) MoveDifficulty(-1);
            if (keyboard.rightShiftKey.wasPressedThisFrame) MoveDifficulty(1);
        }

        private void OnDisable()
        {
            StopPreview();
            if (listTransition != null) StopCoroutine(listTransition);
            if (jacketTransition != null) StopCoroutine(jacketTransition);
            listTransition = jacketTransition = null;
            presentedTrack = presentedPosition = -1;
            presentedArtwork = null;
            if (trackContainer) trackContainer.anchoredPosition = listRestPosition;
            if (jacketContainer)
            {
                jacketContainer.anchoredPosition = jacketRestPosition;
                jacketContainer.localScale = jacketRestScale;
            }
            if (jacketCanvasGroup) jacketCanvasGroup.alpha = 1f;
        }

        public void SelectTrack(int index)
        {
            if (index < 0 || index >= tracks.Length || !tracks[index].gameObject.activeInHierarchy) return;
            selected = index;
            Refresh();
        }

        private void MoveSelection(int direction)
        {
            if (visibleOrder.Count == 0) return;
            int position = visibleOrder.IndexOf(selected);
            selected = visibleOrder[Mathf.Clamp(position + direction, 0, visibleOrder.Count - 1)];
            Refresh();
        }

        private void MoveDifficulty(int direction)
        {
            if (selected < 0 || selected >= tracks.Length) return;
            for (int step = 1; step <= DifficultyIds.Length; step++)
            {
                int index = (selectedDifficulty + direction * step + DifficultyIds.Length * 2) %
                    DifficultyIds.Length;
                if (!tracks[selected].TryGetLevel(DifficultyIds[index], out _)) continue;
                if (index != selectedDifficulty) SetDifficulty(index);
                return;
            }
        }

        private static int AvailableDifficulty(MusicTrackRow row, int preferred)
        {
            int nearest = -1;
            int distance = int.MaxValue;
            for (int index = 0; index < DifficultyIds.Length; index++)
            {
                if (!row.TryGetLevel(DifficultyIds[index], out _)) continue;
                int nextDistance = Math.Abs(index - preferred);
                if (nextDistance >= distance) continue;
                nearest = index;
                distance = nextDistance;
            }
            return nearest;
        }

        private void ApplyAvailableDifficulties()
        {
            if (selected >= 0 && selected < tracks.Length)
            {
                int available = AvailableDifficulty(tracks[selected], selectedDifficulty);
                if (available >= 0) selectedDifficulty = available;
            }
            foreach (MusicTrackRow row in tracks)
            {
                int available = AvailableDifficulty(row, selectedDifficulty);
                if (available >= 0) row.SelectDifficulty(DifficultyIds[available]);
            }
        }

        public void SetCategory(int index) { category = Mathf.Clamp(index, 0, 4); Refresh(); }
        public void SetDifficulty(int index)
        {
            if (index < 0 || index >= DifficultyIds.Length ||
                selected < 0 || selected >= tracks.Length ||
                !tracks[selected].TryGetLevel(DifficultyIds[index], out _)) return;
            selectedDifficulty = index;
            Refresh();
        }
        public void CycleSort() { sort = (sort + 1) % Sorts.Length; Refresh(); }
        public void CycleLevelFilter() { levelFilter = (levelFilter + 1) % Filters.Length; Refresh(); }
        public void ToggleFavorite()
        {
            if (tracks.Length == 0 || !tracks[selected].gameObject.activeInHierarchy) return;
            tracks[selected].ToggleFavorite();
            if (AppRoot.Current)
                AppRoot.Current.SetFavorite(tracks[selected].MusicId,
                    tracks[selected].Favorite);
            Refresh();
        }
        public void PlaySelectedSong()
        {
            if (selected < 0 || selected >= tracks.Length ||
                !tracks[selected].gameObject.activeInHierarchy) return;

            MusicTrackRow track = tracks[selected];
            if (!AppRoot.Current)
            {
                Debug.LogError("Start the Game from Bootstrap to keep the selected song.", this);
                return;
            }
            if (AppRoot.Current.IsTransitioning) return;
            AppRoot.Current.SelectSong(track.MusicId, track.DifficultyId);
            StopPreview();
            if (!AppRoot.Current.TryStartSelectedGame())
                Debug.LogError("Music Selected transition is not configured.", this);
        }
        public void CloseNotice() => playNotice.SetActive(false);
        public void Back()
        {
            if (AppRoot.Current && AppRoot.Current.IsTransitioning) return;
            SceneManager.LoadScene("Home");
        }

        private void StartPreview(MusicTrackRow track)
        {
            StopPreview();
            if (!previewAudioSource || !track.PreviewAudio) return;
            AudioClip clip = track.PreviewAudio;
            if (clip.samples <= 0) return;
            previewTargetVolume = track.PreviewVolume * (AppRoot.Current
                ? AppRoot.Current.Settings.MusicVolume : 1f);
            previewStartSample = (int)Math.Min(clip.samples - 1d, Math.Max(0d,
                Math.Round(track.PreviewStartMs * clip.frequency / 1000d)));
            previewEndSample = (int)Math.Min(clip.samples, Math.Max(
                previewStartSample + 1d,
                previewStartSample + Math.Round(
                    track.PreviewDurationMs * clip.frequency / 1000d)));
            previewAudioSource.clip = clip;
            previewAudioSource.timeSamples = previewStartSample;
            previewAudioSource.volume = 0f;
            previewAudioSource.Play();
        }

        private void UpdatePreviewLoop()
        {
            if (!previewAudioSource || !previewAudioSource.clip) return;
            if (previewAudioSource.clip.loadState == AudioDataLoadState.Loading) return;
            if (previewAudioSource.clip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogError("Preview audio could not load: " +
                    previewAudioSource.clip.name, this);
                StopPreview();
                return;
            }
            int sample = previewAudioSource.timeSamples;
            if (!previewAudioSource.isPlaying || sample < previewStartSample ||
                sample >= previewEndSample)
            {
                previewAudioSource.Stop();
                previewAudioSource.timeSamples = previewStartSample;
                previewAudioSource.volume = 0f;
                previewAudioSource.Play();
                return;
            }

            int fadeSamples = Math.Max(1, (int)Math.Min(
                Math.Round(previewFadeSeconds * previewAudioSource.clip.frequency),
                Math.Max(1, (previewEndSample - previewStartSample) / 2)));
            float fadeIn = Mathf.Clamp01((sample - previewStartSample) / (float)fadeSamples);
            float fadeOut = Mathf.Clamp01((previewEndSample - sample) / (float)fadeSamples);
            previewAudioSource.volume = previewTargetVolume * Mathf.Min(fadeIn, fadeOut);
        }

        private void StopPreview()
        {
            if (!previewAudioSource) return;
            previewAudioSource.Stop();
            previewAudioSource.clip = null;
            previewAudioSource.volume = 0f;
            previewTargetVolume = 0f;
            previewStartSample = previewEndSample = 0;
        }

        private void SetArtwork(MusicTrackRow track)
        {
            if (selectedArtwork) selectedArtwork.sprite = track?.Artwork;
            if (artworkCaption) artworkCaption.text = track == null
                ? "JACKET ART" : track.Title + "    /    ReMind";
        }

        private IEnumerator AnimateList(int indexDelta)
        {
            if (!trackContainer || indexDelta == 0) yield break;
            Vector2 from = listRestPosition + Vector2.down *
                (Mathf.Clamp(indexDelta, -2, 2) * rowSpacing);
            trackContainer.anchoredPosition = from;
            float elapsed = 0f;
            while (elapsed < listTransitionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / listTransitionSeconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                trackContainer.anchoredPosition = Vector2.LerpUnclamped(from, listRestPosition, eased);
                yield return null;
            }
            trackContainer.anchoredPosition = listRestPosition;
            listTransition = null;
        }

        private IEnumerator AnimateJacket(MusicTrackRow track)
        {
            if (!jacketCanvasGroup || !jacketContainer)
            {
                SetArtwork(track);
                yield break;
            }
            float fromAlpha = jacketCanvasGroup.alpha;
            Vector3 fromScale = jacketContainer.localScale;
            Vector2 fromPosition = jacketContainer.anchoredPosition;
            Vector3 smallScale = jacketRestScale * 0.94f;
            Vector2 smallPosition = jacketRestPosition + new Vector2(
                jacketContainer.rect.width * 0.03f, -jacketContainer.rect.height * 0.03f);
            float elapsed = 0f;
            while (elapsed < jacketFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / jacketFadeSeconds);
                jacketCanvasGroup.alpha = Mathf.Lerp(fromAlpha, 0f, t);
                jacketContainer.localScale = Vector3.Lerp(fromScale, smallScale, t);
                jacketContainer.anchoredPosition = Vector2.Lerp(fromPosition, smallPosition, t);
                yield return null;
            }
            SetArtwork(track);
            elapsed = 0f;
            while (elapsed < jacketFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / jacketFadeSeconds);
                jacketCanvasGroup.alpha = t;
                jacketContainer.localScale = Vector3.Lerp(smallScale, jacketRestScale, t);
                jacketContainer.anchoredPosition = Vector2.Lerp(smallPosition, jacketRestPosition, t);
                yield return null;
            }
            jacketCanvasGroup.alpha = 1f;
            jacketContainer.anchoredPosition = jacketRestPosition;
            jacketContainer.localScale = jacketRestScale;
            jacketTransition = null;
        }

        private bool Matches(MusicTrackRow row)
        {
            if (row.HasSongError) return false;
            bool group = category == 0 || category == 4 && row.Favorite || category - 1 == (int)row.Category;
            bool level = levelFilter == 0 || row.Level > 0 &&
                (levelFilter == 1 && row.Level <= 8 ||
                 levelFilter == 2 && row.Level <= 10 ||
                 levelFilter == 3 && row.Level >= 11);
            return group && level;
        }

        private int BuildVisibleOrder()
        {
            var order = new int[tracks.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => sort == 1
                ? string.Compare(tracks[a].Title, tracks[b].Title, StringComparison.CurrentCulture)
                : sort == 2
                    ? tracks[a].Level != tracks[b].Level ? tracks[a].Level.CompareTo(tracks[b].Level) : a.CompareTo(b)
                    : a.CompareTo(b));
            int first = -1;
            visibleOrder.Clear();
            for (int position = 0; position < order.Length; position++)
            {
                int i = order[position];
                if (!Matches(tracks[i])) continue;
                visibleOrder.Add(i);
                if (first < 0) first = i;
            }
            return first;
        }

        private void Refresh()
        {
            ApplyAvailableDifficulties();
            if (AppRoot.Current)
                foreach (MusicTrackRow row in tracks)
                {
                    bool hasRecord = AppRoot.Current.TryGetBestRecord(
                        row.MusicId, row.DifficultyId,
                        out double score, out RankGrade grade);
                    row.ApplyPlayerData(
                        AppRoot.Current.IsFavorite(row.MusicId),
                        hasRecord, score, grade);
                }
            int first = BuildVisibleOrder();
            if (first >= 0 && !visibleOrder.Contains(selected))
            {
                selected = first;
                ApplyAvailableDifficulties();
                first = BuildVisibleOrder();
            }
            int selectedPosition = visibleOrder.IndexOf(selected);
            var shown = new bool[tracks.Length];
            for (int position = 0; position < visibleOrder.Count; position++)
            {
                int distance = selectedPosition - position;
                if (Math.Abs(distance) > 3) continue;
                int index = visibleOrder[position];
                shown[index] = true;
                MusicTrackRow row = tracks[index];
                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                if (row.transform is RectTransform rect)
                    rect.anchoredPosition = new Vector2(0f, distance * rowSpacing);
                row.transform.SetAsLastSibling();
            }
            for (int i = 0; i < tracks.Length; i++)
                if (!shown[i] && tracks[i].gameObject.activeSelf)
                    tracks[i].gameObject.SetActive(false);
            if (first < 0)
            {
                presentedTrack = -1;
                presentedPosition = -1;
                presentedArtwork = null;
                StopPreview();
                if (listTransition != null) StopCoroutine(listTransition);
                if (jacketTransition != null) StopCoroutine(jacketTransition);
                if (trackContainer) trackContainer.anchoredPosition = listRestPosition;
                if (jacketContainer)
                {
                    jacketContainer.anchoredPosition = jacketRestPosition;
                    jacketContainer.localScale = jacketRestScale;
                }
                if (jacketCanvasGroup) jacketCanvasGroup.alpha = 1f;
                selectedTitle.text = "표시할 곡이 없습니다";
                selectedArtist.text = "다른 분류를 선택하세요";
                selectedDescription.text = "";
                selectedLevel.text = selectedRank.text = selectedBestScore.text = "-";
                favoriteLabel.text = "☆ 즐겨찾기";
                SetArtwork(null);
            }
            else
            {
                if (!tracks[selected].gameObject.activeSelf) selected = first;
                var track = tracks[selected];
                if (presentedTrack != selected)
                {
                    if (presentedTrack < 0) SetArtwork(track);
                    else
                    {
                        if (listTransition != null) StopCoroutine(listTransition);
                        listTransition = StartCoroutine(AnimateList(selectedPosition - presentedPosition));
                        if (jacketTransition != null) StopCoroutine(jacketTransition);
                        jacketTransition = StartCoroutine(AnimateJacket(track));
                    }
                    StartPreview(track);
                    presentedTrack = selected;
                    presentedArtwork = track.Artwork;
                }
                else if (presentedArtwork != track.Artwork)
                {
                    if (jacketTransition != null) StopCoroutine(jacketTransition);
                    jacketTransition = StartCoroutine(AnimateJacket(track));
                    presentedArtwork = track.Artwork;
                }
                presentedPosition = selectedPosition;
                selectedTitle.text = track.Title;
                selectedArtist.text = track.Artist;
                selectedDescription.text = track.Description;
                selectedLevel.text = track.Level > 0 ? $"Lv. {track.Level}" : "Lv. -";
                selectedRank.text = track.Rank;
                selectedBestScore.text = track.BestScore;
                favoriteLabel.text = track.Favorite ? "★ 즐겨찾기" : "☆ 즐겨찾기";
            }
            for (int i = 0; i < tracks.Length; i++)
                tracks[i].Display(i == selected && tracks[i].gameObject.activeSelf, rowNormal, rowSelected);
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                Image categoryImage = categoryButtons[i].GetComponent<Image>();
                if (categoryNormalSprite != null && categorySelectedSprite != null)
                {
                    categoryImage.sprite = i == category ? categorySelectedSprite : categoryNormalSprite;
                    categoryImage.color = Color.white;
                }
                else categoryImage.color = i == category ? optionSelected : rowNormal;
                if (categoryIndicators != null && i < categoryIndicators.Length && categoryIndicators[i] != null)
                    categoryIndicators[i].SetActive(i == category);
            }
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                Image difficultyImage = difficultyButtons[i].GetComponent<Image>();
                int chartLevel = 0;
                bool available = first >= 0 && i < DifficultyIds.Length &&
                    tracks[selected].TryGetLevel(DifficultyIds[i], out chartLevel);
                difficultyButtons[i].interactable = available;
                if (difficultyLevelLabels != null && i < difficultyLevelLabels.Length &&
                    difficultyLevelLabels[i] != null)
                    difficultyLevelLabels[i].text = available && chartLevel > 0
                        ? chartLevel.ToString() : "-";
                if (difficultyRings != null && i < difficultyRings.Length && difficultyRings[i] != null &&
                    difficultyNormalSprite != null && difficultySelectedSprite != null)
                {
                    difficultyRings[i].sprite = available && i == selectedDifficulty
                        ? difficultySelectedSprite : difficultyNormalSprite;
                    difficultyRings[i].color = available ? Color.white : new Color(1f, 1f, 1f, 0.28f);
                    difficultyImage.color = Color.clear;
                }
                else difficultyImage.color = available && i == selectedDifficulty
                    ? optionSelected : rowNormal;
            }
            sortLabel.text = "R  정렬: " + Sorts[sort];
            filterLabel.text = "T  난이도: " + Filters[levelFilter];
        }
    }
}
