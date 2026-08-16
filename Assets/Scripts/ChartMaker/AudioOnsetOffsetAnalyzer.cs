using System;
using System.Collections.Generic;
using REmind.Data;
using UnityEngine;

public static class AudioOnsetOffsetAnalyzer
{
    private const double MillisecondsPerMinute = 60000d;
    private const double BeatsPerMeasure = 4d;

    public readonly struct Settings
    {
        public Settings(
            int maximumScanDurationSeconds,
            int fftSize,
            int hopSize,
            double peakThresholdMadMultiplier,
            double minimumOnsetDistanceMs,
            double maximumMatchDistanceMs)
        {
            MaximumScanDurationSeconds = maximumScanDurationSeconds;
            FftSize = fftSize;
            HopSize = hopSize;
            PeakThresholdMadMultiplier = peakThresholdMadMultiplier;
            MinimumOnsetDistanceMs = minimumOnsetDistanceMs;
            MaximumMatchDistanceMs = maximumMatchDistanceMs;
        }

        public int MaximumScanDurationSeconds { get; }
        public int FftSize { get; }
        public int HopSize { get; }
        public double PeakThresholdMadMultiplier { get; }
        public double MinimumOnsetDistanceMs { get; }
        public double MaximumMatchDistanceMs { get; }
    }

    public readonly struct Result
    {
        public Result(
            double audioOffsetMs,
            double chartCorrectionMs,
            int detectedOnsetCount,
            int matchedOnsetCount)
        {
            AudioOffsetMs = audioOffsetMs;
            ChartCorrectionMs = chartCorrectionMs;
            DetectedOnsetCount = detectedOnsetCount;
            MatchedOnsetCount = matchedOnsetCount;
        }

        public double AudioOffsetMs { get; }
        public double ChartCorrectionMs { get; }
        public int DetectedOnsetCount { get; }
        public int MatchedOnsetCount { get; }
    }

    private readonly struct OffsetMatch
    {
        public OffsetMatch(
            List<double> offsetsMs,
            int firstChartIndex,
            int firstAudioIndex,
            int lastChartIndex,
            int lastAudioIndex)
        {
            OffsetsMs = offsetsMs;
            MedianOffsetMs = Median(offsetsMs);
            MedianErrorMs = MedianAbsoluteDeviation(
                offsetsMs,
                MedianOffsetMs);
            FirstChartIndex = firstChartIndex;
            FirstAudioIndex = firstAudioIndex;
            LastChartIndex = lastChartIndex;
            LastAudioIndex = lastAudioIndex;
        }

        public List<double> OffsetsMs { get; }
        public double MedianOffsetMs { get; }
        public double MedianErrorMs { get; }
        public int FirstChartIndex { get; }
        public int FirstAudioIndex { get; }
        public int LastChartIndex { get; }
        public int LastAudioIndex { get; }
        public int Count => OffsetsMs?.Count ?? 0;
        public bool IsValid => Count > 0;
    }

    public static bool TryAnalyze(
        AudioClip clip,
        IReadOnlyList<ChartHolder> chartHolders,
        double bpm,
        Settings settings,
        out Result result)
    {
        result = default;

        if (!TryGetMonoSamples(
                clip,
                settings.MaximumScanDurationSeconds,
                out float[] mono,
                out int sampleRate))
        {
            return false;
        }

        int fftSize = NormalizeFftSize(settings.FftSize);
        int hopSize = Math.Max(1, settings.HopSize);

        if (mono.Length < fftSize ||
            !TryDetectOnsets(
                mono,
                sampleRate,
                fftSize,
                hopSize,
                settings,
                out List<double> audioOnsetsMs) ||
            audioOnsetsMs.Count == 0)
        {
            return false;
        }

        List<double> chartOnsetsMs = GetChartOnsetsMs(
            chartHolders,
            bpm,
            settings.MaximumScanDurationSeconds * 1000d);

        if (chartOnsetsMs.Count == 0)
        {
            double firstAudioOnsetMs = audioOnsetsMs[0];
            result = new Result(
                firstAudioOnsetMs,
                -firstAudioOnsetMs,
                audioOnsetsMs.Count,
                0);
            return true;
        }

        List<double> offsetsMs = FindBestOffsetMatches(
            audioOnsetsMs,
            chartOnsetsMs,
            settings.MaximumMatchDistanceMs);

        if (offsetsMs.Count == 0)
        {
            return false;
        }

        double audioOffsetMs = Median(offsetsMs);
        result = new Result(
            audioOffsetMs,
            -audioOffsetMs,
            audioOnsetsMs.Count,
            offsetsMs.Count);
        return true;
    }

    public static bool TryAnalyzeFirstOnset(
        AudioClip clip,
        Settings settings,
        out Result result)
    {
        return TryAnalyze(
            clip,
            null,
            1d,
            settings,
            out result);
    }

    public static bool TryCreateSpectrogramTexture(
        AudioClip clip,
        Settings settings,
        int width,
        int height,
        out Texture2D texture)
    {
        texture = null;

        if (!TryGetMonoSamples(
                clip,
                settings.MaximumScanDurationSeconds,
                out float[] mono,
                out int sampleRate))
        {
            return false;
        }

        int fftSize = NormalizeFftSize(settings.FftSize);
        int hopSize = Math.Max(1, settings.HopSize);

        if (mono.Length < fftSize || width <= 0 || height <= 0)
        {
            return false;
        }

        int frameCount = 1 + (mono.Length - fftSize) / hopSize;
        double[] window = CreateHannWindow(fftSize);
        double[] real = new double[fftSize];
        double[] imaginary = new double[fftSize];
        double[] magnitudes = new double[width * height];
        double maximumMagnitude = 1e-9d;

        for (int x = 0; x < width; x++)
        {
            int frame = width == 1
                ? 0
                : Mathf.RoundToInt(x * (frameCount - 1f) / (width - 1f));
            int sampleStart = frame * hopSize;

            for (int i = 0; i < fftSize; i++)
            {
                real[i] = mono[sampleStart + i] * window[i];
                imaginary[i] = 0d;
            }

            FastFourierTransform(real, imaginary);

            for (int y = 0; y < height; y++)
            {
                int bin = 1 + Mathf.RoundToInt(
                    y * (fftSize / 2f - 2f) / Mathf.Max(1f, height - 1f));
                double magnitude = Math.Log10(
                    1d +
                    Math.Sqrt(
                        real[bin] * real[bin] +
                        imaginary[bin] * imaginary[bin]));
                int index = y * width + x;
                magnitudes[index] = magnitude;
                maximumMagnitude = Math.Max(maximumMagnitude, magnitude);
            }
        }

        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float value = (float)(magnitudes[index] / maximumMagnitude);
                pixels[index] = EvaluateSpectrumColor(value);
            }
        }

        if (TryDetectOnsets(
                mono,
                sampleRate,
                fftSize,
                hopSize,
                settings,
                out List<double> onsetsMs))
        {
            double durationMs = mono.Length * 1000d / sampleRate;

            for (int i = 0; i < onsetsMs.Count; i++)
            {
                int x = Mathf.RoundToInt(
                    (float)(onsetsMs[i] / durationMs * (width - 1)));
                DrawVerticalLine(pixels, width, height, x, new Color32(41, 214, 230, 210));
            }
        }

        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return true;
    }

    private static bool TryGetMonoSamples(
        AudioClip clip,
        int maximumScanDurationSeconds,
        out float[] mono,
        out int sampleRate)
    {
        mono = Array.Empty<float>();
        sampleRate = 0;

        if (!clip ||
            clip.loadState != AudioDataLoadState.Loaded ||
            clip.samples <= 0 ||
            clip.channels <= 0 ||
            clip.frequency <= 0)
        {
            return false;
        }

        sampleRate = clip.frequency;
        int frameCount = (int)Math.Min(
            clip.samples,
            (long)clip.frequency * Math.Max(1, maximumScanDurationSeconds));
        long sampleValueCount = (long)frameCount * clip.channels;

        if (frameCount <= 0 || sampleValueCount > int.MaxValue)
        {
            return false;
        }

        float[] interleaved = new float[(int)sampleValueCount];

        try
        {
            if (!clip.GetData(interleaved, 0))
            {
                return false;
            }
        }
        catch (UnityException)
        {
            return false;
        }

        mono = new float[frameCount];

        for (int frame = 0; frame < frameCount; frame++)
        {
            int sampleStart = frame * clip.channels;
            double sum = 0d;

            for (int channel = 0; channel < clip.channels; channel++)
            {
                sum += interleaved[sampleStart + channel];
            }

            mono[frame] = (float)(sum / clip.channels);
        }

        return true;
    }

    private static Color32 EvaluateSpectrumColor(float value)
    {
        value = Mathf.Clamp01(value);
        Color low = new Color(0.08f, 0.09f, 0.12f, 1f);
        Color mid = new Color(0.08f, 0.55f, 0.67f, 1f);
        Color high = new Color(0.94f, 0.95f, 0.72f, 1f);

        Color color = value < 0.65f
            ? Color.Lerp(low, mid, value / 0.65f)
            : Color.Lerp(mid, high, (value - 0.65f) / 0.35f);

        return color;
    }

    private static void DrawVerticalLine(
        Color32[] pixels,
        int width,
        int height,
        int x,
        Color32 color)
    {
        if (x < 0 || x >= width)
        {
            return;
        }

        for (int y = 0; y < height; y++)
        {
            int index = y * width + x;
            pixels[index] = color;
        }
    }

    private static bool TryDetectOnsets(
        float[] mono,
        int sampleRate,
        int fftSize,
        int hopSize,
        Settings settings,
        out List<double> onsetsMs)
    {
        onsetsMs = new List<double>();
        int frameCount = 1 + (mono.Length - fftSize) / hopSize;

        if (frameCount < 3)
        {
            return false;
        }

        double[] window = CreateHannWindow(fftSize);
        double[] previousMagnitude = new double[fftSize / 2];
        double[] flux = new double[frameCount];
        double[] real = new double[fftSize];
        double[] imaginary = new double[fftSize];

        for (int frame = 0; frame < frameCount; frame++)
        {
            int sampleStart = frame * hopSize;

            for (int i = 0; i < fftSize; i++)
            {
                real[i] = mono[sampleStart + i] * window[i];
                imaginary[i] = 0d;
            }

            FastFourierTransform(real, imaginary);

            double positiveDifferenceSum = 0d;

            for (int bin = 1; bin < previousMagnitude.Length; bin++)
            {
                double magnitude = Math.Sqrt(
                    real[bin] * real[bin] +
                    imaginary[bin] * imaginary[bin]);
                double difference = magnitude - previousMagnitude[bin];

                if (difference > 0d)
                {
                    positiveDifferenceSum += difference;
                }

                previousMagnitude[bin] = magnitude;
            }

            flux[frame] = positiveDifferenceSum;
        }

        double median = Median(flux);
        double mad = MedianAbsoluteDeviation(flux, median);
        double threshold = median + Math.Max(
            1e-9d,
            mad * Math.Max(1d, settings.PeakThresholdMadMultiplier));
        double minimumDistanceMs = Math.Max(0d, settings.MinimumOnsetDistanceMs);
        double lastOnsetMs = double.NegativeInfinity;

        for (int frame = 1; frame < frameCount - 1; frame++)
        {
            if (flux[frame] <= threshold ||
                flux[frame] < flux[frame - 1] ||
                flux[frame] < flux[frame + 1])
            {
                continue;
            }

            double onsetMs =
                (frame * hopSize + fftSize * 0.5d) * 1000d / sampleRate;

            if (onsetMs - lastOnsetMs < minimumDistanceMs)
            {
                continue;
            }

            onsetsMs.Add(onsetMs);
            lastOnsetMs = onsetMs;
        }

        return onsetsMs.Count > 0;
    }

    private static List<double> GetChartOnsetsMs(
        IReadOnlyList<ChartHolder> chartHolders,
        double bpm,
        double maximumMs)
    {
        List<double> result = new List<double>();

        if (chartHolders == null || !IsFinite(bpm) || bpm <= 0d)
        {
            return result;
        }

        int previousPosition = -1;

        for (int i = 0; i < chartHolders.Count; i++)
        {
            ChartHolder holder = chartHolders[i];

            if (holder == null)
            {
                continue;
            }

            holder.EnsureStorage();

            if (!HasPlayableNote(holder) ||
                holder.AbsoluteChartPosition == previousPosition)
            {
                continue;
            }

            double chartMs =
                holder.AbsoluteChartPosition *
                BeatsPerMeasure *
                MillisecondsPerMinute /
                ChartHolder.PositionUnitsPerMeasure /
                bpm;

            if (chartMs <= maximumMs)
            {
                result.Add(chartMs);
            }

            previousPosition = holder.AbsoluteChartPosition;
        }

        result.Sort();
        return result;
    }

    private static bool HasPlayableNote(ChartHolder holder)
    {
        for (int i = 0; i < ChartHolder.TotalLineCount; i++)
        {
            if (holder.noteTypes[i] != NoteType.Unknown)
            {
                return true;
            }
        }

        for (int i = 0; i < ChartHolder.AirNoteCount; i++)
        {
            if (holder.airNoteValues[i] > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 큰 시작 지연도 찾을 수 있도록 모든 전역 이동 후보를 평가한 뒤,
    /// 시간 순서를 유지하는 일대일 onset 매칭 중 가장 안정적인 묶음을 반환합니다.
    /// </summary>
    private static List<double> FindBestOffsetMatches(
        IReadOnlyList<double> audioOnsetsMs,
        IReadOnlyList<double> chartOnsetsMs,
        double maximumMatchDistanceMs)
    {
        if (audioOnsetsMs == null || chartOnsetsMs == null ||
            audioOnsetsMs.Count == 0 || chartOnsetsMs.Count == 0)
        {
            return new List<double>();
        }

        double maximumDistance = Math.Max(0d, maximumMatchDistanceMs);
        double candidateBinSize = Math.Max(1d, maximumDistance * 0.25d);
        HashSet<long> testedCandidateBins = new HashSet<long>();
        OffsetMatch bestMatch = default;

        for (int i = 0; i < chartOnsetsMs.Count; i++)
        {
            for (int j = 0; j < audioOnsetsMs.Count; j++)
            {
                double candidateOffsetMs =
                    audioOnsetsMs[j] - chartOnsetsMs[i];
                long candidateBin = (long)Math.Round(
                    candidateOffsetMs / candidateBinSize,
                    MidpointRounding.AwayFromZero);

                if (!testedCandidateBins.Add(candidateBin))
                {
                    continue;
                }

                OffsetMatch candidateMatch = EvaluateOffsetCandidate(
                    audioOnsetsMs,
                    chartOnsetsMs,
                    candidateOffsetMs,
                    maximumDistance);

                if (!candidateMatch.IsValid)
                {
                    continue;
                }

                // Re-evaluate around the robust center of the first pass. This
                // removes the bias from whichever onset pair created the bin.
                OffsetMatch refinedMatch = EvaluateOffsetCandidate(
                    audioOnsetsMs,
                    chartOnsetsMs,
                    candidateMatch.MedianOffsetMs,
                    maximumDistance);

                if (IsBetterOffsetMatch(refinedMatch, candidateMatch))
                {
                    candidateMatch = refinedMatch;
                }

                if (IsBetterOffsetMatch(candidateMatch, bestMatch))
                {
                    bestMatch = candidateMatch;
                }
            }
        }

        int requiredMatches = Math.Min(3, chartOnsetsMs.Count);
        return bestMatch.Count >= requiredMatches
            ? bestMatch.OffsetsMs
            : new List<double>();
    }

    private static OffsetMatch EvaluateOffsetCandidate(
        IReadOnlyList<double> audioOnsetsMs,
        IReadOnlyList<double> chartOnsetsMs,
        double candidateOffsetMs,
        double maximumDistanceMs)
    {
        List<double> offsetsMs = new List<double>();
        int audioIndex = 0;
        int firstChartIndex = -1;
        int firstAudioIndex = -1;
        int lastChartIndex = -1;
        int lastAudioIndex = -1;

        for (int chartIndex = 0;
             chartIndex < chartOnsetsMs.Count && audioIndex < audioOnsetsMs.Count;
             chartIndex++)
        {
            double shiftedChartMs =
                chartOnsetsMs[chartIndex] + candidateOffsetMs;
            double minimumAudioMs = shiftedChartMs - maximumDistanceMs;
            double maximumAudioMs = shiftedChartMs + maximumDistanceMs;

            while (audioIndex < audioOnsetsMs.Count &&
                   audioOnsetsMs[audioIndex] < minimumAudioMs)
            {
                audioIndex++;
            }

            if (audioIndex >= audioOnsetsMs.Count)
            {
                break;
            }

            if (audioOnsetsMs[audioIndex] > maximumAudioMs)
            {
                continue;
            }

            if (firstChartIndex < 0)
            {
                firstChartIndex = chartIndex;
                firstAudioIndex = audioIndex;
            }

            offsetsMs.Add(
                audioOnsetsMs[audioIndex] - chartOnsetsMs[chartIndex]);
            lastChartIndex = chartIndex;
            lastAudioIndex = audioIndex;
            audioIndex++;
        }

        return offsetsMs.Count == 0
            ? default
            : new OffsetMatch(
                offsetsMs,
                firstChartIndex,
                firstAudioIndex,
                lastChartIndex,
                lastAudioIndex);
    }

    private static bool IsBetterOffsetMatch(
        OffsetMatch candidate,
        OffsetMatch current)
    {
        if (!candidate.IsValid)
        {
            return false;
        }

        if (!current.IsValid)
        {
            return true;
        }

        if (candidate.Count != current.Count)
        {
            return candidate.Count > current.Count;
        }

        const double comparisonToleranceMs = 0.001d;
        double errorDifference =
            candidate.MedianErrorMs - current.MedianErrorMs;

        if (Math.Abs(errorDifference) > comparisonToleranceMs)
        {
            return errorDifference < 0d;
        }

        int candidateAnchor =
            candidate.FirstChartIndex + candidate.FirstAudioIndex;
        int currentAnchor = current.FirstChartIndex + current.FirstAudioIndex;

        if (candidateAnchor != currentAnchor)
        {
            return candidateAnchor < currentAnchor;
        }

        int candidateSpan =
            candidate.LastChartIndex - candidate.FirstChartIndex +
            candidate.LastAudioIndex - candidate.FirstAudioIndex;
        int currentSpan =
            current.LastChartIndex - current.FirstChartIndex +
            current.LastAudioIndex - current.FirstAudioIndex;

        if (candidateSpan != currentSpan)
        {
            return candidateSpan > currentSpan;
        }

        return Math.Abs(candidate.MedianOffsetMs) <
            Math.Abs(current.MedianOffsetMs);
    }

    private static int NormalizeFftSize(int requestedSize)
    {
        int size = Mathf.Max(256, requestedSize);
        int powerOfTwo = 1;

        while (powerOfTwo < size)
        {
            powerOfTwo <<= 1;
        }

        return powerOfTwo;
    }

    private static double[] CreateHannWindow(int size)
    {
        double[] window = new double[size];

        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5d - 0.5d * Math.Cos(2d * Math.PI * i / (size - 1));
        }

        return window;
    }

    private static void FastFourierTransform(double[] real, double[] imaginary)
    {
        int n = real.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;

            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;

            if (i >= j)
            {
                continue;
            }

            (real[i], real[j]) = (real[j], real[i]);
            (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
        }

        for (int length = 2; length <= n; length <<= 1)
        {
            double angle = -2d * Math.PI / length;
            double wLengthReal = Math.Cos(angle);
            double wLengthImaginary = Math.Sin(angle);

            for (int i = 0; i < n; i += length)
            {
                double wReal = 1d;
                double wImaginary = 0d;

                for (int j = 0; j < length / 2; j++)
                {
                    int evenIndex = i + j;
                    int oddIndex = evenIndex + length / 2;
                    double oddReal =
                        real[oddIndex] * wReal -
                        imaginary[oddIndex] * wImaginary;
                    double oddImaginary =
                        real[oddIndex] * wImaginary +
                        imaginary[oddIndex] * wReal;

                    real[oddIndex] = real[evenIndex] - oddReal;
                    imaginary[oddIndex] = imaginary[evenIndex] - oddImaginary;
                    real[evenIndex] += oddReal;
                    imaginary[evenIndex] += oddImaginary;

                    double nextWReal =
                        wReal * wLengthReal -
                        wImaginary * wLengthImaginary;
                    wImaginary =
                        wReal * wLengthImaginary +
                        wImaginary * wLengthReal;
                    wReal = nextWReal;
                }
            }
        }
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values == null || values.Count == 0)
        {
            return 0d;
        }

        double[] sorted = new double[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            sorted[i] = values[i];
        }

        Array.Sort(sorted);
        int middle = sorted.Length / 2;

        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) * 0.5d
            : sorted[middle];
    }

    private static double MedianAbsoluteDeviation(
        IReadOnlyList<double> values,
        double median)
    {
        double[] deviations = new double[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            deviations[i] = Math.Abs(values[i] - median);
        }

        return Median(deviations);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
