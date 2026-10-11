using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Input.Judgement;
using REmind.Gameplay.Input.Routing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace REmind.Gameplay.Tests
{
    public sealed class GameComponentDisableTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void DisablingJudgementAloneStopsPlaybackAndInvalidatesAttempt()
        {
            using (var fixture = new Fixture())
            {
                int failures = 0;
                fixture.Judgement.PlaybackFailed += _ => failures++;
                LogAssert.Expect(LogType.Error,
                    new Regex("Judgement component was disabled during playback"));

                fixture.Judgement.enabled = false;
                InvokeLifecycle(fixture.Judgement, "OnDisable");

                Assert.That(failures, Is.EqualTo(1));
                fixture.AssertInterrupted();
                fixture.Judgement.enabled = true;
                InvokeLifecycle(fixture.Judgement, "OnEnable");
                Assert.That(failures, Is.EqualTo(1));
                fixture.AssertInterrupted();
            }
        }

        [Test]
        public void DisablingEffectAloneReportsFailureEvenWhenCleanupSucceeds()
        {
            using (var fixture = new Fixture())
            {
                int failures = 0;
                fixture.Effect.PlaybackFailed += _ => failures++;
                LogAssert.Expect(LogType.Error,
                    new Regex("Effect component was disabled during playback"));

                fixture.Effect.enabled = false;
                InvokeLifecycle(fixture.Effect, "OnDisable");

                Assert.That(failures, Is.EqualTo(1));
                fixture.AssertInterrupted();
                fixture.Effect.enabled = true;
                InvokeLifecycle(fixture.Effect, "OnEnable");
                Assert.That(failures, Is.EqualTo(1));
                fixture.AssertInterrupted();
            }
        }

        [Test]
        public void WholeGameObjectTeardownDoesNotReportComponentFailure()
        {
            using (var fixture = new Fixture())
            {
                int failures = 0;
                fixture.Judgement.PlaybackFailed += _ => failures++;
                fixture.Effect.PlaybackFailed += _ => failures++;

                fixture.Root.SetActive(false);
                fixture.DisableComponents();
                Assert.That(failures, Is.Zero);
                Assert.That(fixture.Manager.PlaybackState,
                    Is.EqualTo(PlaybackState.Empty));
                fixture.Root.SetActive(true);
                fixture.EnableComponents();
                fixture.Playing();
                fixture.DisableComponents();
                fixture.DestroyGameRoot();
                Assert.That(failures, Is.Zero);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject appObject;
            private readonly AppRoot previousApp;
            private readonly GameRuleConfig config;
            private bool rootDestroyed;

            public GameObject Root { get; }
            public GameManager Manager { get; }
            public GamePlay Playback { get; }
            public GameFlowController Flow { get; }
            public NoteJudgementSystem Judgement { get; }
            public GameplayChartEffectController Effect { get; }
            public AppRoot App { get; }

            public Fixture()
            {
                previousApp = AppRoot.Current;
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, null);
                appObject = new GameObject("Disable test AppRoot");
                appObject.SetActive(false);
                App = appObject.AddComponent<AppRoot>();

                Root = new GameObject("Disable test Game scene");
                Root.SetActive(false);
                var source = Root.AddComponent<AudioSource>();
                Playback = Root.AddComponent<GamePlay>();
                SetField(Playback, "audioSource", source);
                SetField(Playback, "cameraTransform", Root.transform);
                config = ScriptableObject.CreateInstance<GameRuleConfig>();
                var rule = Root.AddComponent<DefaultGameRule>();
                SetField(rule, "config", config);
                Manager = Root.AddComponent<GameManager>();
                SetField(Manager, "gamePlay", Playback);
                SetField(Manager, "gameRule", rule);

                var router = Root.AddComponent<RhythmInputRouter>();
                UnityEngine.Object inputActions = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/InputSystem_Actions.inputactions");
                Assert.That(inputActions, Is.Not.Null);
                SetField(router, "inputActions", inputActions);
                Judgement = Root.AddComponent<NoteJudgementSystem>();
                SetField(Judgement, "inputRouter", router);
                SetField(Judgement, "gameManager", Manager);
                SetField(Judgement, "gameRule", rule);
                Effect = Root.AddComponent<GameplayChartEffectController>();
                SetField(Effect, "gameManager", Manager);
                SetField(Effect, "judgementSystem", Judgement);
                Flow = Root.AddComponent<GameFlowController>();
                SetField(Flow, "gameManager", Manager);
                SetField(Flow, "judgementSystem", Judgement);
                SetField(Flow, "effectController", Effect);
                var errorPanel = new GameObject("Error panel");
                errorPanel.transform.SetParent(Root.transform);
                errorPanel.SetActive(false);
                SetField(Flow, "errorPanel", errorPanel);
                Root.SetActive(true);
                // EditMode activation does not dispatch these MonoBehaviour callbacks.
                EnableComponents();

                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, App);
                App.SelectSong("i", "hard");
                var attempt = new GameAttemptStartSnapshot(Guid.NewGuid(),
                    "i", "hard", false,
                    REmind.Gameplay.Characters.CharacterGaugeRuleSnapshot
                        .Capture(config), null);
                App.ActivateAttempt(attempt);
                SetField(Flow, "activeAttempt", attempt);
                SetScreen(Flow, "Playing");
                Playing();
            }

            public void Playing() =>
                SetField(Playback, "<State>k__BackingField",
                    PlaybackState.Playing);

            public void EnableComponents()
            {
                InvokeLifecycle(Judgement, "OnEnable");
                InvokeLifecycle(Effect, "OnEnable");
                InvokeLifecycle(Flow, "OnEnable");
            }

            public void DisableComponents()
            {
                InvokeLifecycle(Judgement, "OnDisable");
                InvokeLifecycle(Effect, "OnDisable");
                InvokeLifecycle(Flow, "OnDisable");
            }

            public void AssertInterrupted()
            {
                Assert.That(Manager.PlaybackState, Is.EqualTo(PlaybackState.Empty));
                Assert.That(Flow.CurrentAttempt, Is.Null);
                Assert.That(GetField(App, "activeAttempt"), Is.Null);
                Assert.That(GetField(Flow, "resultDispatched"), Is.True);
                Assert.That(GetField(Flow, "screen").ToString(), Is.EqualTo("Error"));
                Assert.That(App.TryTakeResult(out _), Is.False);
            }

            public void DestroyGameRoot()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                rootDestroyed = true;
            }

            public void Dispose()
            {
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, previousApp);
                if (!rootDestroyed) UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(appObject);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void SetScreen(GameFlowController flow, string name)
        {
            FieldInfo field = FindField(flow.GetType(), "screen");
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

        private static void InvokeLifecycle(object target, string name) =>
            target.GetType().GetMethod(name, PrivateInstance)
                .Invoke(target, null);
    }
}
