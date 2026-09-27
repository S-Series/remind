using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Explicit scene and assembly boundary for the two Windows products.</summary>
public static class RuntimeProductBuilds
{
    [MenuItem("REmind/Build Game")]
    public static void BuildGame()
    {
        MusicCatalogBuilder.Rebuild();
        Build("Game", "ReMind.exe", "REMIND_GAME",
            "Assets/Scenes/Home.unity",
            "Assets/Scenes/Story.unity",
            "Assets/Scenes/MusicSelect.unity",
            "Assets/Scenes/Music.unity",
            "Assets/Scenes/Character.unity",
            "Assets/Scenes/ReMind.unity",
            "Assets/Scenes/Option.unity",
            "Assets/Scenes/Settings.unity",
            "Assets/Scenes/Game.unity");
    }

    [MenuItem("REmind/Build ChartMaker")]
    public static void BuildChartMaker()
    {
        Build("ChartMaker", "ReMind ChartMaker.exe",
            "REMIND_CHARTMAKER", "Assets/Scenes/ChartMaker.unity");
    }

    private static void Build(string product, string executable,
        string define, params string[] scenes)
    {
        string outputDirectory = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Logs", "ProductBuilds", product);
        Directory.CreateDirectory(outputDirectory);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDirectory, executable),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
            extraScriptingDefines = new[] { define }
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(product + " build failed: " +
                report.summary.result + " (" + report.summary.totalErrors +
                " errors).");
        Debug.Log(product + " build: " + options.locationPathName);
    }
}
