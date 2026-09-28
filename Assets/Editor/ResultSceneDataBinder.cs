using System;
using REmind.Gameplay.Demo;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Assigns Result presenter references without rebuilding authored UI.</summary>
public static class ResultSceneDataBinder
{
    private const string ScenePath = "Assets/Scenes/Result.unity";

    [MenuItem("REmind/Bind Result Scene Data")]
    public static void Bind()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Transform layout = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                if (!canvas) continue;
                layout = canvas.transform.Find("ResultLayout");
                if (!layout && canvas.transform.Find("02_SongInformation"))
                    layout = canvas.transform;
                if (layout) break;
            }
            if (!layout) throw new InvalidOperationException("ResultLayout was not found.");
            ResultScenePresenter presenter = layout.GetComponent<ResultScenePresenter>() ??
                layout.gameObject.AddComponent<ResultScenePresenter>();
            var serialized = new SerializedObject(presenter);
            var catalog = AssetDatabase.LoadAssetAtPath<MusicCatalog>(
                "Assets/Data/Music/MusicCatalog.asset");
            if (!catalog) throw new InvalidOperationException("MusicCatalog is missing.");
            serialized.FindProperty("catalog").objectReferenceValue = catalog;

            Set<TMP_Text>(serialized, layout, "songTitle", "02_SongInformation/Song_Title");
            Set<TMP_Text>(serialized, layout, "songArtist", "02_SongInformation/Song_Artist");
            Set<Image>(serialized, layout, "jacket", "02_SongInformation/Jacket/Temp_Jacket_1x1_SpriteSlot");
            SetArray<TMP_Text>(serialized, layout, "difficultyLabels", new[]
            {
                "02_SongInformation/Difficulty/Difficulty_EASY",
                "02_SongInformation/Difficulty/Difficulty_NORMAL",
                "02_SongInformation/Difficulty/Difficulty_HARD",
                "02_SongInformation/Difficulty/Difficulty_CHAOS"
            });
            Set<Image>(serialized, layout, "selectedDifficulty", "02_SongInformation/Difficulty/Difficulty_Selected");
            Set<TMP_Text>(serialized, layout, "scoreValue", "03_Score/Score_Value");
            Set<TMP_Text>(serialized, layout, "rankValue", "04_Rank/Rank_Value");
            Set<TMP_Text>(serialized, layout, "rankCaption", "04_Rank/Rank_Caption");
            SetArray<TMP_Text>(serialized, layout, "judgementCounts", GradePaths("Count"));
            SetArray<TMP_Text>(serialized, layout, "judgementPercents", GradePaths("Percent"));
            Set<TMP_Text>(serialized, layout, "comboValue", "06_Performance/Combo_Value");
            Set<TMP_Text>(serialized, layout, "accuracyValue", "06_Performance/Accuracy_Value");
            Set<Image>(serialized, layout, "accuracyFill", "06_Performance/Accuracy_Fill");
            Set<Button>(serialized, layout, "retryButton", "11_Actions/RETRY_Button");
            Set<GameObject>(serialized, layout, "newRecordLabel", "03_Score/New_Record");
            Set<GameObject>(serialized, layout, "newRecordUnderline", "03_Score/Record_Underline");
            Set<GameObject>(serialized, layout, "rewardSection", "09_Rewards");
            Set<TMP_Text>(serialized, layout, "rewardLabel", "09_Rewards/Reward_Label");
            Set<TMP_Text>(serialized, layout, "fragmentCount", "09_Rewards/Crystal/Count");
            Set<TMP_Text>(serialized, layout, "fragmentName", "09_Rewards/Crystal/Name");
            Set<GameObject>(serialized, layout, "otherReward", "09_Rewards/Butterfly");
            Set<GameObject>(serialized, layout, "memorySection", "07_Memory");
            Set<TMP_Text>(serialized, layout, "memoryTitle", "07_Memory/Memory_Title");
            Set<TMP_Text>(serialized, layout, "memorySubtitle", "07_Memory/Memory_Subtitle");
            Set<TMP_Text>(serialized, layout, "memoryQuote", "07_Memory/Memory_Quote");
            Set<GameObject>(serialized, layout, "rightQuoteSection", "08_RightQuote");
            Set<GameObject>(serialized, layout, "songMemo", "02_SongInformation/Song_Memo");
            Set<GameObject>(serialized, layout, "songQuote", "02_SongInformation/Song_Quote");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save Result scene.");
            Debug.Log("Result scene data references bound.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static string[] GradePaths(string suffix)
    {
        string[] names = { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" };
        var paths = new string[names.Length];
        for (int index = 0; index < names.Length; index++)
            paths[index] = "05_Judgements/" + names[index] + "_" + suffix;
        return paths;
    }

    private static void Set<T>(SerializedObject owner, Transform layout, string field,
        string path) where T : UnityEngine.Object
    {
        Transform found = layout.Find(path);
        if (!found) throw new InvalidOperationException("Missing Result UI: " + path);
        T value = typeof(T) == typeof(GameObject)
            ? found.gameObject as T : found.GetComponent(typeof(T)) as T;
        if (!value) throw new InvalidOperationException("Missing component at " + path);
        owner.FindProperty(field).objectReferenceValue = value;
    }

    private static void SetArray<T>(SerializedObject owner, Transform layout,
        string field, string[] paths) where T : UnityEngine.Object
    {
        SerializedProperty array = owner.FindProperty(field);
        array.arraySize = paths.Length;
        for (int index = 0; index < paths.Length; index++)
        {
            Transform found = layout.Find(paths[index]);
            T value = found ? found.GetComponent(typeof(T)) as T : null;
            if (!value) throw new InvalidOperationException("Missing Result UI: " + paths[index]);
            array.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }
    }
}
