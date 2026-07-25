using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameManager : MonoSingleton<GameManager>
{
    [Header("Systems")]
    [SerializeField] private GamePlay gamePlay;
    [SerializeField] private GameRule gameRule;

    public GamePlay GamePlay => gamePlay;
    public GameRule GameRule => gameRule;

    public double CorePlayMs => GamePlay != null ? GamePlay.SongTimeMs : 0d;
    public PlaybackState PlaybackState => GamePlay != null
        ? GamePlay.State
        : PlaybackState.Empty;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        if (gamePlay == null)
        {
            gamePlay = GetComponentInChildren<GamePlay>(true);
        }

        if (gamePlay == null)
        {
            Debug.LogError("GamePlay is missing under Core System.", this);
        }

        if (gameRule == null)
        {
            gameRule = GetComponentInChildren<GameRule>(true);
        }

        if (gameRule == null)
        {
            Debug.LogError("GameRule is missing under Core System.", this);
        }
    }

    public bool SetAudioSource(AudioClip audioClip, float volume = 1f)
    {
        return GamePlay != null && GamePlay.PrepareSong(audioClip, volume);
    }

    public bool StartGame()
    {
        return GamePlay != null && GamePlay.Play();
    }

    public bool PauseGame()
    {
        return GamePlay != null && GamePlay.Pause();
    }

    public bool ResumeGame()
    {
        return GamePlay != null && GamePlay.Resume();
    }

    public bool RestartGame()
    {
        return GamePlay != null && GamePlay.Restart();
    }

    public bool PlayHitSound()
    {
        return GamePlay != null && GamePlay.PlayHitSound();
    }

    public bool TryGetJudgeOffsetMs(
        double inputEventTime,
        double noteHitTimeMs,
        double userOffsetMs,
        out double judgeOffsetMs)
    {
        if (GamePlay == null)
        {
            judgeOffsetMs = 0d;
            return false;
        }

        return GamePlay.TryGetJudgeOffsetMs(
            inputEventTime,
            noteHitTimeMs,
            userOffsetMs,
            out judgeOffsetMs);
    }

    public void StopGame()
    {
        GamePlay?.Stop();
    }
}
