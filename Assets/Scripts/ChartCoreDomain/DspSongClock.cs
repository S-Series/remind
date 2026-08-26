using System;

namespace REmind.Charting
{
    /// <summary>
    /// 변경하지 않는 DSP 기준점과 SongTimeMs 사이의 절대 변환입니다.
    /// BPM과 카메라 상태는 이 시계에 관여하지 않습니다.
    /// </summary>
    public readonly struct DspSongClock
    {
        public DspSongClock(double originDspTime, double originSongTimeMs)
        {
            if (!IsFinite(originDspTime))
            {
                throw new ArgumentOutOfRangeException(nameof(originDspTime));
            }

            if (!IsFinite(originSongTimeMs))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(originSongTimeMs));
            }

            OriginDspTime = originDspTime;
            OriginSongTimeMs = originSongTimeMs;
        }

        public double OriginDspTime { get; }
        public double OriginSongTimeMs { get; }

        public double SongTimeMsAt(double dspTime)
        {
            if (!IsFinite(dspTime))
            {
                throw new ArgumentOutOfRangeException(nameof(dspTime));
            }

            return OriginSongTimeMs +
                (dspTime - OriginDspTime) * 1000d;
        }

        public double DspTimeAt(double songTimeMs)
        {
            if (!IsFinite(songTimeMs))
            {
                throw new ArgumentOutOfRangeException(nameof(songTimeMs));
            }

            return OriginDspTime +
                (songTimeMs - OriginSongTimeMs) / 1000d;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
