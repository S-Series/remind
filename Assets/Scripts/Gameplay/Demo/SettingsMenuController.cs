using System;
using System.IO;
using REmind.Common.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace REmind.Gameplay.Demo
{
    /// <summary>Functional settings screen for the Game's local preferences.</summary>
    [DisallowMultipleComponent]
    public sealed class SettingsMenuController : MonoBehaviour
    {
        [SerializeField] private MenuNavigationController navigation;
        [SerializeField] private NavigationScope scope;
        [SerializeField] private TMP_Text volumeValue;
        [SerializeField] private TMP_Text offsetValue;
        [SerializeField] private TMP_Text[] laneValues;
        [SerializeField] private TMP_Text status;

        private LocalGameSettingsStore settings;
        private int pendingLane = -1;
        private int rebindStartFrame;

        private void Start()
        {
            settings = AppRoot.Current
                ? AppRoot.Current.Settings
                : LocalGameSettingsStore.Load(Path.Combine(
                    Application.persistentDataPath, "player-settings-v1.json"));
            Refresh();
        }

        private void Update()
        {
            if (pendingLane < 0 || Time.frameCount == rebindStartFrame ||
                Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                EndRebind("Key change cancelled.");
                return;
            }
            foreach (KeyControl key in Keyboard.current.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                string binding = "<Keyboard>/" + key.name;
                try
                {
                    settings.SetLaneBinding(pendingLane, binding);
                    settings.Save();
                    EndRebind("Lane " + (pendingLane + 1) + " key saved.");
                }
                catch (Exception exception)
                {
                    if (status) status.text = exception.Message;
                }
                return;
            }
        }

        public void ChangeMusicVolume(int steps)
        {
            if (settings == null) return;
            try
            {
                float value = Mathf.Clamp01(Mathf.Round(
                    (settings.MusicVolume + steps * 0.05f) * 20f) / 20f);
                settings.SetMusicVolume(value);
                settings.Save();
                Refresh();
            }
            catch (Exception exception) { ShowError(exception); }
        }

        public void ChangeJudgementOffset(int steps)
        {
            if (settings == null) return;
            try
            {
                double value = Math.Max(
                    -LocalGameSettingsStore.MaximumJudgementOffsetMs,
                    Math.Min(LocalGameSettingsStore.MaximumJudgementOffsetMs,
                        settings.JudgementOffsetMs + steps * 5d));
                settings.SetJudgementOffsetMs(value);
                settings.Save();
                Refresh();
            }
            catch (Exception exception) { ShowError(exception); }
        }

        public void BeginRebind(int lane)
        {
            if (settings == null || lane < 0 ||
                lane >= LocalGameSettingsStore.LaneCount) return;
            pendingLane = lane;
            rebindStartFrame = Time.frameCount;
            navigation?.Clear();
            if (status)
                status.text = "Press a key for lane " + (lane + 1) +
                    ". ESC cancels. Each lane needs a unique key.";
        }

        public void ResetBindings()
        {
            if (settings == null) return;
            try
            {
                settings.ResetBindings();
                settings.Save();
                Refresh();
                if (status) status.text = "Default lane keys restored.";
            }
            catch (Exception exception) { ShowError(exception); }
        }

        public void ReturnHome()
        {
            if (pendingLane >= 0)
            {
                EndRebind("Key change cancelled.");
                return;
            }
            SceneManager.LoadScene("Home");
        }

        private void EndRebind(string message)
        {
            pendingLane = -1;
            Refresh();
            if (status) status.text = message;
            navigation?.ShowRoot(scope);
        }

        private void Refresh()
        {
            if (settings == null) return;
            if (volumeValue)
                volumeValue.text = Mathf.RoundToInt(settings.MusicVolume * 100f) + "%";
            if (offsetValue)
                offsetValue.text = settings.JudgementOffsetMs.ToString(
                    "+0;-0;0") + " ms";
            if (laneValues != null)
                for (int i = 0; i < Math.Min(laneValues.Length,
                         LocalGameSettingsStore.LaneCount); i++)
                    if (laneValues[i])
                        laneValues[i].text = "Lane " + (i + 1) + "  " +
                            settings.GetLaneBinding(i).Substring(11).ToUpperInvariant();
            if (status) status.text = "Changes are saved immediately.";
        }

        private void ShowError(Exception exception)
        {
            if (status) status.text = exception.Message;
            Debug.LogError("Could not save Game settings: " +
                exception.Message, this);
        }
    }
}
