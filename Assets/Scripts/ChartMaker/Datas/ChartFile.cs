using System;

[Serializable]
public sealed class ChartFile
{
    public int FormatVersion { get; internal set; }
    public bool HasBaseBpm { get; internal set; }
    public double BaseBpm { get; internal set; }
    public bool HasMusicStartCorrectionMs { get; internal set; }
    public double MusicStartCorrectionMs { get; internal set; }
    public ChartHolder[] chartDatas = Array.Empty<ChartHolder>();
}
