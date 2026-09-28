using System;
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
        [SerializeField] private SceneTransitionController sceneTransition;
        private bool duplicate;

        public bool LastResultIsNewRecord { get; private set; }
        public bool LastResultWasFirstClear { get; private set; }
        public int MemoryFragmentCount => playerData.MemoryFragmentCount;
        public LocalGameSettingsStore Settings => settings;
        public bool IsTransitioning => sceneTransition &&
            sceneTransition.IsTransitioning;

        public bool TryStartSelectedGame()
        {
            if (!hasSelection || !sceneTransition ||
                sceneTransition.IsTransitioning) return false;
            return sceneTransition.BeginMusicSelected();
        }

        public void SetMusicVolume(float value)
        {
            settings.SetMusicVolume(value);
            SaveSettings();
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
            if (!duplicate && SceneManager.GetActiveScene().name == "Bootstrap")
                SceneManager.LoadScene("Home");
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Current, this)) Current = null;
        }
    }
}
