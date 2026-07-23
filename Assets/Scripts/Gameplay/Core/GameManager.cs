using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameManager : MonoSingleton<GameManager>
{
    public GamePlay GamePlay { get; private set; }
    public GameRule GameRule { get; private set; }

    public int CorePlayMs { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this) return;

        if (!TryGetComponent(out GamePlay gamePlay))
        {
            Debug.LogError(
                "GameManager와 같은 GameObject에 GamePlay가 없습니다.",
                this
            );
        }
        else
        {
            GamePlay = gamePlay;
        }

        if (!TryGetComponent(out GameRule gameRule))
        {
            Debug.LogError(
                "GameManager와 같은 GameObject에 GameRule이 없습니다.",
                this
            );
        }
        else
        {
            GameRule = gameRule;
        }
    }

    public void SetAudioSource(AudioClip audioClip, float volume)
    {
        GamePlay.SetAudioSource(audioClip, volume);
    }
}