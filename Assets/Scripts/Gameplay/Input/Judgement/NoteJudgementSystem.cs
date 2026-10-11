using System;
using System.Collections.Generic;
using REmind.Charting;
using REmind.Data;
using REmind.Gameplay.Input.Routing;
using UnityEngine;

namespace REmind.Gameplay.Input.Judgement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RhythmInputRouter))]
    public sealed class NoteJudgementSystem : MonoBehaviour
    {
        [SerializeField] private RhythmInputRouter inputRouter;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameRule gameRule;
        [SerializeField] private double userOffsetMs;
        [SerializeField] private NoteJudgeWindowProfile[] noteWindowProfiles =
            Array.Empty<NoteJudgeWindowProfile>();

        private readonly Dictionary<string, GameObject> noteViews =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<QueuedInput> pendingInputs =
            new List<QueuedInput>();
        private PlayableChartSnapshot chart;
        private PlayableJudgementSession session;
        private EffectRunner effectRunner;
        private long frameGeneration;
        private long inputSequence;
        private double chartOffsetMs;
        private double timelineTimeMs = double.NegativeInfinity;
        private double lastCommittedEffectTimeMs = double.NegativeInfinity;
        private double lastAutomaticTimeMs = double.NegativeInfinity;
        private double judgementTimeMs;

        public event Action<NoteJudgementEvent> NoteJudged;
        public event Action<EffectRunner> EffectFrameCompleted;
        public event Action<string> PlaybackFailed;
        public Func<PlayableNoteSnapshot, RuleContext> RuleContextFactory
            { get; set; }
        public bool IsInitialized => session != null;
        public bool IsAutoPlayEnabled { get; private set; }
        /// <summary>Sticky for the current attempt; only a fresh start clears it.</summary>
        public bool UsedAutoPlayInCurrentAttempt { get; private set; }
        public int PendingNoteCount => session?.PendingNoteCount ?? 0;
        public double PlayerKnobX => session?.PlayerKnobX ?? 0d;
        public string PlayerKnobNoteId => session?.PlayerKnobNoteId;
        public double ChartOffsetMs => chartOffsetMs;
        public double UserOffsetMs => userOffsetMs;
        public string LastTimelineError { get; private set; }
        public int DiscardedLateInputCount { get; private set; }

        private void Awake()
        {
            if (!inputRouter) inputRouter = GetComponent<RhythmInputRouter>();
            if (!gameManager) gameManager =
                GetComponentInParent<GameManager>() ?? GameManager.Instance;
            if (!gameRule && gameManager) gameRule = gameManager.GameRule;
            if (!gameManager || !gameRule)
            {
                Debug.LogError("NoteJudgementSystem needs GameManager and GameRule.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!inputRouter) return;
            inputRouter.InputPerformed += HandleInput;
            inputRouter.InputReleased += HandleInput;
            if (gameManager && gameManager.GamePlay)
            {
                gameManager.GamePlay.PlaybackStarted += HandlePlaybackStarted;
                gameManager.GamePlay.PlaybackStateChanged += HandlePlaybackStateChanged;
            }
        }

        private void OnDisable()
        {
            // A disabled component leaves its GameObject active; scene teardown
            // disables the hierarchy while the component's enabled flag stays set.
            bool interrupted = !enabled && gameObject.activeInHierarchy &&
                gameManager && gameManager.GamePlay &&
                (gameManager.PlaybackState == PlaybackState.Playing ||
                 gameManager.PlaybackState == PlaybackState.Paused);
            pendingInputs.Clear();
            session?.Halt();
            unchecked { frameGeneration++; }
            if (inputRouter)
            {
                inputRouter.InputPerformed -= HandleInput;
                inputRouter.InputReleased -= HandleInput;
            }
            if (gameManager && gameManager.GamePlay)
            {
                gameManager.GamePlay.PlaybackStarted -= HandlePlaybackStarted;
                gameManager.GamePlay.PlaybackStateChanged -= HandlePlaybackStateChanged;
            }
            if (interrupted)
            {
                gameManager.StopGame();
                const string reason = "Judgement component was disabled during playback.";
                Debug.LogError(reason, this);
                NotifyPlaybackFailed(reason);
            }
        }

        private void NotifyPlaybackFailed(string reason)
        {
            if (PlaybackFailed == null) return;
            foreach (Delegate callback in PlaybackFailed.GetInvocationList())
            {
                try { ((Action<string>)callback)(reason); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void LateUpdate()
        {
            if (!IsInitialized || !gameManager ||
                (gameManager.PlaybackState != PlaybackState.Playing &&
                 gameManager.PlaybackState != PlaybackState.Finished)) return;
            if (gameManager.PlaybackState == PlaybackState.Playing &&
                !gameManager.GamePlay.HasReachedScheduledStart) return;
            ProcessFrame(gameManager.CorePlayMs);
        }

        public bool Initialize(PlayableChartSnapshot snapshot,
            double noteChartOffsetMs = 0d)
        {
            ClearInitialization();
            if (snapshot == null || double.IsNaN(noteChartOffsetMs) ||
                double.IsInfinity(noteChartOffsetMs) || !gameRule)
                return false;
            chart = snapshot;
            if (inputRouter) inputRouter.RefreshSpaceTap();
            chartOffsetMs = noteChartOffsetMs;
            session = CreateSession(snapshot);
            return true;
        }

        public void AttachEffectRunner(EffectRunner runner)
        {
            effectRunner = runner;
            unchecked { frameGeneration++; }
            pendingInputs.Clear();
            timelineTimeMs = double.NegativeInfinity;
            lastCommittedEffectTimeMs = double.NegativeInfinity;
            lastAutomaticTimeMs = double.NegativeInfinity;
            judgementTimeMs = 0d;
            LastTimelineError = null;
            DiscardedLateInputCount = 0;
        }

        /// <summary>
        /// At equal chart time: Effect, input arrival order, automatic judgement.
        /// Input exactly at the Miss deadline remains eligible.
        /// </summary>
        public void ProcessFrame(double songTimeMs)
        {
            if (!IsInitialized || double.IsNaN(songTimeMs) ||
                double.IsInfinity(songTimeMs)) return;
            double currentTime = songTimeMs - chartOffsetMs;
            EffectRunner frameRunner = effectRunner;
            long generation = frameGeneration;
            try
            {
                if (LastTimelineError != null) return;
                if (currentTime < timelineTimeMs)
                {
                    LastTimelineError = "Timeline moved backwards; restart the session.";
                    return;
                }
                pendingInputs.Sort((a, b) =>
                {
                    int order = a.ChartTimeMs.CompareTo(b.ChartTimeMs);
                    return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
                });
                while (generation == frameGeneration)
                {
                    double effectTime = frameRunner?.NextEventTimeMs ??
                        double.PositiveInfinity;
                    double inputTime = pendingInputs.Count > 0
                        ? pendingInputs[0].ChartTimeMs : double.PositiveInfinity;
                    double autoTime = session.NextAutomaticTime(
                        IsAutoPlayEnabled);
                    double next = Math.Min(effectTime,
                        Math.Min(inputTime, autoTime));
                    if (double.IsPositiveInfinity(next) || next > currentTime)
                        break;
                    if (effectTime <= inputTime && effectTime <= autoTime)
                    {
                        timelineTimeMs = Math.Max(timelineTimeMs, effectTime);
                        judgementTimeMs = effectTime;
                        if (!frameRunner.TriggerThrough(effectTime, currentTime))
                            break;
                        if (generation != frameGeneration) break;
                        lastCommittedEffectTimeMs = effectTime;
                        continue;
                    }
                    if (inputTime <= autoTime)
                    {
                        QueuedInput input = pendingInputs[0];
                        pendingInputs.RemoveAt(0);
                        // Input System can deliver an event in the next render
                        // frame even though its device timestamp precedes the
                        // previous frame's DSP position. The frame watermark is
                        // not an Effect event. Accept that timestamp unless an
                        // irreversible Effect or automatic result crossed it.
                        if (input.ChartTimeMs < lastCommittedEffectTimeMs ||
                            input.ChartTimeMs < lastAutomaticTimeMs)
                        {
                            DiscardedLateInputCount++;
                            continue;
                        }
                        timelineTimeMs = Math.Max(timelineTimeMs,
                            input.ChartTimeMs);
                        judgementTimeMs = input.ChartTimeMs;
                        session.Input(input.Lane, input.ChartTimeMs,
                            input.Pressed);
                        continue;
                    }
                    timelineTimeMs = Math.Max(timelineTimeMs, autoTime);
                    judgementTimeMs = autoTime;
                    if (IsAutoPlayEnabled)
                        UsedAutoPlayInCurrentAttempt = true;
                    session.ProcessAutomatic(autoTime, IsAutoPlayEnabled);
                    if (generation != frameGeneration) break;
                    lastAutomaticTimeMs = autoTime;
                }
                if (generation == frameGeneration &&
                    frameRunner != null && LastTimelineError == null &&
                    !frameRunner.TransitionRequested &&
                    frameRunner.Failure == null)
                    frameRunner.AdvanceTo(currentTime);
                if (generation == frameGeneration)
                {
                    session.UpdateKnobDisplayTime(currentTime, IsAutoPlayEnabled);
                    timelineTimeMs = Math.Max(timelineTimeMs, currentTime);
                }
            }
            catch (Exception exception)
            {
                if (generation == frameGeneration)
                    LastTimelineError = "Timeline processing failed: " +
                        exception.Message;
            }
            finally
            {
                if (generation == frameGeneration)
                    NotifyEffectFrameCompleted(frameRunner);
            }
        }

        public void ResetJudgements(bool reactivateRegisteredViews = true)
        {
            unchecked { frameGeneration++; }
            pendingInputs.Clear();
            inputSequence = 0;
            timelineTimeMs = double.NegativeInfinity;
            lastCommittedEffectTimeMs = double.NegativeInfinity;
            lastAutomaticTimeMs = double.NegativeInfinity;
            judgementTimeMs = 0d;
            LastTimelineError = null;
            DiscardedLateInputCount = 0;
            session?.Reset();
            if (!reactivateRegisteredViews) return;
            foreach (GameObject view in noteViews.Values)
                if (view) view.SetActive(true);
        }

        public void SetUserOffsetMs(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (gameManager && (gameManager.PlaybackState ==
                    PlaybackState.Playing || gameManager.PlaybackState ==
                    PlaybackState.Paused))
                throw new InvalidOperationException(
                    "Change judgement offset before starting playback.");
            userOffsetMs = value;
            if (chart != null)
            {
                session?.Halt();
                unchecked { frameGeneration++; }
                session = CreateSession(chart);
            }
        }

        public void SetAutoPlayEnabled(bool value)
        {
            IsAutoPlayEnabled = value;
            if (value && gameManager &&
                (gameManager.PlaybackState == PlaybackState.Playing ||
                 gameManager.PlaybackState == PlaybackState.Paused ||
                 gameManager.PlaybackState == PlaybackState.Finished))
                UsedAutoPlayInCurrentAttempt = true;
        }

        public bool RegisterNoteView(string noteId, GameObject noteView)
        {
            if (string.IsNullOrWhiteSpace(noteId) || !noteView) return false;
            if (noteViews.TryGetValue(noteId, out GameObject current))
                return current == noteView;
            noteViews.Add(noteId, noteView);
            return true;
        }

        public bool UnregisterNoteView(string noteId)
        {
            return !string.IsNullOrWhiteSpace(noteId) &&
                noteViews.Remove(noteId);
        }

        public bool TryGetRegisteredNoteView(string noteId,
            out GameObject noteView)
        {
            if (!string.IsNullOrWhiteSpace(noteId) &&
                noteViews.TryGetValue(noteId, out noteView) && noteView)
                return true;
            noteView = null;
            return false;
        }

        public void ClearRegisteredNoteViews() { noteViews.Clear(); }

        public void ClearInitialization()
        {
            pendingInputs.Clear();
            effectRunner = null;
            session?.Halt();
            unchecked { frameGeneration++; }
            inputSequence = 0;
            timelineTimeMs = double.NegativeInfinity;
            lastCommittedEffectTimeMs = double.NegativeInfinity;
            lastAutomaticTimeMs = double.NegativeInfinity;
            judgementTimeMs = 0d;
            LastTimelineError = null;
            DiscardedLateInputCount = 0;
            UsedAutoPlayInCurrentAttempt = false;
            chart = null;
            session = null;
            chartOffsetMs = 0d;
        }

        private void HandleInput(RhythmInputEvent inputEvent)
        {
            if (IsAutoPlayEnabled || !IsInitialized || !gameManager ||
                gameManager.PlaybackState != PlaybackState.Playing ||
                !gameManager.GamePlay.HasReachedScheduledStart ||
                !ChartLaneLayout.IsValid(inputEvent.Lane)) return;
            if (gameManager.GamePlay.TryGetInputSongTimeMs(
                    inputEvent.EventTime, out double songTime))
                pendingInputs.Add(new QueuedInput(inputEvent.Lane,
                    songTime - chartOffsetMs, inputSequence++,
                    inputEvent.Pressed));
        }

        private void HandlePlaybackStarted(double songTimeMs)
        {
            if (inputRouter) inputRouter.RefreshSpaceTap();
            if (gameManager.GamePlay.StartReason != PlaybackStartReason.Resume)
                UsedAutoPlayInCurrentAttempt = IsAutoPlayEnabled;
            if (gameManager.GamePlay.StartReason == PlaybackStartReason.Resume &&
                session != null && inputRouter != null)
                session.BreakReleasedHolds(inputRouter.IsLanePressed,
                    songTimeMs - chartOffsetMs);
        }

        private void HandlePlaybackStateChanged(PlaybackState state)
        {
            // Ready/Empty are terminal for this attempt. Pause and Resume keep
            // the same judgement session and queued timeline position.
            if (state != PlaybackState.Ready && state != PlaybackState.Empty)
                return;
            session?.Halt();
            pendingInputs.Clear();
            unchecked { frameGeneration++; }
        }

        private PlayableJudgementSession CreateSession(
            PlayableChartSnapshot snapshot)
        {
            return new PlayableJudgementSession(snapshot, JudgeOffset,
                GetMissWindow, HandleResolution, userOffsetMs);
        }

        private ChartJudgementGrade JudgeOffset(PlayableNoteSnapshot note,
            int pointIndex, double offsetMs)
        {
            if (note.Kind == ChartNoteKind.LongScratch && pointIndex > 0)
            {
                int limit = GetLongScratchPointWindow(
                    pointIndex == note.Points.Count - 1);
                return Math.Abs(offsetMs) <= limit
                    ? ChartJudgementGrade.Perfect : ChartJudgementGrade.None;
            }
            RuleContext context = CreateRuleContext(note);
            return (ChartJudgementGrade)gameRule.Judge(offsetMs, context,
                GetBaseWindows(note.Kind));
        }

        private double GetMissWindow(PlayableNoteSnapshot note, int pointIndex)
        {
            if (note.Kind == ChartNoteKind.LongScratch && pointIndex > 0)
                return GetLongScratchPointWindow(
                    pointIndex == note.Points.Count - 1);
            return gameRule.GetJudgeWindows(CreateRuleContext(note),
                GetBaseWindows(note.Kind)).MissWindowMs;
        }

        private int GetLongScratchPointWindow(bool end)
        {
            if (noteWindowProfiles != null)
                foreach (NoteJudgeWindowProfile profile in noteWindowProfiles)
                    if (profile && profile.NoteKind == ChartNoteKind.LongScratch)
                        return end ? profile.LongScratchEndPerfectMs :
                            profile.LongScratchMidPerfectMs;
            return end ? 75 : 100;
        }

        private RuleContext CreateRuleContext(PlayableNoteSnapshot note)
        {
            return RuleContextFactory != null
                ? RuleContextFactory(note).AtTime(judgementTimeMs)
                : new RuleContext(0, 0, ToNoteType(note.Kind), false,
                    false, false, judgementTimeMs);
        }

        private JudgeWindows GetBaseWindows(ChartNoteKind kind)
        {
            if (noteWindowProfiles != null)
                foreach (NoteJudgeWindowProfile profile in noteWindowProfiles)
                    if (profile && profile.NoteKind == kind)
                        return profile.Windows;
            return gameRule.BaseJudgeWindows;
        }

        private void HandleResolution(ChartJudgementResolution resolution)
        {
            PlayableNoteSnapshot note = resolution.Note;
            bool final = resolution.IsNoteComplete;
            if (final && noteViews.TryGetValue(note.Id, out GameObject view) &&
                view) view.SetActive(false);
            NoteJudged?.Invoke(new NoteJudgementEvent(note,
                resolution.SegmentIndex, (JudgeResult)resolution.Grade,
                gameRule.GetTimingSide(resolution.OffsetMs),
                resolution.OffsetMs,
                resolution.TargetTimeMs + chartOffsetMs,
                resolution.EvaluationTimeMs,
                resolution.IsAutomaticMiss, resolution.IsNoteComplete,
                resolution.TargetKind));
        }

        private void NotifyEffectFrameCompleted(EffectRunner runner)
        {
            if (EffectFrameCompleted == null) return;
            foreach (Delegate callback in
                EffectFrameCompleted.GetInvocationList())
            {
                try { ((Action<EffectRunner>)callback)(runner); }
                catch (Exception exception)
                { Debug.LogException(exception, this); }
            }
        }

        private static NoteType ToNoteType(ChartNoteKind kind)
        {
            switch (kind)
            {
                case ChartNoteKind.Tap: return NoteType.Tap;
                case ChartNoteKind.Hold: return NoteType.LongTap;
                case ChartNoteKind.Scratch: return NoteType.Scratch;
                case ChartNoteKind.LongScratch: return NoteType.LongScratch;
                case ChartNoteKind.Air: return NoteType.Air;
                case ChartNoteKind.CenterTap: return NoteType.CenterTap;
                case ChartNoteKind.Knob: return NoteType.Knob;
                default: return NoteType.Unknown;
            }
        }

        private readonly struct QueuedInput
        {
            public readonly int Lane;
            public readonly double ChartTimeMs;
            public readonly long Sequence;
            public readonly bool Pressed;
            public QueuedInput(int lane, double timeMs, long sequence)
                : this(lane, timeMs, sequence, true) { }
            public QueuedInput(int lane, double timeMs, long sequence,
                bool pressed)
            {
                Lane = lane;
                ChartTimeMs = timeMs;
                Sequence = sequence;
                Pressed = pressed;
            }
        }
    }
}
