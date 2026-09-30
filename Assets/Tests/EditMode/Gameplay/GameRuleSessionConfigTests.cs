using System;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class GameRuleSessionConfigTests
    {
        [Test]
        public void SessionCopyKeepsRuleValuesWhenSourceChangesAndRestoresDefault()
        {
            var go = new GameObject("Session rule test (inactive)");
            go.SetActive(false);
            var source = ScriptableObject.CreateInstance<GameRuleConfig>();
            DefaultGameRule rule = null;
            try
            {
                rule = go.AddComponent<DefaultGameRule>();
                SetField(typeof(GameRule), rule, "config", source);
                CompleteChange(rule, source);

                GameRuleConfig activeCopy = rule.Config;
                Assert.AreNotSame(source, activeCopy);
                Assert.AreNotSame(source.HealthDelta,
                    activeCopy.HealthDelta);
                Assert.AreNotSame(source.ScoreWeight,
                    activeCopy.ScoreWeight);
                Assert.AreNotSame(source.RankThresholds,
                    activeCopy.RankThresholds);
                Assert.AreNotSame(source.ComboBehavior,
                    activeCopy.ComboBehavior);
                Assert.IsTrue(rule.HasSessionConfig);
                Assert.AreEqual(100, rule.BaseMaxHealth);
                Assert.AreEqual(70, rule.BaseClearHealth);
                Assert.IsFalse(rule.FailImmediately);
                Assert.IsTrue(rule.ContinueAfterFail);
                Assert.AreEqual(1000000, rule.MaxScore);
                Assert.AreEqual(-10, rule.GetHealthDelta(
                    JudgeResult.Miss, default(RuleContext)));

                SetField(typeof(GameRuleConfig), source, "maxHealth", 200);
                SetField(typeof(GameRuleConfig), source, "initialHealth", 200);
                SetField(typeof(GameRuleConfig), source, "clearHealth", 1);
                SetField(typeof(GameRuleConfig), source,
                    "failImmediately", true);
                SetField(typeof(GameRuleConfig), source,
                    "continueAfterFail", false);
                SetField(typeof(GameRuleConfig), source, "maxScore", 123);
                SetField(typeof(JudgeHealthDeltaSettings),
                    source.HealthDelta, "miss", -1);
                Assert.AreSame(activeCopy, rule.Config);
                Assert.AreEqual(100, rule.BaseMaxHealth);
                Assert.AreEqual(100, rule.GetInitialHealth(
                    default(RuleContext)));
                Assert.AreEqual(70, rule.BaseClearHealth);
                Assert.IsFalse(rule.FailImmediately);
                Assert.IsTrue(rule.ContinueAfterFail);
                Assert.AreEqual(1000000, rule.MaxScore);
                Assert.AreEqual(-10, rule.GetHealthDelta(
                    JudgeResult.Miss, default(RuleContext)));

                rule.ClearSessionConfig();
                Assert.IsFalse(rule.HasSessionConfig);
                Assert.AreSame(source, rule.Config);
                Assert.AreEqual(200, rule.BaseMaxHealth);
            }
            finally
            {
                rule?.ClearSessionConfig();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void InvalidReplacementLeavesPreviousSessionRuleActive()
        {
            var go = new GameObject("Invalid rule test (inactive)");
            go.SetActive(false);
            var valid = ScriptableObject.CreateInstance<GameRuleConfig>();
            var invalid = ScriptableObject.CreateInstance<GameRuleConfig>();
            DefaultGameRule rule = null;
            try
            {
                rule = go.AddComponent<DefaultGameRule>();
                SetField(typeof(GameRule), rule, "config", valid);
                CompleteChange(rule, valid);
                GameRuleConfig firstCopy = rule.Config;
                SetField(typeof(GameRuleConfig), invalid, "maxHealth", -1);

                Assert.Throws<InvalidOperationException>(() =>
                    rule.BeginSessionConfigChange(invalid));
                Assert.AreSame(firstCopy, rule.Config);
                Assert.AreEqual(100, rule.BaseMaxHealth);
            }
            finally
            {
                rule?.ClearSessionConfig();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(valid);
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }

        [Test]
        public void AbortedFirstStartRestoresDefaultRule()
        {
            var go = new GameObject("First start rollback test (inactive)");
            go.SetActive(false);
            var normal = ScriptableObject.CreateInstance<GameRuleConfig>();
            var hard = ScriptableObject.CreateInstance<GameRuleConfig>();
            DefaultGameRule rule = null;
            try
            {
                SetField(typeof(GameRuleConfig), hard, "gaugeType",
                    HealthGaugeType.Hard);
                rule = go.AddComponent<DefaultGameRule>();
                SetField(typeof(GameRule), rule, "config", normal);

                using (GameRule.SessionConfigChange change =
                       rule.BeginSessionConfigChange(hard))
                {
                    Assert.AreSame(normal, rule.Config);
                    change.Commit();
                    Assert.AreEqual(HealthGaugeType.Hard, rule.GaugeType);
                    change.Rollback();
                }

                Assert.AreSame(normal, rule.Config);
                Assert.IsFalse(rule.HasSessionConfig);
            }
            finally
            {
                rule?.ClearSessionConfig();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(hard);
            }
        }

        [Test]
        public void AbortedRetryRestoresPreviousSessionCopy()
        {
            var go = new GameObject("Retry rollback test (inactive)");
            go.SetActive(false);
            var normal = ScriptableObject.CreateInstance<GameRuleConfig>();
            var hard = ScriptableObject.CreateInstance<GameRuleConfig>();
            DefaultGameRule rule = null;
            try
            {
                SetField(typeof(GameRuleConfig), hard, "gaugeType",
                    HealthGaugeType.Hard);
                rule = go.AddComponent<DefaultGameRule>();
                SetField(typeof(GameRule), rule, "config", normal);
                CompleteChange(rule, hard);
                GameRuleConfig previous = rule.Config;

                using (GameRule.SessionConfigChange change =
                       rule.BeginSessionConfigChange(normal))
                {
                    Assert.AreSame(previous, rule.Config);
                    change.Commit();
                    Assert.AreEqual(HealthGaugeType.Normal, rule.GaugeType);
                    change.Rollback();
                }

                Assert.AreSame(previous, rule.Config);
                Assert.AreEqual(HealthGaugeType.Hard, rule.GaugeType);
                Assert.IsTrue(rule.HasSessionConfig);
            }
            finally
            {
                rule?.ClearSessionConfig();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(hard);
            }
        }

        [Test]
        public void CompletedRetryReplacesOldCopyAndRetainsPreparedValues()
        {
            var go = new GameObject("Retry complete test (inactive)");
            go.SetActive(false);
            var normal = ScriptableObject.CreateInstance<GameRuleConfig>();
            var hard = ScriptableObject.CreateInstance<GameRuleConfig>();
            DefaultGameRule rule = null;
            try
            {
                SetField(typeof(GameRuleConfig), hard, "gaugeType",
                    HealthGaugeType.Hard);
                rule = go.AddComponent<DefaultGameRule>();
                SetField(typeof(GameRule), rule, "config", normal);
                CompleteChange(rule, normal);
                GameRuleConfig previous = rule.Config;

                using (GameRule.SessionConfigChange change =
                       rule.BeginSessionConfigChange(hard))
                {
                    SetField(typeof(GameRuleConfig), hard,
                        "maxHealth", 200);
                    change.Commit();
                    Assert.AreEqual(100, rule.BaseMaxHealth);
                    change.Complete();
                }

                Assert.IsTrue(previous == null);
                Assert.AreEqual(HealthGaugeType.Hard, rule.GaugeType);
                Assert.AreEqual(100, rule.BaseMaxHealth);
            }
            finally
            {
                rule?.ClearSessionConfig();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(hard);
            }
        }

        [Test]
        public void DevelopmentHardAssetHasExplicitGaugeRulesWithoutChangingNormal()
        {
            const string folder = "Assets/Settings/Gameplay/Rules/";
            GameRuleConfig normal = AssetDatabase.LoadAssetAtPath<GameRuleConfig>(
                folder + "DefaultGameRuleConfig.asset");
            GameRuleConfig hard = AssetDatabase.LoadAssetAtPath<GameRuleConfig>(
                folder + "DevelopmentHardGameRuleConfig.asset");

            Assert.IsNotNull(normal);
            Assert.IsNotNull(hard);
            Assert.AreEqual(HealthGaugeType.Normal, normal.GaugeType);
            Assert.AreEqual(HealthGaugeType.Hard, hard.GaugeType);
            CharacterGaugeRuleSnapshot snapshot =
                CharacterGaugeRuleSnapshot.Capture(hard);
            Assert.AreEqual(100, snapshot.MaxHealth);
            Assert.AreEqual(100, snapshot.InitialHealth);
            Assert.AreEqual(1, snapshot.ClearHealth);
            Assert.AreEqual(1, snapshot.PerfectHealthDelta);
            Assert.AreEqual(1, snapshot.GreatHealthDelta);
            Assert.AreEqual(-5, snapshot.GoodHealthDelta);
            Assert.AreEqual(-20, snapshot.MissHealthDelta);
            Assert.IsTrue(snapshot.FailImmediately);
            Assert.IsFalse(snapshot.ContinueAfterFail);
        }

        private static void SetField(Type owner, object target,
            string fieldName, object value)
        {
            owner.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void CompleteChange(GameRule rule,
            GameRuleConfig source)
        {
            using (GameRule.SessionConfigChange change =
                   rule.BeginSessionConfigChange(source))
            {
                change.Commit();
                change.Complete();
            }
        }
    }
}
