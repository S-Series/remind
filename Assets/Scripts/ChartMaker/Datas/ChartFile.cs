using System;

[Serializable]
public sealed class ChartFile
{
    public int FormatVersion { get; internal set; }
    public bool HasBaseBpm { get; internal set; }
    public double BaseBpm { get; internal set; }
    public bool HasMusicStartCorrectionMs { get; internal set; }
    public double MusicStartCorrectionMs { get; internal set; }
    public string MusicId { get; internal set; }
    public string DifficultyId { get; internal set; }
    public string JacketFile { get; internal set; }
    public string EffectiveJacketFile => string.IsNullOrEmpty(JacketFile)
        ? DifficultyId + ".jpg" : JacketFile;
    public string GimmickId { get; internal set; }
    public string EffectRevision { get; internal set; }
    // Only older paired charts require an external Effect parameter file.
    public bool HasEffectParameterFile { get; internal set; }
    public ChartHolder[] chartDatas = Array.Empty<ChartHolder>();
}
