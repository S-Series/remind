using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace REmind.Effects.Tests
{
    /// <summary>
    /// Stage 5 guardrails for the real ChartMaker asset graph. Reflection keeps
    /// these editor tests independent of ChartMaker's runtime assembly.
    /// </summary>
    public sealed class ChartMakerSceneFlowTests
    {
        private const string ScenePath = "Assets/Scenes/ChartMaker.unity";
        private const string TopMenuPath =
            "Assets/UI/ChartMaker/ChartMakerTopMenu.uxml";
        private static bool playFixtureSceneLoaded;
        private static bool playFixtureNeedsCleanup;

        [Test]
        [Category("Architecture")]
        public void SharedAndChartMakerAssemblies_DoNotReferenceGameplay()
        {
            AssertReferencesExclude("REmind.ChartCore", "REmind.ChartMaker",
                "Assembly-CSharp");
            AssertReferencesExclude("REmind.NoteRules", "REmind.ChartMaker",
                "Assembly-CSharp");
            AssertReferencesExclude("REmind.Common", "REmind.ChartMaker",
                "Assembly-CSharp");
            AssertReferencesExclude("REmind.Presentation", "REmind.ChartMaker",
                "Assembly-CSharp");
            AssertReferencesExclude("REmind.ChartMaker", "Assembly-CSharp");
            AssertReferencesExclude("REmind.Gameplay", "REmind.ChartMaker",
                "Assembly-CSharp");
        }

        private static void AssertReferencesExclude(string assemblyName,
            params string[] forbidden)
        {
            Assembly assembly = Assembly.Load(assemblyName);
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                foreach (string name in forbidden)
                {
                    Assert.That(reference.Name, Is.Not.EqualTo(name),
                        assemblyName + " references " + name);
                }
            }
        }

        [Test]
        [Category("EffectAcceptance")]
        [Category("ChartMakerFlow")]
        public void Scene_ConnectsEffectPlacementSavingAndPreview()
        {
            using (var scene = new AdditiveSceneScope(ScenePath))
            {
                List<object> components = scene.GetComponents();
                object placement = FindSingleComponent(
                    components, "ChartPlacementController");
                object router = FindSingleComponent(
                    components, "ChartMakerInputRouter");
                object selection = FindSingleComponent(
                    components, "ChartNoteSelectionController");
                object loader = FindSingleComponent(
                    components, "FileToChart");
                object saver = FindSingleComponent(
                    components, "ChartToFile");
                object core = FindSingleComponent(components, "ChartCore");
                object testPlay = FindSingleComponent(
                    components, "ChartTestPlay");
                object topMenu = FindSingleComponent(
                    components, "ChartMakerTopMenuController");
                object noteEditor = FindSingleComponent(
                    components, "ChartNoteEditPopupController");
                object chartScroll = FindSingleComponent(
                    components, "ChartScroll");
                object floorRenderer = FindSingleComponent(
                    components, "ChartPreviewFloorRenderer");

                object effectPrefab = RequiredField(
                    placement, "chartPreviewEffectNotePrefab");
                Assert.That(
                    GetObjectName(effectPrefab),
                    Is.EqualTo("Effect"),
                    "The Effect placement slot must point to the Effect preview prefab.");
                Assert.That(
                    HasComponent(effectPrefab, "BoxCollider2D"),
                    Is.True,
                    "A placed Effect needs a collider so it can be selected and edited.");

                Assert.That(
                    RequiredField(placement, "inputRouter"),
                    Is.SameAs(router));
                Assert.That(
                    RequiredField(placement, "selectionController"),
                    Is.SameAs(selection));
                RequiredField(placement, "previewField");
                RequiredField(placement, "specialNoteField");
                RequiredField(placement, "chartPreviewNoteField");

                // Selection resolves the placement controller from the same
                // GameObject during Awake when its optional serialized slot is empty.
                Assert.That(
                    GetGameObject(selection),
                    Is.SameAs(GetGameObject(placement)));
                Assert.That(
                    RequiredField(selection, "inputRouter"),
                    Is.SameAs(router));

                Assert.That(
                    RequiredField(loader, "placementController"),
                    Is.SameAs(placement));
                Assert.That(
                    RequiredField(loader, "chartToFile"),
                    Is.SameAs(saver));
                Assert.That(
                    RequiredField(loader, "chartCore"),
                    Is.SameAs(core));
                Assert.That(
                    (bool)GetField(loader, "restoreRecentFilesOnStart"),
                    Is.True,
                    "ChartMaker should restore the most recent chart/music on normal startup.");
                Assert.That(
                    RequiredField(saver, "chartCore"),
                    Is.SameAs(core));

                RequiredField(topMenu, "menuAsset");
                Assert.That(RequiredField(topMenu, "inputRouter"), Is.SameAs(router));
                Assert.That(RequiredField(topMenu, "chartToFile"), Is.SameAs(saver));
                Assert.That(RequiredField(topMenu, "fileToChart"), Is.SameAs(loader));
                Assert.That(
                    RequiredField(topMenu, "selectionController"),
                    Is.SameAs(selection));
                Assert.That(
                    RequiredField(topMenu, "placementController"),
                    Is.SameAs(placement));
                Assert.That(RequiredField(topMenu, "chartCore"), Is.SameAs(core));
                Assert.That(
                    RequiredField(topMenu, "noteEditPopup"),
                    Is.SameAs(noteEditor));
                Assert.That(
                    GetGameObject(topMenu),
                    Is.SameAs(GetGameObject(noteEditor)),
                    "The top menu must initialize the note editor hosted on its UI object.");

                Assert.That(
                    RequiredField(testPlay, "chartScroll"),
                    Is.SameAs(chartScroll));
                RequiredField(testPlay, "moveCameraTransform");
                RequiredField(testPlay, "hitSource");
                RequiredField(testPlay, "laneHitEffectPlayer");
                Assert.That(
                    RequiredField(chartScroll, "previewFloorRenderer"),
                    Is.SameAs(floorRenderer));
                RequiredField(core, "audioSource");
            }
        }

        [Test]
        [Category("EffectAcceptance")]
        [Category("ChartMakerFlow")]
        public void EditorUi_ContainsEffectControlsAndUsesDocumentDifficulty()
        {
            object topMenuAsset = LoadAsset(TopMenuPath,
                FindType("UnityEngine.UIElements.VisualTreeAsset"));
            Assert.That(topMenuAsset, Is.Not.Null,
                "ChartMakerTopMenu.uxml must be importable as a VisualTreeAsset.");

            object topMenuRoot = InstantiateVisualTree(topMenuAsset);
            HashSet<string> topMenuNames = CollectVisualElementNames(topMenuRoot);
            AssertContainsAll(topMenuNames,
                "chart-maker-top-menu",
                "file-menu-button",
                "open-chart-menu-item",
                "save-chart-menu-item",
                "save-as-chart-menu-item",
                "playback-menu-button",
                "start-test-play-menu-item",
                "end-test-play-menu-item",
                "document-status-label",
                "note-edit-window",
                "note-edit-apply-button");

            Type panelType = FindType("ChartEffectNoteEditorPanel");
            object panel = Activator.CreateInstance(
                panelType,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                new object[]
                {
                    new Action<string, bool>((_, _) => { }),
                    new Action(() => { }),
                    new Action(() => { })
                },
                null);
            object effectRoot = panelType.GetProperty(
                "Element", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(panel);
            Assert.That(effectRoot, Is.Not.Null,
                "The Effect editor must construct its UI root.");

            HashSet<string> effectNames = CollectVisualElementNames(effectRoot);
            AssertContainsAll(effectNames,
                "effect-note-edit-panel",
                "effect-music-id-field",
                "effect-gimmick-field",
                "effect-type-field",
                "effect-command-field",
                "effect-order-field",
                "effect-identity-label",
                "effect-parameters-json-field",
                "effect-reset-json-button",
                "effect-validate-json-button",
                "effect-copy-button",
                "effect-reload-json-button");

            PropertyInfo difficulty = FindType("ChartEffectDocumentState")
                .GetProperty("DifficultyId", BindingFlags.Public |
                    BindingFlags.Static);
            Assert.That(difficulty, Is.Not.Null);
            string previousDifficulty = (string)difficulty.GetValue(null);
            try
            {
                difficulty.SetValue(null, "hard");
                Assert.That(panelType.GetProperty("DifficultyId")
                    ?.GetValue(panel), Is.EqualTo("hard"),
                    "Effect difficulty must follow the open chart document.");
                difficulty.SetValue(null, "chaos");
                Assert.That(panelType.GetProperty("DifficultyId")
                    ?.GetValue(panel), Is.EqualTo("chaos"));
            }
            finally
            {
                difficulty.SetValue(null, previousDifficulty);
            }
        }

        [UnityTest]
        [Category("EffectAcceptance")]
        [Category("ChartMakerFlow")]
        public IEnumerator EffectPlacementOpensEditorAndPreviewBlocksFurtherPlacement()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            Scene openChartMaker = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(openChartMaker.IsValid() && openChartMaker.isLoaded,
                Is.False, "Close the live ChartMaker scene before this check.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                Assert.That(SceneManager.GetSceneAt(index).isDirty, Is.False,
                    "Save modified editor scenes before entering PlayMode.");
            Type chartManager = FindType("ChartManager");
            PropertyInfo holders = chartManager.GetProperty("ChartHolders");
            Assert.That(((IList)holders.GetValue(null)).Count, Is.Zero,
                "Do not run the placement check with a live ChartMaker document.");
            Type history = FindType("ChartEditHistory");
            Assert.That((bool)history.GetProperty("CanUndo")
                .GetValue(null), Is.False,
                "Do not replace a live ChartMaker edit history.");
            Assert.That((bool)history.GetProperty("CanRedo")
                .GetValue(null), Is.False,
                "Do not replace a live ChartMaker edit history.");

            // The EditMode runner restores the user's scene setup after its
            // empty bootstrap scene; load ChartMaker only inside PlayMode.
            Scene bootstrap = SceneManager.GetActiveScene();
            Assert.That(bootstrap.path, Is.Empty,
                "Run this fixture from the Test Runner bootstrap scene.");
            foreach (GameObject root in bootstrap.GetRootGameObjects())
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                camera.enabled = false;
            yield return new EnterPlayMode();
            Scene scene = default;
            AsyncOperation sceneUnload = null;
            AudioClip clip = null;
            object core = null;
            object audio = null;
            playFixtureSceneLoaded = false;
            playFixtureNeedsCleanup = true;
            SceneManager.sceneLoaded += PreparePlayFixtureScene;
            try
            {
                chartManager = FindType("ChartManager");
                holders = chartManager.GetProperty("ChartHolders");
                scene = EditorSceneManager.LoadSceneInPlayMode(
                    ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                for (int frame = 0; frame < 120; frame++)
                {
                    Scene loaded = SceneManager.GetSceneByPath(ScenePath);
                    if (playFixtureSceneLoaded && loaded.IsValid() && loaded.isLoaded)
                    {
                        scene = loaded;
                        break;
                    }
                    yield return null;
                }
                Assert.That(playFixtureSceneLoaded, Is.True,
                    "ChartMaker sceneLoaded did not arrive within 120 frames.");
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                List<object> components = new List<object>();
                foreach (GameObject root in scene.GetRootGameObjects())
                    components.AddRange(GetComponentsInChildren(root));
                Assert.That((bool)GetField(FindSingleComponent(
                    components, "FileToChart"), "restoreRecentFilesOnStart"),
                    Is.False, "Recent-file startup must be disabled in this fixture.");
                yield return null;
                Assert.That(((IList)holders.GetValue(null)).Count, Is.Zero,
                    "The isolated ChartMaker scene must start with an empty chart.");
                object placement = FindSingleComponent(
                    components, "ChartPlacementController");
                object selection = FindSingleComponent(
                    components, "ChartNoteSelectionController");
                object popup = FindSingleComponent(
                    components, "ChartNoteEditPopupController");
                core = FindSingleComponent(components, "ChartCore");
                object testPlay = FindSingleComponent(
                    components, "ChartTestPlay");
                object topMenu = FindSingleComponent(
                    components, "ChartMakerTopMenuController");
                object router = FindSingleComponent(
                    components, "ChartMakerInputRouter");
                audio = RequiredField(core, "audioSource");
                Assert.That((bool)GetField(topMenu, "isMenuBound"), Is.True,
                        "The actual menu must subscribe to Preview state changes.");
                Assert.That((bool)GetField(popup, "isInitialized"), Is.True);
                Assert.That((bool)GetField(testPlay, "eventsBound"), Is.True,
                    "The Preview presenter must subscribe during PlayMode startup.");

                RequiredMethod(placement.GetType(), "SelectEffectNote")
                    .Invoke(placement, null);
                object position = Activator.CreateInstance(
                    FindType("UnityEngine.Vector2"), 0f, 0f);
                RequiredMethod(placement.GetType(), "HandlePositionClicked")
                    .Invoke(placement, new[] { null, position, null });

                IList placed = (IList)holders.GetValue(null);
                Assert.That(placed.Count, Is.EqualTo(1),
                    "Clicking the Effect preview must place one chart event.");
                Assert.That((bool)GetField(placed[0], "isEffect"), Is.True);
                Assert.That(GetField(placed[0], "effectId"),
                    Is.Not.Empty);
                Assert.That(placement.GetType().GetProperty("CurrentTool")
                    .GetValue(placement).ToString(), Is.EqualTo("None"));

                IList selected = (IList)selection.GetType()
                    .GetProperty("SelectedNoteObjects").GetValue(selection);
                Assert.That(selected.Count, Is.EqualTo(1));
                Assert.That(GetField(popup, "selectedNoteObject"),
                    Is.SameAs(selected[0]));
                Assert.That(GetField(popup, "selectedNoteType").ToString(),
                    Is.EqualTo("Effect"));
                Assert.That(GetField(popup, "effectPanel"), Is.Not.Null);
                Assert.That(IsDisplayed(GetField(popup, "editWindow")), Is.True,
                    "Selecting the placed Effect must open its editor.");

                placed[0].GetType().GetField("effectTypeId")
                    .SetValue(placed[0], "camera.offset");
                placed[0].GetType().GetField("effectParametersJson")
                    .SetValue(placed[0],
                        "{\"durationMs\":1000,\"offsetX\":1," +
                        "\"offsetY\":0,\"rollDegrees\":0}");
                RequiredMethod(placement.GetType(), "SelectEffectNote")
                    .Invoke(placement, null);
                Assert.That((bool)placement.GetType()
                    .GetProperty("IsPreviewing").GetValue(placement), Is.True);
                Assert.That(GetField(placement, "currentNoteType").ToString(),
                    Is.EqualTo("Effect"));
                Assert.That((bool)GetField(placement,
                    "canPlaceCurrentPreview"), Is.True,
                    "Effect placement must be ready before testing the Preview lock.");
                Assert.That(IsDisplayed(GetField(popup, "editWindow")), Is.False,
                    "Selecting the next Effect must close the previous editor.");

                clip = AudioClip.Create("Effect placement preview", 4410,
                    1, 44100, false);
                ((AudioSource)audio).clip = clip;
                core.GetType().GetMethod("StartTestPlay", new[] { typeof(double) })
                    .Invoke(core, new object[] { 0d });
                Assert.That((bool)core.GetType().GetProperty("IsTestPlaying")
                    .GetValue(core), Is.True);
                object snapshot = testPlay.GetType()
                    .GetProperty("CurrentSnapshot").GetValue(testPlay);
                Assert.That(snapshot, Is.Not.Null,
                    "Preview must compile the placed Effect.");
                Assert.That(((ICollection)snapshot.GetType()
                    .GetProperty("EffectEvents").GetValue(snapshot)).Count,
                    Is.EqualTo(1));
                Assert.That((long)GetField(testPlay, "effectSessionId"),
                    Is.GreaterThan(0),
                    "Preview must prepare an Effect session.");
                Assert.That((string)testPlay.GetType()
                    .GetProperty("LastEffectMessage").GetValue(testPlay),
                    Is.Null.Or.Empty);
                Assert.That((bool)placement.GetType().GetProperty("CanEditChart")
                    .GetValue(placement), Is.False);
                Assert.That((bool)router.GetType().GetProperty("EditingEnabled")
                    .GetValue(router), Is.False,
                    "Preview must disable the input router through the menu callback.");
                object saveButton = GetField(topMenu, "saveChartButton");
                PropertyInfo saveEnabled = saveButton.GetType()
                    .GetProperty("enabledSelf");
                Assert.That(saveEnabled.GetValue(saveButton), Is.False,
                    "Preview must disable the Save command.");
                Assert.That(topMenu.GetType()
                    .GetProperty("CurrentStatusMessage").GetValue(topMenu),
                    Does.Contain("Editing is locked"));
                RequiredMethod(placement.GetType(), "HandlePositionClicked")
                    .Invoke(placement, new[] { null, position, null });
                Assert.That(((IList)holders.GetValue(null)).Count, Is.EqualTo(1),
                    "Preview must reject a placement click.");
                RequiredMethod(core.GetType(), "EndTestPlay")
                    .Invoke(core, null);
                Assert.That((bool)router.GetType()
                    .GetProperty("EditingEnabled").GetValue(router), Is.True,
                    "Stopping Preview must restore editor input.");
                Assert.That(saveEnabled.GetValue(saveButton), Is.True,
                    "Stopping Preview must restore the Save command.");
            }
            finally
            {
                SceneManager.sceneLoaded -= PreparePlayFixtureScene;
                playFixtureSceneLoaded = false;
                if (core is Component component && component &&
                    (bool)core.GetType().GetProperty("IsTestPlaying")
                        .GetValue(core))
                    RequiredMethod(core.GetType(), "EndTestPlay")
                        .Invoke(core, null);
                if (audio is AudioSource source && source)
                    source.clip = null;
                if (clip)
                    UnityEngine.Object.Destroy(clip);
                ClearPlayFixtureState();
                if (scene.IsValid() && scene.isLoaded)
                    sceneUnload = SceneManager.UnloadSceneAsync(scene);
            }
            if (sceneUnload != null)
                yield return sceneUnload;
            yield return new ExitPlayMode();
            if (playFixtureNeedsCleanup)
            {
                ClearPlayFixtureState();
                playFixtureNeedsCleanup = false;
            }
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAfterPlacementCheck()
        {
            if (playFixtureNeedsCleanup && EditorApplication.isPlaying)
                ClearPlayFixtureState();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                yield return new ExitPlayMode();
            if (playFixtureNeedsCleanup)
            {
                ClearPlayFixtureState();
                playFixtureNeedsCleanup = false;
            }
        }

        private static void ClearPlayFixtureState()
        {
            FindType("ChartManager").GetMethod("ClearChart",
                new[] { typeof(bool) }).Invoke(null, new object[] { false });
            RequiredMethod(FindType("ChartEditHistory"), "Clear")
                .Invoke(null, null);
        }

        private static void PreparePlayFixtureScene(Scene loaded, LoadSceneMode _)
        {
            if (loaded.path != ScenePath) return;
            playFixtureSceneLoaded = true;
            foreach (GameObject root in loaded.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
                foreach (object component in GetComponentsInChildren(root))
                {
                    if (component.GetType().Name != "FileToChart") continue;
                    component.GetType().GetField("restoreRecentFilesOnStart",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(component, false);
                }
            }
        }

        private static bool IsDisplayed(object element)
        {
            return ((VisualElement)element).style.display.value ==
                DisplayStyle.Flex;
        }

        private static object LoadAsset(string path, Type assetType)
        {
            Type assetDatabase = FindType("UnityEditor.AssetDatabase");
            MethodInfo load = assetDatabase.GetMethod(
                "LoadAssetAtPath",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(Type) },
                null);
            if (load == null)
            {
                throw new MissingMethodException(
                    assetDatabase.FullName, "LoadAssetAtPath(string, Type)");
            }
            return load.Invoke(null, new object[] { path, assetType });
        }

        private static object InstantiateVisualTree(object asset)
        {
            MethodInfo method = asset.GetType().GetMethod(
                "Instantiate", BindingFlags.Instance | BindingFlags.Public,
                null, Type.EmptyTypes, null)
                ?? asset.GetType().GetMethod(
                    "CloneTree", BindingFlags.Instance | BindingFlags.Public,
                    null, Type.EmptyTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(
                    asset.GetType().FullName, "Instantiate/CloneTree");
            }
            return method.Invoke(asset, null);
        }

        private static HashSet<string> CollectVisualElementNames(object root)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<object>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                object current = pending.Pop();
                Type type = current.GetType();
                string name = type.GetProperty("name")?.GetValue(current)
                    as string;
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(name);
                }

                PropertyInfo childCountProperty = type.GetProperty("childCount");
                MethodInfo elementAt = type.GetMethod(
                    "ElementAt", BindingFlags.Instance | BindingFlags.Public,
                    null, new[] { typeof(int) }, null);
                if (childCountProperty == null || elementAt == null)
                {
                    throw new InvalidOperationException(
                        $"{type.FullName} is not a traversable VisualElement.");
                }

                int childCount = (int)childCountProperty.GetValue(current);
                for (int index = childCount - 1; index >= 0; index--)
                {
                    pending.Push(elementAt.Invoke(current, new object[] { index }));
                }
            }

            return result;
        }

        private static void AssertContainsAll(
            HashSet<string> actual, params string[] expected)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual.Contains(expected[index]), Is.True,
                    $"UI element '{expected[index]}' is missing.");
            }
        }

        private static object FindSingleComponent(
            List<object> components, string typeName)
        {
            object found = null;
            int count = 0;
            for (int index = 0; index < components.Count; index++)
            {
                if (components[index].GetType().Name != typeName)
                {
                    continue;
                }
                found = components[index];
                count++;
            }

            Assert.That(count, Is.EqualTo(1),
                $"ChartMaker scene must contain exactly one {typeName}.");
            return found;
        }

        private static object RequiredField(object target, string fieldName)
        {
            object value = GetField(target, fieldName);
            Assert.That(value, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} must be connected.");
            return value;
        }

        private static object GetField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName, fieldName);
            }
            return field.GetValue(target);
        }

        private static object GetGameObject(object component)
        {
            PropertyInfo property = component.GetType().GetProperty(
                "gameObject", BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(component);
        }

        private static string GetObjectName(object unityObject)
        {
            return unityObject.GetType().GetProperty("name")
                ?.GetValue(unityObject) as string;
        }

        private static bool HasComponent(object gameObject, string typeName)
        {
            List<object> components = GetComponentsInChildren(gameObject);
            for (int index = 0; index < components.Count; index++)
            {
                if (components[index].GetType().Name == typeName)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<object> GetComponentsInChildren(object gameObject)
        {
            Type componentType = FindType("UnityEngine.Component");
            MethodInfo getComponents = gameObject.GetType().GetMethod(
                "GetComponentsInChildren",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Type), typeof(bool) },
                null);
            if (getComponents == null)
            {
                throw new MissingMethodException(
                    gameObject.GetType().FullName,
                    "GetComponentsInChildren(Type, bool)");
            }

            Array values = (Array)getComponents.Invoke(
                gameObject, new object[] { componentType, true });
            var result = new List<object>(values.Length);
            foreach (object value in values)
            {
                result.Add(value);
            }
            return result;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            throw new TypeLoadException($"Could not find loaded type '{fullName}'.");
        }

        private static MethodInfo RequiredMethod(Type type, string name)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }

        private sealed class AdditiveSceneScope : IDisposable
        {
            private readonly Type sceneType;
            private readonly Type sceneManagerType;
            private readonly Type editorSceneManagerType;
            private readonly object originalActiveScene;
            private readonly bool openedByTest;
            private object scene;

            public bool OpenedByTest => openedByTest;

            public AdditiveSceneScope(string path)
            {
                sceneType = FindType("UnityEngine.SceneManagement.Scene");
                sceneManagerType = FindType(
                    "UnityEngine.SceneManagement.SceneManager");
                editorSceneManagerType = FindType(
                    "UnityEditor.SceneManagement.EditorSceneManager");
                originalActiveScene = InvokeStatic(
                    sceneManagerType, "GetActiveScene", Type.EmptyTypes, null);
                scene = InvokeStatic(
                    sceneManagerType,
                    "GetSceneByPath",
                    new[] { typeof(string) },
                    new object[] { path });

                bool isValid = (bool)sceneType.GetMethod("IsValid")
                    .Invoke(scene, null);
                bool isLoaded = isValid && (bool)sceneType
                    .GetProperty("isLoaded").GetValue(scene);
                if (isLoaded)
                {
                    return;
                }

                Type modeType = FindType(
                    "UnityEditor.SceneManagement.OpenSceneMode");
                object additive = Enum.Parse(modeType, "Additive");
                scene = InvokeStatic(
                    editorSceneManagerType,
                    "OpenScene",
                    new[] { typeof(string), modeType },
                    new[] { (object)path, additive });
                openedByTest = true;
            }

            public List<object> GetComponents()
            {
                MethodInfo rootsMethod = sceneType.GetMethod(
                    "GetRootGameObjects",
                    BindingFlags.Instance | BindingFlags.Public,
                    null, Type.EmptyTypes, null);
                Array roots = (Array)rootsMethod.Invoke(scene, null);
                var result = new List<object>();
                foreach (object root in roots)
                {
                    result.AddRange(GetComponentsInChildren(root));
                }
                return result;
            }

            public void Dispose()
            {
                if (!openedByTest)
                {
                    return;
                }

                InvokeStatic(
                    editorSceneManagerType,
                    "CloseScene",
                    new[] { sceneType, typeof(bool) },
                    new[] { scene, (object)true });

                bool originalIsValid = (bool)sceneType
                    .GetMethod("IsValid").Invoke(originalActiveScene, null);
                bool originalIsLoaded = originalIsValid && (bool)sceneType
                    .GetProperty("isLoaded").GetValue(originalActiveScene);
                if (originalIsLoaded)
                {
                    InvokeStatic(
                        sceneManagerType,
                        "SetActiveScene",
                        new[] { sceneType },
                        new[] { originalActiveScene });
                }
            }

            private static object InvokeStatic(
                Type type,
                string name,
                Type[] parameterTypes,
                object[] arguments)
            {
                MethodInfo method = type.GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    parameterTypes,
                    null);
                if (method == null)
                {
                    throw new MissingMethodException(type.FullName, name);
                }
                return method.Invoke(null, arguments);
            }
        }
    }
}
