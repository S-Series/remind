using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Explicit scene and assembly boundary for the two Windows products.</summary>
public static class RuntimeProductBuilds
{
    private const string ProfileDirectory = "Assets/Settings/Build Profiles/";
    private static readonly string[] GameScenes =
    {
        "Assets/Scenes/Bootstrap.unity", "Assets/Scenes/Home.unity",
        "Assets/Scenes/Story.unity", "Assets/Scenes/MusicSelect.unity",
        "Assets/Scenes/Music.unity", "Assets/Scenes/Character.unity",
        "Assets/Scenes/ReMind.unity", "Assets/Scenes/Option.unity",
        "Assets/Scenes/Game.unity",
        "Assets/Scenes/Result.unity"
    };
    private static readonly string[] ChartMakerScenes =
    {
        "Assets/Scenes/ChartMaker.unity"
    };

    [MenuItem("REmind/Build Game")]
    public static void BuildGame()
    {
        MusicCatalogBuilder.Rebuild();
        Build("Game", "ReMind.exe", "Play", "REMIND_GAME", GameScenes);
    }

    [MenuItem("REmind/Build ChartMaker")]
    public static void BuildChartMaker()
    {
        Build("ChartMaker", "ReMind ChartMaker.exe", "Chart",
            "REMIND_CHARTMAKER", ChartMakerScenes);
    }

    [MenuItem("REmind/Configure Product Build Profiles")]
    public static void ConfigureProfiles()
    {
        Configure("Play", "REMIND_GAME", GameScenes);
        Configure("Chart", "REMIND_CHARTMAKER", ChartMakerScenes);
        AssetDatabase.SaveAssets();
        Debug.Log("Game and ChartMaker Build Profiles configured.");
    }

    private static void Configure(string profileName, string define,
        string[] scenes)
    {
        BuildProfile profile = LoadProfile(profileName);
        profile.overrideGlobalScenes = true;
        var entries = new EditorBuildSettingsScene[scenes.Length];
        for (int index = 0; index < scenes.Length; index++)
        {
            if (!File.Exists(scenes[index]))
                throw new FileNotFoundException("Build scene is missing.", scenes[index]);
            entries[index] = new EditorBuildSettingsScene(scenes[index], true);
        }
        profile.scenes = entries;
        profile.scriptingDefines = new[] { define };
        EditorUtility.SetDirty(profile);
    }

    private static BuildProfile LoadProfile(string name)
    {
        BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(
            ProfileDirectory + name + ".asset");
        if (!profile) throw new InvalidOperationException(
            "Product Build Profile is missing: " + name);
        return profile;
    }

    private static void Build(string product, string executable,
        string profileName, string define, string[] expectedScenes)
    {
        BuildProfile profile = LoadProfile(profileName);
        EditorBuildSettingsScene[] scenes = profile.GetScenesForBuild();
        if (!profile.overrideGlobalScenes ||
            profile.scriptingDefines == null ||
            Array.IndexOf(profile.scriptingDefines, define) < 0 ||
            scenes.Length != expectedScenes.Length)
            throw new InvalidOperationException(product +
                " Build Profile has incorrect scenes or scripting defines. " +
                "Run Configure Product Build Profiles.");
        for (int index = 0; index < scenes.Length; index++)
            if (!scenes[index].enabled || scenes[index].path != expectedScenes[index])
                throw new InvalidOperationException(product +
                    " Build Profile scene order differs at index " + index);
        string outputDirectory = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Logs", "ProductBuilds", product);
        Directory.CreateDirectory(outputDirectory);
        var options = new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = Path.Combine(outputDirectory, executable),
            options = BuildOptions.None
        };
        BuildProfile previousProfile = BuildProfile.GetActiveBuildProfile();
        BuildReport report;
        try { report = BuildPipeline.BuildPlayer(options); }
        finally
        {
            if (BuildProfile.GetActiveBuildProfile() != previousProfile)
                BuildProfile.SetActiveBuildProfile(previousProfile);
        }
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(product + " build failed: " +
                report.summary.result + " (" + report.summary.totalErrors +
                " errors).");
        Debug.Log(product + " build: " + options.locationPathName);
    }
}
