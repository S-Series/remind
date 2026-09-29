using System;
using System.Collections.Generic;
using System.IO;
using REmind.Gameplay.Demo;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the Result screen as editable uGUI objects at 1920 x 1080.</summary>
public static class ResultSceneLayoutBuilder
{
    private const string ScenePath = "Assets/Scenes/Result.unity";
    private static readonly Color Ivory = Hex("E9EDFF");
    private static readonly Color Muted = Hex("A6B5DD");
    private static readonly Color Blue = Hex("94ACFF");
    private static TMP_FontAsset font;
    private static Sprite background;
    private static Sprite jacket;
    private static Sprite compass;
    private static readonly Dictionary<Transform, Vector2> groupOrigins = new Dictionary<Transform, Vector2>();

    [MenuItem("REmind/Build Result Scene Layout")]
    public static void Build()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !scene.isLoaded;
        if (openedForBuild)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Canvas canvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas) break;
        }
        if (!canvas) throw new InvalidOperationException("Result scene has no Canvas.");
        canvas.gameObject.name = "ResultCanvas";
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        foreach (Transform child in canvas.transform)
            if (child.name == "HomeReferenceLayout" || child.name == "ResultLayout")
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
                break;
            }
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "System") UnityEngine.Object.DestroyImmediate(root);

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/NanumMyeongjo SDF.asset");
        background = LoadSprite("Assets/Art/Sprites/AI Generated/HomeBackground.png");
        jacket = LoadSprite("Assets/Art/UI/MusicSelectUI/StellarPromise_Jacket.png");
        compass = LoadSprite("Assets/Art/UI/MusicSelectUI/CompassRose.png");
        if (!font || !background || !jacket)
            throw new InvalidOperationException("Result layout assets are missing.");

        groupOrigins.Clear();
        RectTransform rootLayout = Rect("ResultLayout", canvas.transform, 0, 0, 1920, 1080);
        rootLayout.anchorMin = Vector2.zero;
        rootLayout.anchorMax = Vector2.one;
        rootLayout.offsetMin = Vector2.zero;
        rootLayout.offsetMax = Vector2.zero;

        RectTransform backdrop = Group("00_Backdrop", rootLayout, 0, 0, 1920, 1080);
        Image tempBackground = Image("Temp_Background", backdrop, 0, 0, 1920, 1080,
            Color.white, background, false);
        tempBackground.rectTransform.anchoredPosition = new Vector2(1920, 0);
        tempBackground.rectTransform.localScale = new Vector3(-1, 1, 1);
        Image("Left_Veil", backdrop, 0, 0, 1090, 1080, new Color(.025f, .035f, .10f, .65f));
        Image("Bottom_Veil", backdrop, 0, 920, 1920, 160, new Color(.025f, .035f, .10f, .48f));
        Image("Left_Edge", backdrop, 48, 92, 2, 820, new Color(.75f, .83f, 1f, .52f));

        RectTransform header = Group("01_Header", rootLayout, 28, 25, 1860, 90);
        IconSlot("Header_Icon_1x1_SpriteSlot", header, 32, 39, 44, compass);
        Text("Header_Title", header, "R E S U L T", 91, 28, 310, 45, 32, Ivory);
        Text("Header_Subtitle", header, "음악이 남긴 흔적", 92, 76, 280, 34, 18, Muted);
        Text("World_Title", header, "ASTRAEA ACADEMY CITY", 1585, 31, 300, 22, 14, Muted, TextAlignmentOptions.Right);
        Text("World_Subtitle", header, "THE STARS REMEMBER.", 1585, 54, 300, 22, 13, Muted, TextAlignmentOptions.Right);

        RectTransform song = Group("02_SongInformation", rootLayout, 89, 127, 901, 260);
        Panel("Song_Information", song, 89, 127, 901, 260, .53f);
        RectTransform jacketGroup = Group("Jacket", song, 91, 129, 250, 250);
        Image("Jacket_Frame", jacketGroup, 91, 129, 250, 250, new Color(.77f, .84f, 1f, .85f));
        IconSlot("Temp_Jacket_1x1_SpriteSlot", jacketGroup, 94, 132, 244, jacket);
        Text("Song_Title", song, "Stellar Promise", 369, 139, 412, 62, 48, Ivory);
        Text("Song_Artist", song, "Astraea Sound Team", 371, 205, 430, 35, 24, Muted);
        Text("Song_Memo", song, "언젠가, 다시.\n— Someday, again.", 372, 255, 414, 72, 21, Muted);
        Text("Song_Quote", song, "“사라지지 않는\n   기억이 있으니까.”", 801, 153, 180, 79, 19, Muted, TextAlignmentOptions.Right);
        RectTransform difficulty = Group("Difficulty", song, 357, 315, 633, 72);
        Image("Song_Divider", difficulty, 370, 315, 602, 1, new Color(.79f, .84f, 1f, .35f));
        Difficulty(difficulty, "NORMAL", "8", 371, true);
        Difficulty(difficulty, "EASY", "3", 571, false);
        Difficulty(difficulty, "HARD", "12", 694, false);
        Difficulty(difficulty, "CHAOS", "15", 830, false);

        RectTransform score = Group("03_Score", rootLayout, 89, 398, 602, 177);
        Panel("Score_Panel", score, 89, 398, 602, 177, .61f);
        IconSlot("Score_Icon_1x1_SpriteSlot", score, 100, 449, 105, compass);
        Text("Score_Label", score, "S C O R E", 233, 411, 345, 34, 22, Ivory);
        Text("Score_Value", score, "1,004,532", 228, 441, 457, 88, 72, Ivory);
        Text("New_Record", score, "N E W   R E C O R D", 233, 537, 455, 30, 23, Blue);
        Image("Record_Underline", score, 231, 568, 395, 2, Blue);

        RectTransform rank = Group("04_Rank", rootLayout, 704, 398, 329, 177);
        Panel("Rank_Panel", rank, 704, 398, 329, 177, .48f);
        Text("Rank_Label", rank, "R A N K", 732, 411, 259, 35, 22, Ivory);
        Text("Rank_Value", rank, "S", 775, 431, 178, 120, 137, Hex("FFE6AE"), TextAlignmentOptions.Center);
        Text("Rank_Caption", rank, "All Perfect?", 876, 513, 132, 36, 22, Hex("FFE6AE"), TextAlignmentOptions.Center);

        RectTransform judgement = Group("05_Judgements", rootLayout, 89, 584, 455, 218);
        Panel("Judgement_Panel", judgement, 89, 584, 455, 218, .68f);
        string[] grades = { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" };
        string[] counts = { "823", "6", "0", "0", "0" };
        string[] percentages = { "99.2%", "0.7%", "0.0%", "0.0%", "0.0%" };
        for (int i = 0; i < grades.Length; i++)
        {
            int y = 602 + i * 36;
            Text(grades[i] + "_Label", judgement, grades[i], 135, y, 170, 28, 21, Muted);
            Text(grades[i] + "_Count", judgement, counts[i], 311, y, 90, 28, 21, Ivory, TextAlignmentOptions.Right);
            Text(grades[i] + "_Percent", judgement, percentages[i], 438, y, 67, 28, 17, Muted, TextAlignmentOptions.Right);
        }

        RectTransform performance = Group("06_Performance", rootLayout, 560, 584, 474, 218);
        Panel("Performance_Panel", performance, 560, 584, 474, 218, .60f);
        Text("Combo_Label", performance, "MAX COMBO", 594, 621, 184, 34, 22, Muted);
        Text("Combo_Value", performance, "829 / 829", 786, 621, 209, 34, 23, Ivory);
        Image("Performance_Divider", performance, 596, 668, 394, 1, new Color(.8f, .85f, 1f, .28f));
        Text("Accuracy_Label", performance, "ACCURACY", 594, 680, 196, 38, 23, Muted);
        Text("Accuracy_Value", performance, "99.9%", 790, 676, 200, 47, 33, Ivory);
        Image("Accuracy_Track", performance, 595, 728, 392, 5, new Color(.38f, .47f, .74f, .55f));
        Image("Accuracy_Fill", performance, 595, 728, 390, 5, Blue);
        Text("Performance_Memo", performance, "The melody remains in you.", 725, 750, 262, 30, 17, Muted, TextAlignmentOptions.Right);

        RectTransform memory = Group("07_Memory", rootLayout, 90, 811, 945, 103);
        Panel("Memory_Panel", memory, 90, 811, 945, 103, .62f);
        IconSlot("Memory_Icon_1x1_SpriteSlot", memory, 102, 818, 89, jacket);
        Text("Memory_Title", memory, "기억의 조각", 229, 825, 361, 36, 24, Ivory);
        Text("Memory_Subtitle", memory, "처음의 약속", 232, 864, 330, 30, 17, Muted);
        Text("Memory_Quote", memory, "“... 이번에도, 돌아와줘서 고마워.”", 539, 855, 450, 35, 18, Muted);

        RectTransform quote = Group("08_RightQuote", rootLayout, 1517, 173, 342, 191);
        Text("Right_Quote", quote, "지금도,\n누군가의 소원이 여기,\n빛나고 있어.", 1553, 173, 306, 142, 27, Ivory, TextAlignmentOptions.Right);
        Text("Right_Quote_English", quote, "Still, someone’s wish shines here.", 1517, 329, 342, 35, 17, Muted, TextAlignmentOptions.Right);
        RectTransform rewards = Group("09_Rewards", rootLayout, 1577, 630, 278, 192);
        Panel("Reward_Panel", rewards, 1577, 630, 278, 192, .47f);
        Text("Reward_Label", rewards, "R E W A R D", 1606, 643, 230, 34, 22, Ivory);
        Reward(rewards, "Crystal", "× 20", "기억의 결정", 1608);
        Reward(rewards, "Butterfly", "× 1", "별의 잔향", 1734);

        RectTransform footer = Group("10_Footer", rootLayout, 39, 979, 1850, 87);
        Text("Footer_Brand", footer, "Project ReMind", 39, 982, 330, 31, 19, Muted);
        Text("Footer_Motto", footer, "For all the memories\nthat still shine.", 39, 1014, 340, 52, 15, Muted);
        IconSlot("Footer_Icon_1x1_SpriteSlot", footer, 1613, 981, 61, compass);
        Text("Footer_Korean", footer, "잊혀져가는 마음을, 다시.", 1684, 979, 205, 28, 16, Ivory, TextAlignmentOptions.Right);
        Text("Footer_English", footer, "A city where stars meet again.", 1678, 1009, 211, 25, 13, Muted, TextAlignmentOptions.Right);

        RectTransform actionsGroup = Group("11_Actions", rootLayout, 533, 931, 843, 77);
        ResultMenuActions actions = actionsGroup.gameObject.AddComponent<ResultMenuActions>();
        Button retry = ActionButton(actionsGroup, "RETRY_Button", "RETRY", "다시하기", 533, 931, 243, 77, false);
        Button next = ActionButton(actionsGroup, "NEXT_Button", "NEXT", "다음 곡", 818, 931, 271, 77, true);
        Button select = ActionButton(actionsGroup, "MUSIC_SELECT_Button", "MUSIC SELECT", "곡 선택", 1131, 931, 245, 77, false);
        UnityEventTools.AddPersistentListener(retry.onClick, actions.Retry);
        UnityEventTools.AddPersistentListener(next.onClick, actions.Next);
        UnityEventTools.AddPersistentListener(select.onClick, actions.MusicSelect);
        actions.SetInitialSelection(next);
        ValidateSpriteSlots(rootLayout);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Could not save Result scene.");
        CapturePreview(canvas);
        if (openedForBuild) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Result scene layout saved: " + ScenePath);
    }

    private static void CapturePreview(Canvas canvas)
    {
        var cameraObject = new GameObject("Result Preview Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("0B1022");
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 540;
        var target = new RenderTexture(1920, 1080, 24);
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        var oldDistance = canvas.planeDistance;
        var oldTarget = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../Library/ResultPreview.png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally
        {
            RenderTexture.active = oldTarget;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        Vector2 origin = groupOrigins.TryGetValue(parent, out Vector2 parentOrigin)
            ? parentOrigin : Vector2.zero;
        rt.anchoredPosition = new Vector2(x - origin.x, origin.y - y);
        rt.sizeDelta = new Vector2(width, height);
        return rt;
    }

    private static RectTransform Group(string name, Transform parent, float x, float y,
        float width, float height)
    {
        RectTransform group = Rect(name, parent, x, y, width, height);
        groupOrigins.Add(group, new Vector2(x, y));
        return group;
    }

    private static Image IconSlot(string name, Transform parent, float x, float y,
        float size, Sprite placeholder)
    {
        Image slot = Image(name, parent, x, y, size, size, Color.white, placeholder, true);
        slot.raycastTarget = false;
        return slot;
    }

    private static void ValidateSpriteSlots(Transform root)
    {
        int count = 0;
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (!image.name.EndsWith("_1x1_SpriteSlot", StringComparison.Ordinal)) continue;
            Vector2 size = image.rectTransform.sizeDelta;
            if (Mathf.Abs(size.x - size.y) > .01f || !image.preserveAspect)
                throw new InvalidOperationException("Sprite slot must be square: " + image.name);
            count++;
        }
        if (count != 10)
            throw new InvalidOperationException("Expected ten Result sprite slots, found " + count);
    }

    private static Image Image(string name, Transform parent, float x, float y, float width, float height,
        Color color, Sprite sprite = null, bool preserveAspect = false)
    {
        var rt = Rect(name, parent, x, y, width, height);
        rt.gameObject.AddComponent<CanvasRenderer>();
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value, float x, float y,
        float width, float height, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var rt = Rect(name, parent, x, y, width, height);
        rt.gameObject.AddComponent<CanvasRenderer>();
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Panel(string name, Transform parent, float x, float y, float width, float height, float alpha)
    {
        Image(name, parent, x, y, width, height, new Color(.035f, .055f, .13f, alpha));
        Image(name + "_TopEdge", parent, x, y, width, 1, new Color(.77f, .83f, 1f, .31f));
        Image(name + "_LeftEdge", parent, x, y, 1, height, new Color(.77f, .83f, 1f, .24f));
    }

    private static void Difficulty(Transform parent, string name, string level, int x, bool selected)
    {
        if (selected) Image("Difficulty_Selected", parent, x - 14, 326, 180, 52, new Color(.18f, .31f, .67f, .78f));
        Text("Difficulty_" + name, parent, name + "   " + level, x, 336, 175, 38,
            selected ? 22 : 20, selected ? Ivory : Muted);
    }

    private static void Reward(Transform parent, string name, string count, string label, int x)
    {
        RectTransform reward = Group(name, parent, x - 8, 686, 116, 136);
        Image("Icon_Frame", reward, x, 686, 101, 101, new Color(.1f, .16f, .33f, .73f));
        IconSlot("Icon_1x1_SpriteSlot", reward, x + 20, 690, 61, compass);
        Text("Count", reward, count, x + 9, 758, 84, 27, 18, Ivory, TextAlignmentOptions.Center);
        Text("Name", reward, label, x - 8, 794, 116, 28, 16, Ivory, TextAlignmentOptions.Center);
    }

    private static Button ActionButton(Transform parent, string name, string title, string subtitle,
        int x, int y, int width, int height, bool primary)
    {
        RectTransform buttonRoot = Group(name, parent, x, y, width, height);
        Image image = Image("Background", buttonRoot, x, y, width, height,
            primary ? new Color(.22f, .34f, .70f, .82f) : new Color(.04f, .06f, .14f, .68f));
        image.raycastTarget = true;
        Image(name + "_Top", buttonRoot, x, y, width, 1, primary ? Blue : Muted);
        Image(name + "_Bottom", buttonRoot, x, y + height - 1, width, 1, primary ? Blue : Muted);
        Image(name + "_Left", buttonRoot, x, y, 1, height, primary ? Blue : Muted);
        Image(name + "_Right", buttonRoot, x + width - 1, y, 1, height, primary ? Blue : Muted);
        IconSlot("Icon_1x1_SpriteSlot", buttonRoot, x + 18, y + 13, 52, compass);
        Text("Title", buttonRoot, title, x + 90, y + 12, width - 99, 30, 21, Ivory);
        Text("Subtitle", buttonRoot, subtitle, x + 90, y + 42, width - 99, 26, 17, Muted);
        var button = buttonRoot.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    private static Color Hex(string rgb)
    {
        ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
        return color;
    }
}
