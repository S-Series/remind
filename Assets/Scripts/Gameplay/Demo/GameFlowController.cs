using System;
using REmind.Common.UI;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Characters;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Input.Judgement;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace REmind.Gameplay.Demo
{
    /// <summary>Wires the scene-authored selection, play, pause and result screens.</summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        private enum Screen { Loading, Select, Playing, Paused, Result, Error }

        [Header("Session")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private DemoPlayController notePresenter;
        [SerializeField] private GameplayChartSessionController chartSession;
        [SerializeField] private GameplaySessionState sessionState;
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private GameplayChartEffectController effectController;
        [SerializeField] private string songTitle = "Chroma I";

        [Header("Scene UI")]
        [SerializeField] private MenuNavigationController navigation;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private NavigationScope selectionScope;
        [SerializeField] private NavigationScope pauseScope;
        [SerializeField] private NavigationScope resultScope;
        [SerializeField] private NavigationScope errorScope;
        [SerializeField] private TMP_Text selectionTitle;
        [SerializeField] private TMP_Text selectionDetail;
        [SerializeField] private TMP_Text hudText;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultDetail;
        [SerializeField] private TMP_Text errorDetail;
        [SerializeField] private TMP_Text errorActionLabel;
        [SerializeField] private TMP_Text startNotice;

        private Screen screen = Screen.Loading;
        private bool autoPlay;
        private int perfectPlus;
        private int perfect;
        private int great;
        private int miss;
        private int maxCombo;
        private int lastPauseTransitionFrame = -1;
        private bool resultDispatched;
        private GameResultSnapshot pendingSaveResult;
        private bool handlingEffectFailure;
        private string startError;
        private float startNoticeUntil;
        private GameAttemptStartSnapshot activeAttempt;
        private GameAttemptStartSnapshot preparedAttempt;
        private CharacterAbilityDefinition activeAbilitySource;
        private GameRuleConfig activeRuleSource;
        private CharacterAbilityDefinition preparedAbilitySource;
        private GameRuleConfig preparedRuleSource;
        private string preparedMusicId;
        private string preparedDifficultyId;
        private string preparedUnlockTargetMusicId;
        private GameRule.SessionConfigChange pendingRuleChange;

        public GameAttemptStartSnapshot CurrentAttempt => activeAttempt;
        public string LastStartError => startError;

        private void Awake()
        {
            if (!gameManager) gameManager = GetComponentInParent<GameManager>();
            if (!notePresenter) notePresenter = GetComponentInChildren<DemoPlayController>(true);
            if (!chartSession) chartSession = GetComponentInChildren<GameplayChartSessionController>(true);
            if (!sessionState) sessionState = GetComponentInChildren<GameplaySessionState>(true);
            if (!judgementSystem) judgementSystem = GetComponentInChildren<NoteJudgementSystem>(true);
            if (!effectController && chartSession)
                effectController = chartSession.GetComponent<GameplayChartEffectController>();
        }

        private void OnEnable()
        {
            if (judgementSystem)
            {
                judgementSystem.NoteJudged += OnNoteJudged;
                judgementSystem.PlaybackFailed += HandlePlaybackFailed;
            }
            if (sessionState) sessionState.StateChanged += OnSessionStateChanged;
            if (effectController) effectController.PlaybackFailed += HandlePlaybackFailed;
            if (gameManager && gameManager.GamePlay)
            {
                gameManager.GamePlay.PlaybackStarting += HandlePlaybackStarting;
                gameManager.GamePlay.PlaybackCommitting += HandlePlaybackCommitting;
                gameManager.GamePlay.PlaybackStarted += HandlePlaybackStarted;
                gameManager.GamePlay.PlaybackStartAborted += HandlePlaybackStartAborted;
            }
        }

        private void OnDisable()
        {
            if (judgementSystem)
            {
                judgementSystem.NoteJudged -= OnNoteJudged;
                judgementSystem.PlaybackFailed -= HandlePlaybackFailed;
            }
            if (sessionState) sessionState.StateChanged -= OnSessionStateChanged;
            if (effectController) effectController.PlaybackFailed -= HandlePlaybackFailed;
            if (gameManager && gameManager.GamePlay)
            {
                gameManager.GamePlay.PlaybackStarting -= HandlePlaybackStarting;
                gameManager.GamePlay.PlaybackCommitting -= HandlePlaybackCommitting;
                gameManager.GamePlay.PlaybackStarted -= HandlePlaybackStarted;
                gameManager.GamePlay.PlaybackStartAborted -= HandlePlaybackStartAborted;
            }
            pendingRuleChange?.Rollback();
            pendingRuleChange = null;
            gameManager?.StopGame();
            if (gameManager && gameManager.GameRule)
                gameManager.GameRule.ClearSessionConfig();
            if (activeAttempt != null && AppRoot.Current)
                AppRoot.Current.EndAttempt(activeAttempt.AttemptId);
            activeAttempt = null;
            pendingSaveResult = null;
            activeAbilitySource = null;
            activeRuleSource = null;
            ClearPreparedStart();
            resultDispatched = true;
            handlingEffectFailure = false;
            ResetCounts();
            startError = null;
            ClearStartNotice();
            screen = Screen.Loading;
        }

        private void Start() => Show(Screen.Loading);

        private void Update()
        {
            if (startNotice && startNotice.gameObject.activeSelf &&
                Time.unscaledTime >= startNoticeUntil)
                startNotice.gameObject.SetActive(false);

            if (screen == Screen.Loading)
            {
                if (notePresenter && notePresenter.IsReady &&
                    chartSession && chartSession.IsPrepared)
                {
                    AudioClip song = gameManager && gameManager.GamePlay
                        ? gameManager.GamePlay.CurrentSong : null;
                    if (song && song.loadState == AudioDataLoadState.Loading)
                        return;
                    if (!song || song.loadState == AudioDataLoadState.Failed)
                    {
                        Show(Screen.Error);
                        return;
                    }
                    if (!string.IsNullOrEmpty(chartSession.CurrentSongTitle))
                        songTitle = chartSession.CurrentSongTitle;
                    if (AppRoot.Current &&
                        AppRoot.Current.TryGetSelectedSong(out _))
                    {
                        // Scene activation precedes the transition's Outro. Keep
                        // the prepared chart idle until the cover is fully gone.
                        if (!AppRoot.Current.IsTransitioning)
                            BeginSong(false);
                    }
                    else
                        Show(Screen.Select);
                }
                else if (notePresenter && !notePresenter.enabled && !notePresenter.IsReady)
                    Show(Screen.Error);
            }
            else if (screen == Screen.Playing)
            {
                if (Keyboard.current != null &&
                    Keyboard.current.escapeKey.wasPressedThisFrame &&
                    lastPauseTransitionFrame != Time.frameCount)
                    Pause();
                else if (gameManager &&
                    gameManager.PlaybackState == PlaybackState.Finished &&
                    judgementSystem && judgementSystem.PendingNoteCount == 0)
                    FinishSong();
                else if (sessionState && sessionState.IsFailed && gameManager &&
                    gameManager.PlaybackState == PlaybackState.Ready)
                    FinishSong();
            }
            else if (screen == Screen.Paused && Keyboard.current != null &&
                     Keyboard.current.escapeKey.wasPressedThisFrame &&
                     lastPauseTransitionFrame != Time.frameCount)
                Resume();

            if ((screen == Screen.Playing || screen == Screen.Paused) && hudText && sessionState)
                hudText.text = $"{songTitle}    Score {sessionState.CurrentScore:0}    " +
                    $"Combo {sessionState.CurrentCombo}    Health {sessionState.CurrentHealth:0}";
        }

        public void StartSong()
        {
            BeginSong(false);
        }

        public void StartAutoSong()
        {
            BeginSong(true);
        }

        public void Pause()
        {
            if (screen == Screen.Playing &&
                lastPauseTransitionFrame != Time.frameCount &&
                gameManager && gameManager.PauseGame())
            {
                lastPauseTransitionFrame = Time.frameCount;
                Show(Screen.Paused);
            }
        }

        public void Resume()
        {
            if ((!AppRoot.Current || !AppRoot.Current.IsTransitioning) &&
                screen == Screen.Paused &&
                lastPauseTransitionFrame != Time.frameCount &&
                gameManager && gameManager.ResumeGame())
            {
                lastPauseTransitionFrame = Time.frameCount;
                ClearStartNotice();
                startError = null;
                Show(Screen.Playing);
            }
        }

        public void Restart()
        {
            if (!gameManager ||
                (AppRoot.Current && AppRoot.Current.IsTransitioning)) return;
            notePresenter?.SetAutoPlayEnabled(autoPlay);
            PresentStartOutcome(gameManager.RestartGame());
        }

        public void ReturnToSelect()
        {
            StopAttempt();
            if (AppRoot.Current && AppRoot.Current.TryGetSelectedSong(out _))
            {
                AppRoot.NavigateToScene("MusicSelect");
                return;
            }
            notePresenter?.ResetJudgements();
            notePresenter?.ResetView();
            Show(Screen.Select);
        }

        public void ReturnToMusic()
        {
            StopAttempt();
            AppRoot.NavigateToScene("MusicSelect");
        }

        public void ResolveError()
        {
            if (screen != Screen.Error ||
                (AppRoot.Current && AppRoot.Current.IsTransitioning)) return;
            if (pendingSaveResult == null)
            {
                ReturnToMusic();
                return;
            }

            try { AppRoot.Current.PublishResult(pendingSaveResult); }
            catch (Exception exception)
            {
                Debug.LogError("Could not retry gameplay result save: " +
                    exception, this);
                startError = "플레이 결과를 저장하지 못했습니다. 저장 공간을 확인한 뒤 다시 시도해주세요. Esc를 누르면 곡 선택으로 돌아갑니다.";
                Show(Screen.Error);
                return;
            }

            pendingSaveResult = null;
            startError = null;
            if (!AppRoot.NavigateToScene("Result"))
            {
                startError = "결과는 저장했지만 결과 화면으로 이동하지 못했습니다. 곡 선택으로 돌아가 주세요.";
                Show(Screen.Error);
            }
        }

        private void BeginSong(bool enableAutoPlay)
        {
            if (AppRoot.Current && AppRoot.Current.IsTransitioning) return;
            if (!notePresenter || !notePresenter.IsReady || !gameManager ||
                gameManager.PlaybackState == PlaybackState.Playing ||
                gameManager.PlaybackState == PlaybackState.Paused) return;
            autoPlay = enableAutoPlay;
            notePresenter.SetAutoPlayEnabled(enableAutoPlay);
            PresentStartOutcome(gameManager.StartGame());
        }

        private void PresentStartOutcome(bool started)
        {
            if (started)
            {
                ClearStartNotice();
                Show(Screen.Playing);
                return;
            }

            Debug.LogWarning("Gameplay start was rejected: " +
                (string.IsNullOrEmpty(startError) ? "Unknown reason." : startError),
                this);

            if (gameManager.PlaybackState == PlaybackState.Playing ||
                gameManager.PlaybackState == PlaybackState.Paused)
            {
                Show(gameManager.PlaybackState == PlaybackState.Playing
                    ? Screen.Playing : Screen.Paused);
                if (startNotice)
                {
                    startNotice.text = string.IsNullOrEmpty(startError)
                        ? "다시 시작할 수 없습니다."
                        : startError;
                    startNoticeUntil = Time.unscaledTime + 4f;
                    startNotice.gameObject.SetActive(true);
                }
                return;
            }

            ClearStartNotice();
            // A rejected Restart can stop playback after commit has begun. In
            // that case the prior attempt cannot produce a valid result.
            StopAttempt();
            Show(Screen.Error);
        }

        private void Show(Screen next)
        {
            screen = next;
            if (next != Screen.Playing && next != Screen.Paused)
                ClearStartNotice();
            navigation?.Clear();
            SetVisible(loadingPanel, next == Screen.Loading);
            SetVisible(selectionPanel, next == Screen.Select);
            SetVisible(hudPanel, next == Screen.Playing || next == Screen.Paused);
            SetVisible(pausePanel, next == Screen.Paused);
            SetVisible(resultPanel, next == Screen.Result);
            SetVisible(errorPanel, next == Screen.Error);

            if (next == Screen.Select)
            {
                if (selectionTitle) selectionTitle.text = songTitle;
                if (selectionDetail)
                {
                    string difficulty = chartSession?.CurrentChart?.Metadata.DifficultyId;
                    selectionDetail.text = "Difficulty  " +
                        (string.IsNullOrEmpty(difficulty) ? "Sample" : difficulty) +
                        "\nZ X C V  /  M , . /  /  Left and Right Shift";
                }
                navigation?.ShowRoot(selectionScope);
            }
            else if (next == Screen.Paused)
                navigation?.ShowRoot(pauseScope);
            else if (next == Screen.Result)
            {
                UpdateResult();
                navigation?.ShowRoot(resultScope);
            }
            else if (next == Screen.Error)
            {
                if (errorActionLabel)
                    errorActionLabel.text = pendingSaveResult == null
                        ? "Song Select" : "Retry Save";
                if (errorDetail)
                    errorDetail.text = !string.IsNullOrEmpty(startError)
                        ? startError
                        : notePresenter &&
                        !string.IsNullOrEmpty(notePresenter.LastError)
                            ? notePresenter.LastError
                            : chartSession && !string.IsNullOrEmpty(chartSession.LastError)
                                ? chartSession.LastError
                                : "Could not prepare the song.";
                navigation?.ShowRoot(errorScope);
            }
        }

        private void UpdateResult()
        {
            if (resultTitle)
                resultTitle.text = sessionState && sessionState.IsCleared &&
                    !sessionState.IsFailed ? "Clear" : "Finished";
            if (!resultDetail || !sessionState || !gameManager) return;
            double max = gameManager.GameRule.Config.MaxScore;
            double ratio = max > 0d ? sessionState.CurrentScore / max : 0d;
            RankGrade rank = gameManager.GameRule.Config.RankThresholds.Evaluate(ratio);
            resultDetail.text = $"Rank {rank}    Score {sessionState.CurrentScore:0}\n" +
                $"Perfect+ {perfectPlus}    Perfect {perfect}    Great {great}    Miss {miss}\n" +
                $"Health {sessionState.CurrentHealth:0}    " +
                $"Judged {sessionState.JudgedNoteCount}/{sessionState.TotalNoteCount}";
        }

        private void OnNoteJudged(NoteJudgementEvent judgement)
        {
            switch (judgement.Result)
            {
                case JudgeResult.PerfectPlus: perfectPlus++; break;
                case JudgeResult.Perfect: perfect++; break;
                case JudgeResult.Great: great++; break;
                case JudgeResult.Miss: miss++; break;
            }
        }

        private void OnSessionStateChanged()
        {
            if (sessionState && sessionState.CurrentCombo > maxCombo)
                maxCombo = sessionState.CurrentCombo;
        }

        private void HandlePlaybackFailed(string diagnostic)
        {
            if (!isActiveAndEnabled || handlingEffectFailure ||
                (screen != Screen.Playing && screen != Screen.Paused) ||
                (gameManager && gameManager.GamePlay &&
                 gameManager.GamePlay.IsStartInProgress)) return;

            // A rejected start is handled by PresentStartOutcome, preserving
            // the previous attempt on failed Restart. Here playback was active:
            // invalidate it before any result can settle.
            handlingEffectFailure = true;
            try
            {
                startError = "곡을 재생하는 중 문제가 발생했습니다. 곡 선택으로 돌아가 다시 시도해주세요.";
                StopAttempt();
                Show(Screen.Error);
            }
            finally { handlingEffectFailure = false; }
        }

        private void FinishSong()
        {
            if (resultDispatched) return;
            resultDispatched = true;
            if (AppRoot.Current && activeAttempt != null &&
                chartSession && chartSession.CurrentChart != null &&
                sessionState && judgementSystem && gameManager &&
                gameManager.GameRule != null &&
                activeAttempt.MusicId == chartSession.CurrentChart.Metadata.MusicId &&
                activeAttempt.DifficultyId == chartSession.CurrentChart.Metadata.DifficultyId)
            {
                var result = new GameResultSnapshot(
                    activeAttempt.MusicId, activeAttempt.DifficultyId,
                    sessionState.CurrentScore, gameManager.GameRule.MaxScore,
                    gameManager.GameRule.GetRank(sessionState.CurrentScore),
                    perfectPlus, perfect, great, miss, maxCombo,
                    sessionState.TotalNoteCount, sessionState.IsCleared,
                    sessionState.IsFailed,
                    activeAttempt.IsAutoPlay ||
                        judgementSystem.UsedAutoPlayInCurrentAttempt,
                    activeAttempt);
                try { AppRoot.Current.PublishResult(result); }
                catch (Exception exception)
                {
                    Debug.LogError("Could not save gameplay result: " +
                        exception, this);
                    pendingSaveResult = result;
                    startError = "플레이 결과를 저장하지 못했습니다. 저장 공간을 확인한 뒤 다시 시도해주세요. Esc를 누르면 곡 선택으로 돌아갑니다.";
                    Show(Screen.Error);
                    return;
                }
                AppRoot.NavigateToScene("Result");
                return;
            }
            Show(Screen.Result);
        }

        private bool HandlePlaybackStarting(double songTimeMs)
        {
            if (gameManager.GamePlay.StartReason == PlaybackStartReason.Resume)
                return true;

            startError = null;
            preparedAttempt = null;
            if (!chartSession || chartSession.CurrentChart == null ||
                !gameManager || !gameManager.GameRule ||
                pendingRuleChange != null)
                return RejectStart("게임을 시작할 준비가 되지 않았습니다. 곡 선택으로 돌아가 다시 시도해주세요.");

            string musicId = chartSession.CurrentChart.Metadata.MusicId;
            string difficultyId = chartSession.CurrentChart.Metadata.DifficultyId;
            CharacterAbilityDefinition ability = null;
            GameRuleConfig selectedRule = null;
            string unlockTargetMusicId = null;
            AppRoot.SongSelection selection = default;
            bool hasSelection = AppRoot.Current &&
                AppRoot.Current.TryGetSelectedSong(out selection);
            bool selectionMatchesChart = hasSelection &&
                string.Equals(selection.MusicId, musicId, StringComparison.Ordinal) &&
                string.Equals(selection.DifficultyId, difficultyId, StringComparison.Ordinal);
            if (selectionMatchesChart)
                AppRoot.Current.TryGetSelectedPlayConditions(
                    out ability, out selectedRule, out unlockTargetMusicId);
            else if (gameManager.GamePlay.StartReason == PlaybackStartReason.Restart &&
                     activeAttempt != null &&
                     string.Equals(activeAttempt.MusicId, musicId,
                         StringComparison.Ordinal) &&
                     string.Equals(activeAttempt.DifficultyId, difficultyId,
                         StringComparison.Ordinal))
            {
                // A pending selection for another song cannot change the chart
                // that Restart is actually going to replay.
                ability = activeAbilitySource;
                selectedRule = activeRuleSource;
                unlockTargetMusicId = activeAttempt.UnlockTargetMusicId;
            }
            else if (hasSelection)
                return RejectStart(
                    "선택한 곡이 준비된 곡과 다릅니다. 곡 선택으로 돌아가 다시 시도해주세요.");

            try
            {
                GameRule rule = gameManager.GameRule;
                if (ability != null && !selectedRule)
                    return RejectStart(
                        "선택한 캐릭터의 게이지 규칙이 없습니다. 곡 선택으로 돌아가 다시 시도해주세요.");
                GameRuleConfig source = selectedRule
                    ? selectedRule : rule.DefaultConfig;
                if (!source)
                    return RejectStart("게이지 규칙이 없습니다. 곡 선택으로 돌아가 다시 시도해주세요.");
                source.ValidateForSession();
                ability?.FreezeForStart(source);

                pendingRuleChange = rule.BeginSessionConfigChange(source);

                preparedAbilitySource = ability;
                preparedRuleSource = selectedRule;
                preparedMusicId = musicId;
                preparedDifficultyId = difficultyId;
                preparedUnlockTargetMusicId = unlockTargetMusicId;
                return true;
            }
            catch (Exception exception)
            {
                pendingRuleChange?.Rollback();
                pendingRuleChange = null;
                Debug.LogError("Game start validation failed: " + exception,
                    this);
                return RejectStart("선택한 캐릭터와 게이지 규칙을 확인할 수 없습니다. 곡 선택으로 돌아가 다시 시도해주세요.");
            }
        }

        private bool HandlePlaybackCommitting(double songTimeMs)
        {
            if (gameManager.GamePlay.StartReason == PlaybackStartReason.Resume)
                return true;
            if (string.IsNullOrEmpty(preparedMusicId) ||
                string.IsNullOrEmpty(preparedDifficultyId))
                return RejectStart("게임을 시작할 준비가 되지 않았습니다. 곡 선택으로 돌아가 다시 시도해주세요.");

            try
            {
                pendingRuleChange?.Commit();
                GameRuleConfig committed = gameManager.GameRule.Config;
                if (!string.IsNullOrEmpty(preparedUnlockTargetMusicId) &&
                    committed.SpecialHardGauge == null)
                    throw new InvalidOperationException(
                        "A song unlock attempt must start with Special Hard.");
                CharacterGaugeRuleSnapshot gauge =
                    CharacterGaugeRuleSnapshot.Capture(committed);
                CharacterAbilityStartSnapshot character =
                    preparedAbilitySource?.FreezeForStart(committed);
                preparedAttempt = new GameAttemptStartSnapshot(
                    Guid.NewGuid(), preparedMusicId, preparedDifficultyId,
                    autoPlay, gauge, character, preparedUnlockTargetMusicId);
                return true;
            }
            catch (Exception exception)
            {
                pendingRuleChange?.Rollback();
                pendingRuleChange = null;
                preparedAttempt = null;
                Debug.LogError("Game start commit failed: " + exception,
                    this);
                return RejectStart("게임 시작 중 문제가 발생했습니다. 곡 선택으로 돌아가 다시 시도해주세요.");
            }
        }

        private void HandlePlaybackStarted(double songTimeMs)
        {
            if (gameManager.GamePlay.StartReason == PlaybackStartReason.Resume)
                return;

            pendingRuleChange?.Complete();
            pendingRuleChange = null;
            activeAttempt = preparedAttempt;
            pendingSaveResult = null;
            activeAbilitySource = preparedAbilitySource;
            activeRuleSource = preparedRuleSource;
            ClearPreparedStart();
            if (AppRoot.Current) AppRoot.Current.ActivateAttempt(activeAttempt);
            resultDispatched = false;
            ResetCounts();
            startError = null;
            ClearStartNotice();
        }

        private void HandlePlaybackStartAborted(double songTimeMs)
        {
            pendingRuleChange?.Rollback();
            pendingRuleChange = null;
            ClearPreparedStart();
        }

        private void ClearPreparedStart()
        {
            preparedAttempt = null;
            preparedAbilitySource = null;
            preparedRuleSource = null;
            preparedMusicId = null;
            preparedDifficultyId = null;
            preparedUnlockTargetMusicId = null;
        }

        private bool RejectStart(string error)
        {
            startError = string.IsNullOrWhiteSpace(error)
                ? "Could not prepare the gameplay start."
                : error;
            return false;
        }

        private void ClearStartNotice()
        {
            if (startNotice) startNotice.gameObject.SetActive(false);
        }

        private void StopAttempt()
        {
            pendingRuleChange?.Rollback();
            pendingRuleChange = null;
            ClearPreparedStart();
            gameManager?.StopGame();
            if (activeAttempt != null && AppRoot.Current)
                AppRoot.Current.EndAttempt(activeAttempt.AttemptId);
            activeAttempt = null;
            pendingSaveResult = null;
            activeAbilitySource = null;
            activeRuleSource = null;
            resultDispatched = true;
            ClearStartNotice();
            if (gameManager && gameManager.GameRule)
                gameManager.GameRule.ClearSessionConfig();
        }

        private void ResetCounts()
        {
            perfectPlus = perfect = great = miss = maxCombo = 0;
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target && target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
