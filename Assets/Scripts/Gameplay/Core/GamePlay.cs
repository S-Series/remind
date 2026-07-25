using System;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class GamePlay : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private AudioSource audioSource;
    [FormerlySerializedAs("HitSource")]
    [SerializeField] private AudioSource hitSource;
    [SerializeField] private NoteJudgementSystem judgementSystem;
    [SerializeField, Min(0f)] private float hitSoundTrimStartMs;

    [Header("Playback")]
    [SerializeField] private AudioClip initialSong;
    [SerializeField] private bool playOnStart;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField, Min(0.05f)] private float schedulingLeadTimeSeconds = 0.2f;

    private double songStartDspTime;
    private double heldSongTimeMs;
    private double scheduledSongTimeMs;
    private AudioClip preparedHitClip;

    public event Action<PlaybackState> PlaybackStateChanged;
    public event Action PlaybackCompleted;

    public PlaybackState State { get; private set; } = PlaybackState.Empty;
    public AudioClip CurrentSong => audioSource != null ? audioSource.clip : null;
    public bool IsPlaying => State == PlaybackState.Playing;
    public double SongStartDspTime { get; private set; }
    public double InputTimeToDspOffset { get; private set; }

    public double SongDurationMs
    {
        get
        {
            AudioClip song = CurrentSong;
            return song == null || song.frequency <= 0
                ? 0d
                : song.samples / (double)song.frequency * 1000d;
        }
    }

    public double SongTimeMs
    {
        get
        {
            switch (State)
            {
                case PlaybackState.Playing:
                    double elapsedMs = (AudioSettings.dspTime - SongStartDspTime) * 1000d;
                    return Clamp(elapsedMs, scheduledSongTimeMs, SongDurationMs);
                case PlaybackState.Paused:
                    return heldSongTimeMs;
                case PlaybackState.Finished:
                    return SongDurationMs;
                default:
                    return 0d;
            }
        }
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            if (sources.Length > 0)
            {
                audioSource = sources[0];
            }
        }

        if (hitSource == null)
        {
            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != audioSource)
                {
                    hitSource = sources[i];
                    break;
                }
            }
        }

        if (judgementSystem == null)
        {
            GameManager manager = GetComponentInParent<GameManager>();
            if (manager != null)
            {
                judgementSystem = manager.GetComponentInChildren<NoteJudgementSystem>(true);
            }
        }

        if (audioSource == null)
        {
            Debug.LogError("Music AudioSource is not assigned under Playback System.", this);
            enabled = false;
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        if (hitSource != null)
        {
            hitSource.playOnAwake = false;
            hitSource.loop = false;
            hitSource.spatialBlend = 0f;
            PrepareHitSound();
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning("Camera Transform is not assigned in GamePlay.", this);
        }

        if (initialSong != null)
        {
            PrepareSong(initialSong, musicVolume);
        }
    }

    private void OnEnable()
    {
        if (judgementSystem != null)
        {
            judgementSystem.NoteJudged += HandleNoteJudged;
        }
    }

    private void OnDisable()
    {
        if (judgementSystem != null)
        {
            judgementSystem.NoteJudged -= HandleNoteJudged;
        }
    }

    private void OnDestroy()
    {
        if (preparedHitClip != null)
        {
            Destroy(preparedHitClip);
        }
    }

    private void Start()
    {
        if (playOnStart && State == PlaybackState.Ready)
        {
            Play();
        }
    }

    private void Update()
    {
        if (State != PlaybackState.Playing || SongTimeMs < SongDurationMs)
        {
            return;
        }

        audioSource.Stop();
        heldSongTimeMs = SongDurationMs;
        SetState(PlaybackState.Finished);
        PlaybackCompleted?.Invoke();
    }

    public bool PrepareSong(AudioClip song, float volume = 1f)
    {
        if (song == null)
        {
            Debug.LogError("Cannot prepare a null AudioClip.", this);
            return false;
        }

        audioSource.Stop();
        audioSource.clip = song;
        audioSource.volume = Mathf.Clamp01(volume);

        if (song.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogError($"Audio data failed to load: {song.name}", this);
            SetState(PlaybackState.Empty);
            return false;
        }

        if (song.loadState == AudioDataLoadState.Unloaded && !song.LoadAudioData())
        {
            Debug.LogError($"Could not start loading audio data: {song.name}", this);
            SetState(PlaybackState.Empty);
            return false;
        }

        heldSongTimeMs = 0d;
        scheduledSongTimeMs = 0d;
        SetState(PlaybackState.Ready);
        return true;
    }

    public bool Play()
    {
        if (State == PlaybackState.Paused)
        {
            return Resume();
        }

        return ScheduleFrom(0d);
    }

    public bool Pause()
    {
        if (State != PlaybackState.Playing)
        {
            return false;
        }

        heldSongTimeMs = Clamp(SongTimeMs, 0d, SongDurationMs);
        audioSource.Stop();
        SetState(PlaybackState.Paused);
        return true;
    }

    public bool Resume()
    {
        return State == PlaybackState.Paused && ScheduleFrom(heldSongTimeMs);
    }

    public bool Restart()
    {
        return ScheduleFrom(0d);
    }

    public bool PlayHitSound()
    {
        if (hitSource == null)
        {
            return false;
        }

        AudioClip clip = preparedHitClip != null ? preparedHitClip : hitSource.clip;
        if (clip == null)
        {
            return false;
        }

        hitSource.PlayOneShot(clip);
        return true;
    }

    public bool TryGetInputSongTimeMs(double inputEventTime, out double inputSongTimeMs)
    {
        if (State != PlaybackState.Playing || double.IsNaN(inputEventTime) || double.IsInfinity(inputEventTime))
        {
            inputSongTimeMs = 0d;
            return false;
        }

        double inputDspTime = inputEventTime + InputTimeToDspOffset;
        inputSongTimeMs = (inputDspTime - SongStartDspTime) * 1000d;
        return true;
    }

    public bool TryGetJudgeOffsetMs(
        double inputEventTime,
        double noteHitTimeMs,
        double userOffsetMs,
        out double judgeOffsetMs)
    {
        if (double.IsNaN(noteHitTimeMs) || double.IsInfinity(noteHitTimeMs) ||
            double.IsNaN(userOffsetMs) || double.IsInfinity(userOffsetMs) ||
            !TryGetInputSongTimeMs(inputEventTime, out double inputSongTimeMs))
        {
            judgeOffsetMs = 0d;
            return false;
        }

        judgeOffsetMs = inputSongTimeMs - userOffsetMs - noteHitTimeMs;
        return true;
    }

    public void Stop()
    {
        audioSource.Stop();
        if (CurrentSong != null)
        {
            audioSource.timeSamples = 0;
        }

        heldSongTimeMs = 0d;
        scheduledSongTimeMs = 0d;
        SetState(CurrentSong == null ? PlaybackState.Empty : PlaybackState.Ready);
    }

    private bool ScheduleFrom(double songTimeMs)
    {
        AudioClip song = CurrentSong;
        if (song == null || song.samples <= 0 || song.frequency <= 0)
        {
            Debug.LogError("No playable song has been prepared.", this);
            return false;
        }

        double durationMs = SongDurationMs;
        double startTimeMs = Clamp(songTimeMs, 0d, durationMs);
        if (startTimeMs >= durationMs)
        {
            heldSongTimeMs = durationMs;
            SetState(PlaybackState.Finished);
            return false;
        }

        int startSample = (int)Math.Round(startTimeMs / 1000d * song.frequency);
        startSample = Mathf.Clamp(startSample, 0, song.samples - 1);
        double sampleAlignedStartTimeMs = startSample / (double)song.frequency * 1000d;

        audioSource.Stop();
        audioSource.timeSamples = startSample;

        double dspNow = AudioSettings.dspTime;
        InputTimeToDspOffset = dspNow - Time.realtimeSinceStartupAsDouble;

        double scheduledDspTime = dspNow + schedulingLeadTimeSeconds;
        SongStartDspTime = scheduledDspTime - sampleAlignedStartTimeMs / 1000d;
        heldSongTimeMs = sampleAlignedStartTimeMs;
        scheduledSongTimeMs = sampleAlignedStartTimeMs;

        audioSource.PlayScheduled(scheduledDspTime);
        SetState(PlaybackState.Playing);
        return true;
    }

    private void SetState(PlaybackState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        PlaybackStateChanged?.Invoke(state);
    }

    private void HandleNoteJudged(NoteJudgementEvent judgementEvent)
    {
        if (judgementEvent.Result == JudgeResult.Perfect)
        {
            PlayHitSound();
        }
    }

    private void PrepareHitSound()
    {
        AudioClip sourceClip = hitSource.clip;
        if (sourceClip == null || hitSoundTrimStartMs <= 0f)
        {
            return;
        }

        if (sourceClip.loadState == AudioDataLoadState.Unloaded &&
            !sourceClip.LoadAudioData())
        {
            Debug.LogWarning($"Could not preload hit sound: {sourceClip.name}", this);
            return;
        }

        int trimFrames = Mathf.RoundToInt(
            hitSoundTrimStartMs / 1000f * sourceClip.frequency);
        trimFrames = Mathf.Clamp(trimFrames, 0, sourceClip.samples - 1);
        if (trimFrames == 0)
        {
            return;
        }

        int channelCount = sourceClip.channels;
        float[] sourceData = new float[sourceClip.samples * channelCount];
        if (!sourceClip.GetData(sourceData, 0))
        {
            Debug.LogWarning($"Could not read hit sound data: {sourceClip.name}", this);
            return;
        }

        int remainingFrames = sourceClip.samples - trimFrames;
        float[] trimmedData = new float[remainingFrames * channelCount];
        Array.Copy(
            sourceData,
            trimFrames * channelCount,
            trimmedData,
            0,
            trimmedData.Length);

        AudioClip trimmedClip = AudioClip.Create(
            $"{sourceClip.name}_RuntimeTrimmed",
            remainingFrames,
            channelCount,
            sourceClip.frequency,
            false);

        if (!trimmedClip.SetData(trimmedData, 0))
        {
            Destroy(trimmedClip);
            Debug.LogWarning($"Could not prepare hit sound: {sourceClip.name}", this);
            return;
        }

        preparedHitClip = trimmedClip;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
