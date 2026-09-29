using System;
using REmind.Charting;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GamePlay : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private AudioSource audioSource;

    [Header("Playback")]
    [SerializeField] private AudioClip initialSong;
    [SerializeField] private bool playOnStart;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField, Min(0.05f)] private float schedulingLeadTimeSeconds = 0.2f;

    private double heldSongTimeMs;
    private double requiredCompletionTimeMs;
    private double scheduledSongTimeMs;
    private DspSongClock songClock;
    private bool startInProgress;
    private float baseSongVolume = 1f;
    private bool startCancellationRequested;
    private int stateNotificationDepth;

    public event Action<PlaybackState> PlaybackStateChanged;
    public event Action PlaybackCompleted;
    // Starting prepares private state, Committing activates it before audio/state are
    // published, and Started confirms success. Any failed start after preparation is
    // paired with PlaybackStartAborted so provisional state can be rolled back.
    public event Func<double, bool> PlaybackStarting;
    public event Func<double, bool> PlaybackCommitting;
    public event Action<double> PlaybackStarted;
    public event Action<double> PlaybackStartAborted;

    public PlaybackState State { get; private set; } = PlaybackState.Empty;
    public AudioClip CurrentSong => audioSource != null ? audioSource.clip : null;
    public bool IsPlaying => State == PlaybackState.Playing;
    public bool IsStartInProgress => startInProgress;
    public PlaybackStartReason StartReason { get; private set; } =
        PlaybackStartReason.Play;
    public bool HasReachedScheduledStart => IsPlaying &&
        AudioSettings.dspTime >= songClock.DspTimeAt(scheduledSongTimeMs);
    public double SongStartDspTime { get; private set; }
    public double InputTimeToDspOffset { get; private set; }

    public double SongDurationMs
    {
        get
        {
            AudioClip song = CurrentSong;
            double audioDurationMs = song == null || song.frequency <= 0
                ? 0d
                : song.samples / (double)song.frequency * 1000d;
            return Math.Max(audioDurationMs, requiredCompletionTimeMs);
        }
    }

    public double AudioDurationMs
    {
        get
        {
            AudioClip song = CurrentSong;
            return song == null || song.frequency <= 0 ? 0d :
                song.samples / (double)song.frequency * 1000d;
        }
    }

    public double SongTimeMs
    {
        get
        {
            switch (State)
            {
                case PlaybackState.Playing:
                    double currentSongTimeMs = songClock.SongTimeMsAt(
                        AudioSettings.dspTime);
                    return Clamp(
                        currentSongTimeMs,
                        scheduledSongTimeMs,
                        SongDurationMs);
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

        if (audioSource == null)
        {
            Debug.LogError("Music AudioSource is not assigned under Playback System.", this);
            enabled = false;
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        if (cameraTransform == null)
        {
            Debug.LogWarning("Camera Transform is not assigned in GamePlay.", this);
        }

        if (initialSong != null)
        {
            PrepareSong(initialSong, musicVolume);
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
        if (audioSource)
            audioSource.volume = baseSongVolume *
                (REmind.Gameplay.AppRoot.Current
                    ? REmind.Gameplay.AppRoot.Current.MusicGain : 1f);
        if (State != PlaybackState.Playing || SongTimeMs < SongDurationMs)
        {
            return;
        }

        audioSource.Stop();
        heldSongTimeMs = SongDurationMs;
        SetState(PlaybackState.Finished);
        InvokeSafely(PlaybackCompleted);
    }

    public bool PrepareSong(AudioClip song, float volume = 1f)
    {
        if (startInProgress)
        {
            Debug.LogWarning(
                "Cannot replace the song while a playback start is being committed.",
                this);
            return false;
        }

        if (song == null)
        {
            Debug.LogError("Cannot prepare a null AudioClip.", this);
            return false;
        }

        if (song.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogError($"Audio data failed to load: {song.name}", this);
            return false;
        }

        if (song.loadState == AudioDataLoadState.Unloaded && !song.LoadAudioData())
        {
            Debug.LogError($"Could not start loading audio data: {song.name}", this);
            return false;
        }

        // Do not disturb the currently prepared song until the replacement has
        // passed every synchronous load check.
        audioSource.Stop();
        audioSource.clip = song;
        baseSongVolume = Mathf.Clamp01(volume);
        audioSource.volume = baseSongVolume *
            (REmind.Gameplay.AppRoot.Current
                ? REmind.Gameplay.AppRoot.Current.MusicGain : 1f);
        requiredCompletionTimeMs = 0d;

        heldSongTimeMs = 0d;
        scheduledSongTimeMs = 0d;
        SetState(PlaybackState.Ready);
        return true;
    }

    public bool SetCompletionTimeMs(double completionTimeMs)
    {
        if (CurrentSong == null || State == PlaybackState.Playing ||
            State == PlaybackState.Paused || startInProgress ||
            double.IsNaN(completionTimeMs) ||
            double.IsInfinity(completionTimeMs) || completionTimeMs < 0d)
            return false;
        requiredCompletionTimeMs = completionTimeMs;
        return true;
    }

    public bool Play()
    {
        if (State == PlaybackState.Paused)
        {
            return Resume();
        }

        return ScheduleFrom(0d, PlaybackStartReason.Play);
    }

    public bool Pause()
    {
        if (startInProgress)
            startCancellationRequested = true;
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
        return State == PlaybackState.Paused &&
            ScheduleFrom(heldSongTimeMs, PlaybackStartReason.Resume);
    }

    public bool Restart()
    {
        return ScheduleFrom(0d, PlaybackStartReason.Restart);
    }

    public bool TryGetInputSongTimeMs(double inputEventTime, out double inputSongTimeMs)
    {
        if (State != PlaybackState.Playing || double.IsNaN(inputEventTime) || double.IsInfinity(inputEventTime))
        {
            inputSongTimeMs = 0d;
            return false;
        }

        double inputDspTime = inputEventTime + InputTimeToDspOffset;
        inputSongTimeMs = songClock.SongTimeMsAt(inputDspTime);
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
        if (startInProgress)
            startCancellationRequested = true;
        audioSource.Stop();
        if (CurrentSong != null)
        {
            audioSource.timeSamples = 0;
        }

        heldSongTimeMs = 0d;
        scheduledSongTimeMs = 0d;
        SetState(CurrentSong == null ? PlaybackState.Empty : PlaybackState.Ready);
    }

    private bool ScheduleFrom(double songTimeMs, PlaybackStartReason reason)
    {
        if (startInProgress || stateNotificationDepth > 0)
        {
            Debug.LogWarning(
                "Playback cannot start recursively from another start or state-change callback.",
                this);
            return false;
        }

        startInProgress = true;
        startCancellationRequested = false;
        try
        {
            return ScheduleFromCore(songTimeMs, reason);
        }
        finally
        {
            startInProgress = false;
            startCancellationRequested = false;
        }
    }

    private bool ScheduleFromCore(double songTimeMs, PlaybackStartReason reason)
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
        // A paused clock may sit between audio samples. Never move its logical
        // timeline backwards merely because the nearest sample is earlier.
        double logicalStartTimeMs = reason == PlaybackStartReason.Resume
            ? Math.Max(startTimeMs, sampleAlignedStartTimeMs)
            : sampleAlignedStartTimeMs;

        StartReason = reason;
        if (!CanStartPlayback(logicalStartTimeMs))
        {
            InvokeSafely(PlaybackStartAborted, logicalStartTimeMs);
            return false;
        }
        if (startCancellationRequested)
        {
            InvokeSafely(PlaybackStartAborted, logicalStartTimeMs);
            return false;
        }

        if (!CanCommitPlayback(logicalStartTimeMs) ||
            startCancellationRequested)
        {
            InvokeSafely(PlaybackStartAborted, logicalStartTimeMs);
            Stop();
            return false;
        }

        try
        {
            audioSource.Stop();
            audioSource.timeSamples = startSample;

            double dspNow = AudioSettings.dspTime;
            InputTimeToDspOffset = dspNow - Time.realtimeSinceStartupAsDouble;

            double scheduledDspTime = dspNow + schedulingLeadTimeSeconds;
            songClock = new DspSongClock(
                scheduledDspTime,
                logicalStartTimeMs);
            SongStartDspTime = songClock.DspTimeAt(0d);
            heldSongTimeMs = logicalStartTimeMs;
            scheduledSongTimeMs = logicalStartTimeMs;

            audioSource.PlayScheduled(scheduledDspTime);
            SetState(PlaybackState.Playing);
            if (State != PlaybackState.Playing)
            {
                InvokeSafely(PlaybackStartAborted, logicalStartTimeMs);
                return false;
            }

            InvokeSafely(PlaybackStarted, logicalStartTimeMs);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            InvokeSafely(PlaybackStartAborted, logicalStartTimeMs);
            Stop();
            return false;
        }
    }

    private bool CanStartPlayback(double startTimeMs)
    {
        if (PlaybackStarting == null) return true;
        foreach (Delegate validator in PlaybackStarting.GetInvocationList())
        {
            try
            {
                if (!((Func<double, bool>)validator)(startTimeMs)) return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }
        return true;
    }

    private bool CanCommitPlayback(double startTimeMs)
    {
        if (PlaybackCommitting == null) return true;
        foreach (Delegate validator in PlaybackCommitting.GetInvocationList())
        {
            try
            {
                if (!((Func<double, bool>)validator)(startTimeMs)) return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }
        return true;
    }

    private void InvokeSafely(Action<double> callbacks, double value)
    {
        if (callbacks == null) return;
        foreach (Delegate callback in callbacks.GetInvocationList())
        {
            try { ((Action<double>)callback)(value); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private void InvokeSafely(Action callbacks)
    {
        if (callbacks == null) return;
        foreach (Delegate callback in callbacks.GetInvocationList())
        {
            try { ((Action)callback)(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private void InvokeSafely(Action<PlaybackState> callbacks,
        PlaybackState value)
    {
        if (callbacks == null) return;
        foreach (Delegate callback in callbacks.GetInvocationList())
        {
            try { ((Action<PlaybackState>)callback)(value); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private void SetState(PlaybackState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        stateNotificationDepth++;
        try { InvokeSafely(PlaybackStateChanged, state); }
        finally { stateNotificationDepth--; }
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
