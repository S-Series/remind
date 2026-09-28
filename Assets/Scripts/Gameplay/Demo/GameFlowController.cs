using REmind.Common.UI;
using REmind.Gameplay.Chart;
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

        private Screen screen = Screen.Loading;
        private bool autoPlay;
        private int perfect;
        private int great;
        private int good;
        private int miss;
        private int maxCombo;
        private int lastPauseTransitionFrame = -1;

        private void Awake()
        {
            if (!gameManager) gameManager = GetComponentInParent<GameManager>();
            if (!notePresenter) notePresenter = GetComponentInChildren<DemoPlayController>(true);
            if (!chartSession) chartSession = GetComponentInChildren<GameplayChartSessionController>(true);
            if (!sessionState) sessionState = GetComponentInChildren<GameplaySessionState>(true);
            if (!judgementSystem) judgementSystem = GetComponentInChildren<NoteJudgementSystem>(true);
        }

        private void OnEnable()
        {
            if (judgementSystem) judgementSystem.NoteJudged += OnNoteJudged;
            if (sessionState) sessionState.StateChanged += OnSessionStateChanged;
        }

        private void OnDisable()
        {
            if (judgementSystem) judgementSystem.NoteJudged -= OnNoteJudged;
            if (sessionState) sessionState.StateChanged -= OnSessionStateChanged;
        }

        private void Start() => Show(Screen.Loading);

        private void Update()
        {
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
                        BeginSong(false);
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
            autoPlay = false;
            BeginSong(false);
        }

        public void StartAutoSong()
        {
            autoPlay = true;
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
            if (screen == Screen.Paused &&
                lastPauseTransitionFrame != Time.frameCount &&
                gameManager && gameManager.ResumeGame())
            {
                lastPauseTransitionFrame = Time.frameCount;
                Show(Screen.Playing);
            }
        }

        public void Restart()
        {
            if (!gameManager) return;
            ResetCounts();
            notePresenter?.SetAutoPlayEnabled(autoPlay);
            Show(gameManager.RestartGame() ? Screen.Playing : Screen.Error);
        }

        public void ReturnToSelect()
        {
            gameManager?.StopGame();
            if (AppRoot.Current && AppRoot.Current.TryGetSelectedSong(out _))
            {
                SceneManager.LoadScene("MusicSelect");
                return;
            }
            notePresenter?.ResetJudgements();
            notePresenter?.ResetView();
            Show(Screen.Select);
        }

        public void ReturnToMusic()
        {
            gameManager?.StopGame();
            SceneManager.LoadScene("MusicSelect");
        }

        private void BeginSong(bool enableAutoPlay)
        {
            if (!notePresenter || !notePresenter.IsReady || !gameManager) return;
            ResetCounts();
            notePresenter.SetAutoPlayEnabled(enableAutoPlay);
            Show(gameManager.StartGame() ? Screen.Playing : Screen.Error);
        }

        private void Show(Screen next)
        {
            screen = next;
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
                if (errorDetail)
                    errorDetail.text = notePresenter &&
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
                $"Perfect {perfect}    Great {great}    Good {good}    Miss {miss}\n" +
                $"Health {sessionState.CurrentHealth:0}    " +
                $"Judged {sessionState.JudgedNoteCount}/{sessionState.TotalNoteCount}";
        }

        private void OnNoteJudged(NoteJudgementEvent judgement)
        {
            switch (judgement.Result)
            {
                case JudgeResult.Perfect: perfect++; break;
                case JudgeResult.Great: great++; break;
                case JudgeResult.Good: good++; break;
                case JudgeResult.Miss: miss++; break;
            }
        }

        private void OnSessionStateChanged()
        {
            if (sessionState && sessionState.CurrentCombo > maxCombo)
                maxCombo = sessionState.CurrentCombo;
        }

        private void FinishSong()
        {
            if (AppRoot.Current &&
                AppRoot.Current.TryGetSelectedSong(out AppRoot.SongSelection selection) &&
                chartSession && chartSession.CurrentChart != null &&
                sessionState && gameManager && gameManager.GameRule != null &&
                selection.MusicId == chartSession.CurrentChart.Metadata.MusicId &&
                selection.DifficultyId == chartSession.CurrentChart.Metadata.DifficultyId)
            {
                var result = new GameResultSnapshot(
                    selection.MusicId, selection.DifficultyId,
                    sessionState.CurrentScore, gameManager.GameRule.MaxScore,
                    gameManager.GameRule.GetRank(sessionState.CurrentScore),
                    perfect, great, good, miss, maxCombo,
                    sessionState.TotalNoteCount, sessionState.IsCleared,
                    sessionState.IsFailed, autoPlay);
                AppRoot.Current.PublishResult(result);
                SceneManager.LoadScene("Result");
                return;
            }
            Show(Screen.Result);
        }

        private void ResetCounts()
        {
            perfect = great = good = miss = maxCombo = 0;
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target && target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
