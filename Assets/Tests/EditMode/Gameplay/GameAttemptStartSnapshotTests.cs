using System;
using NUnit.Framework;
using REmind.Gameplay.Characters;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class GameAttemptStartSnapshotTests
    {
        [Test]
        public void ResultCarriesFrozenAttemptAndCannotUnmarkStartAutoPlay()
        {
            GameRuleConfig source = ScriptableObject.CreateInstance<GameRuleConfig>();
            CharacterGaugeRuleSnapshot gauge;
            try
            {
                gauge = CharacterGaugeRuleSnapshot.Capture(source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            var start = new GameAttemptStartSnapshot(Guid.NewGuid(),
                "i", "hard", false, gauge, null);
            GameResultSnapshot result = Result("i", false, start);

            Assert.AreSame(start, result.AttemptStart);
            Assert.IsNull(result.AttemptStart.CharacterAbility);
            Assert.AreEqual(HealthGaugeType.Normal,
                result.AttemptStart.GaugeRule.GaugeType);
            Assert.AreEqual(100, result.AttemptStart.GaugeRule.InitialHealth);
            Assert.Throws<ArgumentException>(() =>
                Result("designant", false, start));

            // A manual start may switch to Auto Play during the attempt.
            Assert.IsTrue(Result("i", true, start).IsAutoPlay);
            var autoStart = new GameAttemptStartSnapshot(Guid.NewGuid(),
                "i", "hard", true, gauge, null);
            Assert.Throws<ArgumentException>(() =>
                Result("i", false, autoStart));
        }

        private static GameResultSnapshot Result(string musicId,
            bool isAutoPlay, GameAttemptStartSnapshot start) =>
            new GameResultSnapshot(musicId, "hard", 0d, 1_000_000,
                RankGrade.D, 0, 0, 0, 0, 0, 1,
                false, false, isAutoPlay, start);
    }
}
