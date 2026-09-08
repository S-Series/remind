using System;
using System.Collections.Generic;
using REmind.Charting;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;

namespace REmind.Gameplay.Effects
{
    /// <summary>Actual-play bridge. No independent Update: NoteJudgementSystem owns the time pump.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayChartEffectController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [Tooltip("Dedicated identity pivot below the base camera motion, above the camera. Never share with the base presenter.")]
        [SerializeField] private Transform cameraEffectPivot;
        [Tooltip("Optional component implementing IEffectGameState (the real session's health).")]
        [SerializeField] private MonoBehaviour gameStateProvider;

        private PreparedEffectPlan plan;
        private ChartEffectDocumentState.Metadata metadata;
        private RuntimeSession activeSession;
        private RuntimeSession pendingSession;
        private double chartOffsetMs;
        private bool bound;
        private bool acceptingStarts;
        private bool activeSessionAwaitingStart;
        private bool lifecycleStopping;
        private int cleanupDepth;
        private Vector3 pivotPosition;
        private Quaternion pivotRotation;
        public string LastError { get; private set; }
        public IEffectGameState GameState { get; set; }
        // The scene flow owner supplies prepared target lookup and performs the requested transition.
        public Func<string, string, bool> CanTransitionTo { get; set; }
        public event Action<string, string> TransitionRequested;
        public event Action<string> PlaybackFailed;

        public bool Prepare(PlayableChartSnapshot snapshot, IReadOnlyList<ChartHolder> holders,
            ChartEffectDocumentState.Metadata documentMetadata, double noteChartOffsetMs)
        {
            if (lifecycleStopping || cleanupDepth > 0 ||
                (gameManager && gameManager.GamePlay &&
                 gameManager.GamePlay.IsStartInProgress))
            {
                LastError = "Effect definitions cannot be replaced during session cleanup or playback start.";
                return false;
            }
            if (gameManager && (gameManager.PlaybackState == PlaybackState.Playing || gameManager.PlaybackState == PlaybackState.Paused))
            {
                LastError = "Stop playback before replacing Effect definitions.";
                return false;
            }
            // Once replacement begins, a failed cleanup/probe must not leave the
            // previous or partially validated plan startable.
            plan = null;
            Exception pendingCleanup = StopPendingSession();
            Exception activeCleanup = StopSession();
            Exception staleCleanup = CombineCleanupFailures(
                pendingCleanup, activeCleanup);
            if (staleCleanup != null)
            {
                ReportFailure("Could not replace the previous Effect session: " +
                    staleCleanup.Message);
                return false;
            }
            try
            {
                Bind();
                if (gameManager.PlaybackState == PlaybackState.Playing || gameManager.PlaybackState == PlaybackState.Paused)
                    throw new InvalidOperationException("Stop playback before replacing Effect definitions.");
                if (!judgementSystem.IsInitialized || Math.Abs(judgementSystem.ChartOffsetMs - noteChartOffsetMs) > 0.000001)
                    throw new InvalidOperationException("Initialize judgement from the same chart and offset before preparing Effects.");
                plan = ChartEffectPreparation.Prepare(snapshot, holders, documentMetadata.GimmickId);
                metadata = documentMetadata;
                chartOffsetMs = noteChartOffsetMs;
                LastError = null;
                // Validate all services now. Playback creates another fresh session and
                // commits it only after every start subscriber accepts.
                RuntimeSession probe = CreateSession(0d);
                Exception probeCleanup = DisposeSession(
                    probe, "Effect preparation probe cleanup");
                if (probeCleanup != null)
                    throw new InvalidOperationException(
                        "Effect preparation probe cleanup failed.", probeCleanup);
                return true;
            }
            catch (Exception exception)
            {
                plan = null;
                Fail(exception.Message);
                return false;
            }
        }

        private void Bind()
        {
            if (bound) return;
            if (lifecycleStopping)
                throw new InvalidOperationException(
                    "A disabled Gameplay Effect bridge cannot bind a session.");
            if (!gameManager) gameManager = GetComponentInParent<GameManager>();
            if (!judgementSystem) judgementSystem = GetComponent<NoteJudgementSystem>();
            if (!gameManager || !gameManager.GamePlay || !judgementSystem)
                throw new InvalidOperationException("Gameplay Effect bridge requires GameManager, GamePlay and NoteJudgementSystem references.");
            if (cameraEffectPivot)
            {
                pivotPosition = cameraEffectPivot.localPosition;
                pivotRotation = cameraEffectPivot.localRotation;
            }
            gameManager.GamePlay.PlaybackStarting += HandlePlaybackStarting;
            gameManager.GamePlay.PlaybackCommitting += HandlePlaybackCommitting;
            gameManager.GamePlay.PlaybackStarted += HandlePlaybackStarted;
            gameManager.GamePlay.PlaybackStartAborted += HandlePlaybackStartAborted;
            gameManager.GamePlay.PlaybackStateChanged += HandleStateChanged;
            judgementSystem.EffectFrameCompleted += HandleFrameCompleted;
            bound = true;
            acceptingStarts = true;
        }

        private bool HandlePlaybackStarting(double songTimeMs)
        {
            if (!acceptingStarts || cleanupDepth > 0) return false;
            Exception pendingCleanup = StopPendingSession();
            if (pendingCleanup != null)
            {
                ReportFailure("Pending Effect session cleanup failed: " +
                    pendingCleanup.Message);
                return false;
            }
            if (gameManager.GamePlay.StartReason == PlaybackStartReason.Resume &&
                activeSession != null)
                return true;
            if (plan == null || LastError != null) return false;
            try
            {
                pendingSession = CreateSession(songTimeMs);
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure(exception.Message);
                return false;
            }
        }

        private RuntimeSession CreateSession(double songTimeMs)
        {
            var created = new RuntimeSession();
            try
            {
                if (cameraEffectPivot) created.Camera = new EffectCameraMixer((offset, roll) =>
                {
                    // A prepared-but-uncommitted session must not reset the camera
                    // owned by the currently playing generation when validation aborts.
                    if (!created.PresentationEnabled || !cameraEffectPivot) return;
                    cameraEffectPivot.localPosition = pivotPosition + new Vector3(offset.x, offset.y, 0);
                    cameraEffectPivot.localRotation = pivotRotation * Quaternion.Euler(0, 0, roll);
                });
                // Keep prepared modifiers detached until PlaybackCommitting activates this generation.
                created.Rules = new EffectRuleService(null);
                created.Mailbox = new EffectTransitionMailbox((music, difficulty) =>
                    TransitionRequested != null && CanTransitionTo != null && CanTransitionTo(music, difficulty));
                var session = new EffectSessionContext(metadata.MusicId, metadata.DifficultyId,
                    judgementSystem.IsAutoPlayEnabled ? EffectExecutionMode.AutoPlay : EffectExecutionMode.Gameplay,
                    created.Camera, GameState ?? gameStateProvider as IEffectGameState,
                    created.Rules, created.Mailbox);
                created.Runner = new EffectRunner(plan, session, songTimeMs - chartOffsetMs);
                return created;
            }
            catch (Exception creationFailure)
            {
                Exception cleanupFailure = DisposeSession(
                    created, "Failed Effect session cleanup");
                if (cleanupFailure != null)
                    throw new AggregateException(
                        "Effect session creation and cleanup failed.",
                        creationFailure, cleanupFailure);
                throw;
            }
        }

        private bool HandlePlaybackCommitting(double songTimeMs)
        {
            if (!acceptingStarts || cleanupDepth > 0) return false;
            if (gameManager.GamePlay.StartReason == PlaybackStartReason.Resume &&
                activeSession != null && pendingSession == null)
                return true;
            if (plan == null || LastError != null) return false;
            if (pendingSession == null)
                return false;

            RuntimeSession next = pendingSession;
            pendingSession = null;
            Exception previousCleanup = StopSession();
            if (previousCleanup != null)
            {
                Exception nextCleanup = DisposeSession(
                    next, "Uncommitted Effect session cleanup");
                ReportFailure(WithCleanupFailure(
                    "Previous Effect session cleanup failed: " +
                    previousCleanup.Message, nextCleanup));
                return false;
            }

            try
            {
                next.Rules.Activate(gameManager.GameRule);
                next.PresentationEnabled = true;
                activeSession = next;
                activeSessionAwaitingStart = true;
                judgementSystem.ResetJudgements();
                // Keep the session identity attached even for an empty plan so frame
                // completion, failure and Finished cleanup cannot be misattributed.
                judgementSystem.AttachEffectRunner(next.Runner);
                return true;
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(activeSession, next))
                {
                    activeSession = null;
                    activeSessionAwaitingStart = false;
                }
                Exception detachFailure = null;
                try { judgementSystem.AttachEffectRunner(null); }
                catch (Exception detachException)
                { detachFailure = detachException; }
                Exception cleanupFailure = CombineCleanupFailures(
                    detachFailure,
                    DisposeSession(next, "Uncommitted Effect session cleanup"));
                ReportFailure(WithCleanupFailure(
                    "Effect session commit failed: " + exception.Message,
                    cleanupFailure));
                return false;
            }
        }

        private void HandlePlaybackStarted(double songTimeMs)
        {
            activeSessionAwaitingStart = false;
        }

        private void HandlePlaybackStartAborted(double songTimeMs)
        {
            Exception pendingCleanup = StopPendingSession();
            Exception activeCleanup = activeSessionAwaitingStart
                ? StopSession()
                : null;
            Exception cleanupFailure = CombineCleanupFailures(
                pendingCleanup, activeCleanup);
            if (cleanupFailure != null)
                ReportFailure("Aborted Effect start cleanup failed: " +
                    cleanupFailure.Message);
        }

        private void HandleFrameCompleted(EffectRunner completedRunner)
        {
            RuntimeSession current = activeSession;
            if (current == null || !ReferenceEquals(current.Runner, completedRunner)) return;
            try
            {
                if (completedRunner.Failure != null)
                {
                    StopPlaybackWithFailure(completedRunner.Failure.Message);
                    return;
                }
                if (!string.IsNullOrEmpty(judgementSystem.LastTimelineError))
                {
                    StopPlaybackWithFailure(judgementSystem.LastTimelineError);
                    return;
                }
                current.Camera?.Apply();
                if (current.Mailbox.HasRequest)
                {
                    string music = current.Mailbox.TargetMusicId;
                    string difficulty = current.Mailbox.TargetDifficultyId;
                    Exception cleanupFailure = StopSession();
                    gameManager.StopGame();
                    if (cleanupFailure != null)
                    {
                        ReportFailure(
                            "Effect transition was cancelled because session cleanup failed: " +
                            cleanupFailure.Message);
                        return;
                    }
                    try { TransitionRequested?.Invoke(music, difficulty); }
                    catch (Exception exception)
                    { ReportFailure("Transition failed: " + exception.Message); }
                    return;
                }
                if (gameManager.PlaybackState == PlaybackState.Finished)
                {
                    Exception cleanupFailure = StopSession();
                    if (cleanupFailure != null)
                        ReportFailure("Finished Effect session cleanup failed: " +
                            cleanupFailure.Message);
                }
            }
            catch (Exception exception)
            {
                StopPlaybackWithFailure("Effect frame completion failed: " + exception.Message);
            }
        }

        private void HandleStateChanged(PlaybackState state)
        {
            if (state == PlaybackState.Ready || state == PlaybackState.Empty)
            {
                Exception cleanupFailure = CombineCleanupFailures(
                    StopPendingSession(), StopSession());
                if (cleanupFailure != null)
                    ReportFailure("Effect session cleanup failed: " +
                        cleanupFailure.Message);
            }
        }
        private void Fail(string error)
        {
            Exception cleanupFailure = CombineCleanupFailures(
                StopPendingSession(), StopSession());
            ReportFailure(WithCleanupFailure(error, cleanupFailure));
        }
        private void StopPlaybackWithFailure(string error)
        {
            Exception cleanupFailure = CombineCleanupFailures(
                StopPendingSession(), StopSession());
            gameManager?.StopGame();
            ReportFailure(WithCleanupFailure(error, cleanupFailure));
        }
        private void ReportFailure(string error)
        {
            LastError = $"{metadata.MusicId}/{metadata.DifficultyId}: {error}";
            Debug.LogError(LastError, this);
            if (PlaybackFailed == null) return;
            foreach (Delegate callback in PlaybackFailed.GetInvocationList())
            {
                try { ((Action<string>)callback)(LastError); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
        private Exception StopSession()
        {
            RuntimeSession current = activeSession;
            activeSession = null;
            activeSessionAwaitingStart = false;
            Exception detachFailure = null;
            try
            {
                if (judgementSystem) judgementSystem.AttachEffectRunner(null);
            }
            catch (Exception exception)
            {
                detachFailure = exception;
            }
            return CombineCleanupFailures(detachFailure,
                DisposeSession(current, "Effect session cleanup"));
        }
        private Exception StopPendingSession()
        {
            RuntimeSession pending = pendingSession;
            pendingSession = null;
            return DisposeSession(pending, "Pending Effect session cleanup");
        }
        private Exception DisposeSession(RuntimeSession session, string phase)
        {
            if (session == null) return null;
            cleanupDepth++;
            try
            {
                session.Dispose();
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogError(phase + ": " + exception.Message, this);
                return exception;
            }
            finally
            {
                cleanupDepth--;
            }
        }

        private static Exception CombineCleanupFailures(
            params Exception[] failures)
        {
            var present = new List<Exception>();
            foreach (Exception failure in failures)
            {
                if (failure != null) present.Add(failure);
            }
            if (present.Count == 0) return null;
            if (present.Count == 1) return present[0];
            return new AggregateException(
                "Multiple Effect session cleanup operations failed.", present);
        }

        private static string WithCleanupFailure(
            string message, Exception cleanupFailure)
        {
            return cleanupFailure == null
                ? message
                : message + " Cleanup also failed: " + cleanupFailure.Message;
        }

        private void OnEnable()
        {
            lifecycleStopping = false;
        }

        private void OnDisable()
        {
            bool hadRuntimeState = pendingSession != null ||
                activeSession != null;
            lifecycleStopping = true;
            acceptingStarts = false;
            if (bound)
            {
                // Keep only the non-mutating rejection gate subscribed while owned
                // resources run arbitrary cleanup callbacks.
                gameManager.GamePlay.PlaybackCommitting -= HandlePlaybackCommitting;
                gameManager.GamePlay.PlaybackStarted -= HandlePlaybackStarted;
                gameManager.GamePlay.PlaybackStartAborted -= HandlePlaybackStartAborted;
                gameManager.GamePlay.PlaybackStateChanged -= HandleStateChanged;
                judgementSystem.EffectFrameCompleted -= HandleFrameCompleted;
            }
            Exception cleanupFailure = CombineCleanupFailures(
                StopPendingSession(), StopSession());
            if (hadRuntimeState && gameManager && gameManager.GamePlay)
                gameManager.StopGame();
            plan = null;
            if (cleanupFailure != null)
                ReportFailure("Disabled Effect session cleanup failed: " +
                    cleanupFailure.Message);
            if (bound)
            {
                gameManager.GamePlay.PlaybackStarting -= HandlePlaybackStarting;
                bound = false;
            }
        }

        private sealed class RuntimeSession : IDisposable
        {
            public EffectRunner Runner;
            public EffectCameraMixer Camera;
            public EffectRuleService Rules;
            public EffectTransitionMailbox Mailbox;
            public bool PresentationEnabled;

            public void Dispose()
            {
                EffectRunner runner = Runner; Runner = null;
                EffectRuleService rules = Rules; Rules = null;
                EffectCameraMixer camera = Camera; Camera = null;
                EffectTransitionMailbox mailbox = Mailbox; Mailbox = null;
                var failures = new List<Exception>();
                EffectExecutionFailure runnerFailureBefore = runner?.Failure;
                try
                {
                    runner?.Dispose();
                    if (runner != null && runnerFailureBefore == null &&
                        runner.Failure != null)
                        failures.Add(runner.Failure.Exception);
                }
                catch (Exception exception) { failures.Add(exception); }
                try { rules?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
                try { camera?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
                try { mailbox?.Dispose(); } catch (Exception exception) { failures.Add(exception); }
                if (failures.Count > 0)
                    throw new AggregateException("Effect runtime cleanup failed.", failures);
            }
        }
    }
}
