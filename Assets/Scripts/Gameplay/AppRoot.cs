using System;
using REmind.Common.UI;
using REmind.Gameplay.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REmind.Gameplay
{
    /// <summary>Game-wide navigation context. Gameplay sessions remain scene-owned.</summary>
    [DisallowMultipleComponent]
    public sealed class AppRoot : MonoBehaviour
    {
        public readonly struct SongSelection
        {
            public string MusicId { get; }
            public string DifficultyId { get; }

            public SongSelection(string musicId, string difficultyId)
            {
                MusicId = musicId;
                DifficultyId = difficultyId;
            }
        }

        // Only the root reference is static. Navigation payloads belong to this instance.
        public static AppRoot Current { get; private set; }

        private SongSelection selection;
        private bool hasSelection;
        private GameResultSnapshot pendingResult;
        private LocalPlayerDataStore playerData;
        private LocalGameSettingsStore settings;
        private bool hasFocus = true;
        private bool applicationPaused;
        private bool voicePlaybackActive;
        [SerializeField] private SceneTransitionController sceneTransition;
        [SerializeField] private SettingsMenuController settingsOverlay;
        [SerializeField] private bool autoEnterHomeOnStart = true;
        private bool duplicate;

        public bool LastResultIsNewRecord { get; private set; }
        public bool LastResultWasFirstClear { get; private set; }
        public int MemoryFragmentCount => playerData.MemoryFragmentCount;
        public LocalGameSettingsStore Settings => settings;
        public float MusicGain => settings.MusicVolume *
            (voicePlaybackActive && settings.ReduceBgmDuringVoice ? 0.4f : 1f);
        public bool IsTransitioning => sceneTransition &&
            sceneTransition.IsTransitioning;

        public bool TryStartSelectedGame()
        {
            if (!hasSelection || !sceneTransition ||
                sceneTransition.IsTransitioning) return false;
            return sceneTransition.BeginMusicSelected();
        }

        public bool TryTransitionToScene(string sceneName) =>
            sceneTransition && sceneTransition.TransitionTo(sceneName);

        public bool TryOpenSettings(MenuNavigationController navigation)
        {
            if (duplicate || IsTransitioning || !settingsOverlay || !navigation)
                return false;
            if (settingsOverlay.IsOverlayOpen) return true;
            settingsOverlay.OpenOverlay(navigation);
            return settingsOverlay.IsOverlayOpen;
        }

        /// <summary>Called when Bootstrap has finished presenting its logo and loading its startup data.</summary>
        public void CompleteBootstrapLoading()
        {
            if (duplicate || SceneManager.GetActiveScene().name != "Bootstrap" ||
                IsTransitioning) return;
            if (!TryTransitionToScene("Home"))
                Debug.LogError("Bootstrap scene transition is not configured.", this);
        }

        public static bool NavigateToScene(string sceneName)
        {
            if (Current) return Current.TryTransitionToScene(sceneName);
            if (string.IsNullOrEmpty(sceneName) ||
                !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
            SceneManager.LoadScene(sceneName);
            return true;
        }

        public void SetMusicVolume(float value)
        {
            settings.SetMusicVolume(value);
            SaveSettings();
        }

        public void SetVoicePlaybackActive(bool active)
        {
            voicePlaybackActive = active;
        }

        public void ApplyAudioPreferences()
        {
            if (settings == null) return;
            AudioListener.volume =
                (!hasFocus && settings.MuteWhenUnfocused) ||
                (applicationPaused && !settings.KeepAudioInBackground)
                    ? 0f : settings.MasterVolume;
        }

        public void SetJudgementOffsetMs(double value)
        {
            settings.SetJudgementOffsetMs(value);
            SaveSettings();
        }

        public void SetLaneBinding(int lane, string path)
        {
            settings.SetLaneBinding(lane, path);
            SaveSettings();
        }

        public void ResetLaneBindings()
        {
            settings.ResetBindings();
            SaveSettings();
        }

        public bool TryGetSelectedSong(out SongSelection selectedSong)
        {
            selectedSong = selection;
            return hasSelection;
        }

        public void SelectSong(string musicId, string difficultyId)
        {
            if (string.IsNullOrWhiteSpace(musicId))
                throw new ArgumentException("A selected song needs a music ID.", nameof(musicId));
            if (string.IsNullOrWhiteSpace(difficultyId))
                throw new ArgumentException("A selected song needs a difficulty ID.", nameof(difficultyId));

            selection = new SongSelection(musicId, difficultyId);
            hasSelection = true;
            pendingResult = null;
            LastResultIsNewRecord = false;
            LastResultWasFirstClear = false;
        }

        public void PublishResult(GameResultSnapshot result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!hasSelection ||
                !string.Equals(selection.MusicId, result.MusicId, StringComparison.Ordinal) ||
                !string.Equals(selection.DifficultyId, result.DifficultyId, StringComparison.Ordinal))
                throw new InvalidOperationException("Result IDs do not match the selected song.");
            pendingResult = result;
            if (result.IsAutoPlay)
            {
                LastResultIsNewRecord = false;
                LastResultWasFirstClear = false;
                return;
            }
            bool previouslyCleared = playerData.TryGetProgress(result.MusicId,
                result.DifficultyId, out ChartProgressSnapshot previous) &&
                previous.Cleared;
            LastResultWasFirstClear = result.IsCleared && !previouslyCleared;
            LastResultIsNewRecord = playerData.RecordResult(result);
            try { playerData.Save(); }
            catch (Exception exception)
            {
                Debug.LogError("Could not save local player data: " +
                    exception.Message, this);
            }
        }

        public bool TryGetBestRecord(string musicId, string difficultyId,
            out double score, out RankGrade rank) =>
            playerData.TryGetBestRecord(musicId, difficultyId, out score,
                out rank);

        public bool TryGetChartProgress(string musicId, string difficultyId,
            out ChartProgressSnapshot progress) =>
            playerData.TryGetProgress(musicId, difficultyId, out progress);

        public bool IsFavorite(string musicId) => playerData.IsFavorite(musicId);

        public void SetFavorite(string musicId, bool favorite)
        {
            playerData.SetFavorite(musicId, favorite);
            try { playerData.Save(); }
            catch (Exception exception)
            {
                Debug.LogError("Could not save local player data: " +
                    exception.Message, this);
            }
        }

        public bool TryTakeResult(out GameResultSnapshot result)
        {
            result = pendingResult;
            pendingResult = null;
            return result != null;
        }

        private void Awake()
        {
            if (Current && Current != this)
            {
                duplicate = true;
                Destroy(gameObject);
                return;
            }

            Current = this;
            DontDestroyOnLoad(gameObject);
            playerData = LocalPlayerDataStore.Load(
                System.IO.Path.Combine(Application.persistentDataPath,
                    "player-progress-v1.json"));
            settings = LocalGameSettingsStore.Load(
                System.IO.Path.Combine(Application.persistentDataPath,
                    "player-settings-v1.json"));
            ApplyAudioPreferences();
        }

        private void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            ApplyAudioPreferences();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            ApplyAudioPreferences();
        }

        private void SaveSettings()
        {
            try { settings.Save(); }
            catch (Exception exception)
            {
                Debug.LogError("Could not save local player settings: " +
                    exception.Message, this);
            }
        }

        private void Start()
        {
            if (autoEnterHomeOnStart) CompleteBootstrapLoading();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Current, this)) Current = null;
        }
    }
}
