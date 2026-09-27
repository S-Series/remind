using System.Globalization;
using REmind.Gameplay.Demo;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Test : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshPro timeText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Toggle autoToggle;

    [Header("Calls")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DemoPlayController demoPlayController;

    private void Start()
    {
        if (!timeText ||
            !playButton ||
            !resetButton ||
            !autoToggle ||
            !gameManager ||
            !demoPlayController)
        {
            Debug.LogError("Test UI references are incomplete.", this);
            enabled = false;
            return;
        }

        playButton.onClick.AddListener(TogglePlayback);
        resetButton.onClick.AddListener(ResetPlayback);
        autoToggle.onValueChanged.AddListener(SetAutoPlayEnabled);
        SetAutoPlayEnabled(autoToggle.isOn);
        SetDisplayedSongTime(0d);
    }

    private void Update()
    {
        SetDisplayedSongTime(gameManager.CorePlayMs);
    }

    private void OnDestroy()
    {
        if (playButton)
        {
            playButton.onClick.RemoveListener(TogglePlayback);
        }

        if (resetButton)
        {
            resetButton.onClick.RemoveListener(ResetPlayback);
        }

        if (autoToggle)
        {
            autoToggle.onValueChanged.RemoveListener(SetAutoPlayEnabled);
        }

        demoPlayController?.SetAutoPlayEnabled(false);
    }

    public void TogglePlayback()
    {
        bool succeeded = gameManager.PlaybackState switch
        {
            PlaybackState.Playing => gameManager.PauseGame(),
            PlaybackState.Paused => gameManager.ResumeGame(),
            PlaybackState.Ready => gameManager.StartGame(),
            PlaybackState.Finished => RestartFinishedPlayback(),
            _ => false
        };

        if (!succeeded)
        {
            Debug.LogWarning(
                $"Playback toggle was ignored in state {gameManager.PlaybackState}.",
                this);
        }
    }

    public void ResetPlayback()
    {
        gameManager.StopGame();
        demoPlayController.ResetJudgements();
        demoPlayController.ResetView();
        SetDisplayedSongTime(0d);
    }

    private bool RestartFinishedPlayback()
    {
        demoPlayController.ResetJudgements();
        return gameManager.RestartGame();
    }

    private void SetAutoPlayEnabled(bool value)
    {
        demoPlayController.SetAutoPlayEnabled(value);
    }

    private void SetDisplayedSongTime(double valueMs)
    {
        timeText.SetText(
            valueMs.ToString("F6", CultureInfo.InvariantCulture) + " ms");
    }
}
