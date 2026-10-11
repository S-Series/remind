using System;
using REmind.Charting;
using REmind.Common.UI;
using REmind.Gameplay.Characters;
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
        private CharacterAbilityDefinition selectedAbility;
        private GameRuleConfig selectedGaugeRule;
        private string selectedUnlockTargetMusicId;
        private GameAttemptStartSnapshot activeAttempt;
        private GameResultSnapshot pendingResult;
        private LocalPlayerDataStore playerData;
        private LocalGameSettingsStore settings;
        private bool hasFocus = true;
        private bool applicationPaused;
        private bool voicePlaybackActive;
        [SerializeField] private SceneTransitionController sceneTransition;
        [SerializeField] private SettingsMenuController settingsOverlay;
        [SerializeField] private CharacterOverlayController characterOverlay;
        [SerializeField] private CharacterCatalog characterCatalog;
        [SerializeField] private MusicUnlockLibrary musicUnlockLibrary;
        [SerializeField] private bool autoEnterHomeOnStart = true;
        private bool duplicate;
        private string playerDataError;

        public bool LastResultIsNewRecord { get; private set; }
        public bool LastResultWasFirstClear { get; private set; }
        public int MemoryFragmentCount => playerData?.MemoryFragmentCount ?? 0;
        public string PlayerDataError => playerDataError;
        public string EquippedCharacterId => playerData?.EquippedCharacterId;
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
            if (duplicate || IsTransitioning || !settingsOverlay || !navigation ||
                characterOverlay && characterOverlay.IsOverlayOpen)
                return false;
            if (settingsOverlay.IsOverlayOpen) return true;
            settingsOverlay.OpenOverlay(navigation);
            return settingsOverlay.IsOverlayOpen;
        }

        public bool TryOpenCharacter(MenuNavigationController navigation)
        {
            if (duplicate || IsTransitioning || !characterOverlay || !navigation ||
                settingsOverlay && settingsOverlay.IsOverlayOpen)
                return false;
            if (characterOverlay.IsOverlayOpen) return true;
            characterOverlay.OpenOverlay(navigation);
            return characterOverlay.IsOverlayOpen;
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
            // Explicit no-character path for fixtures and legacy diagnostics.
            SelectSong(musicId, difficultyId, null, null);
        }

        /// <summary>The product selection path freezes the equipped character.</summary>
        public bool TrySelectSongWithEquippedCharacter(string musicId,
            string difficultyId, out string error)
        {
            if (musicUnlockLibrary)
            {
                if (playerData == null || !string.IsNullOrEmpty(playerDataError))
                {
                    error = string.IsNullOrEmpty(playerDataError)
                        ? "플레이어 데이터를 준비할 수 없습니다." : playerDataError;
                    return false;
                }
                SongUnlockChallengeDefinition challenge;
                try
                {
                    if (!musicUnlockLibrary.TrySelectChallenge(musicId, difficultyId,
                        playerData.IsSongDifficultyUnlocked, out challenge, out error))
                        return false;
                }
                catch (Exception exception)
                {
                    Debug.LogError("Music unlock library is invalid: " + exception, this);
                    error = "해금 도전 규칙을 확인할 수 없습니다.";
                    return false;
                }
                if (challenge)
                {
                    SelectSong(musicId, difficultyId, null,
                        challenge.SpecialHardRule);
                    selectedUnlockTargetMusicId = challenge.TargetMusicId;
                    error = null;
                    return true;
                }
            }
            if (!TryGetEquippedCharacter(out CharacterAbilityDefinition ability,
                out GameRuleConfig gaugeRule, out error)) return false;
            SelectSong(musicId, difficultyId, ability, gaugeRule);
            return true;
        }

        public bool IsCharacterOwned(string characterId) =>
            playerData != null && playerData.OwnsCharacter(characterId);

        public SongAccessPolicy GetSongAccess(SongContent song) =>
            new SongAccessPolicy(song, playerData?.OwnsSong(song.MusicId) ?? false,
                playerData?.HasAnyUnlockedDifficulty(song.MusicId) ?? false);

        public SongAccessPolicy GetSongAccess(SongContent song,
            string difficultyId) =>
            new SongAccessPolicy(song, playerData?.OwnsSong(song.MusicId) ?? false,
                playerData?.HasAnyUnlockedDifficulty(song.MusicId) ?? false,
                playerData?.IsSongDifficultyUnlocked(song.MusicId,
                    difficultyId) ?? false);

        public bool TryGetCharacterEntry(string characterId,
            out CharacterDefinitionAsset entry)
        {
            entry = null;
            return playerData != null && string.IsNullOrEmpty(playerDataError) &&
                characterCatalog && characterCatalog.TryGetEntry(characterId,
                    out entry);
        }

        public bool TryGetCharacterAbility(string characterId,
            out CharacterAbilityDefinition ability,
            out GameRuleConfig gaugeRule)
        {
            ability = null;
            gaugeRule = null;
            return playerData != null && string.IsNullOrEmpty(playerDataError) &&
                characterCatalog && characterCatalog.TryResolve(characterId,
                    out ability, out gaugeRule);
        }

        public bool TryGetCharacterEntryAt(int index,
            out CharacterDefinitionAsset entry)
        {
            entry = null;
            if (playerData == null || !characterCatalog ||
                !string.IsNullOrEmpty(playerDataError) ||
                index < 0 || index >= characterCatalog.Entries.Count)
                return false;
            entry = characterCatalog.Entries[index];
            return entry;
        }

        public bool TryGetEquippedCharacter(
            out CharacterAbilityDefinition ability,
            out GameRuleConfig gaugeRule, out string error)
        {
            ability = null;
            gaugeRule = null;
            error = playerDataError;
            if (playerData == null || !characterCatalog ||
                !string.IsNullOrEmpty(playerDataError))
            {
                if (string.IsNullOrEmpty(error))
                    error = "캐릭터 데이터를 준비할 수 없습니다.";
                return false;
            }
            string characterId = playerData.EquippedCharacterId;
            if (!playerData.OwnsCharacter(characterId) ||
                !characterCatalog.TryResolve(characterId, out ability,
                    out gaugeRule))
            {
                error = "장착한 캐릭터를 확인할 수 없습니다.";
                return false;
            }
            try { ability.FreezeForStart(gaugeRule); }
            catch (Exception exception)
            {
                Debug.LogError("Equipped character is invalid: " +
                    exception, this);
                error = "장착한 캐릭터의 데이터를 확인할 수 없습니다.";
                return false;
            }
            error = null;
            return true;
        }

        public bool TryEquipCharacter(string characterId, out string error)
        {
            error = playerDataError;
            if (playerData == null || !characterCatalog ||
                !string.IsNullOrEmpty(playerDataError))
            {
                if (string.IsNullOrEmpty(error))
                    error = "캐릭터 데이터를 준비할 수 없습니다.";
                return false;
            }
            if (!playerData.OwnsCharacter(characterId))
            {
                error = "아직 보유하지 않은 캐릭터입니다.";
                return false;
            }
            try
            {
                if (!characterCatalog.TryResolve(characterId,
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig gaugeRule))
                    throw new InvalidOperationException(
                        "The selected character is unavailable.");
                ability.FreezeForStart(gaugeRule);
                playerData.EquipCharacter(characterId);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not equip character: " + exception,
                    this);
                error = "캐릭터 장착을 저장하지 못했습니다. 다시 시도해주세요.";
                return false;
            }
        }

        /// <summary>
        /// Explicit play conditions for the next start. The rule asset is only
        /// input; the Game scene freezes it before the attempt begins.
        /// </summary>
        public void SelectSong(string musicId, string difficultyId,
            CharacterAbilityDefinition ability, GameRuleConfig selectedRule)
        {
            if (string.IsNullOrWhiteSpace(musicId))
                throw new ArgumentException("A selected song needs a music ID.", nameof(musicId));
            if (string.IsNullOrWhiteSpace(difficultyId))
                throw new ArgumentException("A selected song needs a difficulty ID.", nameof(difficultyId));
            if (ability != null && !selectedRule)
                throw new ArgumentException(
                    "A selected character needs a complete gauge rule.",
                    nameof(selectedRule));

            selection = new SongSelection(musicId, difficultyId);
            hasSelection = true;
            selectedAbility = ability;
            selectedGaugeRule = selectedRule;
            selectedUnlockTargetMusicId = null;
            pendingResult = null;
            LastResultIsNewRecord = false;
            LastResultWasFirstClear = false;
        }

        public bool TryGetSelectedPlayConditions(
            out CharacterAbilityDefinition ability,
            out GameRuleConfig selectedRule)
        {
            ability = selectedAbility;
            selectedRule = selectedGaugeRule;
            return hasSelection;
        }

        public bool TryGetSelectedPlayConditions(
            out CharacterAbilityDefinition ability,
            out GameRuleConfig selectedRule,
            out string unlockTargetMusicId)
        {
            bool selected = TryGetSelectedPlayConditions(out ability,
                out selectedRule);
            unlockTargetMusicId = selected ? selectedUnlockTargetMusicId : null;
            return selected;
        }

        public void ActivateAttempt(GameAttemptStartSnapshot start)
        {
            activeAttempt = start ?? throw new ArgumentNullException(nameof(start));
        }

        public void EndAttempt(Guid attemptId)
        {
            if (activeAttempt != null && activeAttempt.AttemptId == attemptId)
                activeAttempt = null;
        }

        public void PublishResult(GameResultSnapshot result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (playerData == null || !string.IsNullOrEmpty(playerDataError))
                throw new InvalidOperationException(
                    "Player data is unavailable; a result cannot be saved.");
            bool matchesAttempt = activeAttempt != null &&
                ReferenceEquals(activeAttempt, result.AttemptStart) &&
                string.Equals(activeAttempt.MusicId, result.MusicId,
                    StringComparison.Ordinal) &&
                string.Equals(activeAttempt.DifficultyId, result.DifficultyId,
                    StringComparison.Ordinal);
            bool matchesLegacySelection = activeAttempt == null &&
                result.AttemptStart == null && hasSelection &&
                string.Equals(selection.MusicId, result.MusicId,
                    StringComparison.Ordinal) &&
                string.Equals(selection.DifficultyId, result.DifficultyId,
                    StringComparison.Ordinal);
            if (!matchesAttempt && !matchesLegacySelection)
                throw new InvalidOperationException("Result IDs do not match the selected song.");
            if (result.IsAutoPlay)
            {
                activeAttempt = null;
                pendingResult = result;
                LastResultIsNewRecord = false;
                LastResultWasFirstClear = false;
                return;
            }
            bool previouslyCleared = playerData.TryGetProgress(result.MusicId,
                result.DifficultyId, out ChartProgressSnapshot previous) &&
                previous.Cleared;
            bool newRecord = playerData.RecordAndSaveResult(result,
                result.AttemptStart?.UnlockTargetMusicId);
            activeAttempt = null;
            pendingResult = result;
            LastResultWasFirstClear = result.IsCleared && !result.IsFailed &&
                !previouslyCleared;
            LastResultIsNewRecord = newRecord;
        }

        public bool TryGetBestRecord(string musicId, string difficultyId,
            out double score, out RankGrade rank)
        {
            score = 0d;
            rank = RankGrade.D;
            return playerData != null && playerData.TryGetBestRecord(musicId,
                difficultyId, out score, out rank);
        }

        public bool TryGetChartProgress(string musicId, string difficultyId,
            out ChartProgressSnapshot progress)
        {
            progress = default;
            return playerData != null && playerData.TryGetProgress(musicId,
                difficultyId, out progress);
        }

        public bool IsFavorite(string musicId) =>
            playerData != null && playerData.IsFavorite(musicId);

        public void SetFavorite(string musicId, bool favorite)
        {
            if (playerData == null || !string.IsNullOrEmpty(playerDataError))
                throw new InvalidOperationException(
                    "Player data is unavailable; favorites cannot be saved.");
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
            try
            {
                playerData = LocalPlayerDataStore.Load(
                    System.IO.Path.Combine(Application.persistentDataPath,
                        "player-progress-v1.json"));
                if (!characterCatalog)
                    throw new InvalidOperationException(
                        "Bootstrap AppRoot has no character catalog.");
                characterCatalog.ValidateForUse();
                string starterId = characterCatalog.DefaultCharacterId;
                if (!characterCatalog.TryResolve(starterId,
                    out CharacterAbilityDefinition starter,
                    out GameRuleConfig starterRule))
                    throw new InvalidOperationException(
                        "The starter character is missing.");
                starter.FreezeForStart(starterRule);
                playerData.EnsureStarterCharacter(starterId,
                    id => characterCatalog.TryResolve(id, out _, out _));
            }
            catch (Exception exception)
            {
                playerDataError =
                    "플레이어 또는 기본 캐릭터 데이터를 불러오지 못했습니다.";
                Debug.LogError("Player startup data failed: " +
                    exception, this);
            }
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
