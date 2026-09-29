using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using REmind.Charting;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Input.Judgement;
using REmind.Gameplay.Input.Routing;
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
    private static double expectedScore;

    static PlayableGameSmokeRunner()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
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
        expectedScore = 0d;
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
            if (returningToSelect)
            {
                if (SceneManager.GetActiveScene().name != "MusicSelect") return;
                if (!AppRoot.Current.TryGetSelectedSong(
                        out AppRoot.SongSelection returned) ||
                    returned.MusicId != SongIds[songIndex] ||
                    returned.DifficultyId != "hard")
                    throw new InvalidOperationException(
                        "Gameplay did not preserve the selected song on return.");
                if (!AppRoot.Current.TryGetBestRecord(SongIds[songIndex], "hard",
                        out double bestScore, out _) ||
                    Math.Abs(bestScore - expectedScore) > 0.001d)
                    throw new InvalidOperationException(
                        "Result score was not recorded in local player data.");
                LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(
                    SessionState.GetString(DataPathKey, string.Empty));
                if (!reloaded.TryGetBestRecord(SongIds[songIndex], "hard",
                        out double savedScore, out _) ||
                    Math.Abs(savedScore - expectedScore) > 0.001d ||
                    AppRoot.Current.TryTakeResult(out _))
                    throw new InvalidOperationException(
                        "Returned Music Select retained a result or lost its saved record.");
                if (!reloaded.TryGetProgress(SongIds[songIndex], "hard",
                        out ChartProgressSnapshot progress) ||
                    progress.PlayCount != (songIndex == 0 ? 2 : 1) ||
                    !progress.Cleared || progress.MaxCombo <= 0 ||
                    reloaded.MemoryFragmentCount != songIndex + 1)
                    throw new InvalidOperationException(
                        "Local progression did not survive Result and scene return.");
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
                TMP_Text rewardLabel = (TMP_Text)typeof(ResultScenePresenter)
                    .GetField("rewardLabel", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(result);
                if (!AppRoot.Current.LastResultWasFirstClear ||
                    !progressPanel || !progressPanel.activeSelf ||
                    !progressText || !progressText.text.Contains("누적 " +
                        (songIndex == 0 ? 2 : 1) + "회") ||
                    !rewardPanel || !rewardPanel.activeSelf ||
                    !rewardLabel || !rewardLabel.text.Contains(
                        "보유 " + (songIndex + 1)) ||
                    AppRoot.Current.MemoryFragmentCount != songIndex + 1)
                    throw new InvalidOperationException(
                        "Result did not show first-clear reward and saved progress.");
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
            if (waitingForPauseResume)
            {
                if (Time.frameCount == pauseOpenedFrame) return;
                flow.Resume();
                if (manager.PlaybackState != PlaybackState.Playing)
                    throw new InvalidOperationException(
                        "The pause menu did not resume on the next frame.");
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
            judgement.SetAutoPlayEnabled(true);
            judgement.ProcessFrame(manager.GamePlay.SongDurationMs + 1000d);
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
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Complete(1);
        }
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
        SessionState.SetBool(ActiveKey, false);
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
