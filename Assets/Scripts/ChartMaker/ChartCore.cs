using System;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartCore : MonoSingleton<ChartCore>
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Test Timeline")]
    [SerializeField] private double testMs;
    [SerializeField] private double bpm = 120d;
    [SerializeField] private double startCorrectionMs = -1050d;
    [SerializeField, Min(0.01f)] private float schedulingLeadTimeSeconds = 0.2f;

    private double songStartDspTime;
    private bool isTestPlaying;

    public event Action<double> TestMsChanged;
    public event Action<double> BpmChanged;
    public event Action<bool> TestPlaybackChanged;

    public AudioSource AudioSource => audioSource;
    public bool IsTestPlaying => isTestPlaying;
    public double TestMs => testMs;
    public double Bpm => bpm;
    public double StartCorrectionMs => startCorrectionMs;
    public double CorrectedTestMs => AudioMs + StartCorrectionMs;
    public double AudioDurationMs
    {
        get
        {
            AudioClip clip = audioSource != null ? audioSource.clip : null;
            return clip == null || clip.frequency <= 0
                ? 0d
                : clip.samples / (double)clip.frequency * 1000d;
        }
    }

    public double AudioMs
    {
        get
        {
            if (!IsTestPlaying)
            {
                return TestMs;
            }

            double elapsedMs =
                (AudioSettings.dspTime - songStartDspTime) * 1000d;
            return Math.Max(0d, Math.Min(elapsedMs, AudioDurationMs));
        }
    }

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                "ChartCore could not find an AudioSource on Core System.",
                this);
        }
        else
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
            ResetAudioPosition();
        }

        testMs = NormalizeTestMs(testMs);
        bpm = NormalizeBpm(bpm);
        startCorrectionMs = NormalizeCorrectionMs(startCorrectionMs);
    }

    private void Update()
    {
        if (!IsTestPlaying)
        {
            return;
        }

        double currentAudioMs = AudioMs;
        SetTestMs(currentAudioMs);

        if (currentAudioMs < AudioDurationMs)
        {
            return;
        }

        audioSource.Stop();
        isTestPlaying = false;
        TestPlaybackChanged?.Invoke(false);
    }

    private void OnValidate()
    {
        testMs = NormalizeTestMs(testMs);
        bpm = NormalizeBpm(bpm);
        startCorrectionMs = NormalizeCorrectionMs(startCorrectionMs);
    }

    public void SetTestMs(double value)
    {
        double normalizedValue = NormalizeTestMs(value);

        if (testMs.Equals(normalizedValue))
        {
            return;
        }

        testMs = normalizedValue;
        TestMsChanged?.Invoke(testMs);
    }

    public void SetTestMs(string input)
    {
        if (!TryParseNumber(input, out double value))
        {
            Debug.LogWarning($"Invalid test MS value: {input}", this);
            return;
        }

        SetTestMs(value);
    }

    public void ToggleTestPlay()
    {
        if (IsTestPlaying)
        {
            StopTestPlay();
        }
        else
        {
            StartTestPlay();
        }
    }

    public void StartTestPlay()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogWarning("No audio clip is assigned for test playback.", this);
            return;
        }

        audioSource.Stop();
        ResetAudioPosition();
        SetTestMs(0d);
        songStartDspTime = AudioSettings.dspTime + schedulingLeadTimeSeconds;
        isTestPlaying = true;
        audioSource.PlayScheduled(songStartDspTime);
        TestPlaybackChanged?.Invoke(true);
    }

    public void StopTestPlay()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.Stop();
        ResetAudioPosition();
        isTestPlaying = false;
        SetTestMs(0d);
        TestPlaybackChanged?.Invoke(false);
    }

    public void SetBpm(double value)
    {
        if (!IsFinite(value) || value <= 0d)
        {
            Debug.LogWarning($"BPM must be greater than zero: {value}", this);
            return;
        }

        if (bpm.Equals(value))
        {
            return;
        }

        bpm = value;
        BpmChanged?.Invoke(bpm);
    }

    public void SetBpm(string input)
    {
        if (!TryParseNumber(input, out double value))
        {
            Debug.LogWarning($"Invalid BPM value: {input}", this);
            return;
        }

        SetBpm(value);
    }

    public void SetStartCorrectionMs(double value)
    {
        startCorrectionMs = NormalizeCorrectionMs(value);
    }

    public void SetStartCorrectionMs(string input)
    {
        if (!TryParseNumber(input, out double value))
        {
            Debug.LogWarning($"Invalid start correction MS: {input}", this);
            return;
        }

        SetStartCorrectionMs(value);
    }

    private static double NormalizeTestMs(double value)
    {
        return IsFinite(value) ? Math.Max(0d, value) : 0d;
    }

    private void ResetAudioPosition()
    {
        AudioClip clip = audioSource != null ? audioSource.clip : null;

        if (clip != null && clip.samples > 0)
        {
            audioSource.timeSamples = 0;
        }
    }

    private static double NormalizeBpm(double value)
    {
        return IsFinite(value) && value > 0d ? value : 120d;
    }

    private static double NormalizeCorrectionMs(double value)
    {
        return IsFinite(value) ? value : 0d;
    }

    private static bool TryParseNumber(string input, out double value)
    {
        return double.TryParse(
                   input,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value) ||
               double.TryParse(input, out value);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
