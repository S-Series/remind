using System;
using System.IO;
using REmind.Common.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.Gameplay.Demo
{
    /// <summary>Game preferences for the standalone legacy scene and Home overlay.</summary>
    [DisallowMultipleComponent]
    public sealed class SettingsMenuController : MonoBehaviour
    {
        [SerializeField] private MenuNavigationController navigation;
        [SerializeField] private NavigationScope scope;
        [SerializeField] private TMP_Text volumeValue;
        [SerializeField] private TMP_Text offsetValue;
        [SerializeField] private TMP_Text[] laneValues;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider voiceSlider;
        [SerializeField] private TMP_Text[] audioValues;
        [SerializeField] private TMP_Text[] hitStyleLabels;
        [SerializeField] private Toggle spatialToggle;
        [SerializeField] private Toggle muteUnfocusedToggle;
        [SerializeField] private Toggle reduceBgmToggle;
        [SerializeField] private Toggle keepBackgroundToggle;
        [SerializeField] private GameObject[] categoryPages;
        [SerializeField] private TMP_Text[] categoryLabels;

        private LocalGameSettingsStore settings;
        private bool overlayMode;
        private bool refreshing;
        private int pendingLane = -1;
        private int rebindStartFrame;

        private void Start()
        {
            EnsureSettings();
            Refresh();
        }

        private void OnEnable() =>
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (overlayMode) ReleaseNavigation();
        }

        public bool IsOverlayOpen => overlayMode && gameObject.activeInHierarchy;

        public void OpenOverlay(MenuNavigationController hostNavigation)
        {
            if (!hostNavigation || !scope || IsOverlayOpen) return;
            navigation = hostNavigation;
            overlayMode = true;
            scope.BindController(hostNavigation);
            gameObject.SetActive(true);
            EnsureSettings();
            SelectCategory(2);
            Refresh();
            hostNavigation.Push(scope);
        }

        private void EnsureSettings()
        {
            if (settings != null) return;
            settings = AppRoot.Current
                ? AppRoot.Current.Settings
                : LocalGameSettingsStore.Load(Path.Combine(
                    Application.persistentDataPath, "player-settings-v1.json"));
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

        public void SetMasterVolume(float value) => SaveAudio(
            () => settings.SetMasterVolume(value));
        public void SetBgmVolume(float value) => SaveAudio(
            () => settings.SetMusicVolume(value));
        public void SetSfxVolume(float value) => SaveAudio(
            () => settings.SetSfxVolume(value));
        public void SetVoiceVolume(float value) => SaveAudio(
            () => settings.SetVoiceVolume(value));
        public void SetHitSoundStyle(int value) => SaveAudio(
            () => settings.SetHitSoundStyle(
                (LocalGameSettingsStore.HitSoundStyle)value));
        public void SetSpatialAudio(bool value) => SaveAudio(
            () => settings.SetSpatialAudio(value));
        public void SetMuteWhenUnfocused(bool value) => SaveAudio(
            () => settings.SetMuteWhenUnfocused(value));
        public void SetReduceBgmDuringVoice(bool value) => SaveAudio(
            () => settings.SetReduceBgmDuringVoice(value));
        public void SetKeepAudioInBackground(bool value) => SaveAudio(
            () => settings.SetKeepAudioInBackground(value));

        public void OpenSystemSoundSettings()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Application.OpenURL("ms-settings:sound");
#else
            if (status) status.text =
                "Change the default output device in system settings.";
#endif
        }

        public void SelectCategory(int category)
        {
            if (categoryPages == null || category < 0 ||
                category >= categoryPages.Length) return;
            for (int i = 0; i < categoryPages.Length; i++)
                if (categoryPages[i]) categoryPages[i].SetActive(i == category);
            if (categoryLabels != null)
                for (int i = 0; i < categoryLabels.Length; i++)
                    if (categoryLabels[i])
                        categoryLabels[i].color = i == category
                            ? new Color(0.8f, 0.84f, 1f)
                            : new Color(0.65f, 0.7f, 0.84f);
            scope?.RebuildNavigation();
            if (EventSystem.current && categoryLabels != null &&
                category < categoryLabels.Length && categoryLabels[category])
                EventSystem.current.SetSelectedGameObject(
                    categoryLabels[category].transform.parent.gameObject);
        }

        private void SaveAudio(Action change)
        {
            if (refreshing || settings == null) return;
            try
            {
                change();
                settings.Save();
                if (AppRoot.Current) AppRoot.Current.ApplyAudioPreferences();
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
            scope?.SetNavigationActive(false);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
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
            if (overlayMode)
            {
                CloseOverlay();
            }
            else AppRoot.NavigateToScene("Home");
        }

        public void CloseOverlay()
        {
            if (!overlayMode) return;
            ReleaseNavigation();
            gameObject.SetActive(false);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            CloseOverlay();
        }

        private void ReleaseNavigation()
        {
            pendingLane = -1;
            if (navigation && navigation.ActiveScope == scope)
                navigation.Pop();
            scope?.BindController(null);
            navigation = null;
            overlayMode = false;
        }

        private void EndRebind(string message)
        {
            pendingLane = -1;
            Refresh();
            if (status) status.text = message;
            scope?.SetNavigationActive(true);
            navigation?.FocusActive();
        }

        private void Refresh()
        {
            if (settings == null) return;
            refreshing = true;
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
            if (masterSlider) masterSlider.SetValueWithoutNotify(settings.MasterVolume);
            if (bgmSlider) bgmSlider.SetValueWithoutNotify(settings.MusicVolume);
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(settings.SfxVolume);
            if (voiceSlider) voiceSlider.SetValueWithoutNotify(settings.VoiceVolume);
            float[] volumes = { settings.MasterVolume, settings.MusicVolume,
                settings.SfxVolume, settings.VoiceVolume };
            if (audioValues != null)
                for (int i = 0; i < Math.Min(audioValues.Length, volumes.Length); i++)
                    if (audioValues[i])
                        audioValues[i].text = Mathf.RoundToInt(volumes[i] * 100f).ToString();
            if (hitStyleLabels != null)
                for (int i = 0; i < hitStyleLabels.Length; i++)
                    if (hitStyleLabels[i])
                        hitStyleLabels[i].color = i == (int)settings.HitSound
                            ? new Color(0.93f, 0.94f, 1f) :
                            new Color(0.55f, 0.6f, 0.74f);
            if (spatialToggle)
                spatialToggle.SetIsOnWithoutNotify(settings.SpatialAudio);
            if (muteUnfocusedToggle)
                muteUnfocusedToggle.SetIsOnWithoutNotify(
                    settings.MuteWhenUnfocused);
            if (reduceBgmToggle)
                reduceBgmToggle.SetIsOnWithoutNotify(
                    settings.ReduceBgmDuringVoice);
            if (keepBackgroundToggle)
                keepBackgroundToggle.SetIsOnWithoutNotify(
                    settings.KeepAudioInBackground);
            refreshing = false;
        }

        private void ShowError(Exception exception)
        {
            if (status) status.text = exception.Message;
            Debug.LogError("Could not save Game settings: " +
                exception.Message, this);
        }
    }
}
