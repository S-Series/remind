using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

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
    private double playbackStartMs;
    private bool isTestPlaying;
    private AudioClip loadedAudioClip;

    public event Action<double> TestMsChanged;
    public event Action<double> BpmChanged;
    public event Action<double> StartCorrectionMsChanged;
    public event Action<bool> TestPlaybackChanged;
    public event Action<AudioClip> AudioClipChanged;

    public AudioSource AudioSource => audioSource;
    public bool IsTestPlaying => isTestPlaying;
    public double TestMs => testMs;
    public double Bpm => bpm;
    public double StartCorrectionMs => startCorrectionMs;
    public string CurrentAudioFilePath { get; private set; }
    public bool IsAudioLoading { get; private set; }
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

            double elapsedMs = Math.Max(
                0d,
                (AudioSettings.dspTime - songStartDspTime) * 1000d);
            return Math.Max(
                0d,
                Math.Min(playbackStartMs + elapsedMs, AudioDurationMs));
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
                "ChartCore could not find an AudioSource on its GameObject.",
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

        CompleteTestPlayback(rewindTimeline: false);
    }

    private void OnValidate()
    {
        testMs = NormalizeTestMs(testMs);
        bpm = NormalizeBpm(bpm);
        startCorrectionMs = NormalizeCorrectionMs(startCorrectionMs);
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
        {
            // Scene objects are already being destroyed; only discard static data.
            ChartManager.ClearChart(false);
        }

        if (loadedAudioClip)
        {
            Destroy(loadedAudioClip);
        }

        base.OnDestroy();
    }

    /// <summary>선택한 로컬 음악 파일을 비동기로 읽어 테스트 AudioSource에 적용합니다.</summary>
    public bool LoadAudioFile(string filePath)
    {
        if (audioSource == null || IsAudioLoading)
        {
            return false;
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Invalid audio file path: {exception.Message}", this);
            return false;
        }

        if (!File.Exists(fullPath) ||
            !TryGetAudioType(fullPath, out AudioType audioType))
        {
            Debug.LogError(
                $"Select an existing WAV, MP3, OGG, AIF, or AIFF file: {fullPath}",
                this);
            return false;
        }

        StartCoroutine(LoadAudioFileRoutine(fullPath, audioType));
        return true;
    }

    /// <summary>편집 및 테스트 재생에서 공유하는 현재 타임라인 시간을 설정합니다.</summary>
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

    /// <summary>현재 상태에 따라 테스트 음악을 시작하거나 정지합니다.</summary>
    public void ToggleTestPlay()
    {
        if (IsTestPlaying)
        {
            EndTestPlay();
        }
        else
        {
            StartTestPlay();
        }
    }

    /// <summary>
    /// DSP 예약 재생을 시작하고 예약 시각을 기준으로 오디오 밀리초를 계산합니다.
    /// </summary>
    public void StartTestPlay()
    {
        StartTestPlay(0d);
    }

    /// <summary>지정한 음악 위치부터 DSP 예약 재생을 시작합니다.</summary>
    public void StartTestPlay(double startMs)
    {
        AudioClip clip = audioSource != null ? audioSource.clip : null;

        if (clip == null ||
            clip.samples <= 0 ||
            clip.frequency <= 0 ||
            clip.loadState == AudioDataLoadState.Failed ||
            IsAudioLoading)
        {
            Debug.LogWarning(
                "Test playback requires a loaded audio clip and no active audio load.",
                this);
            return;
        }

        audioSource.Stop();
        int startSample = (int)Math.Min(
            clip.samples - 1L,
            Math.Max(
                0L,
                (long)Math.Round(
                    NormalizeTestMs(startMs) * clip.frequency / 1000d)));
        audioSource.timeSamples = startSample;
        playbackStartMs = startSample * 1000d / clip.frequency;
        SetTestMs(playbackStartMs);
        songStartDspTime = AudioSettings.dspTime + schedulingLeadTimeSeconds;
        SetPlaybackState(true);
        audioSource.PlayScheduled(songStartDspTime);
    }

    /// <summary>테스트 재생을 끝내고 음악·타임라인·표시 위치를 시작점으로 되돌립니다.</summary>
    public void EndTestPlay()
    {
        CompleteTestPlayback(rewindTimeline: true);
    }

    /// <summary>채보 계산에 사용할 BPM을 설정합니다.</summary>
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

    /// <summary>음악 시작점과 채보 시작점 사이의 밀리초 보정값을 설정합니다.</summary>
    public void SetStartCorrectionMs(double value)
    {
        double normalizedValue = NormalizeCorrectionMs(value);

        if (startCorrectionMs.Equals(normalizedValue))
        {
            return;
        }

        startCorrectionMs = normalizedValue;
        StartCorrectionMsChanged?.Invoke(startCorrectionMs);
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

    private IEnumerator LoadAudioFileRoutine(
        string fullPath,
        AudioType audioType)
    {
        IsAudioLoading = true;
        string fileUri = new Uri(fullPath).AbsoluteUri;

        using UnityWebRequest request =
            UnityWebRequestMultimedia.GetAudioClip(fileUri, audioType);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"Failed to load music '{fullPath}': {request.error}",
                this);
            IsAudioLoading = false;
            yield break;
        }

        AudioClip newClip = DownloadHandlerAudioClip.GetContent(request);

        if (!newClip)
        {
            Debug.LogError($"The selected music could not be decoded: {fullPath}", this);
            IsAudioLoading = false;
            yield break;
        }

        EndTestPlay();
        AudioClip previousLoadedClip = loadedAudioClip;
        loadedAudioClip = newClip;
        loadedAudioClip.name = Path.GetFileNameWithoutExtension(fullPath);
        audioSource.clip = loadedAudioClip;
        CurrentAudioFilePath = fullPath;
        IsAudioLoading = false;
        AudioClipChanged?.Invoke(loadedAudioClip);

        if (previousLoadedClip)
        {
            Destroy(previousLoadedClip);
        }

        Debug.Log($"Music opened: {fullPath}", this);
    }

    private static bool TryGetAudioType(
        string filePath,
        out AudioType audioType)
    {
        audioType = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".wav" => AudioType.WAV,
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            ".aif" => AudioType.AIFF,
            ".aiff" => AudioType.AIFF,
            _ => AudioType.UNKNOWN
        };

        return audioType != AudioType.UNKNOWN;
    }

    private void CompleteTestPlayback(bool rewindTimeline)
    {
        double completedTimeMs = rewindTimeline
            ? 0d
            : Math.Min(TestMs, AudioDurationMs);

        if (audioSource)
        {
            audioSource.Stop();
            ResetAudioPosition();
        }

        SetTestMs(completedTimeMs);
        SetPlaybackState(false);
    }

    private void ResetAudioPosition()
    {
        playbackStartMs = 0d;
        AudioClip clip = audioSource != null ? audioSource.clip : null;

        if (clip != null && clip.samples > 0)
        {
            audioSource.timeSamples = 0;
        }
    }

    private void SetPlaybackState(bool playing)
    {
        if (isTestPlaying == playing)
        {
            return;
        }

        isTestPlaying = playing;
        TestPlaybackChanged?.Invoke(isTestPlaying);
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
