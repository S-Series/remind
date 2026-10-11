using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using REmind.Charting;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Characters;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;
using UnityEngine.TestTools;

namespace REmind.Gameplay.Tests
{
    public sealed class GameFlowBoundaryTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void PreparedSelectedSongWaitsForTransitionOutroBeforeTryingPlayback()
        {
            var appObject = new GameObject("Transition owner");
            var gameObject = new GameObject("Prepared Game scene");
            appObject.SetActive(false);
            gameObject.SetActive(false);
            AudioClip song = null;
            AppRoot previous = AppRoot.Current;
            try
            {
                var app = appObject.AddComponent<AppRoot>();
                var transition = appObject.AddComponent<SceneTransitionController>();
                SetField(app, "sceneTransition", transition);
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, app);
                app.SelectSong("i", "hard");
                SetField(transition, "transitioning", true);

                var source = gameObject.AddComponent<AudioSource>();
                var playback = gameObject.AddComponent<GamePlay>();
                SetField(playback, "audioSource", source);
                var manager = gameObject.AddComponent<GameManager>();
                SetField(manager, "gamePlay", playback);
                var presenter = gameObject.AddComponent<DemoPlayController>();
                var chart = gameObject.AddComponent<GameplayChartSessionController>();
                var flow = gameObject.AddComponent<GameFlowController>();
                SetField(flow, "gameManager", manager);
                SetField(flow, "notePresenter", presenter);
                SetField(flow, "chartSession", chart);

                song = AudioClip.Create("transition test", 44100, 1, 44100, false);
                source.clip = song;
                typeof(GameplayChartSessionController)
                    .GetProperty(nameof(GameplayChartSessionController.CurrentChart))
                    .SetValue(chart, FormatterServices.GetUninitializedObject(
                        typeof(PreparedGameplayChart)));

                int attempts = 0;
                playback.PlaybackStarting += _ =>
                {
                    attempts++;
                    return false;
                };
                Invoke(flow, "Update");
                Assert.AreEqual(0, attempts, "Loading must finish first.");

                typeof(DemoPlayController)
                    .GetProperty(nameof(DemoPlayController.IsReady))
                    .SetValue(presenter, true);
                Invoke(flow, "Update");
                Invoke(flow, "Update");
                flow.StartAutoSong();
                flow.Restart();
                Assert.AreEqual(0, attempts, "The prepared song remains idle under Outro.");
                Assert.AreEqual("Loading", GetField(flow, "screen").ToString());

                SetField(transition, "transitioning", false);
                LogAssert.Expect(LogType.Warning,
                    new Regex("Gameplay start was rejected"));
                Invoke(flow, "Update");
                Assert.AreEqual(1, attempts, "Only the first uncovered frame starts.");
                Assert.AreEqual("Error", GetField(flow, "screen").ToString());
                Invoke(flow, "Update");
                Assert.AreEqual(1, attempts, "A rejected start does not loop.");
            }
            finally
            {
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(appObject);
                if (song) UnityEngine.Object.DestroyImmediate(song);
            }
        }

        [Test]
        public void RuntimeEffectFailureOpensErrorOnceAndUnsubscribesOnDisable()
        {
            var root = new GameObject("Game flow error test");
            root.SetActive(false);
            GameRuleConfig ruleConfig = null;
            try
            {
                var source = root.AddComponent<AudioSource>();
                var playback = root.AddComponent<GamePlay>();
                SetField(playback, "audioSource", source);
                SetField(playback, "cameraTransform", root.transform);
                ruleConfig = ScriptableObject.CreateInstance<GameRuleConfig>();
                var rule = root.AddComponent<DefaultGameRule>();
                SetField(rule, "config", ruleConfig);
                var manager = root.AddComponent<GameManager>();
                SetField(manager, "gamePlay", playback);
                SetField(manager, "gameRule", rule);
                var effect = root.AddComponent<GameplayChartEffectController>();
                var flow = root.AddComponent<GameFlowController>();
                var errorPanel = new GameObject("Error panel");
                errorPanel.transform.SetParent(root.transform);
                errorPanel.SetActive(false);
                SetField(flow, "gameManager", manager);
                SetField(flow, "effectController", effect);
                SetField(flow, "errorPanel", errorPanel);

                root.SetActive(true);
                Invoke(flow, "OnEnable");
                Assert.AreEqual(1, FailureListeners(effect));
                SetScreen(flow, "Playing");
                SetField(playback, "startInProgress", true);
                NotifyFailure(effect, "rejected restart");
                Assert.AreEqual("Playing", GetField(flow, "screen").ToString(),
                    "A rejected Restart keeps the previous attempt for PresentStartOutcome.");
                Assert.IsFalse((bool)GetField(flow, "resultDispatched"));
                SetField(playback, "startInProgress", false);
                NotifyFailure(effect, "internal stack detail");
                Assert.AreEqual("Error", GetField(flow, "screen").ToString());
                Assert.IsTrue(errorPanel.activeSelf);
                Assert.IsTrue((bool)GetField(flow, "resultDispatched"));
                StringAssert.DoesNotContain("internal stack detail",
                    (string)GetField(flow, "startError"));
                NotifyFailure(effect, "second notification");
                Assert.AreEqual("Error", GetField(flow, "screen").ToString());

                root.SetActive(false);
                Invoke(flow, "OnDisable");
                Assert.AreEqual(0, FailureListeners(effect));
                root.SetActive(true);
                Invoke(flow, "OnEnable");
                Assert.AreEqual(1, FailureListeners(effect));
                SetScreen(flow, "Paused");
                NotifyFailure(effect, "paused failure");
                Assert.AreEqual("Error", GetField(flow, "screen").ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (ruleConfig) UnityEngine.Object.DestroyImmediate(ruleConfig);
            }
        }

        [Test]
        public void RejectedRestartPreservesPlayingAttemptButClearsItAfterCommitStopsPlayback()
        {
            var appObject = new GameObject("Restart attempt owner");
            var gameObject = new GameObject("Restart Game scene");
            appObject.SetActive(false);
            gameObject.SetActive(false);
            AppRoot previous = AppRoot.Current;
            GameRuleConfig ruleConfig = null;
            AudioClip song = null;
            try
            {
                var app = appObject.AddComponent<AppRoot>();
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, app);
                app.SelectSong("i", "hard");

                var source = gameObject.AddComponent<AudioSource>();
                var playback = gameObject.AddComponent<GamePlay>();
                SetField(playback, "audioSource", source);
                SetField(playback, "cameraTransform", gameObject.transform);
                ruleConfig = ScriptableObject.CreateInstance<GameRuleConfig>();
                var rule = gameObject.AddComponent<DefaultGameRule>();
                typeof(GameRule).GetField("config", PrivateInstance)
                    .SetValue(rule, ruleConfig);
                var manager = gameObject.AddComponent<GameManager>();
                SetField(manager, "gamePlay", playback);
                SetField(manager, "gameRule", rule);
                var judgementObject = new GameObject("Inactive judgement bridge");
                judgementObject.SetActive(false);
                judgementObject.transform.SetParent(gameObject.transform);
                var judgement = judgementObject.AddComponent<NoteJudgementSystem>();
                SetField(judgement, "gameManager", manager);
                SetField(judgement, "gameRule", rule);
                var chart = gameObject.AddComponent<GameplayChartSessionController>();
                var effect = gameObject.AddComponent<GameplayChartEffectController>();
                SetField(effect, "gameManager", manager);
                SetField(effect, "judgementSystem", judgement);
                var flow = gameObject.AddComponent<GameFlowController>();
                SetField(flow, "gameManager", manager);
                SetField(flow, "chartSession", chart);
                SetField(flow, "effectController", effect);
                var prepared = (PreparedGameplayChart)
                    FormatterServices.GetUninitializedObject(
                        typeof(PreparedGameplayChart));
                SetField(prepared, "<Metadata>k__BackingField",
                    new GameplayChartMetadata("i", "hard", "", ""));
                typeof(GameplayChartSessionController)
                    .GetProperty(nameof(GameplayChartSessionController.CurrentChart))
                    .SetValue(chart, prepared);

                song = AudioClip.Create("restart failure", 44100, 1, 44100, false);
                source.clip = song;
                gameObject.SetActive(true);
                SetScreen(flow, "Playing");
                SetField(playback, "<State>k__BackingField",
                    PlaybackState.Playing);
                var prior = new GameAttemptStartSnapshot(Guid.NewGuid(),
                    "i", "hard", false,
                    CharacterGaugeRuleSnapshot.Capture(ruleConfig), null);
                SetField(flow, "activeAttempt", prior);
                app.ActivateAttempt(prior);

                Func<double, bool> rejectBeforeCommit = _ => false;
                playback.PlaybackStarting += rejectBeforeCommit;
                LogAssert.Expect(LogType.Warning,
                    new Regex("Gameplay start was rejected"));
                flow.Restart();
                Assert.AreSame(prior, flow.CurrentAttempt);
                Assert.AreSame(prior, GetField(app, "activeAttempt"));
                Assert.AreEqual(PlaybackState.Playing, manager.PlaybackState);
                Assert.IsFalse((bool)GetField(flow, "resultDispatched"));
                playback.PlaybackStarting -= rejectBeforeCommit;

                PreparedEffectPlan plan = EffectPreparation.Prepare(
                    Array.Empty<PlayableEffectEvent>(), null,
                    new EffectRegistry()).Plan;
                Assert.IsNotNull(plan);
                var oldContext = new EffectSessionContext("i", "hard",
                    EffectExecutionMode.Gameplay);
                oldContext.Own(new ThrowingCleanupResource());
                var oldRunner = new EffectRunner(plan, oldContext);
                Type runtimeType = typeof(GameplayChartEffectController)
                    .GetNestedType("RuntimeSession", BindingFlags.NonPublic);
                object oldSession = Activator.CreateInstance(runtimeType, true);
                runtimeType.GetField("Runner").SetValue(oldSession, oldRunner);
                SetField(effect, "activeSession", oldSession);
                SetField(effect, "plan", plan);
                SetField(effect, "musicId", "i");
                SetField(effect, "difficultyId", "hard");
                SetField(effect, "acceptingStarts", true);
                playback.PlaybackStarting +=
                    (Func<double, bool>)Delegate.CreateDelegate(
                        typeof(Func<double, bool>), effect,
                        runtimeType.DeclaringType.GetMethod(
                            "HandlePlaybackStarting", PrivateInstance));
                playback.PlaybackCommitting +=
                    (Func<double, bool>)Delegate.CreateDelegate(
                        typeof(Func<double, bool>), effect,
                        runtimeType.DeclaringType.GetMethod(
                            "HandlePlaybackCommitting", PrivateInstance));
                LogAssert.Expect(LogType.Error,
                    new Regex("Effect session cleanup: Effect runtime cleanup failed"));
                LogAssert.Expect(LogType.Error,
                    new Regex("Previous Effect session cleanup failed"));
                LogAssert.Expect(LogType.Warning,
                    new Regex("Gameplay start was rejected"));
                flow.Restart();
                Assert.AreEqual(PlaybackState.Ready, manager.PlaybackState);
                Assert.IsNull(flow.CurrentAttempt);
                Assert.IsNull(GetField(app, "activeAttempt"));
                Assert.IsTrue((bool)GetField(flow, "resultDispatched"));
                Assert.IsFalse(rule.HasSessionConfig);
                Assert.AreEqual("Error", GetField(flow, "screen").ToString());
                Assert.IsFalse(app.TryTakeResult(out _));
                var staleResult = new GameResultSnapshot("i", "hard",
                    0d, 1_000_000, RankGrade.D, 0, 0, 0, 0, 0, 1,
                    false, false, false, prior);
                Assert.Throws<InvalidOperationException>(() =>
                    app.PublishResult(staleResult));
            }
            finally
            {
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(appObject);
                if (song) UnityEngine.Object.DestroyImmediate(song);
                if (ruleConfig) UnityEngine.Object.DestroyImmediate(ruleConfig);
            }
        }

        private static void NotifyFailure(GameplayChartEffectController effect,
            string message) => FailureDelegate(effect)?.Invoke(message);

        private static int FailureListeners(GameplayChartEffectController effect) =>
            FailureDelegate(effect)?.GetInvocationList().Length ?? 0;

        private static Action<string> FailureDelegate(
            GameplayChartEffectController effect) =>
            (Action<string>)typeof(GameplayChartEffectController)
                .GetField("PlaybackFailed", PrivateInstance).GetValue(effect);

        private static void SetScreen(GameFlowController flow, string name)
        {
            FieldInfo field = typeof(GameFlowController)
                .GetField("screen", PrivateInstance);
            field.SetValue(flow, Enum.Parse(field.FieldType, name));
        }

        private static object GetField(object target, string name) =>
            FindField(target.GetType(), name).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            FindField(target.GetType(), name).SetValue(target, value);

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type owner = type; owner != null; owner = owner.BaseType)
            {
                FieldInfo field = owner.GetField(name,
                    PrivateInstance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static void Invoke(object target, string name,
            params object[] arguments) =>
            target.GetType().GetMethod(name, PrivateInstance)
                .Invoke(target, arguments);

        private sealed class ThrowingCleanupResource : IDisposable
        {
            public void Dispose() =>
                throw new InvalidOperationException("owned cleanup failed");
        }
    }
}
