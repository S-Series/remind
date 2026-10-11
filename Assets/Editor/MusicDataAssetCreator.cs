using System;
using System.Globalization;
using System.IO;
using System.Text;
using REmind.Charting;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

public static class MusicDataAssetCreator
{
    private const string MenuPath = "Assets/Remind/Music Data";

    [MenuItem(MenuPath, false, 2000)]
    public static void CreateMusicData()
    {
        try
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Editor/MusicDataTemplate.json"))
                .Replace("__LOCAL_DATE__", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            SongContentCodec.Parse(json);
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0,
                ScriptableObject.CreateInstance<CreateMusicDataAction>(), "data.json", null, json);
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not create music data: " + exception.Message);
        }
    }

    private sealed class CreateMusicDataAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            try
            {
                string assetPath = pathName.Replace('\\', '/');
                string assetsRoot = Path.GetFullPath(Application.dataPath);
                string fullPath = Path.GetFullPath(Path.Combine(assetsRoot, "..", assetPath));
                if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    !AssetDatabase.IsValidFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/')))
                    throw new ArgumentException("Music data must be created inside Assets.");

                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                fullPath = Path.GetFullPath(Path.Combine(assetsRoot, "..", assetPath));
                using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(resourceFile);
                AssetDatabase.ImportAsset(assetPath);
                ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath));
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not create music data: " + exception.Message);
            }
        }
    }
}
