using System;
using REmind.Charting;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Input.Judgement;
using REmind.Presentation;
using UnityEngine;

namespace REmind.Gameplay.Chart
{
    /// <summary>
    /// DemoPlay integration composition root. Publishes one fully prepared chart
    /// to judgement, session state and Effect playback. This validates the future
    /// runtime boundary; it is not the final Game scene or content loader.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayChartSessionController : MonoBehaviour
    {
        [Header("Bundled Runtime Package")]
        [SerializeField] private TextAsset chartAsset;

        [Header("Bundled Song")]
        [SerializeField] private string bundledMusicId;
        [SerializeField] private AudioClip bundledSong;
        [SerializeField, Range(0f, 1f)] private float bundledSongVolume = 1f;

        [Header("Systems")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private GameplayChartEffectController effectController;
        [SerializeField] private GameplaySessionState sessionState;
        [SerializeField] private LaneHitEffectPlayer laneHitEffectPlayer;

        private bool playbackGuardBound;
        private bool hitEffectBound;

        public PreparedGameplayChart CurrentChart { get; private set; }
        public string CurrentSongTitle { get; private set; }
        public string LastError { get; private set; }
        public bool IsPrepared => CurrentChart != null;

        public bool TryPrepareConfiguredChart()
        {
            if (!ResolveReferences(out string referenceError))
            {
                return Fail(referenceError);
            }

            if (!chartAsset)
            {
                return Fail("Gameplay chart asset is not assigned.");
            }

            if (!bundledSong || string.IsNullOrWhiteSpace(bundledMusicId))
            {
                return Fail(
                    "Bundled gameplay requires a music ID and AudioClip.");
            }

            return TryPrepareInternal(
                chartAsset.text,
                validateBundledSong: true);
        }

        public bool TryPrepare(string runtimePackageJson)
        {
            return TryPrepareInternal(
                runtimePackageJson,
                validateBundledSong: false);
        }

        public bool TryPrepareSelectedChart(MusicCatalog catalog,
            string musicId, string difficultyId)
        {
            if (!catalog || string.IsNullOrWhiteSpace(musicId) ||
                string.IsNullOrWhiteSpace(difficultyId))
                return Fail("A song and difficulty must be selected.");

            MusicCatalogEntry entry = catalog.FindSong(musicId);
            MusicDifficultyEntry difficulty = entry?.FindDifficulty(difficultyId);
            if (difficulty == null || !entry.SongData || !entry.AudioClip ||
                !difficulty.RuntimePackage)
                return Fail("Selected song content is unavailable: " +
                    musicId + "/" + difficultyId);

            SongContent song;
            try
            {
                song = SongContentCodec.Parse(entry.SongData.text);
            }
            catch (FormatException exception)
            {
                return Fail("Selected song data is invalid: " + exception.Message);
            }
            if (!string.Equals(song.MusicId, musicId, StringComparison.Ordinal) ||
                song.FindChart(difficultyId) == null)
                return Fail("Selected song IDs do not match its catalog entry.");

            return TryPrepareInternal(difficulty.RuntimePackage.text,
                validateBundledSong: false, expectedMusicId: musicId,
                expectedDifficultyId: difficultyId, selectedSong: entry.AudioClip,
                selectedVolume: song.PlaybackVolume, selectedTitle: song.Title);
        }

        private bool TryPrepareInternal(
            string runtimePackageJson,
            bool validateBundledSong,
            string expectedMusicId = null,
            string expectedDifficultyId = null,
            AudioClip selectedSong = null,
            float selectedVolume = 1f,
            string selectedTitle = null)
        {
            if (!ResolveReferences(out string referenceError))
            {
                return Fail(referenceError);
            }

            if (gameManager.PlaybackState == PlaybackState.Playing ||
                gameManager.PlaybackState == PlaybackState.Paused ||
                gameManager.GamePlay.IsStartInProgress)
            {
                return Fail(
                    "Stop gameplay before replacing the prepared chart.");
            }

            PreparedGameplayChart prepared;
            try
            {
                prepared = GameplayChartPreparation.Prepare(
                    runtimePackageJson);
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }

            if (expectedMusicId != null &&
                (!string.Equals(prepared.Metadata.MusicId, expectedMusicId,
                    StringComparison.Ordinal) ||
                 !string.Equals(prepared.Metadata.DifficultyId,
                    expectedDifficultyId, StringComparison.Ordinal)))
                return Fail("Runtime package IDs do not match the selected song and difficulty.");

            if (validateBundledSong)
            {
                string bundledExpectedMusicId = bundledMusicId.Trim();
                if (!string.Equals(
                        prepared.Metadata.MusicId,
                        bundledExpectedMusicId,
                        StringComparison.Ordinal))
                {
                    return Fail(
                        $"Bundled song ID '{bundledExpectedMusicId}' does not match " +
                        $"chart musicId '{prepared.Metadata.MusicId}'.");
                }

                // Preparing even the same reference reapplies volume and rejects
                // a clip whose audio data has entered a failed state.
                if (!gameManager.SetAudioSource(
                        bundledSong,
                        bundledSongVolume * (AppRoot.Current
                            ? AppRoot.Current.Settings.MusicVolume : 1f)))
                {
                    return Fail(
                        $"Could not prepare the AudioClip for '{bundledExpectedMusicId}'.");
                }
            }
            else if (selectedSong &&
                     !gameManager.SetAudioSource(selectedSong,
                         selectedVolume * (AppRoot.Current
                             ? AppRoot.Current.Settings.MusicVolume : 1f)))
                return Fail("Could not prepare the selected AudioClip for '" +
                    expectedMusicId + "'.");

            // Static parsing and bundled-song checks leave the previous prepared
            // chart intact. From this point onward a replacement owns the live
            // judgement/state services, so any failure deliberately invalidates it.
            string invalidationError = InvalidateLivePreparation();
            if (invalidationError != null)
            {
                return Fail(
                    "Could not invalidate the previous gameplay chart: " +
                    invalidationError);
            }

            if (!sessionState.TryConfigureChart(
                    prepared.Snapshot.Notes.Count +
                        prepared.Snapshot.JudgementSegments.Count -
                        CountLongNotes(prepared.Snapshot),
                    out string stateError))
            {
                return FailInvalidating(stateError);
            }

            if (AppRoot.Current)
                judgementSystem.SetUserOffsetMs(
                    AppRoot.Current.Settings.JudgementOffsetMs);
            if (!judgementSystem.Initialize(
                    prepared.Snapshot,
                    prepared.ChartOffsetMs))
            {
                return FailInvalidating(
                    "The prepared Snapshot could not initialize judgement.");
            }

            double lastJudgementMs = 0d;
            foreach (JudgementTarget target in
                prepared.Snapshot.JudgementTargets)
                lastJudgementMs = Math.Max(lastJudgementMs,
                    target.TargetTimeMs + prepared.ChartOffsetMs);
            if (gameManager.GamePlay.CurrentSong != null &&
                !gameManager.GamePlay.SetCompletionTimeMs(
                    lastJudgementMs + 1000d))
                return FailInvalidating(
                    "Could not extend playback through the final chart note.");

            effectController.GameState = sessionState;
            if (!effectController.Prepare(
                    prepared.EffectPlan,
                    prepared.Metadata.MusicId,
                    prepared.Metadata.DifficultyId,
                    prepared.ChartOffsetMs))
            {
                string effectError = effectController.LastError ??
                    "Effect gameplay preparation failed.";
                return FailInvalidating(effectError);
            }

            CurrentChart = prepared;
            CurrentSongTitle = selectedTitle;
            LastError = null;
            return true;
        }

        private void OnEnable()
        {
            ResolveReferences(out _);
            if (judgementSystem && laneHitEffectPlayer && !hitEffectBound)
            {
                judgementSystem.NoteJudged += HandleNoteJudged;
                hitEffectBound = true;
            }
        }

        private void OnDisable()
        {
            UnbindHitEffect();
            // Disabling the chart owner is a session boundary. Keep the start
            // guard subscribed (until destruction) so a stale Effect plan can
            // never start while this owner is disabled.
            CurrentChart = null;
            CurrentSongTitle = null;
            if (gameManager &&
                (gameManager.PlaybackState == PlaybackState.Playing ||
                 gameManager.PlaybackState == PlaybackState.Paused ||
                 gameManager.GamePlay.IsStartInProgress))
            {
                gameManager.StopGame();
            }
            effectController?.ClearPreparation();
            judgementSystem?.ClearInitialization();
            sessionState?.ClearPreparedChart();
        }

        private void OnDestroy()
        {
            UnbindHitEffect();
            if (playbackGuardBound && gameManager && gameManager.GamePlay)
            {
                gameManager.GamePlay.PlaybackStarting -=
                    HandlePlaybackStarting;
            }
            playbackGuardBound = false;
        }

        private void HandleNoteJudged(NoteJudgementEvent judgementEvent)
        {
            if (!judgementEvent.IsAutomaticMiss && laneHitEffectPlayer)
                laneHitEffectPlayer.Play(judgementEvent.Lane);
        }

        private static int CountLongNotes(PlayableChartSnapshot snapshot)
        {
            int count = 0;
            foreach (PlayableNoteSnapshot note in snapshot.Notes)
                if (note.Points.Count > 1) count++;
            return count;
        }

        private void UnbindHitEffect()
        {
            if (!hitEffectBound) return;
            if (judgementSystem)
                judgementSystem.NoteJudged -= HandleNoteJudged;
            hitEffectBound = false;
        }

        private bool ResolveReferences(out string error)
        {
            if (!gameManager)
            {
                gameManager = GetComponent<GameManager>() ??
                    GetComponentInParent<GameManager>();
            }
            if (!judgementSystem)
            {
                judgementSystem = GetComponentInChildren<
                    NoteJudgementSystem>(true);
            }
            if (!effectController)
            {
                effectController = GetComponent<
                    GameplayChartEffectController>();
            }
            if (!sessionState)
            {
                sessionState = GetComponent<GameplaySessionState>();
            }

            if (!gameManager || !gameManager.GamePlay ||
                !judgementSystem || !effectController || !sessionState)
            {
                error = "Gameplay chart preparation requires GameManager, " +
                    "GamePlay, NoteJudgementSystem, GameplaySessionState and " +
                    "GameplayChartEffectController references.";
                return false;
            }

            BindPlaybackGuard();

            error = null;
            return true;
        }

        private void BindPlaybackGuard()
        {
            if (playbackGuardBound || !gameManager || !gameManager.GamePlay)
            {
                return;
            }

            gameManager.GamePlay.PlaybackStarting += HandlePlaybackStarting;
            playbackGuardBound = true;
        }

        private bool HandlePlaybackStarting(double songTimeMs)
        {
            return isActiveAndEnabled && CurrentChart != null &&
                gameManager && gameManager.isActiveAndEnabled &&
                gameManager.GamePlay &&
                gameManager.GamePlay.isActiveAndEnabled &&
                judgementSystem && judgementSystem.isActiveAndEnabled &&
                judgementSystem.IsInitialized &&
                effectController && effectController.isActiveAndEnabled &&
                effectController.IsPrepared &&
                sessionState && sessionState.isActiveAndEnabled &&
                sessionState.IsReadyForPlayback;
        }

        private string InvalidateLivePreparation()
        {
            CurrentChart = null;
            CurrentSongTitle = null;
            string effectError = null;
            if (effectController && !effectController.ClearPreparation())
            {
                effectError = effectController.LastError ??
                    "Effect preparation cleanup failed.";
            }

            judgementSystem?.ClearInitialization();
            sessionState?.ClearPreparedChart();
            return effectError;
        }

        private bool FailInvalidating(string error)
        {
            string invalidationError = InvalidateLivePreparation();
            if (invalidationError != null)
            {
                error = (string.IsNullOrWhiteSpace(error)
                    ? "Gameplay chart preparation failed."
                    : error.Trim()) + " Cleanup also failed: " +
                    invalidationError;
            }
            return Fail(error);
        }

        private bool Fail(string error)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Gameplay chart preparation failed."
                : error.Trim();
            Debug.LogError(LastError, this);
            return false;
        }
    }
}
