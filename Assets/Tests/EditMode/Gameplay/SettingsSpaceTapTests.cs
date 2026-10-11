using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Demo;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace REmind.Gameplay.Tests
{
    public sealed class SettingsSpaceTapTests
    {
        [Test]
        public void OverlayToggle_SavesReloadsAndRefreshesWithoutOverwritingOtherPreferences()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "remind-space-tap-test-" + Guid.NewGuid().ToString("N") + ".json");
            GameObject instance = null;
            try
            {
                LocalGameSettingsStore settings = LocalGameSettingsStore.Load(path);
                settings.SetLaneBinding(0, "<Keyboard>/a");
                settings.SetMusicVolume(0.4f);
                settings.SetJudgementOffsetMs(25d);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/SettingsOverlay.prefab");
                Assert.IsNotNull(prefab);
                instance = UnityEngine.Object.Instantiate(prefab);
                SettingsMenuController controller = instance.GetComponent<SettingsMenuController>();
                FieldInfo settingsField = typeof(SettingsMenuController).GetField("settings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                settingsField.SetValue(controller, settings);
                SerializedObject controllerData = new(controller);
                Component toggle = (Component)controllerData.FindProperty("spaceTapToggle").objectReferenceValue;
                Assert.IsNotNull(toggle, "The actual SettingsOverlay must have its Space Tap toggle wired.");
                Assert.AreEqual("SpaceTap", toggle.name);
                Assert.AreEqual("ControlsPage", toggle.transform.parent.name);
                FieldInfo changedField = toggle.GetType().GetField("onValueChanged");
                Assert.IsNotNull(changedField, "Toggle.onValueChanged must expose its serialized event.");
                UnityEvent<bool> changed = (UnityEvent<bool>)changedField.GetValue(toggle);
                Assert.AreEqual(1, changed.GetPersistentEventCount());
                Assert.AreEqual(controller, changed.GetPersistentTarget(0));
                Assert.AreEqual(nameof(SettingsMenuController.SetSpaceTapEnabled),
                    changed.GetPersistentMethodName(0));
                changed.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
                PropertyInfo isOn = toggle.GetType().GetProperty("isOn");
                controller.SetSpaceTapEnabled(false);
                isOn.SetValue(toggle, true);

                LocalGameSettingsStore reloaded = LocalGameSettingsStore.Load(path);
                Assert.IsTrue(reloaded.SpaceTapEnabled);
                Assert.AreEqual("<Keyboard>/a", reloaded.GetLaneBinding(0));
                Assert.AreEqual(0.4f, reloaded.MusicVolume);
                Assert.AreEqual(25d, reloaded.JudgementOffsetMs);
                settings.SetMusicVolume(0.7f);
                settings.Save();
                Assert.IsTrue(LocalGameSettingsStore.Load(path).SpaceTapEnabled);

                settingsField.SetValue(controller, reloaded);
                typeof(SettingsMenuController).GetMethod("Refresh",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.IsTrue((bool)isOn.GetValue(toggle));
                isOn.SetValue(toggle, false);
                Assert.IsFalse(LocalGameSettingsStore.Load(path).SpaceTapEnabled);
                Assert.AreEqual("<Keyboard>/a", LocalGameSettingsStore.Load(path).GetLaneBinding(0));

                reloaded.SetLaneBinding(0, "<Keyboard>/space");
                reloaded.Save();
                LogAssert.Expect(LogType.Error, "Could not save Game settings: " +
                    "Space is assigned to a lane. Change that key before enabling Space Tap.");
                isOn.SetValue(toggle, true);
                Assert.IsFalse((bool)isOn.GetValue(toggle), "A rejected change must restore the toggle.");
                Assert.IsFalse(LocalGameSettingsStore.Load(path).SpaceTapEnabled);
                Assert.AreEqual("<Keyboard>/space", LocalGameSettingsStore.Load(path).GetLaneBinding(0));

                SerializedObject scope = new(controllerData.FindProperty("scope").objectReferenceValue);
                SerializedProperty nodes = scope.FindProperty("nodes");
                bool registered = false;
                for (int i = 0; i < nodes.arraySize; i++)
                {
                    Component node = nodes.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                    if (node && node.gameObject == toggle.gameObject) registered = true;
                }
                Assert.IsTrue(registered, "Space Tap must participate in menu navigation.");
            }
            finally
            {
                if (instance) UnityEngine.Object.DestroyImmediate(instance);
                foreach (string candidate in new[] { path, path + ".bak", path + ".tmp" })
                    if (File.Exists(candidate)) File.Delete(candidate);
            }
        }
    }
}
