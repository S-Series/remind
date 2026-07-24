using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameManager : MonoSingleton<GameManager>
{
    public GamePlay GamePlay { get; private set; }
    public GameRule GameRule { get; private set; }

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

        if (!TryGetComponent(out GamePlay gamePlay))
        {
            Debug.LogError("GamePlay is missing from the GameManager GameObject.", this);
        }
        else
        {
            GamePlay = gamePlay;
        }

        if (!TryGetComponent(out GameRule gameRule))
        {
            Debug.LogError("GameRule is missing from the GameManager GameObject.", this);
        }
        else
        {
            GameRule = gameRule;
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
