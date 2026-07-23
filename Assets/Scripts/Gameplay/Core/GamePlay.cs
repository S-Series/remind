using UnityEngine;

public sealed class GamePlay : MonoBehaviour
{
    public static GamePlay Instance { get; private set; }

    [SerializeField] Transform cameraTransform;
    [SerializeField] AudioSource audioSource;


    private void Awake() {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (cameraTransform == null)
            Debug.LogError("Camera Transform is not assigned in GamePlay script.");
    }

    public static void SetAudioSource(AudioClip audioClip, float volume)
    {
        AudioSource audioSource = Instance.audioSource;
        if (audioSource != null)
        {
            audioSource.clip = audioClip;
            audioSource.volume = volume;
            audioSource.Play();
        }
        else
        {
            Debug.LogError("AudioSource component not found in the scene.");
        }
    }
}
