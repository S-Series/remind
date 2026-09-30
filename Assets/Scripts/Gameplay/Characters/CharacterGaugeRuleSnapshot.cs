using System;

namespace REmind.Gameplay.Characters
{
    /// <summary>
    /// Gauge values copied for one play attempt. It does not retain the source
    /// ScriptableObject, so later asset edits cannot change these values.
    /// </summary>
    public readonly struct CharacterGaugeRuleSnapshot
    {
        public HealthGaugeType GaugeType { get; }
        public int MaxHealth { get; }
        public int InitialHealth { get; }
        public int ClearHealth { get; }
        public int PerfectHealthDelta { get; }
        public int GreatHealthDelta { get; }
        public int GoodHealthDelta { get; }
        public int MissHealthDelta { get; }
        public bool FailImmediately { get; }
        public bool ContinueAfterFail { get; }

        private CharacterGaugeRuleSnapshot(GameRuleConfig source)
        {
            GaugeType = source.GaugeType;
            MaxHealth = source.MaxHealth;
            InitialHealth = source.InitialHealth;
            ClearHealth = source.ClearHealth;
            PerfectHealthDelta = source.HealthDelta.GetValue(JudgeResult.Perfect);
            GreatHealthDelta = source.HealthDelta.GetValue(JudgeResult.Great);
            GoodHealthDelta = source.HealthDelta.GetValue(JudgeResult.Good);
            MissHealthDelta = source.HealthDelta.GetValue(JudgeResult.Miss);
            FailImmediately = source.FailImmediately;
            ContinueAfterFail = source.ContinueAfterFail;
        }

        public static CharacterGaugeRuleSnapshot Capture(GameRuleConfig source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            source.ValidateForSession();

            return new CharacterGaugeRuleSnapshot(source);
        }

        public int GetHealthDelta(JudgeResult result)
        {
            switch (result)
            {
                case JudgeResult.Perfect: return PerfectHealthDelta;
                case JudgeResult.Great: return GreatHealthDelta;
                case JudgeResult.Good: return GoodHealthDelta;
                case JudgeResult.Miss: return MissHealthDelta;
                default: return 0;
            }
        }
    }
}
