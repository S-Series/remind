using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace REmind.Effects.Tests
{
    /// <summary>
    /// Stage 5 guardrails for the real ChartMaker asset graph. The tests use
    /// reflection so this no-engine test assembly can inspect Unity assets and
    /// compiled scene behaviours without opening file dialogs or PlayerPrefs.
    /// </summary>
    public sealed class ChartMakerSceneFlowTests
    {
        private const string ScenePath = "Assets/Scenes/ChartMaker.unity";
        private const string TopMenuPath =
            "Assets/UI/ChartMaker/ChartMakerTopMenu.uxml";

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
        public void EditorUi_ContainsTopMenuStatusAndCompleteEffectPanel()
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
                "effect-difficulty-id-field",
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
        }

        [Test]
        [Category("EffectAcceptance")]
        [Category("ChartMakerFlow")]
        public void CompiledFlow_AutoOpensEffectEditorAndLocksPreviewEditing()
        {
            Type placementType = FindType("ChartPlacementController");
            MethodInfo place = RequiredMethod(
                placementType, "TryPlaceCurrentNote");
            List<CalledMethod> placementCalls = ReadCalledMethods(place);
            int addEffect = FindCall(
                placementCalls, "ChartHolder", "AddEffectNote");
            int leaveTool = FindCall(
                placementCalls, "ChartPlacementController", "SetCurrentTool",
                addEffect + 1);
            int selectEffect = FindCall(
                placementCalls, "ChartNoteSelectionController",
                "SelectNoteObject", leaveTool + 1);

            Assert.That(addEffect, Is.GreaterThanOrEqualTo(0),
                "Effect placement must add the note to its ChartHolder.");
            Assert.That(leaveTool, Is.GreaterThan(addEffect),
                "Successful Effect placement must leave the placement tool.");
            Assert.That(selectEffect, Is.GreaterThan(leaveTool),
                "The newly placed Effect must be selected so its editor opens immediately.");

            Type popupType = FindType("ChartNoteEditPopupController");
            List<CalledMethod> initializeCalls = ReadCalledMethods(
                RequiredMethod(popupType, "Initialize"));
            Assert.That(
                FindCall(initializeCalls, "ChartEffectNoteEditorPanel", ".ctor"),
                Is.GreaterThanOrEqualTo(0),
                "The note popup must build the Effect-specific editor panel.");

            Type testPlayType = FindType("ChartTestPlay");
            List<CalledMethod> bindCalls = ReadCalledMethods(
                RequiredMethod(testPlayType, "BindEvents"));
            AssertHasCall(bindCalls, "ChartCore", "add_TestPlaybackStarting");
            AssertHasCall(bindCalls, "ChartCore", "add_TestPlaybackStartAborted");
            AssertHasCall(bindCalls, "ChartCore", "add_TestMsChanged");
            AssertHasCall(bindCalls, "ChartCore", "add_TestPlaybackChanged");

            List<CalledMethod> startCalls = ReadCalledMethods(
                RequiredMethod(testPlayType, "HandleTestPlaybackStarting"));
            AssertHasCall(startCalls, "ChartTestPlay", "TryCompileSnapshot");
            AssertHasCall(startCalls, "ChartTestPlay", "PrepareEffects");

            Type topMenuType = FindType("ChartMakerTopMenuController");
            List<CalledMethod> playbackChangedCalls = ReadCalledMethods(
                RequiredMethod(topMenuType, "HandlePlaybackChanged"));
            AssertHasCall(
                playbackChangedCalls,
                "ChartMakerTopMenuController",
                "SetEditingEnabled");
            AssertHasCall(
                playbackChangedCalls,
                "ChartMakerTopMenuController",
                "RefreshDocumentCommandState");
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

        private static void AssertHasCall(
            List<CalledMethod> calls, string declaringType, string methodName)
        {
            Assert.That(
                FindCall(calls, declaringType, methodName),
                Is.GreaterThanOrEqualTo(0),
                $"Expected call to {declaringType}.{methodName} was not found.");
        }

        private static int FindCall(
            List<CalledMethod> calls,
            string declaringType,
            string methodName,
            int startIndex = 0)
        {
            for (int index = Math.Max(0, startIndex);
                 index < calls.Count;
                 index++)
            {
                MethodBase method = calls[index].Method;
                if (method.DeclaringType?.Name == declaringType &&
                    method.Name == methodName)
                {
                    return index;
                }
            }
            return -1;
        }

        private static List<CalledMethod> ReadCalledMethods(MethodInfo method)
        {
            MethodBody body = method.GetMethodBody();
            byte[] bytes = body?.GetILAsByteArray();
            Assert.That(bytes, Is.Not.Null,
                $"{method.DeclaringType?.Name}.{method.Name} has no readable IL body.");

            Dictionary<int, OpCode> opCodes = BuildOpCodeMap();
            var calls = new List<CalledMethod>();
            int position = 0;
            while (position < bytes.Length)
            {
                int offset = position;
                int code = bytes[position++];
                if (code == 0xfe)
                {
                    code = 0xfe00 | bytes[position++];
                }

                if (!opCodes.TryGetValue(code, out OpCode opCode))
                {
                    throw new InvalidOperationException(
                        $"Unknown IL opcode 0x{code:x} at {offset}.");
                }

                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    int token = BitConverter.ToInt32(bytes, position);
                    try
                    {
                        MethodBase called = method.Module.ResolveMethod(
                            token,
                            method.DeclaringType?.GetGenericArguments(),
                            method.GetGenericArguments());
                        calls.Add(new CalledMethod(offset, called));
                    }
                    catch (ArgumentException)
                    {
                        // A method spec from an unrelated generic context is
                        // irrelevant to the concrete workflow calls asserted here.
                    }
                }

                position += OperandSize(opCode.OperandType, bytes, position);
            }
            return calls;
        }

        private static Dictionary<int, OpCode> BuildOpCodeMap()
        {
            var result = new Dictionary<int, OpCode>();
            FieldInfo[] fields = typeof(OpCodes).GetFields(
                BindingFlags.Public | BindingFlags.Static);
            for (int index = 0; index < fields.Length; index++)
            {
                var opCode = (OpCode)fields[index].GetValue(null);
                result[(ushort)opCode.Value] = opCode;
            }
            return result;
        }

        private static int OperandSize(
            OperandType operandType, byte[] bytes, int position)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + BitConverter.ToInt32(bytes, position) * 4;
                default:
                    throw new NotSupportedException(
                        $"Unsupported IL operand type: {operandType}.");
            }
        }

        private readonly struct CalledMethod
        {
            public CalledMethod(int offset, MethodBase method)
            {
                Offset = offset;
                Method = method;
            }

            public int Offset { get; }
            public MethodBase Method { get; }
        }

        private sealed class AdditiveSceneScope : IDisposable
        {
            private readonly Type sceneType;
            private readonly Type sceneManagerType;
            private readonly Type editorSceneManagerType;
            private readonly object originalActiveScene;
            private readonly bool openedByTest;
            private object scene;

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
