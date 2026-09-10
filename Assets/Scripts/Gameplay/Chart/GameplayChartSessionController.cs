using System;
using REmind.Charting;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Input.Judgement;
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
        [Header("Bundled Chart")]
        [SerializeField] private TextAsset chartAsset;
        [SerializeField] private TextAsset effectParameterAsset;
        [SerializeField, Min(1)] private int beatsPerMeasure = 4;

        [Header("Bundled Song")]
        [SerializeField] private string bundledMusicId;
        [SerializeField] private AudioClip bundledSong;
        [SerializeField, Range(0f, 1f)] private float bundledSongVolume = 1f;

        [Header("Systems")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private GameplayChartEffectController effectController;
        [SerializeField] private GameplaySessionState sessionState;

        private bool playbackGuardBound;

        public PreparedGameplayChart CurrentChart { get; private set; }
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
                effectParameterAsset ? effectParameterAsset.text : null,
                validateBundledSong: true);
        }

        public bool TryPrepare(
            string chartJson,
            string effectParameterJson)
        {
            return TryPrepareInternal(
                chartJson,
                effectParameterJson,
                validateBundledSong: false);
        }

        private bool TryPrepareInternal(
            string chartJson,
            string effectParameterJson,
            bool validateBundledSong)
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
                    chartJson,
                    effectParameterJson,
                    beatsPerMeasure);
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }

            if (validateBundledSong)
            {
                string expectedMusicId = bundledMusicId.Trim();
                if (!string.Equals(
                        prepared.Metadata.MusicId,
                        expectedMusicId,
                        StringComparison.Ordinal))
                {
                    return Fail(
                        $"Bundled song ID '{expectedMusicId}' does not match " +
                        $"chart musicId '{prepared.Metadata.MusicId}'.");
                }

                // Preparing even the same reference reapplies volume and rejects
                // a clip whose audio data has entered a failed state.
                if (!gameManager.SetAudioSource(
                        bundledSong,
                        bundledSongVolume))
                {
                    return Fail(
                        $"Could not prepare the AudioClip for '{expectedMusicId}'.");
                }
            }

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
                    prepared.Notes.Count,
                    out string stateError))
            {
                return FailInvalidating(stateError);
            }

            if (!judgementSystem.Initialize(
                    ChartLaneLayout.LaneCount,
                    prepared.Notes,
                    prepared.ChartOffsetMs))
            {
                return FailInvalidating(
                    "The prepared Snapshot could not initialize judgement.");
            }

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
            LastError = null;
            return true;
        }

        private void OnEnable()
        {
            ResolveReferences(out _);
        }

        private void OnDisable()
        {
            // Disabling the chart owner is a session boundary. Keep the start
            // guard subscribed (until destruction) so a stale Effect plan can
            // never start while this owner is disabled.
            CurrentChart = null;
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
            if (playbackGuardBound && gameManager && gameManager.GamePlay)
            {
                gameManager.GamePlay.PlaybackStarting -=
                    HandlePlaybackStarting;
            }
            playbackGuardBound = false;
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
