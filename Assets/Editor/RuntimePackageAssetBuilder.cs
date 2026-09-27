using System;
using System.IO;
using System.Text;
using REmind.Charting;
using UnityEditor;
using UnityEngine;

public static class RuntimePackageAssetBuilder
{
    private const string ISongData = "Assets/Data/Music/i/data.json";
    private const string SourceChart = "Assets/Chart/EffectGameplaySample.rd";
    private const string SourceParameters =
        "Assets/Chart/effect.effect_gameplay_sample.demo.json";
    private const string Output =
        "Assets/Chart/EffectGameplaySample.rmp.json";

    [MenuItem("REmind/Build Demo Runtime Package")]
    public static void BuildDemoPackage()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string package = ChartMakerRuntimePackageExporter.Export(
            File.ReadAllText(Path.Combine(root, SourceChart)),
            File.ReadAllText(Path.Combine(root, SourceParameters)));
        File.WriteAllText(Path.Combine(root, Output), package,
            new UTF8Encoding(false));
        AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceUpdate);
    }

    [MenuItem("REmind/Build Playable Sample Package")]
    public static void BuildPlayableSamplePackage()
    {
        BuildISongPackage();
    }

    [MenuItem("REmind/Build I Song Runtime Package")]
    public static void BuildISongPackage()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string dataPath = Path.Combine(root, ISongData);
        SongContent song = SongContentFileStore.Load(dataPath);
        var entry = song.FindChart("hard") ?? throw new FormatException(
            "The I song has no hard chart entry.");
        string chartPath = REmind.Charting.SongContentCodec.ResolveReference(
            dataPath, entry.ChartFile);
        ChartFile chart = ChartEffectFileStore.Load(chartPath, out _, out string chartText);
        SongContentFileStore.ValidateChart(chartPath, chart);
        string parameters = chart.HasEffectParameterFile
            ? ChartEffectFileStore.SerializeParameters(chart.chartDatas,
                new ChartEffectDocumentState.Metadata(chart.MusicId,
                    chart.DifficultyId, chart.GimmickId, chart.EffectRevision))
            : null;
        string package = ChartMakerRuntimePackageExporter.Export(
            chartText, parameters);
        string outputPath = Path.ChangeExtension(chartPath, ".rmp.json");
        File.WriteAllText(outputPath, package, new UTF8Encoding(false));
        string assetPath = outputPath.Substring(root.Length + 1).Replace('\\', '/');
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("I song runtime package exported: " + assetPath);
    }
}
