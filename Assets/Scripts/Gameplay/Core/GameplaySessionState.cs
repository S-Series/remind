using System;
using REmind.Charting;
using REmind.Data;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;

namespace REmind.Gameplay
{
    /// <summary>
    /// The real, session-local rule state exposed to MusicGimmick.  It is reset
    /// only after a fresh Play/Restart succeeds; Resume keeps the same values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplaySessionState : MonoBehaviour, IEffectGameState
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private GameRule gameRule;

        private Func<PlayableNoteSnapshot, RuleContext> contextFactory;
        private bool bound;
        private int totalNoteCount;

        public event Action StateChanged;

        public double CurrentHealth { get; private set; }
        public int CurrentCombo { get; private set; }
        public double CurrentScore { get; private set; }
        public int JudgedNoteCount { get; private set; }
        public bool IsFailed { get; private set; }
        public bool IsCleared { get; private set; }
        public bool HasPreparedChart { get; private set; }
        public bool IsReadyForPlayback => HasPreparedChart && bound;
        public int TotalNoteCount => totalNoteCount;

        private void Awake()
        {
            contextFactory = CreateRuleContext;
            ResolveReferences();
        }

        private void OnEnable()
        {
            TryBind(out _);
        }

        private void OnDisable()
        {
            Unbind();
        }

        public bool TryConfigureChart(
            int noteCount,
            out string error)
        {
            if (noteCount < 0)
            {
                error = "Gameplay note count cannot be negative.";
                return false;
            }

            ResolveReferences();
            if (!gameManager || !gameManager.GamePlay ||
                !judgementSystem || !gameRule)
            {
                error = "GameplaySessionState requires GameManager, GamePlay, " +
                    "NoteJudgementSystem and GameRule references.";
                return false;
            }

            if (gameManager.PlaybackState == PlaybackState.Playing ||
                gameManager.PlaybackState == PlaybackState.Paused ||
                gameManager.GamePlay.IsStartInProgress)
            {
                error = "Stop gameplay before replacing session state.";
                return false;
            }

            if (!TryBind(out error))
            {
                return false;
            }

            try
            {
                totalNoteCount = noteCount;
                HasPreparedChart = true;
                ResetForFreshSession();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                ClearPreparedChart();
                error = "Could not initialize gameplay rule state: " +
                    exception.Message;
                return false;
            }
        }

        public void ClearPreparedChart()
        {
            HasPreparedChart = false;
            totalNoteCount = 0;
            CurrentHealth = 0d;
            CurrentCombo = 0;
            CurrentScore = 0d;
            JudgedNoteCount = 0;
            IsFailed = false;
            IsCleared = false;
            NotifyStateChanged();
        }

        private void ResolveReferences()
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
            if (!gameRule && gameManager)
            {
                gameRule = gameManager.GameRule;
            }
        }

        private bool TryBind(out string error)
        {
            if (bound)
            {
                error = null;
                return true;
            }

            ResolveReferences();
            if (!gameManager || !gameManager.GamePlay ||
                !judgementSystem || !gameRule)
            {
                error = "Gameplay session state references are incomplete.";
                return false;
            }

            contextFactory ??= CreateRuleContext;
            if (judgementSystem.RuleContextFactory != null &&
                judgementSystem.RuleContextFactory != contextFactory)
            {
                error = "NoteJudgementSystem already has another rule-state " +
                    "provider.";
                return false;
            }

            judgementSystem.RuleContextFactory = contextFactory;
            judgementSystem.NoteJudged += HandleNoteJudged;
            gameManager.GamePlay.PlaybackStarted += HandlePlaybackStarted;
            bound = true;
            error = null;
            return true;
        }

        private void Unbind()
        {
            if (!bound)
            {
                return;
            }

            if (judgementSystem)
            {
                judgementSystem.NoteJudged -= HandleNoteJudged;
                if (judgementSystem.RuleContextFactory == contextFactory)
                {
                    judgementSystem.RuleContextFactory = null;
                }
            }
            if (gameManager && gameManager.GamePlay)
            {
                gameManager.GamePlay.PlaybackStarted -=
                    HandlePlaybackStarted;
            }

            bound = false;
        }

        private RuleContext CreateRuleContext(PlayableNoteSnapshot note)
        {
            NoteType type = NoteType.Unknown;
            if (note != null)
            {
                switch (note.Kind)
                {
                    case ChartNoteKind.Tap: type = NoteType.Tap; break;
                    case ChartNoteKind.Hold: type = NoteType.LongTap; break;
                    case ChartNoteKind.Scratch: type = NoteType.Scratch; break;
                    case ChartNoteKind.LongScratch:
                        type = NoteType.LongScratch; break;
                    case ChartNoteKind.Air: type = NoteType.Air; break;
                }
            }
            return new RuleContext(ToRuleHealth(CurrentHealth), CurrentCombo,
                type, false, false, HasPreparedChart &&
                totalNoteCount > 0 && JudgedNoteCount >= totalNoteCount);
        }

        private void HandleNoteJudged(NoteJudgementEvent judgementEvent)
        {
            if (!HasPreparedChart || judgementEvent.ChartNote == null)
            {
                return;
            }

            bool completesChart = totalNoteCount > 0 &&
                JudgedNoteCount + 1 >= totalNoteCount;
            RuleContext before = new RuleContext(
                ToRuleHealth(CurrentHealth),
                CurrentCombo,
                CreateRuleContext(judgementEvent.ChartNote).NoteType,
                false,
                false,
                completesChart,
                judgementEvent.EvaluationTimeMs);

            int nextHealth = gameRule.ApplyHealthDelta(
                ToRuleHealth(CurrentHealth),
                judgementEvent.Result,
                before);
            double nextScore = gameRule.ClampScore(
                CurrentScore + gameRule.CalculateScoreDelta(
                    judgementEvent.Result,
                    before,
                    totalNoteCount));
            int nextCombo = gameRule.ApplyComboBehavior(
                CurrentCombo,
                judgementEvent.Result,
                before);

            CurrentHealth = nextHealth;
            CurrentScore = nextScore;
            CurrentCombo = nextCombo;
            if (JudgedNoteCount < totalNoteCount)
            {
                JudgedNoteCount++;
            }

            RuleContext after = new RuleContext(
                nextHealth,
                nextCombo,
                CreateRuleContext(judgementEvent.ChartNote).NoteType,
                false,
                false,
                totalNoteCount > 0 && JudgedNoteCount >= totalNoteCount,
                judgementEvent.EvaluationTimeMs);
            IsFailed = gameRule.ShouldFail(after);
            IsCleared = gameRule.IsCleared(after);
            NotifyStateChanged();

            if (gameRule.ShouldStopAfterFail(after))
            {
                gameManager.StopGame();
            }
        }

        private void HandlePlaybackStarted(double songTimeMs)
        {
            if (!HasPreparedChart ||
                gameManager.GamePlay.StartReason == PlaybackStartReason.Resume)
            {
                return;
            }

            ResetForFreshSession();
        }

        private void ResetForFreshSession()
        {
            RuleContext initial = new RuleContext(
                0,
                0,
                NoteType.Unknown,
                false,
                false,
                false,
                0d);
            CurrentHealth = gameRule.GetInitialHealth(initial);
            CurrentCombo = 0;
            CurrentScore = 0d;
            JudgedNoteCount = 0;
            IsFailed = false;
            IsCleared = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            if (StateChanged == null)
            {
                return;
            }

            foreach (Delegate callback in StateChanged.GetInvocationList())
            {
                try
                {
                    ((Action)callback)();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private static int ToRuleHealth(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0;
            }

            if (value <= int.MinValue)
            {
                return int.MinValue;
            }
            if (value >= int.MaxValue)
            {
                return int.MaxValue;
            }
            return (int)Math.Round(
                value,
                MidpointRounding.AwayFromZero);
        }
    }
}
