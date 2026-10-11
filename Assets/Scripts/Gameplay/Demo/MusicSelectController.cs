using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.Gameplay.Demo
{
    // Static UI lives in the scene; song rows come from the data catalog and authored prefab.
    public sealed class MusicSelectController : MonoBehaviour
    {
        [Serializable]
        private sealed class DifficultySlot
        {
            public RectTransform holder;
            public Image ring;
            public TMP_Text level;
            public TMP_Text caption;
            [NonSerialized] public Button button;
        }

        [Header("Song List")]
        [SerializeField] private MusicCatalog catalog;
        [SerializeField] private MusicTrackRow trackPrefab;
        [SerializeField] private RectTransform trackContainer;
        [SerializeField] private Button[] categoryButtons;
        [SerializeField] private Sprite categoryNormalSprite;
        [SerializeField] private Sprite categorySelectedSprite;
        [SerializeField] private DifficultySlot[] difficultySlots;
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
        [SerializeField] private TMP_Text selectedBestScore;
        [SerializeField] private TMP_Text artworkCaption;
        [SerializeField] private Image selectedArtwork;
        [SerializeField] private GameObject[] categoryIndicators;
        [SerializeField] private TMP_Text sortLabel;
        [SerializeField] private TMP_Text filterLabel;
        [SerializeField] private GameObject playNotice;
        [SerializeField] private TMP_Text playErrorLabel;
        [SerializeField] private Color optionNormal = new(0.05f, 0.08f, 0.17f, 0.78f);
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
        private AudioClip presentedPreview;
        private TMP_Text noticeTitle;
        private GameObject noticePreviousSelection;
        private readonly List<Selectable> noticeBlockedControls = new();
        private bool IsNoticeOpen => playNotice && playNotice.activeSelf;
        private int presentedPosition = -1;
        private int previewStartSample;
        private int previewEndSample;
        private float previewTargetVolume;
        private float rowSpacing;
        private Vector2 rowBasePosition;
        private Vector2 listRestPosition;
        private Vector2 jacketRestPosition;
        private Vector3 jacketRestScale;
        private Coroutine listTransition;
        private Coroutine jacketTransition;
        private static readonly string[] Filters = { "모두", "Lv.8 이하", "Lv.10 이하", "Lv.11 이상" };
        private static readonly string[] Sorts = { "기본", "제목", "레벨" };

        private void Awake()
        {
            if (trackContainer) listRestPosition = trackContainer.anchoredPosition;
            if (trackPrefab && trackPrefab.transform is RectTransform prefabRect)
            {
                rowSpacing = Mathf.Max(1f,
                    prefabRect.rect.height * Mathf.Abs(prefabRect.localScale.y));
                rowBasePosition = prefabRect.anchoredPosition;
            }
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
            InitializeDifficultySlots();
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
                             difficulty < MusicDifficultySlots.Count; difficulty++)
                            if (MusicDifficultySlots.GetId(tracks[index].Difficulties,
                                    difficulty) == previous.DifficultyId)
                            {
                                selectedDifficulty = difficulty;
                                break;
                            }
                        break;
                    }
            }
            SelectUnlockedDifficultyIfNeeded();
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
            if (playNotice) playNotice.SetActive(false);
            ReleaseNoticeFocus();
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
            if (playNotice && playNotice.activeSelf) return;
            if (index < 0 || index >= tracks.Length || !tracks[index].gameObject.activeInHierarchy) return;
            selected = index;
            SelectUnlockedDifficultyIfNeeded();
            Refresh();
            ShowSelectedPuzzleHint();
        }

        private void MoveSelection(int direction)
        {
            if (IsNoticeOpen) return;
            if (visibleOrder.Count == 0) return;
            int position = visibleOrder.IndexOf(selected);
            selected = visibleOrder[Mathf.Clamp(position + direction, 0, visibleOrder.Count - 1)];
            SelectUnlockedDifficultyIfNeeded();
            Refresh();
            ShowSelectedPuzzleHint();
        }

        private void MoveDifficulty(int direction)
        {
            if (IsNoticeOpen) return;
            if (selected < 0 || selected >= tracks.Length) return;
            for (int step = 1; step <= MusicDifficultySlots.Count; step++)
            {
                int index = (selectedDifficulty + direction * step +
                    MusicDifficultySlots.Count * 2) % MusicDifficultySlots.Count;
                string id = MusicDifficultySlots.GetId(tracks[selected].Difficulties, index);
                if (id == null || !tracks[selected].TryGetLevel(id, out _)) continue;
                if (index != selectedDifficulty) SetDifficulty(index);
                return;
            }
        }

        private void InitializeDifficultySlots()
        {
            if (difficultySlots == null) return;
            for (int index = 0; index < difficultySlots.Length; index++)
            {
                DifficultySlot slot = difficultySlots[index];
                if (slot?.holder == null) continue;
                if (!slot.holder.TryGetComponent(out slot.button))
                    slot.button = slot.holder.gameObject.AddComponent<Button>();
                slot.button.transition = Selectable.Transition.None;
                slot.button.navigation = new Navigation { mode = Navigation.Mode.None };
                if (slot.ring)
                {
                    slot.ring.raycastTarget = true;
                    slot.button.targetGraphic = slot.ring;
                }
                int selectedIndex = index;
                slot.button.onClick.AddListener(() => SetDifficulty(selectedIndex));
            }
        }

        private static int AvailableDifficulty(MusicTrackRow row, int preferred)
        {
            int nearest = -1;
            int distance = int.MaxValue;
            for (int index = 0; index < MusicDifficultySlots.Count; index++)
            {
                string id = MusicDifficultySlots.GetId(row.Difficulties, index);
                if (id == null || !row.TryGetLevel(id, out _)) continue;
                int nextDistance = Math.Abs(index - preferred);
                if (nextDistance >= distance) continue;
                nearest = index;
                distance = nextDistance;
            }
            return nearest;
        }

        private void SelectUnlockedDifficultyIfNeeded()
        {
            if (selected < 0 || selected >= tracks.Length || !AppRoot.Current)
                return;
            MusicTrackRow row = tracks[selected];
            int nearest = -1;
            int distance = int.MaxValue;
            for (int index = 0; index < MusicDifficultySlots.Count; index++)
            {
                string id = MusicDifficultySlots.GetId(row.Difficulties, index);
                if (id == null || !row.TryGetLevel(id, out _) ||
                    row.GetDifficultyAccess(id).NeedsUnlock)
                    continue;
                int nextDistance = Math.Abs(index - selectedDifficulty);
                if (nextDistance >= distance) continue;
                nearest = index;
                distance = nextDistance;
            }
            if (nearest >= 0) selectedDifficulty = nearest;
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
                if (available >= 0)
                    row.SelectDifficulty(MusicDifficultySlots.GetId(row.Difficulties, available));
            }
        }

        public void SetCategory(int index)
        {
            if (IsNoticeOpen) return;
            category = Mathf.Clamp(index, 0, 4);
            Refresh();
        }
        public void SetDifficulty(int index)
        {
            if (IsNoticeOpen) return;
            if (selected < 0 || selected >= tracks.Length) return;
            string id = MusicDifficultySlots.GetId(tracks[selected].Difficulties, index);
            if (id == null || !tracks[selected].TryGetLevel(id, out _)) return;
            selectedDifficulty = index;
            Refresh();
        }
        public void CycleSort()
        {
            if (IsNoticeOpen) return;
            sort = (sort + 1) % Sorts.Length;
            Refresh();
        }
        public void CycleLevelFilter()
        {
            if (IsNoticeOpen) return;
            levelFilter = (levelFilter + 1) % Filters.Length;
            Refresh();
        }
        public void ToggleFavorite()
        {
            if (IsNoticeOpen) return;
            if (tracks.Length == 0 || !tracks[selected].gameObject.activeInHierarchy) return;
            if (AppRoot.Current &&
                !string.IsNullOrEmpty(AppRoot.Current.PlayerDataError))
            {
                ShowPlayError(AppRoot.Current.PlayerDataError);
                return;
            }
            tracks[selected].ToggleFavorite();
            if (AppRoot.Current)
                AppRoot.Current.SetFavorite(tracks[selected].MusicId,
                    tracks[selected].Favorite);
            Refresh();
        }
        public void PlaySelectedSong()
        {
            if (playNotice && playNotice.activeSelf) return;
            if (selected < 0 || selected >= tracks.Length ||
                !tracks[selected].gameObject.activeInHierarchy) return;

            MusicTrackRow track = tracks[selected];
            track.RefreshAccess();
            SongAccessPolicy access = track.GetDifficultyAccess(track.DifficultyId);
            if (!access.CanPlay)
            {
                StopPreview();
                ShowNotice(access.ShowPuzzleHint ? track.UnlockHint
                    : access.NeedsUnlock ? "아직 해금되지 않은 난이도입니다."
                    : "이 곡의 이용 권한이 필요합니다.",
                    access.ShowPuzzleHint ? "해금 조건" : "이용 안내");
                return;
            }
            if (!AppRoot.Current)
            {
                Debug.LogError("Start the Game from Bootstrap to keep the selected song.", this);
                return;
            }
            if (AppRoot.Current.IsTransitioning) return;
            if (!AppRoot.Current.TrySelectSongWithEquippedCharacter(
                track.MusicId, track.DifficultyId, out string error))
            {
                ShowPlayError(error);
                return;
            }
            StopPreview();
            if (!AppRoot.Current.TryStartSelectedGame())
                Debug.LogError("Music Selected transition is not configured.", this);
        }
        private void ShowPlayError(string error)
        {
            Debug.LogError("Cannot update player selection: " + error,
                this);
            ShowNotice(error);
        }

        private void ShowSelectedPuzzleHint()
        {
            if (selected >= 0 && selected < tracks.Length &&
                tracks[selected].gameObject.activeInHierarchy &&
                tracks[selected].Access.ShowPuzzleHint)
                ShowNotice(tracks[selected].UnlockHint, "해금 조건");
        }

        private void ShowNotice(string message, string title = "안내")
        {
            StopPreview();
            if (!IsNoticeOpen && playNotice)
            {
                noticePreviousSelection = EventSystem.current?.currentSelectedGameObject;
                Canvas canvas = playNotice.GetComponentInParent<Canvas>();
                if (canvas)
                    foreach (Selectable control in canvas.GetComponentsInChildren<Selectable>(true))
                        if (!control.transform.IsChildOf(playNotice.transform) && control.interactable)
                        {
                            noticeBlockedControls.Add(control);
                            control.interactable = false;
                        }
            }
            if (!noticeTitle && playNotice)
            {
                Transform heading = playNotice.transform.Find("Notice Window/Notice Title");
                if (heading) noticeTitle = heading.GetComponent<TMP_Text>();
            }
            if (noticeTitle) noticeTitle.text = title;
            if (!playErrorLabel && playNotice)
            {
                Transform body = playNotice.transform.Find(
                    "Notice Window/Notice Body");
                if (body) playErrorLabel = body.GetComponent<TMP_Text>();
            }
            if (playErrorLabel) playErrorLabel.text = message;
            if (playNotice) playNotice.SetActive(true);
            Button closeButton = playNotice ? playNotice.GetComponentInChildren<Button>(true) : null;
            if (closeButton)
            {
                closeButton.navigation = new Navigation { mode = Navigation.Mode.None };
                EventSystem.current?.SetSelectedGameObject(closeButton.gameObject);
            }
        }
        public void CloseNotice()
        {
            if (playNotice) playNotice.SetActive(false);
            ReleaseNoticeFocus();
            if (selected >= 0 && selected < tracks.Length &&
                tracks[selected].gameObject.activeInHierarchy)
                StartPreview(tracks[selected]);
        }

        private void ReleaseNoticeFocus()
        {
            foreach (Selectable control in noticeBlockedControls)
                if (control) control.interactable = true;
            noticeBlockedControls.Clear();
            if (noticePreviousSelection && noticePreviousSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(noticePreviousSelection);
            noticePreviousSelection = null;
        }
        public void Back()
        {
            if (IsNoticeOpen) return;
            if (AppRoot.Current && AppRoot.Current.IsTransitioning) return;
            AppRoot.NavigateToScene("Home");
        }

        private void StartPreview(MusicTrackRow track)
        {
            StopPreview();
            if (IsNoticeOpen) return;
            presentedPreview = track.PreviewAudio;
            if (!previewAudioSource || !track.PreviewAudio) return;
            AudioClip clip = track.PreviewAudio;
            if (clip.samples <= 0) return;
            previewTargetVolume = track.PreviewVolume * (AppRoot.Current
                ? AppRoot.Current.MusicGain : 1f);
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
            presentedPreview = null;
            if (!previewAudioSource) return;
            previewAudioSource.Stop();
            previewAudioSource.clip = null;
            previewAudioSource.volume = 0f;
            previewTargetVolume = 0f;
            previewStartSample = previewEndSample = 0;
        }

        private void SetArtwork(MusicTrackRow track)
        {
            if (selectedArtwork)
            {
                selectedArtwork.sprite = track?.Artwork;
                selectedArtwork.enabled = track?.Artwork != null;
            }
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
            if (row.HasSongError || !row.Access.IsVisible) return false;
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
            foreach (MusicTrackRow row in tracks) row.RefreshAccess();
            ApplyAvailableDifficulties();
            if (AppRoot.Current)
                foreach (MusicTrackRow row in tracks)
                {
                    bool hasRecord = AppRoot.Current.TryGetBestRecord(
                        row.MusicId, row.DifficultyId,
                        out double score, out _);
                    row.ApplyPlayerData(
                        AppRoot.Current.IsFavorite(row.MusicId),
                        hasRecord, score);
                }
            int first = BuildVisibleOrder();
            for (int attempts = 0; first >= 0 &&
                 !visibleOrder.Contains(selected) &&
                 attempts < tracks.Length; attempts++)
            {
                selected = first;
                SelectUnlockedDifficultyIfNeeded();
                ApplyAvailableDifficulties();
                first = BuildVisibleOrder();
            }
            if (first < 0 || !visibleOrder.Contains(selected))
                selected = -1;
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
                    rect.anchoredPosition = rowBasePosition + Vector2.up *
                        (distance * rowSpacing);
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
                selectedBestScore.text = "0";
                SetArtwork(null);
            }
            else
            {
                if (!tracks[selected].gameObject.activeSelf) selected = first;
                var track = tracks[selected];
                if (track.Access.IsMasked)
                {
                    if (jacketTransition != null) StopCoroutine(jacketTransition);
                    jacketTransition = null;
                    SetArtwork(track);
                }
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
                selectedBestScore.text = track.BestScore;
                if (!(playNotice && playNotice.activeSelf) &&
                    presentedPreview != track.PreviewAudio)
                    StartPreview(track);
            }
            for (int i = 0; i < tracks.Length; i++)
                tracks[i].Display(i == selected && tracks[i].gameObject.activeSelf);
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                Image categoryImage = categoryButtons[i].GetComponent<Image>();
                if (categoryNormalSprite != null && categorySelectedSprite != null)
                {
                    categoryImage.sprite = i == category ? categorySelectedSprite : categoryNormalSprite;
                    categoryImage.color = Color.white;
                }
                else categoryImage.color = i == category ? optionSelected : optionNormal;
                if (categoryIndicators != null && i < categoryIndicators.Length && categoryIndicators[i] != null)
                    categoryIndicators[i].SetActive(i == category);
            }
            for (int i = 0; difficultySlots != null && i < difficultySlots.Length; i++)
            {
                DifficultySlot slot = difficultySlots[i];
                if (slot == null) continue;
                string id = first >= 0
                    ? MusicDifficultySlots.GetId(tracks[selected].Difficulties, i) : null;
                int chartLevel = 0;
                bool available = id != null && tracks[selected].TryGetLevel(id, out chartLevel);
                if (slot.button) slot.button.interactable = available;
                if (slot.level)
                    slot.level.text = available && chartLevel > 0 ? chartLevel.ToString() : "-";
                if (slot.caption)
                    slot.caption.text = available
                        ? tracks[selected].FindDifficulty(id).DisplayName +
                          (tracks[selected].GetDifficultyAccess(id).NeedsUnlock
                              ? " · 잠김" : string.Empty)
                        : id?.ToUpperInvariant() ?? "—";
                if (slot.ring)
                {
                    if (difficultyNormalSprite && difficultySelectedSprite)
                        slot.ring.sprite = available && i == selectedDifficulty
                            ? difficultySelectedSprite : difficultyNormalSprite;
                    slot.ring.color = available ? Color.white : new Color(1f, 1f, 1f, 0.28f);
                }
            }
            sortLabel.text = "R  정렬: " + Sorts[sort];
            filterLabel.text = "T  난이도: " + Filters[levelFilter];
        }
    }
}
