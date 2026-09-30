using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using REmind.Charting;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Input.Judgement;
using REmind.Gameplay.Input.Routing;
using REmind.Gameplay.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using REmind.Gameplay;
using REmind.Common.UI;
using TMPro;

/// <summary>Batch PlayMode smoke for Music Select through actual gameplay.</summary>
[InitializeOnLoad]
public static class PlayableGameSmokeRunner
{
    private const string ActiveKey = "REmind.GamePlaySmoke.Active";
    private const string DataPathKey = "REmind.GamePlaySmoke.DataPath";
    private const string SettingsPathKey = "REmind.GamePlaySmoke.SettingsPath";
    private const string HardKey = "REmind.GamePlaySmoke.WithHard";
    private const string HardRulePath =
        "Assets/Settings/Gameplay/Rules/DevelopmentHardGameRuleConfig.asset";
    private enum HardPhase { None, Select, Start, Resume, Retry, Result }
    private static readonly string[] SongIds = { "designant", "i" };
    private static readonly string[] SongTitles = { "Designant", "I" };
    private static readonly string[] TemporaryScreens =
        { "Story", "Character", "ReMind", "Option", "Music" };
    private static readonly bool[] checkedPauseResume = new bool[SongIds.Length];
    private static int frames;
    private static int bootstrapReadyFrames;
    private static bool bootstrapContinueRequested;
    private static double smokeStartedAt;
    private static int songIndex;
    private static bool checkedScene;
    private static bool openedMusicSelect;
    private static bool openedSettingsMenu;
    private static bool checkingSettingsMenu;
    private static bool checkedSettingsSceneChange;
    private static bool checkingTemporaryScreen;
    private static int temporaryScreenIndex;
    private static bool startedSelection;
    private static float transitionFirstElapsed;
    private static float transitionStartedAt;
    private static bool transitionAdvanced;
    private static bool assignedTestData;
    private static bool checkingResult;
    private static bool returningToSelect;
    private static bool attemptedFailure;
    private static bool waitingForRetry;
    private static bool waitingForPauseResume;
    private static int pauseOpenedFrame;
    private static bool checkingFailedResult;
    private static bool checkedNormalIsolation;
    private static double expectedScore;
    private static HardPhase hardPhase;
    private static GameRuleConfig hardRuleSource;
    private static CharacterAbilityDefinition hardAbility;
    private static GameAttemptStartSnapshot hardFirstAttempt;
    private static GameAttemptStartSnapshot hardRetryAttempt;
    private static GameResultSnapshot hardResult;
    private static int hardPauseFrame;
    private static GameRuleConfig hardLiveConfig;
    private static int hardObservedMisses;
    private static string hardJudgementError;
    private static GamePlay hardCapturePlayback;
    private static GameAttemptStartSnapshot hardAttemptAtStart;
    private static double hardHealthAtStart;
    private static int hardStartCaptureCount;
    private static double hardHealthBeforePause;

    static PlayableGameSmokeRunner()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(HardKey, false);
        frames = 0;
        bootstrapReadyFrames = 0;
        bootstrapContinueRequested = false;
        smokeStartedAt = EditorApplication.timeSinceStartup;
        songIndex = 0;
        Array.Clear(checkedPauseResume, 0, checkedPauseResume.Length);
        checkedScene = false;
        openedMusicSelect = false;
        openedSettingsMenu = false;
        checkingSettingsMenu = false;
        checkedSettingsSceneChange = false;
        checkingTemporaryScreen = false;
        temporaryScreenIndex = 0;
        startedSelection = false;
        transitionFirstElapsed = 0f;
        transitionStartedAt = 0f;
        transitionAdvanced = false;
        assignedTestData = false;
        checkingResult = false;
        returningToSelect = false;
        attemptedFailure = false;
        waitingForRetry = false;
        waitingForPauseResume = false;
        pauseOpenedFrame = -1;
        checkingFailedResult = false;
        checkedNormalIsolation = false;
        expectedScore = 0d;
        hardPhase = HardPhase.None;
        hardRuleSource = null;
        hardAbility = null;
        hardFirstAttempt = null;
        hardRetryAttempt = null;
        hardResult = null;
        hardPauseFrame = -1;
        hardLiveConfig = null;
        hardObservedMisses = 0;
        hardJudgementError = null;
        hardCapturePlayback = null;
        hardAttemptAtStart = null;
        hardHealthAtStart = double.NaN;
        hardStartCaptureCount = 0;
        hardHealthBeforePause = double.NaN;
        SessionState.SetString(DataPathKey, Path.Combine(Path.GetTempPath(),
            "remind-flow-smoke-" + Guid.NewGuid().ToString("N") + ".json"));
        SessionState.SetString(SettingsPathKey, Path.Combine(Path.GetTempPath(),
            "remind-settings-smoke-" + Guid.NewGuid().ToString("N") +
            ".json"));
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
        EditorApplication.update += Tick;
        Debug.Log("GAME_PLAYMODE_SMOKE_STARTED");
        EditorApplication.isPlaying = true;
    }

    /// <summary>Runs the existing two-song flow, then a selected Hard attempt.</summary>
    public static void RunWithHard()
    {
        Run();
        SessionState.SetBool(HardKey, true);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            frames++;
            if (EditorApplication.timeSinceStartup - smokeStartedAt > 240d)
                throw new TimeoutException(
                    "Full play flow was not ready after 240 seconds.");
            if (SceneManager.GetActiveScene().name == "Bootstrap")
            {
                BootstrapLoadingController bootstrap = UnityEngine.Object
                    .FindFirstObjectByType<BootstrapLoadingController>();
                if (!bootstrap)
                    throw new InvalidOperationException(
                        "Bootstrap loading controller is missing.");
                if (AppRoot.Current && AppRoot.Current.IsTransitioning)
                {
                    if (bootstrapContinueRequested) return;
                    throw new InvalidOperationException(
                        "Bootstrap advanced without interaction.");
                }
                if (!bootstrap.IsReadyForInteraction) return;
                if (++bootstrapReadyFrames < 3) return;
                if (!bootstrap.TryContinue())
                    throw new InvalidOperationException(
                        "Bootstrap did not accept interaction after loading.");
                bootstrapContinueRequested = true;
                return;
            }
            if (AppRoot.Current && AppRoot.Current.IsTransitioning)
            {
                if (startedSelection && !transitionAdvanced &&
                    SceneManager.GetActiveScene().name == "MusicSelect" &&
                    Time.unscaledTime - transitionStartedAt > 0.5f)
                {
                    SceneTransitionController current = AppRoot.Current
                        .GetComponentInChildren<SceneTransitionController>(true);
                    if (!current || current.TransitionElapsed <=
                        transitionFirstElapsed + 0.25f)
                        throw new InvalidOperationException(
                            "Crystal overlay did not advance before the scene load.");
                    transitionAdvanced = true;
                }
                return;
            }
            if (checkingTemporaryScreen)
            {
                if (SceneManager.GetActiveScene().name !=
                    TemporaryScreens[temporaryScreenIndex]) return;
                TemporaryMenuActions temporary = UnityEngine.Object
                    .FindFirstObjectByType<TemporaryMenuActions>();
                if (!temporary || !EventSystem.current ||
                    UnityEngine.Object.FindObjectsByType<Button>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None).Length == 0)
                    throw new InvalidOperationException(
                        "Temporary screen has no usable menu: " +
                        TemporaryScreens[temporaryScreenIndex]);
                temporary.ReturnHome();
                temporaryScreenIndex++;
                checkingTemporaryScreen = false;
                return;
            }
            if (hardPhase != HardPhase.None)
            {
                TickHard();
                return;
            }
            if (returningToSelect)
            {
                if (SceneManager.GetActiveScene().name != "MusicSelect") return;
                if (!AppRoot.Current.TryGetSelectedSong(
                        out AppRoot.SongSelection returned) ||
                    returned.MusicId != SongIds[songIndex] ||
                    returned.DifficultyId != "hard")
                    throw new InvalidOperationException(
                        "Gameplay did not preserve the selected song on return.");
                LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(
                    SessionState.GetString(DataPathKey, string.Empty));
                bool hasBest = AppRoot.Current.TryGetBestRecord(
                    SongIds[songIndex], "hard", out double bestScore, out _);
                bool hasSavedBest = reloaded.TryGetBestRecord(
                    SongIds[songIndex], "hard", out double savedScore, out _);
                if (hasBest != (songIndex == 0) ||
                    hasSavedBest != (songIndex == 0) ||
                    songIndex == 0 && (bestScore != 0d || savedScore != 0d) ||
                    AppRoot.Current.TryTakeResult(out _))
                    throw new InvalidOperationException(
                        "Auto-used play entered records or lost the prior failed record.");
                bool hasProgress = reloaded.TryGetProgress(
                    SongIds[songIndex], "hard",
                    out ChartProgressSnapshot progress);
                if (hasProgress != (songIndex == 0) ||
                    songIndex == 0 && (progress.PlayCount != 1 ||
                        progress.Cleared || progress.MaxCombo != 0) ||
                    reloaded.MemoryFragmentCount != 0)
                    throw new InvalidOperationException(
                        "Auto-used play changed saved progression.");
                if (songIndex == 0 &&
                    (!AppRoot.Current.IsFavorite(SongIds[0]) ||
                     !reloaded.IsFavorite(SongIds[0])))
                    throw new InvalidOperationException(
                        "The selected song's favorite was not persisted.");
                LocalGameSettingsStore savedSettings =
                    LocalGameSettingsStore.Load(SessionState.GetString(
                        SettingsPathKey, string.Empty));
                if (Math.Abs(savedSettings.MusicVolume - 0.6f) > 0.001f ||
                    savedSettings.JudgementOffsetMs != -25d ||
                    savedSettings.GetLaneBinding(0) != "<Keyboard>/a")
                    throw new InvalidOperationException(
                        "Player settings were not preserved across the song flow.");
                if (++songIndex < SongIds.Length)
                {
                    checkedScene = false;
                    startedSelection = false;
                    checkingResult = false;
                    returningToSelect = false;
                    expectedScore = 0d;
                    return;
                }
                if (SessionState.GetBool(HardKey, false))
                {
                    returningToSelect = false;
                    startedSelection = false;
                    hardPhase = HardPhase.Select;
                    return;
                }
                Debug.Log("GAME_PLAYMODE_SMOKE_PASSED");
                Complete(0);
                return;
            }
            if (checkingResult)
            {
                if (SceneManager.GetActiveScene().name != "Result") return;
                ResultScenePresenter result = UnityEngine.Object
                    .FindFirstObjectByType<ResultScenePresenter>();
                ResultMenuActions actions = UnityEngine.Object
                    .FindFirstObjectByType<ResultMenuActions>();
                if (!result || !actions) return;
                if (checkingFailedResult)
                {
                    TMP_Text caption = (TMP_Text)typeof(ResultScenePresenter)
                        .GetField("rankCaption", BindingFlags.Instance |
                            BindingFlags.NonPublic).GetValue(result);
                    if (!caption || caption.text != "FAILED")
                        throw new InvalidOperationException(
                            "A failed play did not reach the failed Result screen.");
                    if (!AppRoot.Current.TryGetChartProgress(SongIds[0],
                            "hard", out ChartProgressSnapshot failedProgress) ||
                        failedProgress.PlayCount != 1 ||
                        failedProgress.Cleared ||
                        AppRoot.Current.LastResultWasFirstClear ||
                        AppRoot.Current.MemoryFragmentCount != 0)
                        throw new InvalidOperationException(
                            "A failed attempt was stored as a first clear.");
                    GameObject failedReward = (GameObject)typeof(
                        ResultScenePresenter).GetField("rewardSection",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(result);
                    if (failedReward && failedReward.activeSelf)
                        throw new InvalidOperationException(
                            "A failed attempt displayed a first-clear reward.");
                    checkingResult = false;
                    checkingFailedResult = false;
                    waitingForRetry = true;
                    waitingForPauseResume = false;
                    checkedScene = false;
                    actions.Retry();
                    return;
                }
                TMP_Text title = (TMP_Text)typeof(ResultScenePresenter)
                    .GetField("songTitle", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(result);
                if (!title || title.text != SongTitles[songIndex])
                    throw new InvalidOperationException(
                        "Result scene did not show the selected song.");
                TMP_Text scoreLabel = (TMP_Text)typeof(ResultScenePresenter)
                    .GetField("scoreValue", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(result);
                if (!scoreLabel || scoreLabel.text != Math.Round(expectedScore)
                        .ToString("N0", CultureInfo.InvariantCulture))
                    throw new InvalidOperationException(
                        "Result scene did not show the judged score.");
                TMP_Text progressText = (TMP_Text)typeof(ResultScenePresenter)
                    .GetField("memorySubtitle", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(result);
                GameObject progressPanel = (GameObject)typeof(ResultScenePresenter)
                    .GetField("memorySection", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(result);
                GameObject rewardPanel = (GameObject)typeof(ResultScenePresenter)
                    .GetField("rewardSection", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(result);
                TMP_Text rankCaption = (TMP_Text)typeof(ResultScenePresenter)
                    .GetField("rankCaption", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(result);
                if (!rankCaption || rankCaption.text != "AUTO PLAY" ||
                    AppRoot.Current.LastResultWasFirstClear ||
                    AppRoot.Current.LastResultIsNewRecord ||
                    !progressPanel ||
                    progressPanel.activeSelf != (songIndex == 0) ||
                    songIndex == 0 && (!progressText ||
                        !progressText.text.Contains("누적 1회")) ||
                    rewardPanel && rewardPanel.activeSelf ||
                    AppRoot.Current.MemoryFragmentCount != 0)
                    throw new InvalidOperationException(
                        "Auto-used Result changed records, progress, or reward.");
                actions.MusicSelect();
                returningToSelect = true;
                return;
            }
            if (checkingSettingsMenu)
            {
                if (SceneManager.GetActiveScene().name != "Home")
                    throw new InvalidOperationException(
                        "Settings overlay replaced the Home scene.");
                SettingsMenuController menu = UnityEngine.Object
                    .FindFirstObjectByType<SettingsMenuController>();
                if (!menu || !menu.IsOverlayOpen ||
                    typeof(SettingsMenuController).GetField("settings",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(menu) == null) return;
                if (menu.transform.parent != AppRoot.Current.transform ||
                    !AppRoot.Current.GetComponent<CanvasScaler>() ||
                    !AppRoot.Current.GetComponent<GraphicRaycaster>())
                    throw new InvalidOperationException(
                        "Settings overlay is not hosted by the persistent AppRoot Canvas.");
                MenuNavigationController modalNavigation = UnityEngine.Object
                    .FindFirstObjectByType<MenuNavigationController>();
                NavigationScope modalScope = menu.GetComponent<NavigationScope>();
                if (!modalNavigation || !modalScope ||
                    modalNavigation.ActiveScope != modalScope)
                    throw new InvalidOperationException(
                        "Settings did not own the Home modal navigation scope.");
                FindSlider(menu, "VolumeSlider0").value = 0.8f;
                if (Math.Abs(AppRoot.Current.Settings.MasterVolume - 0.8f) >
                    0.001f || Math.Abs(AudioListener.volume - 0.8f) > 0.001f)
                    throw new InvalidOperationException(
                        "Master volume did not apply to the audio listener.");
                menu.SetBgmVolume(0.55f);
                if (Math.Abs(AppRoot.Current.Settings.MusicVolume - 0.55f) >
                    0.001f)
                    throw new InvalidOperationException(
                        "BGM volume did not change local preferences.");
                menu.SetBgmVolume(0.6f);
                menu.SetSfxVolume(0.7f);
                menu.SetVoiceVolume(0.75f);
                FindButton(menu, "HitStyle2").onClick.Invoke();
                FindToggle(menu, "SpatialAudio").isOn = true;
                FindToggle(menu, "OtherToggle0").isOn = true;
                if (Math.Abs(AppRoot.Current.Settings.SfxVolume - 0.7f) >
                    0.001f ||
                    Math.Abs(AppRoot.Current.Settings.VoiceVolume - 0.75f) >
                    0.001f || AppRoot.Current.Settings.HitSound !=
                    LocalGameSettingsStore.HitSoundStyle.Sharp ||
                    !AppRoot.Current.Settings.SpatialAudio ||
                    !AppRoot.Current.Settings.MuteWhenUnfocused)
                    throw new InvalidOperationException(
                        "Audio overlay controls did not save their values.");
                menu.SelectCategory(3);
                FindButton(menu, "TimingPlus").onClick.Invoke();
                if (AppRoot.Current.Settings.JudgementOffsetMs != -20d)
                    throw new InvalidOperationException(
                        "Settings timing button did not change local preferences.");
                FindButton(menu, "TimingMinus").onClick.Invoke();
                menu.SelectCategory(5);
                FindButton(menu, "Lane1").onClick.Invoke();
                menu.ReturnHome();
                if (!menu.IsOverlayOpen ||
                    SceneManager.GetActiveScene().name != "Home")
                    throw new InvalidOperationException(
                        "Cancelling a key change closed the overlay.");
                FindButton(menu, "ResetKeys").onClick.Invoke();
                if (AppRoot.Current.Settings.GetLaneBinding(0) !=
                    "<Keyboard>/z")
                    throw new InvalidOperationException(
                        "Reset Keys did not restore the default binding.");
                AppRoot.Current.SetLaneBinding(0, "<Keyboard>/a");
                FindButton(menu, "Close").onClick.Invoke();
                if (menu.IsOverlayOpen ||
                    SceneManager.GetActiveScene().name != "Home" ||
                    modalNavigation.ActiveScope == modalScope)
                    throw new InvalidOperationException(
                        "Settings overlay did not restore Home.");
                HomeMenuActions homeActions = UnityEngine.Object
                    .FindFirstObjectByType<HomeMenuActions>();
                homeActions.OpenSettings();
                if (!menu.IsOverlayOpen) throw new InvalidOperationException(
                    "Settings overlay could not reopen.");
                modalScope.InvokeCancel();
                if (menu.IsOverlayOpen) throw new InvalidOperationException(
                    "Settings overlay Cancel did not restore Home.");
                homeActions.OpenSettings();
                if (!menu.IsOverlayOpen ||
                    !AppRoot.NavigateToScene("MusicSelect"))
                    throw new InvalidOperationException(
                        "Settings overlay could not start a scene change.");
                checkingSettingsMenu = false;
                openedMusicSelect = true;
                return;
            }
            if (!openedMusicSelect &&
                SceneManager.GetActiveScene().name == "Home")
            {
                if (!assignedTestData)
                {
                    typeof(AppRoot).GetField("playerData", BindingFlags.Instance |
                        BindingFlags.NonPublic).SetValue(AppRoot.Current,
                        LocalPlayerDataStore.Load(
                            SessionState.GetString(DataPathKey, string.Empty)));
                    LocalGameSettingsStore settings =
                        LocalGameSettingsStore.Load(SessionState.GetString(
                            SettingsPathKey, string.Empty));
                    settings.SetMusicVolume(0.6f);
                    settings.SetJudgementOffsetMs(-25d);
                    settings.SetLaneBinding(0, "<Keyboard>/a");
                    settings.Save();
                    typeof(AppRoot).GetField("settings", BindingFlags.Instance |
                        BindingFlags.NonPublic).SetValue(AppRoot.Current,
                        settings);
                    AppRoot.Current.SelectSong("designant", "hard");
                    AppRoot.Current.PublishResult(new GameResultSnapshot(
                        "designant", "hard", 1000000d, 1000000,
                        RankGrade.S, 1, 0, 0, 0, 1, 1,
                        true, false, isAutoPlay: true));
                    if (AppRoot.Current.TryGetBestRecord("designant", "hard",
                            out _, out _) ||
                        AppRoot.Current.TryGetChartProgress("designant",
                            "hard", out _) ||
                        AppRoot.Current.LastResultIsNewRecord ||
                        AppRoot.Current.LastResultWasFirstClear)
                        throw new InvalidOperationException(
                            "Auto Play result entered local best records.");
                    assignedTestData = true;
                }
                if (temporaryScreenIndex < TemporaryScreens.Length)
                {
                    checkingTemporaryScreen = true;
                    HomeMenuActions home = UnityEngine.Object
                        .FindFirstObjectByType<HomeMenuActions>();
                    if (!home) throw new InvalidOperationException(
                        "Home menu actions are missing.");
                    switch (TemporaryScreens[temporaryScreenIndex])
                    {
                        case "Story": home.OpenStory(); break;
                        case "Character": home.OpenCharacter(); break;
                        case "ReMind": home.OpenReMind(); break;
                        case "Option": home.OpenOption(); break;
                        default: AppRoot.NavigateToScene("Music"); break;
                    }
                    return;
                }
                if (!openedSettingsMenu)
                {
                    openedSettingsMenu = true;
                    checkingSettingsMenu = true;
                    HomeMenuActions home = UnityEngine.Object
                        .FindFirstObjectByType<HomeMenuActions>();
                    if (!home) throw new InvalidOperationException(
                        "Home actions are missing for Settings overlay.");
                    home.OpenSettings();
                    return;
                }
                openedMusicSelect = true;
                AppRoot.NavigateToScene("MusicSelect");
                return;
            }
            if (openedMusicSelect && !startedSelection &&
                SceneManager.GetActiveScene().name == "MusicSelect")
            {
                if (!checkedSettingsSceneChange)
                {
                    SettingsMenuController persistent = AppRoot.Current
                        .GetComponentInChildren<SettingsMenuController>(true);
                    NavigationScope scope = persistent
                        ? persistent.GetComponent<NavigationScope>() : null;
                    if (!persistent || persistent.IsOverlayOpen ||
                        persistent.gameObject.activeSelf || !scope ||
                        scope.Controller)
                        throw new InvalidOperationException(
                            "Settings overlay kept scene-owned navigation after Home unloaded.");
                    checkedSettingsSceneChange = true;
                }
                MusicSelectController selection = UnityEngine.Object
                    .FindFirstObjectByType<MusicSelectController>();
                if (!selection) return;
                MusicTrackRow[] rows = (MusicTrackRow[])typeof(
                    MusicSelectController).GetField("tracks",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(selection);
                int trackIndex = Array.FindIndex(rows,
                    row => row.MusicId == SongIds[songIndex]);
                if (trackIndex < 0)
                    throw new InvalidOperationException(
                        "Song is missing from Music Select: " +
                        SongIds[songIndex]);
                selection.SelectTrack(trackIndex);
                selection.SetDifficulty(2);
                typeof(MusicSelectController).GetMethod("StartPreview",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(selection, new object[] { rows[trackIndex] });
                float previewVolume = (float)typeof(MusicSelectController)
                    .GetField("previewTargetVolume", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(selection);
                if (Math.Abs(previewVolume -
                        rows[trackIndex].PreviewVolume * 0.6f) > 0.001f)
                    throw new InvalidOperationException(
                        "Music Select preview did not apply player volume.");
                if (songIndex == 0 && !AppRoot.Current.IsFavorite(SongIds[0]))
                {
                    selection.ToggleFavorite();
                    if (!AppRoot.Current.IsFavorite(SongIds[0]))
                        throw new InvalidOperationException(
                            "Music Select did not save the favorite toggle.");
                }
                selection.PlaySelectedSong();
                SceneTransitionController transition = AppRoot.Current
                    .GetComponentInChildren<SceneTransitionController>(true);
                if (!AppRoot.Current.IsTransitioning ||
                    !transition || !transition.OverlayVisible ||
                    SceneManager.GetActiveScene().name != "MusicSelect")
                    throw new InvalidOperationException(
                        "Music Select did not start the persistent visual transition.");
                selection.PlaySelectedSong();
                if (!AppRoot.Current.IsTransitioning ||
                    SceneManager.GetActiveScene().name != "MusicSelect")
                    throw new InvalidOperationException(
                        "A second Play action interrupted the transition.");
                transitionFirstElapsed = transition.TransitionElapsed;
                transitionStartedAt = Time.unscaledTime;
                transitionAdvanced = false;
                startedSelection = true;
                return;
            }
            if (!startedSelection ||
                SceneManager.GetActiveScene().name != "Game") return;
            if (!transitionAdvanced)
                throw new InvalidOperationException(
                    "Game opened before the Music Selected animation advanced.");
            if (AppRoot.Current.IsTransitioning) return;
            SceneTransitionController completedTransition = AppRoot.Current
                .GetComponentInChildren<SceneTransitionController>(true);
            if (!completedTransition || completedTransition.OverlayVisible)
                throw new InvalidOperationException(
                    "The Music Select overlay remained visible after entering Game.");
            DemoPlayController presenter = UnityEngine.Object
                .FindFirstObjectByType<DemoPlayController>();
            if (!presenter || !presenter.IsReady) return;
            if (checkedScene && !waitingForPauseResume) return;

            GameplayChartSessionController charts = UnityEngine.Object
                .FindFirstObjectByType<GameplayChartSessionController>();
            NoteJudgementSystem judgement = UnityEngine.Object
                .FindFirstObjectByType<NoteJudgementSystem>();
            GameManager manager = UnityEngine.Object
                .FindFirstObjectByType<GameManager>();
            GameplaySessionState state = UnityEngine.Object
                .FindFirstObjectByType<GameplaySessionState>();
            if (manager && manager.PlaybackState != PlaybackState.Playing &&
                !waitingForPauseResume)
                return;
            if (!charts || !charts.IsPrepared || !judgement || !manager ||
                !state ||
                charts.CurrentChart.Snapshot.Notes.Count == 0 ||
                charts.CurrentChart.Metadata.MusicId != SongIds[songIndex] ||
                charts.CurrentChart.Metadata.DifficultyId != "hard" ||
                (manager.PlaybackState != PlaybackState.Playing &&
                 !(waitingForPauseResume &&
                   manager.PlaybackState == PlaybackState.Paused)))
                throw new InvalidOperationException(
                    "Music Select did not start the selected Game chart.");
            checkedScene = true;
            if (waitingForRetry)
            {
                if (state.JudgedNoteCount != 0 || state.CurrentScore != 0d ||
                    state.CurrentCombo != 0 || state.IsFailed ||
                    judgement.PendingNoteCount == 0)
                    throw new InvalidOperationException(
                        "Retry inherited a previous session's judgement or result state.");
                waitingForRetry = false;
            }
            MusicCatalog catalog = (MusicCatalog)typeof(DemoPlayController)
                .GetField("musicCatalog", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(presenter);
            MusicCatalogEntry selectedSong = catalog.FindSong(SongIds[songIndex]);
            SongContent song = SongContentCodec.Parse(selectedSong.SongData.text);
            if (manager.GamePlay.CurrentSong != selectedSong.AudioClip ||
                Math.Abs(manager.GamePlay.GetComponentInChildren<AudioSource>()
                    .volume - song.PlaybackVolume * 0.6f) > 0.001f)
                throw new InvalidOperationException(
                    "The selected chart is not paired with its audio and volume.");
            RhythmInputRouter router = UnityEngine.Object
                .FindFirstObjectByType<RhythmInputRouter>();
            if (judgement.UserOffsetMs != -25d || !router ||
                router.InputActions.FindActionMap("Rhythm")
                    .FindAction("Lane01").bindings[0].effectivePath !=
                    "<Keyboard>/a")
                throw new InvalidOperationException(
                    "The Game session did not apply player timing or lane settings.");
            foreach (var note in charts.CurrentChart.Snapshot.Notes)
                if (!judgement.TryGetRegisteredNoteView(note.Id, out _))
                    throw new InvalidOperationException(
                        "Missing note view: " + note.Id);
            double end = 0d;
            foreach (var note in charts.CurrentChart.Snapshot.Notes)
                end = Math.Max(end, note.StartTimeMs +
                    charts.CurrentChart.ChartOffsetMs);
            if (manager.GamePlay.SongDurationMs <= end)
                throw new InvalidOperationException(
                    "Playback ends before the final chart note.");
            GameFlowController flow = UnityEngine.Object
                .FindFirstObjectByType<GameFlowController>();
            if (!checkedNormalIsolation)
            {
                GameRule rule = manager.GameRule;
                GameRuleConfig originalDefault = rule.DefaultConfig;
                GameRuleConfig active = rule.Config;
                if (!rule.HasSessionConfig || !originalDefault ||
                    ReferenceEquals(originalDefault, active))
                    throw new InvalidOperationException(
                        "Normal play did not freeze its default rule at start.");
                GameRuleConfig editedSource = UnityEngine.Object.Instantiate(
                    originalDefault);
                editedSource.hideFlags = HideFlags.HideAndDontSave;
                double currentHealth = state.CurrentHealth;
                FieldInfo defaultField = typeof(GameRule).GetField("config",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                try
                {
                    defaultField.SetValue(rule, editedSource);
                    typeof(GameRuleConfig).GetField("initialHealth",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editedSource, 1);
                    if (!ReferenceEquals(rule.Config, active) ||
                        rule.BaseInitialHealth != active.InitialHealth ||
                        state.CurrentHealth != currentHealth)
                        throw new InvalidOperationException(
                            "An edited Normal source changed the live attempt.");
                }
                finally
                {
                    defaultField.SetValue(rule, originalDefault);
                    UnityEngine.Object.Destroy(editedSource);
                }
                checkedNormalIsolation = true;
            }
            if (waitingForPauseResume)
            {
                if (Time.frameCount == pauseOpenedFrame) return;
                flow.Resume();
                if (manager.PlaybackState != PlaybackState.Playing)
                    throw new InvalidOperationException(
                        "The pause menu did not resume on the next frame.");
                if (!judgement.UsedAutoPlayInCurrentAttempt)
                    throw new InvalidOperationException(
                        "Resume cleared the current attempt's Auto Play history.");
                flow.Pause();
                if (manager.PlaybackState != PlaybackState.Playing)
                    throw new InvalidOperationException(
                        "A second pause transition occurred in the resume frame.");
                waitingForPauseResume = false;
            }
            if (songIndex == 0 && !attemptedFailure)
            {
                attemptedFailure = true;
                judgement.ProcessFrame(manager.GamePlay.SongDurationMs + 1000d);
                if (!state.IsFailed || state.CurrentScore != 0d ||
                    state.JudgedNoteCount != state.TotalNoteCount)
                    throw new InvalidOperationException(
                        "Missed input did not produce a complete failed session.");
                checkingResult = true;
                checkingFailedResult = true;
                typeof(GameFlowController).GetMethod("FinishSong",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(flow, null);
                return;
            }
            if (!waitingForPauseResume && manager.PlaybackState ==
                PlaybackState.Playing && !checkedPauseResume[songIndex])
            {
                judgement.SetAutoPlayEnabled(true);
                judgement.SetAutoPlayEnabled(false);
                if (!judgement.UsedAutoPlayInCurrentAttempt)
                    throw new InvalidOperationException(
                        "Turning Auto Play off erased its use in this attempt.");
                flow.Pause();
                flow.Resume();
                if (manager.PlaybackState != PlaybackState.Paused)
                    throw new InvalidOperationException(
                        "The pause menu closed on the opening frame.");
                checkedPauseResume[songIndex] = true;
                waitingForPauseResume = true;
                pauseOpenedFrame = Time.frameCount;
                return;
            }
            if (!manager.PauseGame() ||
                !manager.ResumeGame() || !manager.RestartGame())
                throw new InvalidOperationException(
                    "Play/Pause/Resume/Restart transition failed.");
            if (judgement.UsedAutoPlayInCurrentAttempt)
                throw new InvalidOperationException(
                    "A successful Restart inherited Auto Play usage.");
            judgement.SetAutoPlayEnabled(true);
            judgement.ProcessFrame(manager.GamePlay.SongDurationMs + 1000d);
            judgement.SetAutoPlayEnabled(false);
            if (!judgement.UsedAutoPlayInCurrentAttempt)
                throw new InvalidOperationException(
                    "Auto Play usage was lost after toggling it off.");
            if (judgement.LastTimelineError != null ||
                judgement.PendingNoteCount != 0)
                throw new InvalidOperationException(
                    "Prepared chart did not finish judgement: " +
                    judgement.LastTimelineError);
            if (!state || state.JudgedNoteCount != state.TotalNoteCount ||
                state.CurrentScore <= 0d)
                throw new InvalidOperationException(
                    "Automatic judgement did not update score and session state.");
            expectedScore = state.CurrentScore;
            checkingResult = true;
            typeof(GameFlowController).GetMethod("FinishSong",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(flow, null);
            GameResultSnapshot autoResult = (GameResultSnapshot)typeof(AppRoot)
                .GetField("pendingResult", BindingFlags.Instance |
                    BindingFlags.NonPublic).GetValue(AppRoot.Current);
            if (autoResult == null || !autoResult.IsAutoPlay ||
                autoResult.AttemptStart == null ||
                autoResult.AttemptStart.IsAutoPlay)
                throw new InvalidOperationException(
                    "An attempt that used Auto Play was published as manual.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Complete(1);
        }
    }

    private static void CaptureHardSceneLoad(Scene scene, LoadSceneMode mode)
    {
        if (hardPhase != HardPhase.Start || scene.name != "Game") return;
        SceneManager.sceneLoaded -= CaptureHardSceneLoad;
        GameManager manager = UnityEngine.Object
            .FindFirstObjectByType<GameManager>();
        if (!manager || !manager.GamePlay) return;
        hardCapturePlayback = manager.GamePlay;
        hardCapturePlayback.PlaybackStarted += CaptureHardPlaybackStart;
    }

    private static void CaptureHardPlaybackStart(double songTimeMs)
    {
        if (hardPhase != HardPhase.Start ||
            !hardCapturePlayback ||
            hardCapturePlayback.StartReason != PlaybackStartReason.Play)
            return;
        hardStartCaptureCount++;
        GameplaySessionState state = UnityEngine.Object
            .FindFirstObjectByType<GameplaySessionState>();
        GameFlowController flow = UnityEngine.Object
            .FindFirstObjectByType<GameFlowController>();
        hardHealthAtStart = state ? state.CurrentHealth : double.NaN;
        hardAttemptAtStart = flow ? flow.CurrentAttempt : null;
        hardCapturePlayback.PlaybackStarted -= CaptureHardPlaybackStart;
        hardCapturePlayback = null;
    }

    private static void TickHard()
    {
        if (hardPhase == HardPhase.Select)
        {
            if (SceneManager.GetActiveScene().name != "MusicSelect") return;
            GameRuleConfig asset = AssetDatabase.LoadAssetAtPath<GameRuleConfig>(
                HardRulePath);
            if (!asset) throw new InvalidOperationException(
                "The development Hard gauge asset is missing.");
            hardRuleSource = UnityEngine.Object.Instantiate(asset);
            hardRuleSource.hideFlags = HideFlags.HideAndDontSave;
            hardAbility = ExampleCharacterAbility.CreateHardWorldReward();
            bool rejectedIncompleteSelection = false;
            try { AppRoot.Current.SelectSong("i", "hard", hardAbility, null); }
            catch (ArgumentException) { rejectedIncompleteSelection = true; }
            if (!rejectedIncompleteSelection ||
                !AppRoot.Current.TryGetSelectedSong(out AppRoot.SongSelection old) ||
                old.MusicId != "i" || old.DifficultyId != "hard")
                throw new InvalidOperationException(
                    "An incomplete character selection changed the pending song.");
            AppRoot.Current.SelectSong("i", "hard", hardAbility,
                hardRuleSource);
            SceneManager.sceneLoaded += CaptureHardSceneLoad;
            if (!AppRoot.Current.TryStartSelectedGame())
                throw new InvalidOperationException(
                    "The selected Hard song did not start its Game transition.");
            hardPhase = HardPhase.Start;
            return;
        }

        if (hardPhase == HardPhase.Result)
        {
            if (SceneManager.GetActiveScene().name != "Result") return;
            ResultScenePresenter presenter = UnityEngine.Object
                .FindFirstObjectByType<ResultScenePresenter>();
            if (!presenter) return;
            TMP_Text caption = (TMP_Text)typeof(ResultScenePresenter)
                .GetField("rankCaption", BindingFlags.Instance |
                    BindingFlags.NonPublic).GetValue(presenter);
            if (!caption || string.IsNullOrEmpty(caption.text)) return;
            if (caption.text != "FAILED" || hardResult == null ||
                !hardResult.IsFailed ||
                !ReferenceEquals(hardResult.AttemptStart, hardRetryAttempt) ||
                hardResult.AttemptStart.GaugeRule.InitialHealth != 80 ||
                hardResult.AttemptStart.GaugeRule.MissHealthDelta != -20 ||
                hardResult.AttemptStart.CharacterAbility?.CharacterId !=
                    hardAbility.CharacterId || hardLiveConfig ||
                AppRoot.Current.TryTakeResult(out _))
                throw new InvalidOperationException(
                    "The Hard result lost its attempt values after Game teardown.");
            Debug.Log("GAME_HARD_PLAYMODE_SMOKE_PASSED");
            Complete(0);
            return;
        }

        if (SceneManager.GetActiveScene().name != "Game") return;
        DemoPlayController play = UnityEngine.Object
            .FindFirstObjectByType<DemoPlayController>();
        GameFlowController flow = UnityEngine.Object
            .FindFirstObjectByType<GameFlowController>();
        GameManager manager = UnityEngine.Object
            .FindFirstObjectByType<GameManager>();
        GameplaySessionState state = UnityEngine.Object
            .FindFirstObjectByType<GameplaySessionState>();
        GameplayChartSessionController charts = UnityEngine.Object
            .FindFirstObjectByType<GameplayChartSessionController>();
        NoteJudgementSystem judgement = UnityEngine.Object
            .FindFirstObjectByType<NoteJudgementSystem>();
        if (!play || !play.IsReady || !flow || !manager || !state ||
            !charts || !charts.IsPrepared || !judgement ||
            manager.PlaybackState != PlaybackState.Playing &&
            hardPhase != HardPhase.Resume) return;
        if (charts.CurrentChart.Metadata.MusicId != "i" ||
            charts.CurrentChart.Metadata.DifficultyId != "hard")
            throw new InvalidOperationException(
                "The Hard attempt did not load the selected chart.");

        if (hardPhase == HardPhase.Start)
        {
            hardFirstAttempt = flow.CurrentAttempt;
            hardLiveConfig = manager.GameRule.Config;
            if (hardStartCaptureCount != 1 ||
                !ReferenceEquals(hardAttemptAtStart, hardFirstAttempt) ||
                hardHealthAtStart != 100d ||
                hardFirstAttempt == null || hardFirstAttempt.AttemptId ==
                    Guid.Empty || hardFirstAttempt.MusicId != "i" ||
                hardFirstAttempt.DifficultyId != "hard" ||
                hardFirstAttempt.IsAutoPlay ||
                hardFirstAttempt.CharacterAbility?.CharacterId !=
                    hardAbility.CharacterId ||
                hardFirstAttempt.CharacterAbility.Stats.WorldProgressBonusPercent
                    != 20 ||
                hardFirstAttempt.CharacterAbility.Stats.RewardBonusPercent != 50 ||
                hardFirstAttempt.GaugeRule.GaugeType != HealthGaugeType.Hard ||
                hardFirstAttempt.GaugeRule.MaxHealth != 100 ||
                hardFirstAttempt.GaugeRule.InitialHealth != 100 ||
                hardFirstAttempt.GaugeRule.ClearHealth != 1 ||
                hardFirstAttempt.GaugeRule.PerfectHealthDelta != 1 ||
                hardFirstAttempt.GaugeRule.GreatHealthDelta != 1 ||
                hardFirstAttempt.GaugeRule.GoodHealthDelta != -5 ||
                hardFirstAttempt.GaugeRule.MissHealthDelta != -20 ||
                !hardFirstAttempt.GaugeRule.FailImmediately ||
                hardFirstAttempt.GaugeRule.ContinueAfterFail ||
                ReferenceEquals(hardLiveConfig, hardRuleSource) ||
                !manager.GameRule.HasSessionConfig)
                throw new InvalidOperationException(
                    "The Hard play did not freeze and activate its complete gauge. " +
                    "Attempt=" + (hardFirstAttempt == null ? "null" :
                        hardFirstAttempt.MusicId + "/" +
                        hardFirstAttempt.DifficultyId + ", auto=" +
                        hardFirstAttempt.IsAutoPlay + ", ability=" +
                        hardFirstAttempt.CharacterAbility?.CharacterId +
                        ", stats=" +
                        hardFirstAttempt.CharacterAbility?.Stats
                            .WorldProgressBonusPercent + "/" +
                        hardFirstAttempt.CharacterAbility?.Stats
                            .RewardBonusPercent +
                        ", gauge=" + hardFirstAttempt.GaugeRule.GaugeType +
                        ", max=" + hardFirstAttempt.GaugeRule.MaxHealth +
                        ", initial=" +
                        hardFirstAttempt.GaugeRule.InitialHealth +
                        ", clear=" + hardFirstAttempt.GaugeRule.ClearHealth +
                        ", deltas=" +
                        hardFirstAttempt.GaugeRule.PerfectHealthDelta + "/" +
                        hardFirstAttempt.GaugeRule.GreatHealthDelta + "/" +
                        hardFirstAttempt.GaugeRule.GoodHealthDelta + "/" +
                        hardFirstAttempt.GaugeRule.MissHealthDelta +
                        ", immediate=" +
                        hardFirstAttempt.GaugeRule.FailImmediately +
                        ", continue=" +
                        hardFirstAttempt.GaugeRule.ContinueAfterFail) +
                    ", captured initial health=" + hardHealthAtStart +
                    ", capture count=" + hardStartCaptureCount +
                    ", live health=" + state.CurrentHealth +
                    ", session copy=" + manager.GameRule.HasSessionConfig +
                    ", same source=" +
                    ReferenceEquals(hardLiveConfig, hardRuleSource));

            // Both pending selection and its mutable source may change while
            // the current attempt keeps the values captured at start.
            typeof(GameRuleConfig).GetField("initialHealth",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hardRuleSource, 80);
            hardHealthBeforePause = state.CurrentHealth;
            AppRoot.Current.SelectSong("designant", "hard", hardAbility,
                hardRuleSource);
            if (!ReferenceEquals(flow.CurrentAttempt, hardFirstAttempt) ||
                !ReferenceEquals(manager.GameRule.Config, hardLiveConfig) ||
                manager.GameRule.BaseInitialHealth != 100 ||
                hardFirstAttempt.GaugeRule.InitialHealth != 100 ||
                state.CurrentHealth != hardHealthBeforePause)
                throw new InvalidOperationException(
                    "A pending selection or source edit changed the live Hard attempt.");
            AppRoot.Current.SelectSong("i", "hard", hardAbility,
                hardRuleSource);
            flow.Pause();
            if (manager.PlaybackState != PlaybackState.Paused)
                throw new InvalidOperationException("Hard play did not pause.");
            hardPauseFrame = Time.frameCount;
            hardPhase = HardPhase.Resume;
            return;
        }

        if (hardPhase == HardPhase.Resume)
        {
            if (Time.frameCount == hardPauseFrame) return;
            if (!ReferenceEquals(flow.CurrentAttempt, hardFirstAttempt) ||
                state.CurrentHealth != hardHealthBeforePause)
                throw new InvalidOperationException(
                    "Pause changed the Hard attempt or health.");
            flow.Resume();
            if (manager.PlaybackState != PlaybackState.Playing ||
                !ReferenceEquals(flow.CurrentAttempt, hardFirstAttempt) ||
                state.CurrentHealth != hardHealthBeforePause)
                throw new InvalidOperationException(
                    "Resume created a new Hard attempt or reset its health.");
            hardPhase = HardPhase.Retry;
            return;
        }

        if (hardPhase == HardPhase.Retry)
        {
            typeof(GameRuleConfig).GetField("initialHealth",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hardRuleSource, 101);
            ExpectInvalidHardRuleStart(flow.Restart);
            if (!ReferenceEquals(flow.CurrentAttempt, hardFirstAttempt) ||
                !ReferenceEquals(manager.GameRule.Config, hardLiveConfig) ||
                manager.GameRule.GaugeType != HealthGaugeType.Hard ||
                manager.GameRule.BaseInitialHealth != 100 ||
                state.CurrentHealth != hardHealthBeforePause ||
                manager.PlaybackState != PlaybackState.Playing)
                throw new InvalidOperationException(
                    "A rejected Retry replaced the active Hard attempt or rule.");
            AssertHardContinuesPlaying(flow);

            typeof(GameRuleConfig).GetField("initialHealth",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hardRuleSource, 80);
            flow.Restart();
            hardRetryAttempt = flow.CurrentAttempt;
            hardLiveConfig = manager.GameRule.Config;
            if (manager.PlaybackState != PlaybackState.Playing ||
                hardRetryAttempt == null ||
                hardRetryAttempt.AttemptId == hardFirstAttempt.AttemptId ||
                hardRetryAttempt.GaugeRule.InitialHealth != 80 ||
                hardRetryAttempt.GaugeRule.GaugeType != HealthGaugeType.Hard ||
                state.CurrentHealth != 80d ||
                state.JudgedNoteCount != 0 || state.CurrentScore != 0d)
                throw new InvalidOperationException(
                    "A successful Retry did not create a fresh Hard attempt.");

            // Disabling the scene owner ends this session. Its old snapshot
            // must not be accepted later as a newly completed result.
            GameAttemptStartSnapshot endedAttempt = hardRetryAttempt;
            flow.enabled = false;
            if (flow.CurrentAttempt != null ||
                manager.PlaybackState != PlaybackState.Ready ||
                manager.GameRule.HasSessionConfig)
                throw new InvalidOperationException(
                    "Disabling GameFlow retained a live attempt or gauge.");
            bool rejectedStaleResult = false;
            try
            {
                AppRoot.Current.PublishResult(new GameResultSnapshot(
                    "i", "hard", 0d, 1000000, RankGrade.D,
                    0, 0, 0, 1, 0, state.TotalNoteCount,
                    false, true, attemptStart: endedAttempt));
            }
            catch (InvalidOperationException) { rejectedStaleResult = true; }
            if (!rejectedStaleResult)
                throw new InvalidOperationException(
                    "A completed result from an ended attempt was accepted.");
            flow.enabled = true;
            AppRoot.Current.SelectSong("designant", "hard", hardAbility,
                hardRuleSource);
            flow.StartSong();
            if (flow.CurrentAttempt != null ||
                manager.PlaybackState != PlaybackState.Ready)
                throw new InvalidOperationException(
                    "A mismatched song start created an attempt.");
            AssertHardStartError(flow);
            AppRoot.Current.SelectSong("i", "hard", hardAbility,
                hardRuleSource);
            typeof(GameRuleConfig).GetField("initialHealth",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hardRuleSource, 101);
            ExpectInvalidHardRuleStart(flow.StartSong);
            if (flow.CurrentAttempt != null ||
                manager.PlaybackState != PlaybackState.Ready)
                throw new InvalidOperationException(
                    "An invalid fresh start created a Hard attempt.");
            AssertHardStartError(flow);
            typeof(GameRuleConfig).GetField("initialHealth",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hardRuleSource, 80);
            flow.StartSong();
            hardRetryAttempt = flow.CurrentAttempt;
            hardLiveConfig = manager.GameRule.Config;
            if (manager.PlaybackState != PlaybackState.Playing ||
                hardRetryAttempt == null ||
                hardRetryAttempt.AttemptId == endedAttempt.AttemptId ||
                hardRetryAttempt.GaugeRule.InitialHealth != 80 ||
                state.CurrentHealth != 80d)
                throw new InvalidOperationException(
                    "Reenabled GameFlow reused an ended attempt.");
            GameAttemptStartSnapshot forgedStart = new GameAttemptStartSnapshot(
                hardRetryAttempt.AttemptId, "i", "hard", false,
                hardRetryAttempt.GaugeRule,
                hardRetryAttempt.CharacterAbility);
            bool rejectedForgedResult = false;
            try
            {
                AppRoot.Current.PublishResult(new GameResultSnapshot(
                    "i", "hard", 0d, 1000000, RankGrade.D,
                    0, 0, 0, 1, 0, state.TotalNoteCount,
                    false, true, attemptStart: forgedStart));
            }
            catch (InvalidOperationException) { rejectedForgedResult = true; }
            if (!rejectedForgedResult)
                throw new InvalidOperationException(
                    "A different snapshot with the active attempt ID was accepted.");
            hardObservedMisses = 0;
            hardJudgementError = null;
            judgement.NoteJudged += OnHardJudged;
            try
            {
                judgement.ProcessFrame(manager.GamePlay.SongDurationMs + 1000d);
            }
            finally { judgement.NoteJudged -= OnHardJudged; }
            if (hardJudgementError != null || hardObservedMisses < 4 ||
                state.CurrentHealth != 0d || !state.IsFailed ||
                manager.PlaybackState != PlaybackState.Ready)
                throw new InvalidOperationException(
                    "Hard Miss deltas or immediate failure differed: " +
                    hardJudgementError);
            typeof(GameFlowController).GetMethod("FinishSong",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(flow, null);
            hardResult = (GameResultSnapshot)typeof(AppRoot).GetField(
                "pendingResult", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(AppRoot.Current);
            if (hardResult == null ||
                !ReferenceEquals(hardResult.AttemptStart, hardRetryAttempt))
                throw new InvalidOperationException(
                    "Hard failure did not publish its captured attempt.");
            hardPhase = HardPhase.Result;
        }
    }

    private static void ExpectInvalidHardRuleStart(Action start)
    {
        int expectedValidationLogs = 0;
        string unexpectedLog = null;
        Application.LogCallback capture = (condition, _, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (type == LogType.Error && condition.StartsWith(
                    "Game start validation failed:",
                    StringComparison.Ordinal) && condition.Contains(
                    "GameRuleConfig has invalid session values."))
                expectedValidationLogs++;
            else unexpectedLog = condition;
        };
        Application.logMessageReceived += capture;
        try { start(); }
        finally { Application.logMessageReceived -= capture; }
        if (expectedValidationLogs != 1 || unexpectedLog != null)
            throw new InvalidOperationException(
                "The rejected Hard start logged an unexpected error: " +
                unexpectedLog);
    }

    private static void AssertHardStartError(GameFlowController flow)
    {
        GameObject panel = (GameObject)typeof(GameFlowController).GetField(
            "errorPanel", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(flow);
        TMP_Text detail = (TMP_Text)typeof(GameFlowController).GetField(
            "errorDetail", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(flow);
        if (!panel || !panel.activeInHierarchy || !detail ||
            string.IsNullOrWhiteSpace(detail.text) ||
            panel.GetComponentsInChildren<Button>(true).Length == 0)
            throw new InvalidOperationException(
                "A rejected Hard start has no visible error or return action.");
    }

    private static void AssertHardContinuesPlaying(GameFlowController flow)
    {
        GameObject playCanvas = GameObject.Find("Play Canvas");
        GameObject score = GameObject.Find("Score");
        GameObject error = (GameObject)typeof(GameFlowController).GetField(
            "errorPanel", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(flow);
        TMP_Text notice = (TMP_Text)typeof(GameFlowController).GetField(
            "startNotice", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(flow);
        if (!playCanvas || !playCanvas.activeInHierarchy || !score ||
            !score.activeInHierarchy || error && error.activeInHierarchy ||
            string.IsNullOrWhiteSpace(flow.LastStartError) || !notice ||
            !notice.gameObject.activeInHierarchy ||
            notice.text != flow.LastStartError)
            throw new InvalidOperationException(
                "A rejected Retry hid playback or its start notice. " +
                "playCanvas=" + (playCanvas ? playCanvas.activeInHierarchy :
                    false) + ", score=" + (score ? score.activeInHierarchy :
                    false) +
                ", error=" + (error ? error.activeInHierarchy : false) +
                ", lastError='" + flow.LastStartError +
                "', notice=" + (notice ? notice.gameObject.activeInHierarchy :
                    false) + ", noticeText='" + (notice ? notice.text :
                    "missing") + "'.");
    }

    private static void OnHardJudged(NoteJudgementEvent judged)
    {
        if (judged.Result != JudgeResult.Miss) return;
        hardObservedMisses++;
        GameplaySessionState state = UnityEngine.Object
            .FindFirstObjectByType<GameplaySessionState>();
        double expected = Math.Max(0d, 80d - 20d * hardObservedMisses);
        if (!state || state.CurrentHealth != expected)
            hardJudgementError = "Miss " + hardObservedMisses +
                " left Health " + (state ? state.CurrentHealth : -1d) +
                ", expected " + expected;
    }

    private static Button FindButton(Component root, string name)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        throw new InvalidOperationException("Settings button is missing: " + name);
    }

    private static Slider FindSlider(Component root, string name)
    {
        foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
            if (slider.name == name) return slider;
        throw new InvalidOperationException("Settings slider is missing: " + name);
    }

    private static Toggle FindToggle(Component root, string name)
    {
        foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
            if (toggle.name == name) return toggle;
        throw new InvalidOperationException("Settings toggle is missing: " + name);
    }

    private static void Complete(int code)
    {
        EditorApplication.update -= Tick;
        SceneManager.sceneLoaded -= CaptureHardSceneLoad;
        if (hardCapturePlayback)
            hardCapturePlayback.PlaybackStarted -= CaptureHardPlaybackStart;
        hardCapturePlayback = null;
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(HardKey, false);
        if (hardRuleSource)
        {
            if (EditorApplication.isPlaying)
                UnityEngine.Object.Destroy(hardRuleSource);
            else UnityEngine.Object.DestroyImmediate(hardRuleSource);
            hardRuleSource = null;
        }
        string path = SessionState.GetString(DataPathKey, string.Empty);
        string settingsPath = SessionState.GetString(SettingsPathKey,
            string.Empty);
        SessionState.EraseString(DataPathKey);
        SessionState.EraseString(SettingsPathKey);
        foreach (string savedPath in new[] { path, settingsPath })
            if (!string.IsNullOrEmpty(savedPath))
                foreach (string candidate in new[] { savedPath,
                             savedPath + ".bak", savedPath + ".tmp" })
                    if (File.Exists(candidate)) File.Delete(candidate);
        EditorApplication.Exit(code);
    }
}
