using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using REmind.Charting;
using REmind.Gameplay.Input.Routing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace REmind.Gameplay.Tests
{
    public sealed class CenterTapInputRouterTests
    {
        [Test]
        public void SpaceOptionRefreshesWithCachedBindingsAndNeverSetsShiftHeld()
        {
            using (var fixture = new Fixture())
            {
                fixture.Router.SetInputEnabled(true);
                fixture.Keys(Key.RightShift);
                fixture.Keys(Key.RightShift, Key.LeftShift);
                fixture.Keys(Key.RightShift, Key.LeftShift, Key.Space);
                CollectionAssert.AreEqual(new[] { 9, 8 }, fixture.Presses);

                fixture.Keys();
                fixture.Settings.SetSpaceTapEnabled(true);
                fixture.Router.SetInputEnabled(true);
                fixture.Keys(Key.Space);
                Assert.That(fixture.Presses[fixture.Presses.Count - 1], Is.EqualTo(10));
                Assert.That(fixture.Router.IsLanePressed(8), Is.False);
                Assert.That(fixture.Router.IsLanePressed(9), Is.False);
                Assert.That(fixture.Router.IsLanePressed(10), Is.True);

                fixture.Settings.SetSpaceTapEnabled(false);
                fixture.Router.RefreshSpaceTap();
                Assert.That(fixture.SpaceAction.enabled, Is.False);
                fixture.Keys();
                int count = fixture.Presses.Count;
                fixture.Keys(Key.Space);
                Assert.That(fixture.Presses.Count, Is.EqualTo(count));
            }
        }

        [Test]
        public void SpaceRefreshCannotEnableDisabledRhythmMap()
        {
            using (var fixture = new Fixture())
            {
                fixture.Settings.SetSpaceTapEnabled(true);
                fixture.Router.SetInputEnabled(true);
                fixture.Router.SetInputEnabled(false);
                fixture.Router.RefreshSpaceTap();
                Assert.That(fixture.SpaceAction.enabled, Is.False);
                fixture.Keys(Key.Space, Key.LeftShift);
                Assert.That(fixture.Presses, Is.Empty);

                fixture.Keys();
                fixture.Router.SetInputEnabled(true);
                fixture.Keys(Key.Space);
                CollectionAssert.AreEqual(new[] { 10 }, fixture.Presses);
            }
        }

        [TestCase(false, ChartJudgementGrade.Miss)]
        [TestCase(true, ChartJudgementGrade.PerfectPlus)]
        public void SpaceOptionAlsoGatesCentralHoldButNeverMovesKnob(bool enabled, ChartJudgementGrade grade)
        {
            using (var fixture = new Fixture())
            {
                fixture.Settings.SetSpaceTapEnabled(enabled);
                fixture.Router.SetInputEnabled(true);
                var results = new List<ChartJudgementResolution>();
                var session = HoldSession(results);
                fixture.Router.InputPerformed += input => session.Input(input.Lane, 0d, input.Pressed);
                fixture.Router.InputReleased += input => session.Input(input.Lane, 0d, input.Pressed);
                fixture.Keys(Key.Space);
                session.ProcessAutomatic(1000d, false);
                Assert.That(results.Find(r => r.Note.Kind == ChartNoteKind.CenterHold).Grade, Is.EqualTo(grade));
                Assert.That(session.PlayerKnobX, Is.Zero);
                Assert.That(results.Exists(r => r.Note.Kind == ChartNoteKind.Knob &&
                    r.Grade == ChartJudgementGrade.Miss), Is.True);
            }
        }

        [TestCase(false, ChartJudgementGrade.Miss)]
        [TestCase(true, ChartJudgementGrade.PerfectPlus)]
        public void ShiftToSpaceSwapUsesActualRoutedDownAndUp(bool enabled, ChartJudgementGrade grade)
        {
            using (var fixture = new Fixture())
            {
                fixture.Settings.SetSpaceTapEnabled(enabled);
                fixture.Router.SetInputEnabled(true);
                var results = new List<ChartJudgementResolution>();
                var session = HoldSession(results);
                double time = 0d;
                fixture.Router.InputPerformed += input => session.Input(input.Lane, time, input.Pressed);
                fixture.Router.InputReleased += input => session.Input(input.Lane, time, input.Pressed);
                fixture.Keys(Key.LeftShift);
                session.ProcessAutomatic(0d, false);
                time = 500d;
                fixture.Keys(Key.Space);
                session.ProcessAutomatic(1000d, false);
                Assert.That(results.Find(r => r.Note.Kind == ChartNoteKind.CenterHold).Grade, Is.EqualTo(grade));
            }
        }

        private static PlayableJudgementSession HoldSession(List<ChartJudgementResolution> results)
        {
            var document = new ChartDocument(4800, 4, 120d);
            document.Notes.Add(new ChartDocumentNote("hold", ChartNoteKind.CenterHold, 10, 0, 2400));
            var knob = new ChartDocumentNote("knob", ChartNoteKind.Knob, 10, 0, 2400);
            knob.Points[1].KnobX = 1d;
            document.Notes.Add(knob);
            var compiled = ChartCompiler.Compile(document);
            Assert.That(compiled.Succeeded, Is.True);
            return new PlayableJudgementSession(compiled.Snapshot,
                (_, __, offset) => Math.Abs(offset) <= 175d ? ChartJudgementGrade.PerfectPlus
                    : ChartJudgementGrade.None, (_, __) => 175d, results.Add);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly RhythmInputRouter Router;
            public readonly LocalGameSettingsStore Settings;
            public readonly List<int> Presses = new List<int>();
            public InputAction SpaceAction => Router.InputActions.FindAction("Rhythm/CentralTapSpace");
            private readonly GameObject root = new GameObject("Center Tap input test");
            private readonly InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            private readonly Keyboard keyboard;
            private readonly InputSettings previousInputSettings = InputSystem.settings;
            private readonly bool previousRunInBackground = InputSystem.runInBackground;
            private readonly InputSettings inputSettings;
            private readonly AppRoot previous = AppRoot.Current;

            public Fixture()
            {
                inputSettings = UnityEngine.Object.Instantiate(previousInputSettings);
                inputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                inputSettings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = inputSettings;
                // Native runtime rejects even explicit player updates outside PlayMode without this flag.
                inputSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
                InputSystem.runInBackground = true;
                keyboard = InputSystem.AddDevice<Keyboard>();
                root.SetActive(false);
                var app = root.AddComponent<AppRoot>();
                Settings = LocalGameSettingsStore.Load(Path.Combine(Path.GetTempPath(),
                    "remind-center-input-" + Guid.NewGuid().ToString("N") + ".json"));
                SetField(app, "settings", Settings);
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current)).SetValue(null, app);
                var map = new InputActionMap("Rhythm");
                for (int lane = 0; lane < LocalGameSettingsStore.LaneCount; lane++)
                    map.AddAction("Lane" + (lane + 1).ToString("00"), InputActionType.Button,
                        Settings.GetLaneBinding(lane));
                map.AddAction("CentralTapSpace", InputActionType.Button, "<Keyboard>/space");
                actions.AddActionMap(map);
                Router = root.AddComponent<RhythmInputRouter>();
                SetField(Router, "inputActions", actions);
                Router.InputPerformed += input => Presses.Add(input.Lane);
            }

            public void Keys(params Key[] keys)
            {
                Assert.That(Router.IsReady, Is.True, "Router did not bind its runtime actions.");
                Assert.That(keyboard.enabled, Is.True, "Synthetic keyboard is disabled.");
                foreach (InputAction action in Router.InputActions.FindActionMap("Rhythm").actions)
                {
                    if (!action.enabled) continue;
                    bool resolvesKeyboard = false;
                    foreach (InputControl control in action.controls)
                        resolvesKeyboard |= control.device == keyboard;
                    Assert.That(resolvesKeyboard, Is.True,
                        action.name + " did not resolve the synthetic keyboard.");
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                InputSystem.Update();
                Assert.That(keyboard.rightShiftKey.isPressed,
                    Is.EqualTo(Array.IndexOf(keys, Key.RightShift) >= 0), "Synthetic right Shift was not processed.");
                Assert.That(keyboard.leftShiftKey.isPressed,
                    Is.EqualTo(Array.IndexOf(keys, Key.LeftShift) >= 0), "Synthetic left Shift was not processed.");
                Assert.That(keyboard.spaceKey.isPressed,
                    Is.EqualTo(Array.IndexOf(keys, Key.Space) >= 0), "Synthetic Space was not processed.");
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(actions);
                InputSystem.RemoveDevice(keyboard);
                // Reset the manager flag before restoring an asset whose feature flags may be null.
                inputSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", false);
                InputSystem.settings = previousInputSettings;
                InputSystem.runInBackground = previousRunInBackground;
                UnityEngine.Object.DestroyImmediate(inputSettings);
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current)).SetValue(null, previous);
            }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
