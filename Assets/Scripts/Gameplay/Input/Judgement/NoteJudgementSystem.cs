using System;
using System.Collections.Generic;
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

        private readonly Dictionary<string, GameObject> noteViews =
            new Dictionary<string, GameObject>();

        private LaneNoteQueue[] laneQueues = Array.Empty<LaneNoteQueue>();
        private double chartOffsetMs;

        public event Action<NoteJudgementEvent> NoteJudged;

        public Func<NoteData, RuleContext> RuleContextFactory { get; set; }
        public IReadOnlyList<LaneNoteQueue> LaneQueues => laneQueues;
        public bool IsInitialized { get; private set; }
        public bool IsAutoPlayEnabled { get; private set; }
        public double ChartOffsetMs => chartOffsetMs;
        public double UserOffsetMs => userOffsetMs;

        public int PendingNoteCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < laneQueues.Length; i++)
                {
                    count += laneQueues[i].PendingCount;
                }

                return count;
            }
        }

        private void Awake()
        {
            if (inputRouter == null)
            {
                inputRouter = GetComponent<RhythmInputRouter>();
            }

            if (gameManager == null)
            {
                gameManager = GetComponentInParent<GameManager>();
            }

            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (gameRule == null && gameManager != null)
            {
                gameRule = gameManager.GameRule;
            }

            if (gameManager == null || gameRule == null)
            {
                Debug.LogError(
                    "NoteJudgementSystem requires an available GameManager and GameRule.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (inputRouter != null)
            {
                inputRouter.InputPerformed += HandleInputPerformed;
            }
        }

        private void OnDisable()
        {
            if (inputRouter != null)
            {
                inputRouter.InputPerformed -= HandleInputPerformed;
            }
        }

        private void Update()
        {
            if (!IsInitialized || gameManager.PlaybackState != PlaybackState.Playing)
            {
                return;
            }

            double currentSongTimeMs = gameManager.CorePlayMs;

            if (IsAutoPlayEnabled)
            {
                ProcessAutoPlayNotes(currentSongTimeMs);
                return;
            }

            for (int lane = 0; lane < laneQueues.Length; lane++)
            {
                ProcessExpiredNotes(laneQueues[lane], currentSongTimeMs);
            }
        }

        public bool Initialize(ChartData chart)
        {
            if (chart == null)
            {
                ClearInitialization();
                Debug.LogError("Cannot initialize NoteJudgementSystem with a null chart.", this);
                return false;
            }

            return Initialize(chart.LaneCount, chart.Notes, chart.ChartOffsetMs);
        }

        public bool Initialize(
            int laneCount,
            IReadOnlyList<NoteData> notes,
            double noteChartOffsetMs = 0d)
        {
            ClearInitialization();

            if (laneCount <= 0)
            {
                Debug.LogError("Lane count must be greater than zero.", this);
                return false;
            }

            if (notes == null)
            {
                Debug.LogError("Note list cannot be null.", this);
                return false;
            }

            if (double.IsNaN(noteChartOffsetMs) || double.IsInfinity(noteChartOffsetMs))
            {
                Debug.LogError("Chart offset must be a finite number.", this);
                return false;
            }

            LaneNoteQueue[] newQueues = new LaneNoteQueue[laneCount];
            HashSet<string> noteIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<(long TimeMs, int Lane)> occupiedSlots =
                new HashSet<(long TimeMs, int Lane)>();

            for (int lane = 0; lane < laneCount; lane++)
            {
                newQueues[lane] = new LaneNoteQueue(lane);
            }

            for (int i = 0; i < notes.Count; i++)
            {
                NoteData note = notes[i];
                if (note == null ||
                    !note.Type.IsGameplayNote() ||
                    note.Lane < 0 ||
                    note.Lane >= laneCount ||
                    string.IsNullOrWhiteSpace(note.Id) ||
                    note.TimeMs < 0 ||
                    (note.Type.IsLong()
                        ? note.DurationMs <= 0
                        : note.DurationMs != 0) ||
                    !noteIds.Add(note.Id) ||
                    !occupiedSlots.Add((note.TimeMs, note.Lane)))
                {
                    Debug.LogError($"Invalid note at index {i}.", this);
                    return false;
                }

                try
                {
                    checked
                    {
                        _ = note.TimeMs + note.DurationMs;
                    }
                }
                catch (OverflowException)
                {
                    Debug.LogError($"Note time overflows at index {i}.", this);
                    return false;
                }

                newQueues[note.Lane].Add(note);
            }

            for (int lane = 0; lane < newQueues.Length; lane++)
            {
                newQueues[lane].Sort();
            }

            laneQueues = newQueues;
            chartOffsetMs = noteChartOffsetMs;
            IsInitialized = true;
            return true;
        }

        public void ResetJudgements(bool reactivateRegisteredViews = true)
        {
            for (int lane = 0; lane < laneQueues.Length; lane++)
            {
                laneQueues[lane].ResetProgress();
            }

            if (!reactivateRegisteredViews)
            {
                return;
            }

            foreach (KeyValuePair<string, GameObject> pair in noteViews)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(true);
                }
            }
        }

        public void SetUserOffsetMs(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            userOffsetMs = value;
        }

        public void SetAutoPlayEnabled(bool value)
        {
            IsAutoPlayEnabled = value;
        }

        public bool RegisterNoteView(string noteId, GameObject noteView)
        {
            if (string.IsNullOrWhiteSpace(noteId) || noteView == null)
            {
                return false;
            }

            if (noteViews.TryGetValue(noteId, out GameObject currentView))
            {
                return currentView == noteView;
            }

            noteViews.Add(noteId, noteView);
            return true;
        }

        public bool UnregisterNoteView(string noteId)
        {
            return !string.IsNullOrWhiteSpace(noteId) && noteViews.Remove(noteId);
        }

        public bool TryGetRegisteredNoteView(string noteId, out GameObject noteView)
        {
            if (!string.IsNullOrWhiteSpace(noteId) &&
                noteViews.TryGetValue(noteId, out noteView) &&
                noteView != null)
            {
                return true;
            }

            noteView = null;
            return false;
        }

        public void ClearRegisteredNoteViews()
        {
            noteViews.Clear();
        }

        public void ClearInitialization()
        {
            laneQueues = Array.Empty<LaneNoteQueue>();
            chartOffsetMs = 0d;
            IsInitialized = false;
        }

        private void HandleInputPerformed(RhythmInputEvent inputEvent)
        {
            if (IsAutoPlayEnabled || !IsInitialized ||
                gameManager.PlaybackState != PlaybackState.Playing ||
                inputEvent.Lane < 0 || inputEvent.Lane >= laneQueues.Length)
            {
                return;
            }

            LaneNoteQueue queue = laneQueues[inputEvent.Lane];
            ProcessExpiredNotes(queue, gameManager.CorePlayMs);

            if (!queue.TryPeek(out NoteData note))
            {
                return;
            }

            RuleContext context = CreateRuleContext(note);
            double effectiveHitTimeMs = GetEffectiveHitTimeMs(note);

            if (!gameManager.TryGetJudgeOffsetMs(
                    inputEvent.EventTime,
                    effectiveHitTimeMs,
                    userOffsetMs,
                    out double offsetMs))
            {
                return;
            }

            JudgeResult result = gameRule.Judge(offsetMs, context);
            if (result == JudgeResult.None)
            {
                return;
            }

            ResolveCurrentNote(
                queue,
                note,
                result,
                offsetMs,
                effectiveHitTimeMs,
                false);
        }

        private void ProcessAutoPlayNotes(double currentSongTimeMs)
        {
            while (true)
            {
                LaneNoteQueue nextQueue = null;
                NoteData nextNote = null;
                double nextHitTimeMs = double.MaxValue;

                for (int lane = 0; lane < laneQueues.Length; lane++)
                {
                    LaneNoteQueue queue = laneQueues[lane];
                    if (!queue.TryPeek(out NoteData note))
                    {
                        continue;
                    }

                    double effectiveHitTimeMs = GetEffectiveHitTimeMs(note);
                    if (effectiveHitTimeMs > currentSongTimeMs ||
                        effectiveHitTimeMs >= nextHitTimeMs)
                    {
                        continue;
                    }

                    nextQueue = queue;
                    nextNote = note;
                    nextHitTimeMs = effectiveHitTimeMs;
                }

                if (nextQueue == null)
                {
                    return;
                }

                ResolveCurrentNote(
                    nextQueue,
                    nextNote,
                    JudgeResult.Perfect,
                    0d,
                    nextHitTimeMs,
                    false);
            }
        }

        private void ProcessExpiredNotes(LaneNoteQueue queue, double currentSongTimeMs)
        {
            while (queue.TryPeek(out NoteData note))
            {
                RuleContext context = CreateRuleContext(note);
                double effectiveHitTimeMs = GetEffectiveHitTimeMs(note);
                int missWindowMs = gameRule.GetJudgeWindows(context).MissWindowMs;
                double offsetMs = currentSongTimeMs - effectiveHitTimeMs;

                if (offsetMs <= missWindowMs)
                {
                    return;
                }

                ResolveCurrentNote(
                    queue,
                    note,
                    JudgeResult.Miss,
                    offsetMs,
                    effectiveHitTimeMs,
                    true);
            }
        }

        private void ResolveCurrentNote(
            LaneNoteQueue queue,
            NoteData note,
            JudgeResult result,
            double offsetMs,
            double effectiveHitTimeMs,
            bool isAutomaticMiss)
        {
            if (!queue.TryAdvance(out NoteData advancedNote) || !ReferenceEquals(note, advancedNote))
            {
                Debug.LogError("Lane note queue changed during judgement.", this);
                return;
            }

            if (noteViews.TryGetValue(note.Id, out GameObject noteView) && noteView != null)
            {
                noteView.SetActive(false);
            }

            NoteJudged?.Invoke(
                new NoteJudgementEvent(
                    note,
                    result,
                    gameRule.GetTimingSide(offsetMs),
                    offsetMs,
                    effectiveHitTimeMs,
                    isAutomaticMiss));
        }

        private RuleContext CreateRuleContext(NoteData note)
        {
            if (RuleContextFactory != null)
            {
                return RuleContextFactory(note);
            }

            return new RuleContext(
                0,
                0,
                note.Type,
                false,
                false,
                false);
        }

        private double GetEffectiveHitTimeMs(NoteData note)
        {
            return note.TimeMs + chartOffsetMs;
        }
    }
}
