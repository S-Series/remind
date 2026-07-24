using System;
using System.Collections.Generic;

namespace REmind.Gameplay.Song.Data
{
    public sealed class SongData
    {
        // 식별자
        public string SongId { get; }
        public int SongSeasen { get; }
        // 음악 정보
        public string Title { get; }
        public string Artist { get; }
        // BPM 정보
        public string DisplayBpm { get; }
        public double MaxBpm { get; }
        public double CommonBpm { get; }
        public double MinBpm { get; }
        // 미리듣기 정보
        public double PreviewStartMs { get; }
        public double PreviewDurationMs { get; }
        // 시스템 정보
        public SongHiddenType hiddenType { get; }
        public bool isNeedBuy { get; }
    }

    public enum SongHiddenType
    {
        NotHidden = 1,
        VisibleHidden = 2,
        InvisibleHidden = 3,
    }
}   
