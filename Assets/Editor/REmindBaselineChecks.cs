using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using REmind.Charting;
using REmind.Data;
using REmind.Gameplay;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Input.Judgement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only bridge to Unity's predefined Assembly-CSharp. Test asmdefs cannot reference that
/// assembly directly; the NUnit wrapper calls this editor-only helper through one reflection boundary.
/// No scene, prefab, user chart, audio or project setting is changed by these checks.
/// </summary>
public static class REmindBaselineChecks
{
    private static object DecodeRuntimeEffect(RuntimeChartPackage package,
        string effectId)
    {
        foreach (PlayableEffectEvent effect in package.Snapshot.EffectEvents)
            if (effect.EffectId == effectId &&
                package.EffectParameterJson.TryGetValue(effectId,
                    out string json) &&
                ChartEffectParameterCodec.TryDecode(effect.EffectTypeId,
                    effect.CommandId, package.GimmickId, json,
                    out object parameters, out _))
                return parameters;
        throw new InvalidOperationException(
            "Runtime Effect is missing or invalid: " + effectId);
    }

    public static void Run(string name)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run these checks in EditMode only.");
        Invoke(typeof(REmindBaselineChecks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static), null);
    }

    private static void LegacyV7_AllNoteFamilies()
    {
        const string json = @"{
            ""format"":""REmindChart"", ""formatVersion"":7,
            ""baseBpm"":120, ""musicStartCorrectionMs"":-100,
            ""events"":[
                ""000|0000|LS------|NS10----|10000000|-1|-|F|-*"",
                ""001|0000|LE------|RE10----|00000000|240|0.5|F|-"",
                ""002|0000|--RF----|----IT10|00000000|-1|-|F|-""]}";
        ChartFile file = ChartFileCodec.Parse(json);
        Require(file.chartDatas.Length == 3 && file.chartDatas[0].isMarker, "Legacy rows/Marker were lost.");
        Require(file.MusicStartCorrectionMs == -100, "Offset changed during legacy load.");
        var build = ChartHolderDocumentAdapter.Build(file.chartDatas, file.BaseBpm, 4);
        Require(build.Succeeded, "Legacy document conversion failed.");
        var compiled = ChartCompiler.Compile(build.Document);
        Require(compiled.Succeeded, "Legacy chart no longer compiles.");
        Require(compiled.Snapshot.Notes.Count == 5, "Expected Hold, LongScratch, Air, Tap and Scratch.");
        Require(compiled.Snapshot.JudgementTargets.Count == 7, "Hold/Scratch start/end targets changed.");
        Require(compiled.Snapshot.EffectEvents.Count == 0, "Ordinary legacy chart acquired executable effects.");
    }

    private static void ChartJacket_RoundTripAndRejectsNested()
    {
        var metadata = new ChartEffectDocumentState.Metadata(
            "song", "hard", "", "");
        string standard = ChartFileCodec.Serialize(
            Array.Empty<ChartHolder>(), 120, 0, metadata);
        ChartFile standardChart = ChartFileCodec.Parse(standard);
        Require(standardChart.FormatVersion == 1,
            "New chart saves must use format version 1.");
        Require(standardChart.EffectiveJacketFile == "hard.jpg",
            "Chart jacket default did not follow the difficulty ID.");

        string custom = ChartFileCodec.Serialize(
            Array.Empty<ChartHolder>(), 120, 0, metadata, "custom.png");
        ChartFile reopened = ChartFileCodec.Parse(custom);
        Require(reopened.JacketFile == "custom.png" &&
                reopened.EffectiveJacketFile == "custom.png",
            "Chart jacket override did not survive serialization.");

        bool rejected = false;
        try
        {
            ChartFileCodec.Serialize(Array.Empty<ChartHolder>(), 120, 0,
                metadata, "jackets/custom.png");
        }
        catch (FormatException) { rejected = true; }
        Require(rejected, "Chart jacket accepted a nested path.");
    }

    private static void LongScratchAdapter_PreservesIntermediatePoint()
    {
        var start = new ChartHolder(0, 0);
        start.noteTypes[ChartHolder.MainLineCount] = NoteType.LongScratch;
        start.scratchPointTypes[0] = ScratchPointType.Start;
        start.scratchMotions[0] = new ScratchMotionData(12,
            ScratchMotionType.Gradual);

        var middle = new ChartHolder(0, 2400);
        middle.noteTypes[ChartHolder.MainLineCount] = NoteType.LongScratch;
        middle.scratchPointTypes[0] = ScratchPointType.Mid;
        middle.scratchMotions[0] = new ScratchMotionData(7,
            ScratchMotionType.Release);

        var end = new ChartHolder(1, 0);
        end.noteTypes[ChartHolder.MainLineCount] = NoteType.LongScratch;
        end.scratchPointTypes[0] = ScratchPointType.End;

        ChartHolderDocumentBuildResult built = ChartHolderDocumentAdapter.Build(
            new[] { start, middle, end }, 120d, 4);
        Require(built.Succeeded, "Long Scratch adapter conversion failed.");
        ChartCompileResult compiled = ChartCompiler.Compile(built.Document);
        Require(compiled.Succeeded && compiled.Snapshot.Notes.Count == 1,
            "Long Scratch point chain did not compile.");
        PlayableNoteSnapshot note = compiled.Snapshot.Notes[0];
        Require(note.Points.Count == 3 &&
                compiled.Snapshot.JudgementTargets.Count == 3 &&
                compiled.Snapshot.JudgementSegments.Count == 2 &&
                compiled.Snapshot.JudgementTargets[1].Kind ==
                    JudgementTargetKind.HoldMid &&
                note.Points[1].Kind == ChartNotePointKind.Mid &&
                note.Points[1].Motion == ChartScratchMotionKind.Release &&
                note.Points[1].MoveAmount == 7 &&
                Math.Abs(note.Points[1].TimeMs - 1000d) < 0.000001d,
            "Long Scratch Mid motion or chart time was lost.");
    }

    private static void LegacyEffect_RemainsUnresolved()
    {
        const string json = @"{""format"":""REmindChart"",""formatVersion"":7,""baseBpm"":120,
            ""musicStartCorrectionMs"":0,""events"":[""000|0000|--------|--------|00000000|-1|-|T|-""]}";
        ChartFile file = ChartFileCodec.Parse(json);
        Require(file.chartDatas[0].isEffect && !string.IsNullOrEmpty(file.chartDatas[0].effectId), "Legacy Effect identity was not assigned.");
        Require(string.IsNullOrEmpty(file.chartDatas[0].effectTypeId), "Legacy Effect was guessed into an executable effect.");
        var build = ChartHolderDocumentAdapter.Build(file.chartDatas, file.BaseBpm, 4);
        Require(!ChartCompiler.Compile(build.Document).Succeeded, "Unresolved legacy Effect must not play.");
    }

    private static void UnclosedLongSave_DoesNotMutateSource()
    {
        var holder = new ChartHolder(0, 0);
        holder.noteTypes[0] = NoteType.LongTap;
        holder.noteHandles[0] = NoteHandleType.Left;
        holder.noteTypes[4] = NoteType.LongScratch;
        holder.scratchPointTypes[0] = ScratchPointType.Start;
        ChartFile saved = ChartFileCodec.Parse(ChartFileCodec.Serialize(new[] { holder }, 120, 0));
        Require(saved.chartDatas[0].noteTypes[0] == NoteType.Tap && saved.chartDatas[0].noteTypes[4] == NoteType.Scratch,
            "Unclosed Long notes were not normalized to single notes in the saved representation.");
        Require(holder.noteTypes[0] == NoteType.LongTap && holder.noteTypes[4] == NoteType.LongScratch,
            "Codec serialization mutated the editor's original notes.");
    }

    private static void EffectPair_RoundTripAndBackupRecovery()
    {
        WithTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "sample.rd");
            WritePair(path, "song", "revision1", 1);
            WritePair(path, "song", "revision2", 2);
            ChartFile current = ChartEffectFileStore.Load(path, out bool recovered, out _);
            Require(!recovered && current.EffectRevision == "revision2", "Latest matching pair did not load.");
            Require(current.FormatVersion == 8 &&
                    current.chartDatas[0].effectId == "fx_sample" &&
                    current.chartDatas[0].effectTypeId == "camera.offset",
                "Older paired chart lost its executable Effect definition.");
            Require(((CameraEffectParameters)ChartEffectJsonCodec.BuildParameterMap(current.chartDatas, "")["fx_sample"]).OffsetX == 2,
                "The ID no longer points to its settings.");
            string parameterPath = ChartEffectFileStore.GetParameterPath(path, "song", "default");
            // Simulate a torn pair using only disposable synthetic test files.
            File.WriteAllText(parameterPath, File.ReadAllText(parameterPath).Replace("revision2", "torn"));
            ChartFile backup = ChartEffectFileStore.Load(path, out recovered, out _);
            Require(recovered && backup.EffectRevision == "revision1", "Matching previous pair was not recovered.");
        });
    }

    private static void EffectPair_ProtectsBackupOnlyOwnership()
    {
        WithTemporaryDirectory(directory =>
        {
            string ownerPath = Path.Combine(directory, "owner.rd");
            string otherPath = Path.Combine(directory, "other.rd");
            WritePair(ownerPath, "song", "owner-revision", 1);
            string parameterPath = ChartEffectFileStore.GetParameterPath(
                ownerPath, "song", "default");
            string backupPath = parameterPath + ".bak";
            File.Move(parameterPath, backupPath);
            string originalBackup = File.ReadAllText(backupPath);

            bool refused = false;
            try { WritePair(otherPath, "song", "other-revision", 2); }
            catch (IOException) { refused = true; }

            Require(refused, "A backup-only sidecar owned by another chart was not protected.");
            Require(!File.Exists(otherPath) && !File.Exists(parameterPath) &&
                    File.ReadAllText(backupPath) == originalBackup,
                "Protecting a backup-only sidecar changed the existing recovery data.");
        });
    }

    private static void InlineEffect_RoundTripInOneChartFile()
    {
        WithTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "hard.rd");
            var metadata = new ChartEffectDocumentState.Metadata(
                "song", "hard", "", "inline-revision");
            string text = ChartFileCodec.Serialize(
                new[] { EffectHolder(4) }, 120, 0, metadata);
            Require(text.Contains("\"formatVersion\": 1") &&
                    text.Contains("\"parameters\": {") &&
                    !text.Contains("\"hasEffectParameters\"") &&
                    text.IndexOf("\"musicId\"", StringComparison.Ordinal) <
                        text.IndexOf("\"notes\"", StringComparison.Ordinal) &&
                    text.IndexOf("\"notes\"", StringComparison.Ordinal) <
                        text.IndexOf("\"eventDictionary\"", StringComparison.Ordinal),
                "Version 1 chart did not put metadata before the renamed arrays.");
            ChartEffectFileStore.Save(path, text, null);
            ChartFile loaded = ChartEffectFileStore.Load(path,
                out bool recovered, out _);
            Require(!recovered && loaded.FormatVersion == 1 &&
                    !loaded.HasEffectParameterFile &&
                    loaded.chartDatas[0].effectTypeId == "camera.offset" &&
                    GetCameraOffset(loaded) == 4 &&
                    !File.Exists(ChartEffectFileStore.GetParameterPath(
                        path, "song", "hard")),
                "Version 1 chart did not reload its Effect from one .rd file.");
            RuntimeChartPackage package = RuntimeChartPackageCodec.Import(
                ChartMakerRuntimePackageExporter.Export(text));
            Require(package.Snapshot.EffectEvents.Count == 1 &&
                    ((CameraEffectParameters)DecodeRuntimeEffect(
                        package, "fx_sample")).OffsetX == 4,
                "Runtime export lost the inline Effect parameters.");

            string changed = ChartFileCodec.Serialize(
                new[] { EffectHolder(6) }, 120, 0, metadata);
            ChartEffectFileStore.Save(path, changed, null);
            string invalid = changed.Replace("\"durationMs\": 400",
                "\"durationMs\": -1");
            Require(invalid != changed,
                "Synthetic invalid Effect did not change the chart text.");
            File.WriteAllText(path, invalid);
            ChartFile backup = ChartEffectFileStore.Load(path,
                out recovered, out _);
            Require(recovered && GetCameraOffset(backup) == 4,
                "An invalid inline Effect did not recover the previous .rd backup.");
        });
    }

    private static void EffectPair_FirstSaveFailureRollsBackForRetry()
    {
        WithTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "blocked.rd");
            // A directory at the exact chart path lets the sidecar install succeed
            // before the chart move fails, without touching a user file.
            Directory.CreateDirectory(path);
            BuildPairText("song", "revision1", 1,
                out string chartText, out string parameterText);
            bool failed = false;
            try { ChartEffectFileStore.Save(path, chartText, parameterText); }
            catch (IOException) { failed = true; }
            Require(failed, "The synthetic first-save chart replacement unexpectedly succeeded.");
            string parameterPath = ChartEffectFileStore.GetParameterPath(
                path, "song", "default");
            Require(!File.Exists(parameterPath),
                "A failed first save left an orphan Effect JSON that blocks retry.");

            Directory.Delete(path);
            ChartEffectFileStore.Save(path, chartText, parameterText);
            ChartFile loaded = ChartEffectFileStore.Load(path,
                out bool recovered, out _);
            Require(!recovered && loaded.EffectRevision == "revision1",
                "Retry after first-save rollback did not produce a current matching pair.");
        });
    }

    private static void EffectPair_MidSaveFailureRecoversAndResaves()
    {
        WithTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "sample.rd");
            WritePair(path, "song", "revision1", 1);
            BuildPairText("song", "revision2", 2,
                out string chartText, out string parameterText);

            bool failed = false;
            using (new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.Read))
            {
                try { ChartEffectFileStore.Save(path, chartText, parameterText); }
                catch (IOException) { failed = true; }
            }
            Require(failed,
                "The synthetic locked-chart replacement unexpectedly succeeded.");

            ChartFile previous = ChartEffectFileStore.Load(path,
                out bool recovered, out _);
            Require(recovered && previous.EffectRevision == "revision1" &&
                    GetCameraOffset(previous) == 1,
                "A one-file replacement failure did not recover the previous matching pair.");

            WritePair(path, "song", "revision3", 3);
            ChartFile current = ChartEffectFileStore.Load(path,
                out recovered, out _);
            Require(!recovered && current.EffectRevision == "revision3" &&
                    GetCameraOffset(current) == 3,
                "A recovered pair could not be explicitly resaved as the current pair.");
        });
    }

    private static void EffectPair_CorruptCurrentFailureKeepsMatchingBackup()
    {
        WithTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "sample.rd");
            WritePair(path, "song", "revision1", 1);
            WritePair(path, "song", "revision2", 2);
            File.WriteAllText(path, "{broken current chart",
                new System.Text.UTF8Encoding(false));
            BuildPairText("song", "revision3", 3,
                out string chartText, out string parameterText);

            bool failed = false;
            using (new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.Read))
            {
                try { ChartEffectFileStore.Save(path, chartText, parameterText); }
                catch (IOException) { failed = true; }
            }
            Require(failed,
                "The synthetic corrupt-current replacement unexpectedly succeeded.");
            ChartFile recoveredChart = ChartEffectFileStore.Load(path,
                out bool recovered, out _);
            Require(recovered && recoveredChart.EffectRevision == "revision1" &&
                    GetCameraOffset(recoveredChart) == 1,
                "Saving over a corrupt current file destroyed the last matching backup pair.");
        });
    }

    private static void FileLoader_RecoversMissingOrCorruptCurrentAndStaysDirty()
    {
        WithTemporaryDirectory(directory =>
        {
            Require(ChartManager.ChartHolders.Count == 0,
                "Do not run the isolated loader check with a live editor document.");
            Type recentFiles = typeof(ChartManager).Assembly.GetType(
                "ChartMakerRecentFiles", true);
            string previousRecent = (string)recentFiles.GetProperty(
                "LastChartPath").GetValue(null);
            var previousMetadata = ChartEffectDocumentState.Capture();
            string path = Path.Combine(directory, "sample.rd");
            WritePair(path, "song", "revision1", 1);
            WritePair(path, "song", "revision2", 2);
            var go = new GameObject("Effect backup loader baseline (inactive)");
            go.SetActive(false);
            try
            {
                var core = go.AddComponent<ChartCore>();
                var saver = go.AddComponent<ChartToFile>();
                var loader = go.AddComponent<FileToChart>();
                SetField(saver, "chartCore", core);
                SetField(loader, "chartCore", core);
                SetField(loader, "chartToFile", saver);

                File.Delete(path);
                ChartFile missingCurrent = loader.LoadFromPath(path);
                Require(missingCurrent.EffectRevision == "revision1" &&
                        saver.HasUnsavedChanges,
                    "A missing current chart did not recover its pair as an unsaved state.");

                File.WriteAllText(path, "{broken current chart", new System.Text.UTF8Encoding(false));
                ChartFile corruptCurrent = loader.LoadFromPath(path);
                Require(corruptCurrent.EffectRevision == "revision1" &&
                        saver.HasUnsavedChanges,
                    "A corrupt current chart did not recover its pair as an unsaved state.");
            }
            finally
            {
                ChartManager.ClearChart(false);
                ChartEffectDocumentState.Restore(previousMetadata);
                if (string.IsNullOrWhiteSpace(previousRecent))
                    Invoke(recentFiles.GetMethod("ForgetChartPath"), null);
                else
                    Invoke(recentFiles.GetMethod("RememberChartPath"), null,
                        new object[] { previousRecent });
                UnityEngine.Object.DestroyImmediate(go);
            }
        });
    }

    private static void EffectPair_InvalidCurrentSettingsRecoverOrLeaveEditorUntouched()
    {
        WithTemporaryDirectory(directory =>
        {
            Require(ChartManager.ChartHolders.Count == 0,
                "Do not run the isolated semantic-load check with a live editor document.");
            Type recentFiles = typeof(ChartManager).Assembly.GetType(
                "ChartMakerRecentFiles", true);
            string previousRecent = (string)recentFiles.GetProperty(
                "LastChartPath").GetValue(null);
            var previousMetadata = ChartEffectDocumentState.Capture();
            var go = new GameObject(
                "Effect semantic loader acceptance (inactive)");
            go.SetActive(false);

            try
            {
                var core = go.AddComponent<ChartCore>();
                var saver = go.AddComponent<ChartToFile>();
                var loader = go.AddComponent<FileToChart>();
                SetField(saver, "chartCore", core);
                SetField(loader, "chartCore", core);
                SetField(loader, "chartToFile", saver);

                string recoveryPath = Path.Combine(directory, "recover.rd");
                WritePair(recoveryPath, "recover_song", "valid-backup", 1);
                WritePair(recoveryPath, "recover_song", "invalid-current", 2);
                string recoveryParameters = ChartEffectFileStore.GetParameterPath(
                    recoveryPath, "recover_song", "default");
                MakeCameraOffsetSemanticallyInvalid(recoveryParameters, 2);

                ChartFile recovered = loader.LoadFromPath(recoveryPath);
                Require(recovered.EffectRevision == "valid-backup" &&
                        GetCameraOffset(recovered) == 1 &&
                        saver.HasUnsavedChanges,
                    "A semantically invalid current pair did not recover the last valid matching backup as dirty.");

                ChartManager.ClearChart(false);
                ChartHolder live = ChartManager.GetOrCreateHolder(3, 120);
                live.isMarker = true;
                ChartEffectDocumentState.Restore(
                    new ChartEffectDocumentState.Metadata(
                        "live_song", "live_difficulty", "", "live-revision"));
                core.SetBpm(137d);
                core.SetStartCorrectionMs(-321d);
                string liveSavePath = saver.CurrentFilePath;

                string invalidOnlyPath = Path.Combine(directory, "invalid-only.rd");
                WritePair(invalidOnlyPath, "invalid_song", "invalid-only", 3);
                string invalidOnlyParameters =
                    ChartEffectFileStore.GetParameterPath(
                        invalidOnlyPath, "invalid_song", "default");
                MakeCameraOffsetSemanticallyInvalid(invalidOnlyParameters, 3);

                bool loaded = loader.TryLoadFromPath(
                    invalidOnlyPath, out _, out string error);
                Require(!loaded && !string.IsNullOrWhiteSpace(error),
                    "A semantically invalid pair without a valid backup was accepted.");
                Require(ChartManager.ChartHolders.Count == 1 &&
                        ChartManager.ChartHolders[0].isMarker &&
                        ChartManager.ChartHolders[0].AbsoluteChartPosition ==
                        3 * ChartHolder.PositionUnitsPerMeasure + 120,
                    "A failed semantic load partially replaced the live editor chart.");
                Require(ChartEffectDocumentState.MusicId == "live_song" &&
                        ChartEffectDocumentState.DifficultyId ==
                        "live_difficulty" &&
                        ChartEffectDocumentState.Revision == "live-revision",
                    "A failed semantic load partially replaced live document metadata.");
                Require(core.Bpm == 137d &&
                        core.StartCorrectionMs == -321d &&
                        saver.CurrentFilePath == liveSavePath,
                    "A failed semantic load changed live timing data or its save target.");
                Require((string)recentFiles.GetProperty("LastChartPath")
                            .GetValue(null) == Path.GetFullPath(recoveryPath),
                    "A failed semantic load replaced the most recent successful chart path.");
            }
            finally
            {
                ChartManager.ClearChart(false);
                ChartEffectDocumentState.Restore(previousMetadata);
                RestoreRecentChartPath(recentFiles, previousRecent);
                UnityEngine.Object.DestroyImmediate(go);
            }
        });
    }

    private static void ParameterTypes_RejectMalformedValues()
    {
        foreach (string json in new[] { "{}", "{\"durationMs\":\"400\"}", "{\"durationMs\":-1}",
            "{\"durationMs\":1e999}", "{\"durationMs\":1,\"durationMs\":2}",
            "{\"durationMs\":400,\"attackMs\":-1}",
            "{\"durationMs\":400,\"releaseMs\":-1}",
            "{\"durationMs\":400,\"attackMs\":1e999}",
            "{\"durationMs\":400,\"releaseMs\":1e999}",
            "{\"durationMs\":400,\"attackMs\":250,\"releaseMs\":200}",
            "{\"durationMs\":1,\"typo\":1}" })
            Require(!ChartEffectJsonCodec.TryDecode("camera.offset", "", "", json, out _, out _),
                "Malformed parameters were accepted: " + json);
        Require(ChartEffectJsonCodec.TryDecode(
                    "camera.offset",
                    "",
                    "",
                    "{\"durationMs\":400,\"attackMs\":100,\"releaseMs\":100}",
                    out object cameraValue,
                    out _) &&
                cameraValue is CameraEffectParameters camera &&
                camera.AttackMs == 100d &&
                camera.ReleaseMs == 100d,
            "Valid camera easing parameters were not decoded.");
        Require(ChartEffectJsonCodec.TryDecode(
                    "camera.offset", "", "", "{\"durationMs\":400}",
                    out object legacyCameraValue, out _) &&
                legacyCameraValue is CameraEffectParameters legacyCamera &&
                legacyCamera.AttackMs == 0d &&
                legacyCamera.ReleaseMs == 0d,
            "A camera setting without easing fields lost legacy snap compatibility.");
        Require(ChartEffectJsonCodec.TryDecode(
                    "camera.offset", "", "",
                    "{\"durationMs\":400,\"attackMs\":200,\"releaseMs\":200}",
                    out object boundaryCameraValue, out _) &&
                boundaryCameraValue is CameraEffectParameters boundaryCamera &&
                boundaryCamera.AttackMs + boundaryCamera.ReleaseMs ==
                    boundaryCamera.DurationMs,
            "A valid camera envelope that exactly fills its duration was rejected.");
        Require(ChartEffectJsonCodec.TryDecode(
                    "camera.offset", "", "",
                    ChartEffectJsonCodec.GetDefaultJson(
                        "camera.offset", "", ""),
                    out object defaultCameraValue, out _) &&
                defaultCameraValue is CameraEffectParameters defaultCamera &&
                defaultCamera.AttackMs == 100d &&
                defaultCamera.ReleaseMs == 100d,
            "New camera settings do not default to a smooth envelope.");
        Require(ChartEffectJsonCodec.TryDecode("music.call", "count-success", "sample", "", out object parameters, out _) && parameters == null,
            "A parameterless command incorrectly requires an empty settings object.");
    }

    private static void ParameterSave_ValidatesKnownAndPreservesUnresolved()
    {
        ChartHolder known = EffectHolder(10001);
        var metadata = new ChartEffectDocumentState.Metadata(
            "song", "default", "", "revision");
        bool rejected = false;
        try
        {
            ChartEffectFileStore.SerializeParameters(new[] { known }, metadata);
        }
        catch (FormatException)
        {
            rejected = true;
        }
        Require(rejected,
            "Save serialization accepted a known parameter range rejected by runtime.");

        ChartHolder unresolved = EffectHolder(1);
        unresolved.effectTypeId = string.Empty;
        unresolved.effectCommandId = string.Empty;
        string preserved = ChartEffectFileStore.SerializeParameters(
            new[] { unresolved }, metadata);
        Require(preserved.Contains("fx_sample"),
            "An unresolved legacy Effect could not be preserved for later editing.");
    }

    private static void Judgement_DelayedInputBeatsFrameMiss()
    {
        WithJudgement((system, rule) =>
        {
            Require(InitializeJudgement(system, new[] { Note("tap", 0, 100) }), "Initialization failed.");
            var results = new List<NoteJudgementEvent>();
            system.NoteJudged += results.Add;
            Enqueue(system, 0, 100, 0);
            system.ProcessFrame(400);
            Require(results.Count == 1 && results[0].Result == JudgeResult.Perfect && !results[0].IsAutomaticMiss,
                "An on-time queued input became an automatic Miss when its frame was late.");
        });
    }

    private static void Judgement_InputDeliveredAfterPreviousFrame()
    {
        WithJudgement((system, rule) =>
        {
            Require(InitializeJudgement(system,
                new[] { Note("late-delivery", 0, 100) }),
                "Initialization failed.");
            var plan = EffectPreparation.Prepare(
                Array.Empty<PlayableEffectEvent>(), null,
                EffectRegistry.CreateDefault()).Plan;
            using var runner = new EffectRunner(plan,
                new EffectSessionContext("test", "default",
                    EffectExecutionMode.Gameplay));
            system.AttachEffectRunner(runner);
            var results = new List<NoteJudgementEvent>();
            system.NoteJudged += results.Add;

            system.ProcessFrame(120d);
            Enqueue(system, 0, 100d, 0);
            system.ProcessFrame(140d);
            Require(system.LastTimelineError == null &&
                    system.DiscardedLateInputCount == 0 &&
                    results.Count == 1 &&
                    results[0].Result == JudgeResult.Perfect,
                "A device input delivered after the previous frame " +
                "incorrectly stopped playback or lost its original time.");
        });

        WithJudgement((system, rule) =>
        {
            Require(InitializeJudgement(system,
                new[] { Note("effect-boundary", 0, 100) }),
                "Initialization failed.");
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("record",
                "Record", null, false, null, null,
                _ => new RecordingEffect(() => { })));
            var document = new ChartDocument(4800, 4, 120);
            document.EffectEvents.Add(new ChartEffectEvent(264,
                "boundary", "record", "", 0));
            var plan = EffectPreparation.Prepare(
                ChartCompiler.Compile(document).Snapshot.EffectEvents,
                null, registry).Plan;
            using var runner = new EffectRunner(plan,
                new EffectSessionContext("test", "default",
                    EffectExecutionMode.Gameplay));
            system.AttachEffectRunner(runner);
            system.ProcessFrame(120d);
            Enqueue(system, 0, 100d, 0);
            system.ProcessFrame(140d);
            Require(system.LastTimelineError == null &&
                    system.DiscardedLateInputCount == 1 &&
                    system.PendingNoteCount == 1,
                "An input older than a committed Effect was replayed " +
                "or stopped the entire song.");
        });
    }

    private static void Judgement_OffsetsAndExactMissBoundary()
    {
        WithJudgement((system, rule) =>
        {
            InitializeJudgement(system, new[] { Note("tap", 0, 100) }, 100);
            system.SetUserOffsetMs(25);
            system.ProcessFrame(325); // 100 note + 100 chart offset + 25 input offset + 100 miss window.
            Require(system.PendingNoteCount == 1, "Automatic Miss must be strictly outside its window.");
            system.ProcessFrame(325.01);
            Require(system.PendingNoteCount == 0, "Automatic Miss did not advance beyond its boundary.");
            system.ResetJudgements(false);
            JudgeResult received = JudgeResult.None;
            system.NoteJudged += value => received = value.Result;
            Enqueue(system, 0, 125, 0); // queued chart time = input song time 225 - chart offset 100.
            system.ProcessFrame(300);
            Require(received == JudgeResult.Perfect, "Chart/input offsets were double-applied or reversed.");
        });
    }

    private static void Judgement_DirectAndIndirectWindows()
    {
        WithJudgement((system, rule) =>
        {
            Require(InitializeJudgement(system,
                    new[] { Note("tap", 0, 100) }),
                "Judgement initialization failed.");
            JudgeResult received = JudgeResult.None;
            system.NoteJudged += value => received = value.Result;
            foreach (var sample in new[]
            {
                (Time: 50d, Expected: JudgeResult.Perfect),
                (Time: 49d, Expected: JudgeResult.Good),
                (Time: 0d, Expected: JudgeResult.Good),
                (Time: 150d, Expected: JudgeResult.Perfect),
                (Time: 151d, Expected: JudgeResult.Good),
                (Time: 200d, Expected: JudgeResult.Good),
                (Time: 201d, Expected: JudgeResult.Miss)
            })
            {
                system.ResetJudgements(false);
                received = JudgeResult.None;
                Enqueue(system, 0, sample.Time, 0);
                system.ProcessFrame(Math.Max(sample.Time, 201d));
                Require(received == sample.Expected,
                    "Direct/indirect judgement boundary changed at " +
                    sample.Time + "ms.");
            }
        });
    }

    private static void Judgement_EffectBeforeSameTimeInput()
    {
        WithJudgement((system, rule) =>
        {
            InitializeJudgement(system, new[] { Note("tap", 0, 100) });
            var order = new List<string>();
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("record", "Record", null, false, null, null,
                _ => new RecordingEffect(() => order.Add("effect"))));
            var document = new ChartDocument(4800, 4, 120);
            document.EffectEvents.Add(new ChartEffectEvent(240, "fx", "record", "", 0));
            var plan = EffectPreparation.Prepare(ChartCompiler.Compile(document).Snapshot.EffectEvents, null, registry).Plan;
            using var runner = new EffectRunner(plan, new EffectSessionContext("test", "default", EffectExecutionMode.Preview));
            system.AttachEffectRunner(runner);
            system.NoteJudged += _ => order.Add("input");
            Enqueue(system, 0, 100, 0);
            system.ProcessFrame(300);
            Require(string.Join(",", order) == "effect,input", "Same-time effect/input ordering changed.");
        });
    }

    private static void Judgement_DelayedFrameUsesActualTimeAndTotalOrder()
    {
        WithJudgement((system, rule) =>
        {
            Require(InitializeJudgement(system, new[]
            {
                Note("input-second", 0, 250),
                Note("input-first", 1, 250),
                Note("automatic", 2, 150)
            }), "Initialization failed.");
            var order = new List<string>();
            var times = new List<string>();
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration(
                "record-context", "Record context", null, false, null, null,
                _ => new ContextRecordingEffect(context =>
                {
                    order.Add(context.EffectId);
                    times.Add(context.ScheduledTimeMs + ":" + context.CurrentTimeMs);
                })));
            var document = new ChartDocument(4800, 4, 120);
            // 600 positions at 120 BPM is 250 ms. Insert in reverse order to prove
            // that explicit order, not collection order, wins.
            document.EffectEvents.Add(new ChartEffectEvent(
                600, "effect-second", "record-context", "", 1));
            document.EffectEvents.Add(new ChartEffectEvent(
                600, "effect-first", "record-context", "", 0));
            var prepared = EffectPreparation.Prepare(
                ChartCompiler.Compile(document).Snapshot.EffectEvents,
                null, registry);
            Require(prepared.Succeeded, "Effect preparation failed.");
            using var runner = new EffectRunner(prepared.Plan,
                new EffectSessionContext("test", "default",
                    EffectExecutionMode.Gameplay));
            system.AttachEffectRunner(runner);
            system.NoteJudged += value => order.Add(
                value.IsAutomaticMiss ? "automatic" : value.NoteId);
            // Same timestamp: sequence zero must win even though its lane is higher.
            Enqueue(system, 1, 250, 0);
            Enqueue(system, 0, 250, 1);
            system.ProcessFrame(300);

            Require(string.Join(",", order) ==
                    "effect-first,effect-second,input-first,input-second,automatic",
                "Same-time order is not Effects(explicit order) -> inputs(arrival) -> automatic Miss.");
            Require(string.Join(",", times) == "250:300,250:300",
                "A delayed frame lost the distinction between scheduled and actual processing time.");
            Require(runner.TriggeredCount == 2,
                "A delayed frame retriggered or skipped an Effect.");
        });
    }

    private static void Judgement_RuleIntervalBoundariesAreExact()
    {
        WithJudgement((system, rule) =>
        {
            var stagedRules = new EffectRuleService(null);
            stagedRules.AddDamageMultiplier("prepared", 2, 0);
            Require(rule.Modifiers.Count == 0,
                "A prepared session exposed its rule modifier before commit.");
            stagedRules.Activate(rule);
            Require(rule.Modifiers.Count == 1,
                "Committing a prepared session did not install its rule modifier.");
            stagedRules.Dispose();
            Require(rule.Modifiers.Count == 0,
                "Committed session cleanup left its rule modifier installed.");

            using var rules = new EffectRuleService(rule);
            IEffectRuleHandle handle = rules.AddDamageMultiplier(
                "boundary", 1.5, 100);
            Require(rule.GetHealthDelta(JudgeResult.Miss,
                        new RuleContext(100, 0, NoteType.Tap, false, false,
                            false, 99.999)) == -10 &&
                    rule.GetHealthDelta(JudgeResult.Miss,
                        new RuleContext(100, 0, NoteType.Tap, false, false,
                            false, 100)) == -15,
                "A rule change was applied before its scheduled time.");
            handle.ReleaseAt(200);
            Require(rule.GetHealthDelta(JudgeResult.Miss,
                        new RuleContext(100, 0, NoteType.Tap, false, false,
                            false, 199.999)) == -15 &&
                    rule.GetHealthDelta(JudgeResult.Miss,
                        new RuleContext(100, 0, NoteType.Tap, false, false,
                            false, 200)) == -10,
                "A released rule leaked across its end-time boundary.");
        });
    }

    private static void Judgement_TimelineFailureStillCompletesFrame()
    {
        WithJudgement((system, rule) =>
        {
            Require(InitializeJudgement(system, new[] { Note("tap", 0, 100) }),
                "Initialization failed.");
            var plan = EffectPreparation.Prepare(
                Array.Empty<PlayableEffectEvent>(), null,
                EffectRegistry.CreateDefault()).Plan;
            using var runner = new EffectRunner(plan,
                new EffectSessionContext("test", "default",
                    EffectExecutionMode.Gameplay));
            system.AttachEffectRunner(runner);
            int completed = 0;
            EffectRunner completedRunner = null;
            system.EffectFrameCompleted += value =>
            {
                completed++;
                completedRunner = value;
            };
            system.NoteJudged += _ => throw new InvalidOperationException(
                "synthetic judgement callback failure");
            Enqueue(system, 0, 100, 0);
            system.ProcessFrame(100);
            Require(completed == 1 && ReferenceEquals(completedRunner, runner),
                "A timeline exception skipped or misattributed frame completion.");
            Require(system.LastTimelineError != null &&
                    system.LastTimelineError.Contains(
                        "synthetic judgement callback failure"),
                "A judgement callback exception escaped without a timeline error.");

            system.ResetJudgements(false);
            Require(system.LastTimelineError == null,
                "A fresh judgement state retained the previous session error.");
        });
    }

    private static void EffectServices_RejectStaleCameraAndDuplicateTransition()
    {
        double appliedX = 0d;
        int applyCount = 0;
        var oldMixer = new EffectCameraMixer((x, y, roll) =>
        {
            appliedX = x;
            applyCount++;
        });
        IEffectCameraOffset oldHandle = oldMixer.CreateOffset("same-id");
        oldHandle.Set(2, 0, 0);
        oldMixer.Apply();
        oldMixer.Dispose();
        int countAfterDispose = applyCount;

        using var currentMixer = new EffectCameraMixer((x, y, roll) =>
        {
            appliedX = x;
            applyCount++;
        });
        currentMixer.CreateOffset("same-id").Set(4, 0, 0);
        currentMixer.Apply();
        oldMixer.Apply();
        Require(appliedX == 4d && applyCount == countAfterDispose + 1,
            "A disposed session's delayed camera Apply changed the current session.");

        using var mailbox = new EffectTransitionMailbox((music, difficulty) => true);
        Require(mailbox.RequestTransition("next", "hard",
                    EffectExecutionMode.Gameplay) &&
                !mailbox.RequestTransition("other", "normal",
                    EffectExecutionMode.Gameplay) &&
                mailbox.TargetMusicId == "next" &&
                mailbox.TargetDifficultyId == "hard",
            "A transition mailbox accepted or replaced a duplicate request.");
    }

    private static void Gameplay_ResumeKeepsSessionRestartReplacesIt()
    {
        var go = new GameObject("Effect gameplay lifecycle baseline (inactive)");
        go.SetActive(false);
        var config = ScriptableObject.CreateInstance<GameRuleConfig>();
        GameplayChartEffectController controller = null;
        try
        {
            var audio = go.AddComponent<AudioSource>();
            var play = go.AddComponent<GamePlay>();
            SetField(play, "audioSource", audio);
            var rule = go.AddComponent<DefaultGameRule>();
            typeof(GameRule).GetField("config",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rule, config);
            var manager = go.AddComponent<GameManager>();
            SetField(manager, "gamePlay", play);
            SetField(manager, "gameRule", rule);
            var system = go.AddComponent<NoteJudgementSystem>();
            SetField(system, "gameManager", manager);
            SetField(system, "gameRule", rule);
            Require(InitializeJudgement(system, Array.Empty<ChartDocumentNote>()),
                "Judgement initialization failed.");
            controller = go.AddComponent<GameplayChartEffectController>();
            SetField(controller, "gameManager", manager);
            SetField(controller, "judgementSystem", system);

            var holder = new ChartHolder(0, 0)
            {
                isEffect = true,
                effectId = "call",
                effectTypeId = "music.call",
                effectCommandId = "count-success",
                effectParametersJson = string.Empty
            };
            var document = new ChartDocument(4800, 4, 120);
            document.EffectEvents.Add(new ChartEffectEvent(
                0, "call", "music.call", "count-success", 0));
            PlayableChartSnapshot snapshot =
                ChartCompiler.Compile(document).Snapshot;
            PreparedEffectPlan preparedPlan =
                ChartEffectJsonCodec.PreparePlan(
                    snapshot,
                    new[] { holder },
                    "sample");
            Require(controller.Prepare(
                    preparedPlan,
                    "song",
                    "default",
                    0),
                "Gameplay Effect preparation failed: " + controller.LastError);

            MethodInfo starting = typeof(GameplayChartEffectController)
                .GetMethod("HandlePlaybackStarting",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo committing = typeof(GameplayChartEffectController)
                .GetMethod("HandlePlaybackCommitting",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo started = typeof(GameplayChartEffectController)
                .GetMethod("HandlePlaybackStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo aborted = typeof(GameplayChartEffectController)
                .GetMethod("HandlePlaybackStartAborted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo disable = typeof(GameplayChartEffectController)
                .GetMethod("OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo reason = typeof(GamePlay).GetProperty(
                nameof(GamePlay.StartReason));
            FieldInfo active = typeof(GameplayChartEffectController).GetField(
                "activeSession", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo pending = typeof(GameplayChartEffectController).GetField(
                "pendingSession", BindingFlags.Instance | BindingFlags.NonPublic);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "^song/default: Value cannot be null\\."));
            Require(!controller.Prepare(
                    null,
                    "song",
                    "default",
                    0),
                "A missing prepared Effect plan unexpectedly replaced the " +
                "runtime plan.");
            Require(!(bool)Invoke(starting, controller, new object[] { 0d }) &&
                    active.GetValue(controller) == null &&
                    pending.GetValue(controller) == null,
                "A failed Prepare left a stale Effect plan available for playback.");
            Require(controller.Prepare(
                    preparedPlan,
                    "song",
                    "default",
                    0),
                "Valid Effect preparation could not recover after a rejected plan: " +
                controller.LastError);

            reason.SetValue(play, PlaybackStartReason.Play);
            Require((bool)Invoke(starting, controller, new object[] { 0d }),
                "Prepared Effect session start was rejected.");
            Invoke(aborted, controller, new object[] { 0d });
            Require(active.GetValue(controller) == null &&
                    pending.GetValue(controller) == null,
                "An aborted playback start retained or committed an Effect session.");

            Require((bool)Invoke(starting, controller, new object[] { 0d }),
                "Initial Effect session start was rejected.");
            Require((bool)Invoke(committing, controller, new object[] { 0d }),
                "Initial Effect session commit was rejected.");
            Invoke(started, controller, new object[] { 0d });
            object firstSession = active.GetValue(controller);
            EffectRunner firstRunner = GetRuntimeRunner(firstSession);

            reason.SetValue(play, PlaybackStartReason.Resume);
            Require((bool)Invoke(starting, controller, new object[] { 200d }),
                "Paused resume was rejected.");
            Require((bool)Invoke(committing, controller, new object[] { 200d }),
                "Paused resume commit was rejected.");
            Invoke(started, controller, new object[] { 200d });
            Require(ReferenceEquals(firstSession, active.GetValue(controller)),
                "Resume replaced the session instead of preserving its state.");

            reason.SetValue(play, PlaybackStartReason.Restart);
            Require((bool)Invoke(starting, controller, new object[] { 0d }),
                "Paused restart was rejected.");
            Require((bool)Invoke(committing, controller, new object[] { 0d }),
                "Paused restart commit was rejected.");
            Invoke(started, controller, new object[] { 0d });
            object restartedSession = active.GetValue(controller);
            EffectRunner restartedRunner = GetRuntimeRunner(restartedSession);
            Require(!ReferenceEquals(firstSession, restartedSession) &&
                    !ReferenceEquals(firstRunner.Gimmick, restartedRunner.Gimmick) &&
                    !firstRunner.AdvanceTo(100),
                "Restart reused the old session/gimmick or left the old runner active.");
            Require(ReferenceEquals(
                    typeof(NoteJudgementSystem).GetField("effectRunner",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(system), restartedRunner),
                "Judgement remained attached to an earlier Effect session.");

            Invoke(disable, controller);
            Require(!(bool)Invoke(starting, controller, new object[] { 0d }) &&
                    active.GetValue(controller) == null &&
                    pending.GetValue(controller) == null,
                "A disabled Gameplay Effect bridge accepted a new session or retained runtime state.");
        }
        finally
        {
            if (controller != null)
                Invoke(typeof(GameplayChartEffectController).GetMethod(
                    "OnDisable", BindingFlags.Instance | BindingFlags.NonPublic),
                    controller);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static void Gameplay_CommitRejectionBlocksSchedulingAndReentrancy()
    {
        var go = new GameObject("Gameplay start transaction baseline (inactive)");
        go.SetActive(false);
        AudioClip song = null;
        try
        {
            var audio = go.AddComponent<AudioSource>();
            var play = go.AddComponent<GamePlay>();
            SetField(play, "audioSource", audio);
            song = AudioClip.Create("Effect start transaction baseline", 4410, 1,
                44100, false);
            Require(play.PrepareSong(song), "Synthetic song preparation failed.");

            bool nestedStartResult = true;
            int startingCount = 0;
            int committingCount = 0;
            int abortedCount = 0;
            Func<double, bool> recursiveStarting = _ =>
            {
                startingCount++;
                nestedStartResult = play.Restart();
                return true;
            };
            Func<double, bool> rejectingCommit = _ =>
            {
                committingCount++;
                return false;
            };
            Action<double> rejectionAborted = _ => abortedCount++;
            play.PlaybackStarting += recursiveStarting;
            play.PlaybackCommitting += rejectingCommit;
            play.PlaybackStartAborted += rejectionAborted;

            bool startResult = play.Play();
            play.PlaybackStarting -= recursiveStarting;
            play.PlaybackCommitting -= rejectingCommit;
            play.PlaybackStartAborted -= rejectionAborted;
            Require(!startResult && !nestedStartResult,
                "A rejected commit or recursive playback start reported success.");
            Require(startingCount == 1 && committingCount == 1 &&
                    abortedCount == 1,
                "Playback start transaction callbacks ran an unexpected number of times.");
            Require(play.State == PlaybackState.Ready && !audio.isPlaying,
                "Commit rejection scheduled audio or left playback outside Ready state.");

            int cancelledAbortedCount = 0;
            Func<double, bool> cancellingCommit = _ =>
            {
                play.Stop();
                return true;
            };
            Action<double> cancellationAborted = _ => cancelledAbortedCount++;
            play.PlaybackCommitting += cancellingCommit;
            play.PlaybackStartAborted += cancellationAborted;

            bool cancelledStartResult = play.Play();
            play.PlaybackCommitting -= cancellingCommit;
            play.PlaybackStartAborted -= cancellationAborted;
            Require(!cancelledStartResult && cancelledAbortedCount == 1,
                "Stop during commit did not cancel the in-progress start exactly once.");
            Require(play.State == PlaybackState.Ready && !audio.isPlaying,
                "Stop during commit still scheduled audio or changed the Ready state.");

            const double heldBetweenSamplesMs = 0.01d;
            double observedResumeTimeMs = double.NegativeInfinity;
            SetField(play, "heldSongTimeMs", heldBetweenSamplesMs);
            typeof(GamePlay).GetProperty(nameof(GamePlay.State))
                .SetValue(play, PlaybackState.Paused);
            Func<double, bool> captureResumeStart = startMs =>
            {
                observedResumeTimeMs = startMs;
                return true;
            };
            Func<double, bool> rejectResumeCommit = _ => false;
            play.PlaybackStarting += captureResumeStart;
            play.PlaybackCommitting += rejectResumeCommit;
            bool resumeResult = play.Resume();
            play.PlaybackStarting -= captureResumeStart;
            play.PlaybackCommitting -= rejectResumeCommit;
            Require(!resumeResult &&
                    observedResumeTimeMs >= heldBetweenSamplesMs,
                "Sample alignment moved the logical Resume timeline backwards.");

            typeof(GamePlay).GetProperty(nameof(GamePlay.State))
                .SetValue(play, PlaybackState.Playing);
            bool stateCallbackRestartResult = true;
            int readyNotificationCount = 0;
            Action<PlaybackState> restartOnReady = state =>
            {
                if (state != PlaybackState.Ready) return;
                readyNotificationCount++;
                stateCallbackRestartResult = play.Restart();
            };
            play.PlaybackStateChanged += restartOnReady;

            play.Stop();
            play.PlaybackStateChanged -= restartOnReady;
            Require(readyNotificationCount == 1 && !stateCallbackRestartResult,
                "A Ready-state listener resurrected playback during Stop notification.");
            Require(play.State == PlaybackState.Ready && !audio.isPlaying,
                "External Stop did not finish in Ready state with audio stopped.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (song != null) UnityEngine.Object.DestroyImmediate(song);
        }
    }

    private static void ChartPreview_StartStopTransactionsAreReentrancySafe()
    {
        var go = new GameObject("Chart preview start transaction baseline (inactive)");
        go.SetActive(false);
        AudioClip song = null;
        try
        {
            var audio = go.AddComponent<AudioSource>();
            var core = go.AddComponent<ChartCore>();
            SetField(core, "audioSource", audio);
            song = AudioClip.Create("Chart preview transaction baseline", 4410,
                1, 44100, false);
            audio.clip = song;

            int startingCount = 0;
            int abortedCount = 0;
            long startingAttemptId = 0L;
            long abortedAttemptId = 0L;
            Func<double, bool> recursiveAndCancellingStart = _ =>
            {
                startingCount++;
                startingAttemptId = core.CurrentTestPlaybackAttemptId;
                core.StartTestPlay(25d);
                core.EndTestPlay();
                return true;
            };
            Action aborted = () =>
            {
                abortedCount++;
                abortedAttemptId = core.CurrentTestPlaybackAttemptId;
            };
            core.TestPlaybackStarting += recursiveAndCancellingStart;
            core.TestPlaybackStartAborted += aborted;

            core.StartTestPlay(25d);

            core.TestPlaybackStarting -= recursiveAndCancellingStart;
            core.TestPlaybackStartAborted -= aborted;
            Require(startingCount == 1 && abortedCount == 1,
                "A recursive preview start entered validation or cancellation did not abort exactly once.");
            Require(startingAttemptId != 0L &&
                    abortedAttemptId == startingAttemptId &&
                    core.CurrentTestPlaybackAttemptId == 0L &&
                    core.ActiveTestPlaybackSessionId == 0L &&
                    !core.IsTestPlaying,
                "A cancelled preview start lost its attempt identity or committed playback.");

            core.StartTestPlay(25d);
            Require(core.IsTestPlaying &&
                    core.ActiveTestPlaybackSessionId != 0L,
                "A clean preview start did not commit a session identity.");

            int nestedValidationCount = 0;
            var manualOrder = new List<string>();
            Func<double, bool> countNestedValidation = _ =>
            {
                nestedValidationCount++;
                return true;
            };
            Action<bool> restartOnStopped = playing =>
            {
                manualOrder.Add("first:" + playing);
                if (!playing) core.StartTestPlay(50d);
            };
            Action<bool> recordTransition = playing =>
                manualOrder.Add("second:" + playing);
            Action<double> recordManualTime = value =>
                manualOrder.Add("time:" + value);
            core.TestPlaybackStarting += countNestedValidation;
            core.TestPlaybackChanged += restartOnStopped;
            core.TestPlaybackChanged += recordTransition;
            core.TestMsChanged += recordManualTime;

            core.EndTestPlay();

            core.TestPlaybackStarting -= countNestedValidation;
            core.TestPlaybackChanged -= restartOnStopped;
            core.TestPlaybackChanged -= recordTransition;
            core.TestMsChanged -= recordManualTime;
            Require(nestedValidationCount == 0 && !core.IsTestPlaying &&
                    core.ActiveTestPlaybackSessionId == 0L,
                "A playback-state callback resurrected preview during Stop.");
            Require(string.Join(",", manualOrder) ==
                    "first:False,second:False,time:0",
                "Manual preview stop changed transition values or rewind ordering: " +
                string.Join(",", manualOrder));

            core.StartTestPlay(25d);
            core.SetTestMs(200d);
            var naturalOrder = new List<string>();
            Action<double> recordNaturalTime = value =>
                naturalOrder.Add("time:" + value);
            Action<bool> recordNaturalState = value =>
                naturalOrder.Add("state:" + value);
            core.TestMsChanged += recordNaturalTime;
            core.TestPlaybackChanged += recordNaturalState;
            Invoke(typeof(ChartCore).GetMethod("CompleteTestPlayback",
                    BindingFlags.Instance | BindingFlags.NonPublic), core,
                new object[] { false });
            core.TestMsChanged -= recordNaturalTime;
            core.TestPlaybackChanged -= recordNaturalState;
            Require(string.Join(",", naturalOrder) == "time:100,state:False",
                "Natural preview completion no longer publishes final time before stop: " +
                string.Join(",", naturalOrder));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (song != null) UnityEngine.Object.DestroyImmediate(song);
        }
    }

    private static void ChartPreview_ReentrantEffectStopDoesNotUseClearedSession()
    {
        var go = new GameObject("Chart preview reentrant Effect baseline (inactive)");
        go.SetActive(false);
        AudioClip song = null;
        EffectRunner runner = null;
        try
        {
            var audio = go.AddComponent<AudioSource>();
            var core = go.AddComponent<ChartCore>();
            var preview = go.AddComponent<ChartTestPlay>();
            SetField(core, "audioSource", audio);
            SetField(preview, "chartCore", core);
            song = AudioClip.Create("Chart preview reentrant Effect baseline",
                4410, 1, 44100, false);
            audio.clip = song;
            core.StartTestPlay(0d);
            long sessionId = core.ActiveTestPlaybackSessionId;
            Require(sessionId != 0L, "Synthetic preview session did not start.");

            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration(
                "stop-preview", "Stop preview", null, false, null, null,
                _ => new RecordingEffect(core.EndTestPlay)));
            var document = new ChartDocument(4800, 4, 120);
            document.EffectEvents.Add(new ChartEffectEvent(
                0, "stop", "stop-preview", "", 0));
            PlayableChartSnapshot snapshot =
                ChartCompiler.Compile(document).Snapshot;
            PreparedEffectPlan plan = EffectPreparation.Prepare(
                snapshot.EffectEvents, null, registry).Plan;
            runner = new EffectRunner(plan,
                new EffectSessionContext("test", "default",
                    EffectExecutionMode.Preview));
            SetField(preview, "snapshot", snapshot);
            SetField(preview, "effectRunner", runner);
            SetField(preview, "effectSessionId", sessionId);
            SetField(core, "songClock", new DspSongClock(
                AudioSettings.dspTime - 1d, 0d));

            Action<bool> clearStoppedSession = playing =>
            {
                if (playing) return;
                SetField(preview, "effectRunner", null);
                SetField(preview, "effectSessionId", 0L);
                SetField(preview, "snapshot", null);
            };
            core.TestPlaybackChanged += clearStoppedSession;
            Invoke(typeof(ChartTestPlay).GetMethod("ApplyTimelinePosition",
                    BindingFlags.Instance | BindingFlags.NonPublic), preview,
                new object[] { 0d });
            core.TestPlaybackChanged -= clearStoppedSession;

            Require(!core.IsTestPlaying &&
                    core.ActiveTestPlaybackSessionId == 0L,
                "An Effect-triggered preview stop did not finish the session.");
        }
        finally
        {
            runner?.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
            if (song != null) UnityEngine.Object.DestroyImmediate(song);
        }
    }

    private static void ChartPreview_UnresolvedEffectUsesSetupGuidance()
    {
        Require(ChartManager.ChartHolders.Count == 0,
            "Do not run the unresolved Effect Preview check with a live editor document.");
        var go = new GameObject(
            "Unresolved Effect Preview guidance baseline (inactive)");
        go.SetActive(false);
        AudioClip song = null;

        try
        {
            var audio = go.AddComponent<AudioSource>();
            var core = go.AddComponent<ChartCore>();
            var preview = go.AddComponent<ChartTestPlay>();
            SetField(core, "audioSource", audio);
            SetField(preview, "chartCore", core);
            song = AudioClip.Create(
                "Unresolved Effect Preview guidance baseline",
                4410, 1, 44100, false);
            audio.clip = song;

            ChartHolder holder = ChartManager.GetOrCreateHolder(2, 24);
            holder.isEffect = true;
            holder.EnsureEffectIdentity();
            holder.effectTypeId = string.Empty;

            Invoke(typeof(ChartTestPlay).GetMethod(
                "BindEvents",
                BindingFlags.Instance | BindingFlags.NonPublic), preview);
            core.StartTestPlay(0d);

            Require(!core.IsTestPlaying &&
                    preview.CurrentSnapshot == null,
                "An unresolved Effect unexpectedly entered Preview.");
            Require(preview.LastEffectMessage != null &&
                    preview.LastEffectMessage.StartsWith(
                        "Preview needs setup:",
                        StringComparison.Ordinal) &&
                    preview.LastEffectMessage.Contains(
                        "measure 2, position 24") &&
                    preview.LastEffectMessage.Contains(
                        "Select an Effect Type and click Apply"),
                "An unresolved Effect did not produce actionable setup guidance.");
            Require(core.LastTestPlaybackError == preview.LastEffectMessage,
                "The Core did not publish the actionable setup guidance.");
        }
        finally
        {
            ChartManager.ClearChart(false);
            UnityEngine.Object.DestroyImmediate(go);
            if (song != null)
            {
                UnityEngine.Object.DestroyImmediate(song);
            }
        }
    }

    private static void Judgement_RuleChangeIsNotRetroactive()
    {
        WithJudgement((system, rule) =>
        {
            InitializeJudgement(system, new[] { Note("a", 0, 0), Note("b", 1, 100), Note("c", 2, 200) });
            var document = new ChartDocument(4800, 4, 120);
            document.EffectEvents.Add(new ChartEffectEvent(240, "begin", "music.call", "begin-section", 0));
            document.EffectEvents.Add(new ChartEffectEvent(480, "end", "music.call", "end-section", 0));
            var values = new Dictionary<string, object> { ["begin"] = new SampleSectionParameters(30, 1.5) };
            var plan = EffectPreparation.Prepare(ChartCompiler.Compile(document).Snapshot.EffectEvents, values, EffectRegistry.CreateDefault(), "sample").Plan;
            using var rules = new EffectRuleService(rule);
            using var runner = new EffectRunner(plan, new EffectSessionContext("test", "default", EffectExecutionMode.Gameplay,
                gameState: new EffectTestGameState(), rules: rules));
            system.AttachEffectRunner(runner);
            var deltas = new List<int>();
            system.NoteJudged += result => deltas.Add(rule.GetHealthDelta(result.Result,
                new RuleContext(100, 0, NoteType.Tap, false, false, false, result.EffectiveHitTimeMs + result.OffsetMs)));
            Enqueue(system, 0, 80, 0);
            Enqueue(system, 1, 180, 1);
            Enqueue(system, 2, 280, 2);
            system.ProcessFrame(300);
            Require(string.Join(",", deltas) == "-2,-3,-2", "Damage multiplier leaked before or after its effective interval.");
        });
    }

    private static void AutoPlay_ChronologicalAndResettable()
    {
        WithJudgement((system, rule) =>
        {
            InitializeJudgement(system, new[] { Note("late", 0, 200), Note("early", 1, 100) });
            system.SetAutoPlayEnabled(true);
            var ids = new List<string>();
            system.NoteJudged += value => ids.Add(value.NoteId);
            system.ProcessFrame(300);
            Require(string.Join(",", ids) == "early,late", "AutoPlay stopped processing in chronological order.");
            system.ResetJudgements(false);
            Require(system.PendingNoteCount == 2, "Reset failed to restore pending notes.");
        });
    }

    private static void SaveAsExisting_ProtectsOtherSidecar()
    {
        WithTemporaryDirectory(directory =>
        {
            string a = Path.Combine(directory, "a.rd"), b = Path.Combine(directory, "b.rd");
            WritePair(a, "songA", "a1", 1);
            WritePair(b, "songB", "b1", 2);
            string parameterA = ChartEffectFileStore.GetParameterPath(a, "songA", "default");
            string original = File.ReadAllText(parameterA);
            try { WritePair(b, "songA", "b2", 3); }
            catch (IOException) { /* Refusing a collision is a valid outcome. */ }
            Require(File.ReadAllText(parameterA) == original,
                "Overwriting existing b.rd with songA metadata changed a.rd's settings JSON.");
        });
    }

    private static void UndoRedo_PreserveLastSavedRevision()
    {
        Type history = typeof(ChartManager).Assembly.GetType("ChartEditHistory", true);
        Require(ChartManager.ChartHolders.Count == 0 && !(bool)history.GetProperty("CanUndo").GetValue(null) &&
            !(bool)history.GetProperty("CanRedo").GetValue(null), "Do not run this isolated history check with a live editor document/history.");
        var originalMetadata = ChartEffectDocumentState.Capture();
        var go = new GameObject("Effect baseline history (inactive)");
        go.SetActive(false);
        try
        {
            var placement = go.AddComponent<ChartPlacementController>();
            ChartHolder holder = ChartManager.GetOrCreateHolder(0, 0);
            holder.isEffect = true;
            holder.EnsureEffectIdentity();
            holder.effectTypeId = "camera.offset";
            holder.effectParametersJson = CameraJson(1);
            ChartEffectDocumentState.Revision = "saved0";
            object transaction = Invoke(history.GetMethod("BeginChange"), null, new object[] { new[] { 0 } });
            holder.effectParametersJson = CameraJson(2);
            Invoke(history.GetMethod("CommitChange"), null, new[] { transaction });
            ChartEffectDocumentState.Revision = "saved1"; // Same bookkeeping update as a successful save.
            Require((bool)Invoke(history.GetMethod("Undo"), null, new object[] { placement }), "Undo did not execute.");
            Require(ChartManager.ChartHolders[0].effectParametersJson == CameraJson(1), "Undo did not restore settings.");
            Require(ChartEffectDocumentState.Revision == "saved1",
                "Undo restored a stale disk revision along with editable settings.");
            Require((bool)Invoke(history.GetMethod("Redo"), null,
                new object[] { placement }), "Redo did not execute.");
            Require(ChartManager.ChartHolders[0].effectParametersJson ==
                    CameraJson(2), "Redo did not restore settings.");
            Require(ChartEffectDocumentState.Revision == "saved1",
                "Redo changed the last saved disk revision.");
        }
        finally
        {
            ChartManager.ClearChart(false);
            Invoke(history.GetMethod("Clear"), null);
            ChartEffectDocumentState.Restore(originalMetadata);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void EditorAndRuntimeValidation_Agree()
    {
        EffectRegistry registry = EffectRegistry.CreateDefault();
        Require(registry.TryGetEffect("camera.offset", out var camera),
            "Camera registration is missing.");
        CheckCameraValidation(registry, camera, 10000, 36000, true);
        CheckCameraValidation(registry, camera, -10000, -36000, true);
        CheckCameraValidation(registry, camera, 10001, 0, false);
        CheckCameraValidation(registry, camera, 0, 36001, false);

        MusicGimmickRegistration gimmick = null;
        MusicGimmickCommandRegistration begin = null;
        Require(registry.TryGetGimmick("sample", out gimmick) &&
                gimmick.TryGetCommand("begin-section", out begin),
            "Sample command registration is missing.");
        CheckSampleValidation(registry, begin, 100, true);
        CheckSampleValidation(registry, begin, 100.01, false);
        Require(!ChartEffectJsonCodec.TryDecode("music.call",
                "request-transition", "sample",
                "{\"targetMusicId\":\"\",\"targetDifficultyId\":\"default\"}",
                registry, out _, out _),
            "Editor accepted a transition target rejected by runtime registration.");
    }

    private static void ChartMaker_SaveReopenPreview_RoundTrips()
    {
        WithTemporaryDirectory(directory =>
        {
            Require(ChartManager.ChartHolders.Count == 0,
                "Do not run the ChartMaker round-trip acceptance check with a live editor document.");
            Type history = typeof(ChartManager).Assembly.GetType(
                "ChartEditHistory", true);
            Type recentFiles = typeof(ChartManager).Assembly.GetType(
                "ChartMakerRecentFiles", true);
            string previousRecent = (string)recentFiles.GetProperty(
                "LastChartPath").GetValue(null);
            var previousMetadata = ChartEffectDocumentState.Capture();
            var go = new GameObject(
                "ChartMaker save reopen preview acceptance (inactive)");
            go.SetActive(false);
            AudioClip song = null;

            try
            {
                var audio = go.AddComponent<AudioSource>();
                var core = go.AddComponent<ChartCore>();
                var saver = go.AddComponent<ChartToFile>();
                var loader = go.AddComponent<FileToChart>();
                var scroll = go.AddComponent<ChartScroll>();
                var preview = go.AddComponent<ChartTestPlay>();
                SetField(core, "audioSource", audio);
                SetField(saver, "chartCore", core);
                SetField(loader, "chartCore", core);
                SetField(loader, "chartToFile", saver);
                SetField(preview, "chartCore", core);
                SetField(preview, "chartScroll", scroll);

                ChartHolder source = ChartManager.GetOrCreateHolder(0, 0);
                source.isEffect = true;
                source.EnsureEffectIdentity();
                string originalEffectId = source.effectId;
                source.effectTypeId = "camera.offset";
                source.effectCommandId = string.Empty;
                source.effectOrder = 7;
                source.effectParametersJson =
                    "{\"durationMs\":1500,\"offsetX\":4," +
                    "\"offsetY\":1.5,\"rollDegrees\":12}";
                ChartEffectDocumentState.Restore(
                    new ChartEffectDocumentState.Metadata(
                        "stage5_flow", "manual", "", ""));

                string chartPath = Path.Combine(directory, "stage5-flow.rd");
                saver.SaveToPath(chartPath);
                string savedRevision = ChartEffectDocumentState.Revision;
                string parameterPath = ChartEffectFileStore.GetParameterPath(
                    chartPath, "stage5_flow", "manual");
                Require(File.Exists(chartPath) && !File.Exists(parameterPath) &&
                        !string.IsNullOrWhiteSpace(savedRevision) &&
                        !saver.HasUnsavedChanges,
                    "Save did not produce one clean chart file with its Effect.");
                Require((string)recentFiles.GetProperty("LastChartPath")
                            .GetValue(null) == Path.GetFullPath(chartPath),
                    "Save did not make its .rd path available for the next ChartMaker session.");

                ChartManager.ClearChart(false);
                ChartEffectDocumentState.Reset();
                ChartFile reopened = loader.LoadFromPath(chartPath);
                Require(reopened.EffectRevision == savedRevision &&
                        ChartEffectDocumentState.MusicId == "stage5_flow" &&
                        ChartEffectDocumentState.DifficultyId == "manual" &&
                        saver.CurrentFilePath == Path.GetFullPath(chartPath) &&
                        !saver.HasUnsavedChanges,
                    "Reopen did not restore the same clean ChartMaker document.");
                Require(ChartManager.ChartHolders.Count == 1 &&
                        ChartManager.ChartHolders[0].effectId ==
                        originalEffectId &&
                        ChartManager.ChartHolders[0].effectOrder == 7 &&
                        ((CameraEffectParameters)
                            ChartEffectJsonCodec.BuildParameterMap(
                                reopened.chartDatas, "")[originalEffectId])
                            .OffsetX == 4,
                    "Reopen changed the placed Effect identity, order, or settings.");

                ChartHolderDocumentBuildResult built =
                    ChartHolderDocumentAdapter.Build(
                        ChartManager.ChartHolders, core.Bpm, 4);
                Require(built.Succeeded,
                    "The reopened ChartMaker document could not build a runtime document.");
                ChartCompileResult compiled = ChartCompiler.Compile(
                    built.Document,
                    1d / ChartHolder.PositionUnitsPerWorldUnit);
                Require(compiled.Succeeded &&
                        compiled.Snapshot.EffectEvents.Count == 1,
                    "The reopened ChartMaker document could not compile the placed Effect.");
                PreparedEffectPlan plan = ChartEffectJsonCodec.PreparePlan(
                    compiled.Snapshot,
                    ChartManager.ChartHolders,
                    ChartEffectDocumentState.GimmickId);
                Require(plan != null && plan.Count == 1,
                    "The reopened Effect could not be prepared with its saved JSON.");

                song = AudioClip.Create("Stage 5 preview acceptance",
                    4410, 1, 44100, false);
                audio.clip = song;
                Invoke(typeof(ChartTestPlay).GetMethod("BindEvents",
                    BindingFlags.Instance | BindingFlags.NonPublic), preview);
                core.StartTestPlay(0d);
                Require(core.IsTestPlaying &&
                        core.ActiveTestPlaybackSessionId != 0L &&
                        preview.CurrentSnapshot != null &&
                        preview.CurrentSnapshot.EffectEvents.Count == 1 &&
                        string.IsNullOrWhiteSpace(preview.LastEffectMessage),
                    "The saved and reopened ChartMaker document did not enter Preview cleanly.");
                Require(!saver.TrySaveToPath(
                            Path.Combine(directory, "blocked-during-preview.rd"),
                            out string saveError) &&
                        saveError.Contains("Stop test playback"),
                    "Preview allowed the live document to be saved while its snapshot was running.");
                Require(!loader.TryLoadFromPath(chartPath, out _,
                            out string loadError) &&
                        loadError.Contains("Stop test playback"),
                    "Preview allowed the live document to be replaced while its snapshot was running.");
                double previewBpm = core.Bpm;
                double previewCorrection = core.StartCorrectionMs;
                core.SetBpm(previewBpm + 20d);
                core.SetStartCorrectionMs(previewCorrection + 25d);
                Require(core.Bpm == previewBpm &&
                        core.StartCorrectionMs == previewCorrection,
                    "Preview allowed BPM or music correction to change under its running snapshot.");

                core.EndTestPlay();
                Require(!core.IsTestPlaying &&
                        core.ActiveTestPlaybackSessionId == 0L,
                    "Stopping the accepted Preview left its session active.");
            }
            finally
            {
                if (go)
                {
                    ChartCore core = go.GetComponent<ChartCore>();
                    if (core && core.IsTestPlaying)
                    {
                        core.EndTestPlay();
                    }
                }
                ChartManager.ClearChart(false);
                Invoke(history.GetMethod("Clear"), null);
                ChartEffectDocumentState.Restore(previousMetadata);
                RestoreRecentChartPath(recentFiles, previousRecent);
                UnityEngine.Object.DestroyImmediate(go);
                if (song != null)
                {
                    UnityEngine.Object.DestroyImmediate(song);
                }
            }
        });
    }

    private static void ChartMaker_CompositeChartRoundTripsToGame()
    {
        WithTemporaryDirectory(directory =>
        {
            Require(ChartManager.ChartHolders.Count == 0,
                "Do not replace a live ChartMaker document during the composite cycle check.");
            Type history = typeof(ChartManager).Assembly.GetType(
                "ChartEditHistory", true);
            Type recentFiles = typeof(ChartManager).Assembly.GetType(
                "ChartMakerRecentFiles", true);
            string previousRecent = (string)recentFiles.GetProperty(
                "LastChartPath").GetValue(null);
            var previousMetadata = ChartEffectDocumentState.Capture();
            var go = new GameObject("Composite ChartMaker cycle (inactive)");
            go.SetActive(false);
            GameObject gameGo = null;
            GameRuleConfig gameConfig = null;
            AudioClip song = null;

            try
            {
                var audio = go.AddComponent<AudioSource>();
                var core = go.AddComponent<ChartCore>();
                var saver = go.AddComponent<ChartToFile>();
                var loader = go.AddComponent<FileToChart>();
                var scroll = go.AddComponent<ChartScroll>();
                var preview = go.AddComponent<ChartTestPlay>();
                SetField(core, "audioSource", audio);
                SetField(saver, "chartCore", core);
                SetField(loader, "chartCore", core);
                SetField(loader, "chartToFile", saver);
                SetField(preview, "chartCore", core);
                SetField(preview, "chartScroll", scroll);
                core.SetBpm(120d);
                core.SetStartCorrectionMs(-125d);
                ChartEffectDocumentState.Restore(
                    new ChartEffectDocumentState.Metadata(
                        "composite_cycle", "hard", "", ""));

                ChartHolder tap = ChartManager.GetOrCreateHolder(1, 0);
                tap.noteTypes[0] = NoteType.Tap;
                tap.noteHandles[0] = NoteHandleType.Left;
                ChartHolder longStart = ChartManager.GetOrCreateHolder(1, 1200);
                longStart.noteTypes[1] = NoteType.LongTap;
                longStart.noteHandles[1] = NoteHandleType.Left;
                ChartHolder scratch = ChartManager.GetOrCreateHolder(1, 2400);
                scratch.noteTypes[ChartHolder.MainLineCount + 1] = NoteType.Scratch;
                scratch.scratchMotions[1] = new ScratchMotionData(10,
                    ScratchMotionType.Instant);
                ChartHolder bpm = ChartManager.GetOrCreateHolder(1, 3000);
                bpm.targetBpm = 180f;
                ChartHolder longEnd = ChartManager.GetOrCreateHolder(1, 3600);
                longEnd.noteTypes[1] = NoteType.LongTap;
                longEnd.noteHandles[1] = NoteHandleType.Left;
                ChartHolder scratchStart = ChartManager.GetOrCreateHolder(2, 0);
                scratchStart.noteTypes[ChartHolder.MainLineCount] =
                    NoteType.LongScratch;
                scratchStart.scratchPointTypes[0] = ScratchPointType.Start;
                scratchStart.scratchMotions[0] = new ScratchMotionData(12,
                    ScratchMotionType.Gradual);
                ChartHolder scratchMid = ChartManager.GetOrCreateHolder(2, 1200);
                scratchMid.noteTypes[ChartHolder.MainLineCount] =
                    NoteType.LongScratch;
                scratchMid.scratchPointTypes[0] = ScratchPointType.Mid;
                scratchMid.scratchMotions[0] = new ScratchMotionData(7,
                    ScratchMotionType.Release);
                scratchMid.hasLineSpeedChange = true;
                scratchMid.targetLineSpeed = 1.5f;
                ChartHolder scratchEnd = ChartManager.GetOrCreateHolder(2, 2400);
                scratchEnd.noteTypes[ChartHolder.MainLineCount] =
                    NoteType.LongScratch;
                scratchEnd.scratchPointTypes[0] = ScratchPointType.End;
                scratchEnd.isEffect = true;
                scratchEnd.effectId = "fx_composite_camera";
                scratchEnd.effectTypeId = "camera.offset";
                scratchEnd.effectCommandId = string.Empty;
                scratchEnd.effectParametersJson =
                    "{\"durationMs\":1000,\"offsetX\":2," +
                    "\"offsetY\":0,\"rollDegrees\":5}";

                string chartPath = Path.Combine(directory, "hard.rd");
                saver.SaveToPath(chartPath);
                string savedRevision = ChartEffectDocumentState.Revision;
                Require(File.Exists(chartPath) &&
                        !string.IsNullOrWhiteSpace(savedRevision) &&
                        !saver.HasUnsavedChanges,
                    "The composite chart was not saved as a clean .rd document.");
                ChartManager.ClearChart(false);
                ChartEffectDocumentState.Reset();
                ChartFile reopened = loader.LoadFromPath(chartPath);
                Require(reopened.FormatVersion == 1 &&
                        !reopened.HasEffectParameterFile &&
                        reopened.EffectRevision == savedRevision &&
                        Math.Abs(core.Bpm - 120d) < 0.000001d &&
                        Math.Abs(core.StartCorrectionMs + 125d) < 0.000001d &&
                        !saver.HasUnsavedChanges,
                    "Reopening changed the composite chart's format or timing metadata.");
                ChartHolderDocumentBuildResult built =
                    ChartHolderDocumentAdapter.Build(
                        ChartManager.ChartHolders, core.Bpm, 4);
                Require(built.Succeeded,
                    "The reopened composite chart could not build a runtime document.");
                ChartCompileResult compiled = ChartCompiler.Compile(
                    built.Document,
                    1d / ChartHolder.PositionUnitsPerWorldUnit);
                Require(compiled.Succeeded &&
                        compiled.Snapshot.Notes.Count == 4 &&
                        compiled.Snapshot.EffectEvents.Count == 1 &&
                        compiled.Snapshot.JudgementSegments.Count == 3,
                    "The composite chart lost notes, Long intervals, or Effect at compile time.");

                song = AudioClip.Create("Composite cycle preview", 44100 * 8,
                    1, 44100, false);
                audio.clip = song;
                Invoke(typeof(ChartTestPlay).GetMethod("BindEvents",
                    BindingFlags.Instance | BindingFlags.NonPublic), preview);
                core.StartTestPlay(0d);
                Require(core.IsTestPlaying && preview.CurrentSnapshot != null &&
                        preview.CurrentSnapshot.Notes.Count == 4 &&
                        preview.CurrentSnapshot.EffectEvents.Count == 1,
                    "The reopened composite chart could not start Preview.");
                PlayableChartSnapshot previewSnapshot = preview.CurrentSnapshot;
                core.EndTestPlay();
                Require(!core.IsTestPlaying,
                    "The composite Preview session remained active after stop.");

                string packagePath =
                    ChartMakerRuntimePackageExporter.DefaultOutputPath(chartPath);
                saver.ExportRuntimePackageToPath(packagePath);
                Require(File.Exists(packagePath) &&
                        Path.GetDirectoryName(packagePath) ==
                        Path.Combine(directory, "rmp"),
                    "The Game package was not exported beside the chart in rmp/.");
                string packageText = File.ReadAllText(packagePath);
                PreparedGameplayChart game = GameplayChartPreparation.Prepare(
                    packageText);
                Require(game.Metadata.MusicId == "composite_cycle" &&
                        game.Metadata.DifficultyId == "hard" &&
                        game.Metadata.Revision == savedRevision &&
                        Math.Abs(game.ChartOffsetMs - 125d) < 0.000001d,
                    "The Game package changed the chart identity or song offset.");
                PlayableChartSnapshot gameSnapshot = game.Snapshot;
                Require(gameSnapshot.Notes.Count == previewSnapshot.Notes.Count &&
                        gameSnapshot.EffectEvents.Count ==
                        previewSnapshot.EffectEvents.Count &&
                        gameSnapshot.JudgementTargets.Count ==
                        previewSnapshot.JudgementTargets.Count &&
                        gameSnapshot.JudgementSegments.Count ==
                        previewSnapshot.JudgementSegments.Count,
                    "Preview and Game compiled different event counts.");
                for (int i = 0; i < gameSnapshot.Notes.Count; i++)
                {
                    PlayableNoteSnapshot left = previewSnapshot.Notes[i];
                    PlayableNoteSnapshot right = gameSnapshot.Notes[i];
                    Require(left.Id == right.Id && left.Kind == right.Kind &&
                            left.Lane == right.Lane &&
                            left.Points.Count == right.Points.Count,
                        "Preview and Game assigned different note identities or lifecycles.");
                    for (int point = 0; point < left.Points.Count; point++)
                        Require(left.Points[point].Position ==
                                    right.Points[point].Position &&
                                left.Points[point].Kind ==
                                    right.Points[point].Kind &&
                                Math.Abs(left.Points[point].TimeMs -
                                    right.Points[point].TimeMs) < 0.000001d &&
                                Math.Abs(left.Points[point].FloorPosition -
                                    right.Points[point].FloorPosition) < 0.000001d,
                            "Preview and Game interpreted a note point differently.");
                }
                foreach (int position in new[] { 4800, 7800, 9600, 10800,
                             12000 })
                {
                    Require(Math.Abs(previewSnapshot.TimingMap
                                .TimeAtPosition(position) - gameSnapshot.TimingMap
                                .TimeAtPosition(position)) < 0.000001d &&
                            Math.Abs(previewSnapshot.ScrollMap
                                .FloorPositionAtChartPosition(position) -
                                gameSnapshot.ScrollMap
                                .FloorPositionAtChartPosition(position)) <
                            0.000001d,
                        "Preview and Game disagreed on BPM or scroll conversion.");
                }
                Require(Math.Abs(gameSnapshot.TimingMap.TimeAtPosition(
                            12000) - 4416.666666666667d) < 0.000001d &&
                        gameSnapshot.ScrollMap.FloorPositionAtChartPosition(
                            12000) > 12000d /
                        ChartHolder.PositionUnitsPerWorldUnit,
                    "The sample did not actually apply its BPM and scroll changes.");
                var editorCamera = (CameraEffectParameters)
                    ChartEffectJsonCodec.BuildParameterMap(
                        reopened.chartDatas, "")["fx_composite_camera"];
                var gameCamera = (CameraEffectParameters)
                    DecodeRuntimeEffect(RuntimeChartPackageCodec.Import(
                        packageText), "fx_composite_camera");
                Require(editorCamera.DurationMs == gameCamera.DurationMs &&
                        Math.Abs(editorCamera.OffsetX - gameCamera.OffsetX) <
                        0.000001d,
                    "Preview and Game decoded different Effect settings.");
                Require(gameSnapshot.Notes[0].Kind == ChartNoteKind.Tap &&
                        gameSnapshot.Notes[1].Kind == ChartNoteKind.Hold &&
                        gameSnapshot.Notes[2].Kind == ChartNoteKind.Scratch &&
                        gameSnapshot.Notes[3].Kind == ChartNoteKind.LongScratch &&
                        gameSnapshot.Notes[3].Points.Count == 3 &&
                        gameSnapshot.EffectEvents[0].EffectId ==
                        "fx_composite_camera" &&
                        Math.Abs(gameSnapshot.EffectEvents[0].TimeMs -
                            gameSnapshot.Notes[3].Points[2].TimeMs) < 0.000001d,
                    "The composite sample did not preserve the intended note families and same-time Effect.");

                gameGo = new GameObject("Composite Game session (inactive)");
                gameGo.SetActive(false);
                gameConfig = ScriptableObject.CreateInstance<GameRuleConfig>();
                var gameAudio = gameGo.AddComponent<AudioSource>();
                var gamePlay = gameGo.AddComponent<GamePlay>();
                SetField(gamePlay, "audioSource", gameAudio);
                var gameRule = gameGo.AddComponent<DefaultGameRule>();
                typeof(GameRule).GetField("config",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(gameRule, gameConfig);
                var gameManager = gameGo.AddComponent<GameManager>();
                SetField(gameManager, "gamePlay", gamePlay);
                SetField(gameManager, "gameRule", gameRule);
                var gameJudgement = gameGo.AddComponent<NoteJudgementSystem>();
                SetField(gameJudgement, "gameManager", gameManager);
                SetField(gameJudgement, "gameRule", gameRule);
                var gameState = gameGo.AddComponent<GameplaySessionState>();
                SetField(gameState, "gameManager", gameManager);
                SetField(gameState, "judgementSystem", gameJudgement);
                SetField(gameState, "gameRule", gameRule);
                var gameEffects = gameGo.AddComponent<
                    GameplayChartEffectController>();
                SetField(gameEffects, "gameManager", gameManager);
                SetField(gameEffects, "judgementSystem", gameJudgement);
                SetField(gameEffects, "gameStateProvider", gameState);
                var pivot = new GameObject("Composite camera Effect pivot");
                pivot.transform.SetParent(gameGo.transform, false);
                SetField(gameEffects, "cameraEffectPivot", pivot.transform);
                SetField(gamePlay, "cameraTransform", pivot.transform);
                var gameChart = gameGo.AddComponent<
                    GameplayChartSessionController>();
                SetField(gameChart, "gameManager", gameManager);
                SetField(gameChart, "judgementSystem", gameJudgement);
                SetField(gameChart, "effectController", gameEffects);
                SetField(gameChart, "sessionState", gameState);
                gameGo.SetActive(true);
                Require(gamePlay.PrepareSong(song) &&
                        gameChart.TryPrepare(packageText),
                    "The composite package could not prepare a live Game session: " +
                    gameChart.LastError);
                Require(gameManager.StartGame(),
                    "The composite Game session could not start playback.");
                gameJudgement.SetAutoPlayEnabled(true);
                gameJudgement.ProcessFrame(gamePlay.SongDurationMs + 1000d);
                Require(gameJudgement.LastTimelineError == null &&
                        gameJudgement.PendingNoteCount == 0 &&
                        gameState.JudgedNoteCount == gameState.TotalNoteCount &&
                        gameState.IsCleared && gameState.CurrentScore > 0d,
                    "The composite Game session did not finish its notes and Effect cleanly.");
                gameManager.StopGame();
            }
            finally
            {
                if (gameGo) UnityEngine.Object.DestroyImmediate(gameGo);
                if (gameConfig) UnityEngine.Object.DestroyImmediate(gameConfig);
                if (go)
                {
                    ChartCore core = go.GetComponent<ChartCore>();
                    if (core && core.IsTestPlaying) core.EndTestPlay();
                }
                ChartManager.ClearChart(false);
                Invoke(history.GetMethod("Clear"), null);
                ChartEffectDocumentState.Restore(previousMetadata);
                RestoreRecentChartPath(recentFiles, previousRecent);
                UnityEngine.Object.DestroyImmediate(go);
                if (song != null) UnityEngine.Object.DestroyImmediate(song);
            }
        });
    }

    private static void GameplayPreparation_UsesSharedPairAndOffset()
    {
        string chartPath = Path.Combine(
            Application.dataPath,
            "Tests/Fixtures/EffectGameplaySample.rd");
        string chartText = File.ReadAllText(chartPath);
        string correctedChartText = chartText.Replace(
            "\"musicStartCorrectionMs\": 0.0",
            "\"musicStartCorrectionMs\": -125.0");
        Require(correctedChartText != chartText,
            "The sample did not contain the expected music-start correction.");

        PreparedGameplayChart prepared = GameplayChartPreparation.Prepare(
            ChartMakerRuntimePackageExporter.Export(correctedChartText, 4));
        RuntimeChartPackage runtimePackage = RuntimeChartPackageCodec.Import(
            ChartMakerRuntimePackageExporter.Export(correctedChartText, 4));
        Require(runtimePackage.MusicId == prepared.Metadata.MusicId &&
                runtimePackage.DifficultyId ==
                prepared.Metadata.DifficultyId &&
                Math.Abs(runtimePackage.ChartOffsetMs -
                    prepared.ChartOffsetMs) < 0.000001d &&
                runtimePackage.Snapshot.Notes.Count ==
                prepared.Snapshot.Notes.Count &&
                runtimePackage.Snapshot.EffectEvents.Count ==
                prepared.Snapshot.EffectEvents.Count,
            "Runtime package changed the chart metadata or compiled event count.");
        for (int index = 0; index < prepared.Snapshot.Notes.Count; index++)
            Require(Math.Abs(runtimePackage.Snapshot.Notes[index].StartTimeMs -
                    prepared.Snapshot.Notes[index].StartTimeMs) < 0.000001d,
                "Runtime package changed a note's chart time.");
        for (int index = 0;
             index < prepared.Snapshot.EffectEvents.Count; index++)
            Require(Math.Abs(runtimePackage.Snapshot.EffectEvents[index].TimeMs -
                    prepared.Snapshot.EffectEvents[index].TimeMs) < 0.000001d,
                "Runtime package changed an Effect's chart time.");
        Require(prepared.Snapshot.EffectEvents.Count == 4,
            "Gameplay did not compile every Effect from the shared Snapshot.");
        Require(prepared.Snapshot.Notes.Count == 2 &&
                prepared.Snapshot.Notes[0].Kind == ChartNoteKind.Tap &&
                prepared.Snapshot.Notes[1].Kind == ChartNoteKind.Tap,
            "Gameplay judgement contract did not preserve the compiled Snapshot.");
        Require(Math.Abs(prepared.ChartOffsetMs - 125d) < 0.000001,
            "Gameplay did not invert ChartMaker's music-start correction.");
        Require(prepared.Metadata.MusicId == "effect_gameplay_sample" &&
                prepared.Metadata.DifficultyId == "demo" &&
                prepared.Metadata.GimmickId == "sample",
                "Gameplay metadata changed while preparing the chart.");

        PlayableNoteSnapshot alignedNote = prepared.Snapshot.Notes[1];
        PlayableEffectEvent alignedEffect =
            prepared.Snapshot.EffectEvents[2];
        Require(alignedEffect.EffectId == "fx_stage6_count" &&
                Math.Abs(alignedNote.StartTimeMs -
                    alignedEffect.TimeMs) < 0.000000001d &&
                alignedNote.Points.Count == 1,
            "Gameplay moved a Snapshot note away from its same-position Effect.");

        WithJudgement((system, rule) =>
        {
            var document = new ChartDocument(4800, 4, 225);
            document.Notes.Add(new ChartDocumentNote(
                alignedNote.Id, ChartNoteKind.Tap, alignedNote.Lane, 8400));
            document.EffectEvents.Add(new ChartEffectEvent(
                8400, "fractional-effect", "fractional-record", "", 0));
            Require(system.Initialize(
                    ChartCompiler.Compile(document).Snapshot),
                "Could not initialize the fractional Snapshot note.");
            system.SetAutoPlayEnabled(true);
            var order = new List<string>();
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration(
                "fractional-record",
                "Fractional record",
                null,
                false,
                null,
                null,
                _ => new RecordingEffect(() => order.Add("effect"))));
            PreparedEffectPlan plan = EffectPreparation.Prepare(
                ChartCompiler.Compile(document).Snapshot.EffectEvents,
                null,
                registry).Plan;
            using var runner = new EffectRunner(
                plan,
                new EffectSessionContext(
                    "test",
                    "default",
                    EffectExecutionMode.AutoPlay));
            system.AttachEffectRunner(runner);
            system.NoteJudged += _ => order.Add("note");
            system.ProcessFrame(2000d);
            Require(string.Join(",", order) == "effect,note",
                "A same-position fractional Effect ran after its gameplay note.");
        });

        bool mismatchRejected = false;
        string mismatchedChartText = chartText.Replace(
            "\"position\": 8400", "\"position\": 9999");
        Require(mismatchedChartText != chartText,
            "The sample did not contain the expected Effect row position.");
        try
        {
            ChartMakerRuntimePackageExporter.Export(mismatchedChartText, 4);
        }
        catch (FormatException)
        {
            mismatchRejected = true;
        }

        Require(mismatchRejected,
            "Gameplay accepted an Effect definition without a matching chart row.");
    }

    private static void GameplayChartSession_FailedReplacementInvalidatesAll()
    {
        string chartText = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Tests/Fixtures/EffectGameplaySample.rd"));
        var go = new GameObject(
            "Stage 6 failed replacement acceptance (inactive)");
        go.SetActive(false);
        var config = ScriptableObject.CreateInstance<GameRuleConfig>();

        try
        {
            var audio = go.AddComponent<AudioSource>();
            var play = go.AddComponent<GamePlay>();
            SetField(play, "audioSource", audio);
            var rule = go.AddComponent<DefaultGameRule>();
            typeof(GameRule).GetField("config",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rule, config);
            var manager = go.AddComponent<GameManager>();
            SetField(manager, "gamePlay", play);
            SetField(manager, "gameRule", rule);
            var judgement = go.AddComponent<NoteJudgementSystem>();
            SetField(judgement, "gameManager", manager);
            SetField(judgement, "gameRule", rule);
            var state = go.AddComponent<GameplaySessionState>();
            SetField(state, "gameManager", manager);
            SetField(state, "judgementSystem", judgement);
            SetField(state, "gameRule", rule);
            var effects = go.AddComponent<GameplayChartEffectController>();
            SetField(effects, "gameManager", manager);
            SetField(effects, "judgementSystem", judgement);
            SetField(effects, "gameStateProvider", state);
            var cameraPivot = new GameObject(
                "Stage 6 test camera Effect pivot");
            cameraPivot.transform.SetParent(go.transform, false);
            SetField(effects, "cameraEffectPivot", cameraPivot.transform);
            var chartSession = go.AddComponent<
                GameplayChartSessionController>();
            SetField(chartSession, "gameManager", manager);
            SetField(chartSession, "judgementSystem", judgement);
            SetField(chartSession, "effectController", effects);
            SetField(chartSession, "sessionState", state);

            string packageText = ChartMakerRuntimePackageExporter.Export(chartText);
            Require(chartSession.TryPrepare(packageText),
                "The valid gameplay chart could not establish the fixture: " +
                chartSession.LastError);
            Require(chartSession.IsPrepared && judgement.IsInitialized &&
                    state.HasPreparedChart &&
                    GetFieldValue(effects, "plan") != null,
                "The valid chart was not published to every live service.");

            SetField(manager, "gameRule", null);
            SetField(state, "gameRule", null);
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                "GameplaySessionState requires GameManager, GamePlay, " +
                "NoteJudgementSystem and GameRule references.");
            Require(!chartSession.TryPrepare(packageText),
                "A replacement with incomplete live state unexpectedly succeeded.");
            Require(!chartSession.IsPrepared && !judgement.IsInitialized &&
                    !state.HasPreparedChart &&
                    GetFieldValue(effects, "plan") == null,
                "A failed replacement left stale chart services startable.");

            SetField(manager, "gameRule", rule);
            SetField(state, "gameRule", rule);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static void GameplayChartSession_DisabledServiceRejectsStart()
    {
        string chartText = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Tests/Fixtures/EffectGameplaySample.rd"));
        var go = new GameObject(
            "Stage 6 disabled service start guard (inactive)");
        go.SetActive(false);
        var config = ScriptableObject.CreateInstance<GameRuleConfig>();
        AudioClip song = null;

        try
        {
            var audio = go.AddComponent<AudioSource>();
            var play = go.AddComponent<GamePlay>();
            SetField(play, "audioSource", audio);
            var rule = go.AddComponent<DefaultGameRule>();
            typeof(GameRule).GetField("config",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rule, config);
            var manager = go.AddComponent<GameManager>();
            SetField(manager, "gamePlay", play);
            SetField(manager, "gameRule", rule);
            var judgement = go.AddComponent<NoteJudgementSystem>();
            SetField(judgement, "gameManager", manager);
            SetField(judgement, "gameRule", rule);
            var state = go.AddComponent<GameplaySessionState>();
            SetField(state, "gameManager", manager);
            SetField(state, "judgementSystem", judgement);
            SetField(state, "gameRule", rule);
            var effects = go.AddComponent<GameplayChartEffectController>();
            SetField(effects, "gameManager", manager);
            SetField(effects, "judgementSystem", judgement);
            SetField(effects, "gameStateProvider", state);
            var cameraPivot = new GameObject(
                "Stage 6 disabled service camera Effect pivot");
            cameraPivot.transform.SetParent(go.transform, false);
            SetField(effects, "cameraEffectPivot", cameraPivot.transform);
            var chartSession = go.AddComponent<
                GameplayChartSessionController>();
            SetField(chartSession, "gameManager", manager);
            SetField(chartSession, "judgementSystem", judgement);
            SetField(chartSession, "effectController", effects);
            SetField(chartSession, "sessionState", state);

            go.SetActive(true);
            Require(chartSession.TryPrepare(
                    ChartMakerRuntimePackageExporter.Export(chartText)),
                "The valid gameplay chart could not establish the disabled-service fixture: " +
                chartSession.LastError);
            song = AudioClip.Create(
                "Stage 6 disabled service start guard",
                4410,
                1,
                44100,
                false);
            Require(play.PrepareSong(song),
                "The disabled-service fixture could not prepare its song.");

            MethodInfo startGuard = typeof(GameplayChartSessionController)
                .GetMethod("HandlePlaybackStarting",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Require((bool)Invoke(startGuard, chartSession,
                    new object[] { 0d }),
                "A fully prepared active gameplay graph was rejected.");

            judgement.enabled = false;
            Require(!(bool)Invoke(startGuard, chartSession,
                        new object[] { 0d }) &&
                    !manager.StartGame() &&
                    play.State == PlaybackState.Ready,
                "Playback was accepted while judgement was disabled.");
            judgement.enabled = true;
            Require((bool)Invoke(startGuard, chartSession,
                    new object[] { 0d }),
                "Re-enabled judgement did not restore the valid graph.");

            state.enabled = false;
            Require(!(bool)Invoke(startGuard, chartSession,
                        new object[] { 0d }) &&
                    !manager.StartGame() &&
                    play.State == PlaybackState.Ready,
                "Playback was accepted while gameplay state was disabled.");
            state.enabled = true;
            Require((bool)Invoke(startGuard, chartSession,
                    new object[] { 0d }),
                "Re-enabled gameplay state did not restore the valid graph.");

            effects.enabled = false;
            Require(!(bool)Invoke(startGuard, chartSession,
                        new object[] { 0d }) &&
                    !manager.StartGame() &&
                    play.State == PlaybackState.Ready,
                "Playback was accepted while Effects were disabled.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(config);
            if (song != null)
            {
                UnityEngine.Object.DestroyImmediate(song);
            }
        }
    }

    private static void GameplaySessionState_UsesRuleStateAndRestartBoundary()
    {
        var go = new GameObject(
            "Stage 6 gameplay state acceptance (inactive)");
        go.SetActive(false);
        var config = ScriptableObject.CreateInstance<GameRuleConfig>();

        try
        {
            var audio = go.AddComponent<AudioSource>();
            var play = go.AddComponent<GamePlay>();
            SetField(play, "audioSource", audio);
            var rule = go.AddComponent<DefaultGameRule>();
            typeof(GameRule).GetField("config",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rule, config);
            var manager = go.AddComponent<GameManager>();
            SetField(manager, "gamePlay", play);
            SetField(manager, "gameRule", rule);
            var judgement = go.AddComponent<NoteJudgementSystem>();
            SetField(judgement, "gameManager", manager);
            SetField(judgement, "gameRule", rule);
            var state = go.AddComponent<GameplaySessionState>();
            SetField(state, "gameManager", manager);
            SetField(state, "judgementSystem", judgement);
            SetField(state, "gameRule", rule);

            Require(InitializeJudgement(judgement,
                    new[] { Note("stage6-state-note", 0, 100) }),
                "Could not initialize the Stage 6 state judgement fixture.");
            judgement.SetAutoPlayEnabled(true);
            Require(state.TryConfigureChart(1, out string error),
                "Could not configure real gameplay state: " + error);
            Require(Math.Abs(state.CurrentHealth - rule.BaseInitialHealth) <
                    0.000001,
                "Gameplay state did not start from the active GameRule.");

            judgement.ProcessFrame(100d);
            Require(state.JudgedNoteCount == 1 &&
                    state.CurrentCombo == 1 &&
                    state.CurrentScore > 0d &&
                    state.IsCleared,
                "A real judgement did not update health/combo/score/clear state.");

            double scoreAfterJudge = state.CurrentScore;
            int comboAfterJudge = state.CurrentCombo;
            SetProperty(play, "StartReason", PlaybackStartReason.Resume);
            Invoke(typeof(GameplaySessionState).GetMethod(
                "HandlePlaybackStarted",
                BindingFlags.Instance | BindingFlags.NonPublic),
                state,
                new object[] { 100d });
            Require(state.CurrentScore == scoreAfterJudge &&
                    state.CurrentCombo == comboAfterJudge,
                "Resume reset live gameplay state.");

            SetProperty(play, "StartReason", PlaybackStartReason.Restart);
            Invoke(typeof(GameplaySessionState).GetMethod(
                "HandlePlaybackStarted",
                BindingFlags.Instance | BindingFlags.NonPublic),
                state,
                new object[] { 0d });
            Require(state.JudgedNoteCount == 0 &&
                    state.CurrentCombo == 0 &&
                    state.CurrentScore == 0d &&
                    Math.Abs(state.CurrentHealth - rule.BaseInitialHealth) <
                        0.000001,
                "Restart did not create fresh gameplay rule state.");

            var longChart = new ChartDocument(4800, 4, 120);
            var longScratch = new ChartDocumentNote("segments",
                ChartNoteKind.LongScratch,
                (int)ChartLane.GroundLeft, 2400, 9600);
            longScratch.Points.Insert(1, new ChartNotePoint(4800,
                ChartNotePointKind.Mid));
            longScratch.Points.Insert(2, new ChartNotePoint(7200,
                ChartNotePointKind.Mid));
            longChart.Notes.Add(longScratch);
            ChartCompileResult compiled = ChartCompiler.Compile(longChart);
            Require(compiled.Succeeded &&
                    compiled.Snapshot.JudgementSegments.Count == 3 &&
                    judgement.Initialize(compiled.Snapshot) &&
                    state.TryConfigureChart(3, out error),
                "Could not prepare three Long Scratch scoring intervals: " +
                error);
            judgement.SetAutoPlayEnabled(true);
            judgement.ProcessFrame(4500d);
            Require(state.JudgedNoteCount == 3 &&
                    state.CurrentCombo == 3 && state.IsCleared &&
                    Math.Abs(state.CurrentScore - rule.MaxScore) < 1d,
                "Long Scratch intervals were not each counted in " +
                "score, combo and clear state.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static void GameplayScene_ConnectsSnapshotStateAndCamera()
    {
        const string scenePath =
            "Assets/Tests/Fixtures/Scenes/DemoPlay.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            GameplayChartSessionController chartSession =
                FindSingleInScene<GameplayChartSessionController>(scene);
            GameplaySessionState state =
                FindSingleInScene<GameplaySessionState>(scene);
            GameplayChartEffectController effects =
                FindSingleInScene<GameplayChartEffectController>(scene);
            DemoPlayController demo =
                FindSingleInScene<DemoPlayController>(scene);
            GameManager manager = FindSingleInScene<GameManager>(scene);
            NoteJudgementSystem judgement =
                FindSingleInScene<NoteJudgementSystem>(scene);
            Camera camera = FindSingleInScene<Camera>(scene);

            int legacyLoaderCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                legacyLoaderCount += root
                    .GetComponentsInChildren<TempLoader>(true).Length;
            }
            Require(legacyLoaderCount == 0,
                "DemoPlay still contains the obsolete TempLoader.");

            var chartAsset = (TextAsset)GetFieldValue(
                chartSession,
                "chartAsset");
            Require(chartAsset,
                "DemoPlay does not reference a bundled runtime package.");
            PreparedGameplayChart prepared = GameplayChartPreparation.Prepare(
                chartAsset.text);
            Require(prepared.Snapshot.EffectEvents.Count == 4 &&
                    prepared.Snapshot.Notes.Count == 2,
                "The pair wired into DemoPlay is not actually gameplay-ready.");
            PlayableEffectEvent cameraEvent = null;
            foreach (PlayableNoteSnapshot note in prepared.Snapshot.Notes)
            {
                Require(note.StartTimeMs > 1000d,
                    "The bundled sample contains a gameplay note at or before " +
                    "the one-second loading guard.");
            }
            foreach (PlayableEffectEvent effectEvent in
                     prepared.Snapshot.EffectEvents)
            {
                Require(effectEvent.TimeMs > 1000d,
                    "The bundled sample contains an Effect at or before the " +
                    "one-second loading guard.");
                if (effectEvent.EffectId == "fx_stage6_camera")
                {
                    cameraEvent = effectEvent;
                }
            }
            RuntimeChartPackage inspectedChart =
                RuntimeChartPackageCodec.Import(chartAsset.text);
            var cameraParameters = (CameraEffectParameters)
                DecodeRuntimeEffect(inspectedChart, "fx_stage6_camera");
            Require(cameraEvent != null &&
                    cameraEvent.TimeMs >= 1500d &&
                    cameraEvent.TimeMs <= 2500d &&
                    cameraParameters.DurationMs >= 1500d &&
                    cameraParameters.AttackMs >= 250d &&
                    cameraParameters.ReleaseMs >= 250d &&
                    cameraParameters.AttackMs +
                        cameraParameters.ReleaseMs <=
                        cameraParameters.DurationMs,
                "The manual camera sample starts too early or ends too fast " +
                "to verify smoothly in Play Mode.");
            string bundledMusicId = (string)GetFieldValue(
                chartSession,
                "bundledMusicId");
            var bundledSong = (AudioClip)GetFieldValue(
                chartSession,
                "bundledSong");
            Require(string.Equals(
                        bundledMusicId,
                        prepared.Metadata.MusicId,
                        StringComparison.Ordinal) &&
                    bundledSong &&
                    ReferenceEquals(
                        bundledSong,
                        GetFieldValue(manager.GamePlay, "initialSong")),
                "DemoPlay's chart musicId is not bound to its actual AudioClip.");

            Require(ReferenceEquals(
                    GetFieldValue(chartSession, "judgementSystem"),
                    judgement) &&
                ReferenceEquals(
                    GetFieldValue(chartSession, "effectController"),
                    effects) &&
                ReferenceEquals(
                    GetFieldValue(chartSession, "sessionState"),
                    state),
                "DemoPlay chart preparation does not publish to the same " +
                "judgement, Effect and state services.");
            Require(ReferenceEquals(
                    GetFieldValue(effects, "gameStateProvider"),
                    state),
                "The real Effect session is not reading real gameplay health.");

            Transform cameraBase = (Transform)GetFieldValue(
                demo,
                "cameraTransform");
            Transform effectPivot = (Transform)GetFieldValue(
                effects,
                "cameraEffectPivot");
            Require(cameraBase && effectPivot &&
                    cameraBase.name == "Camera Base Motion" &&
                    effectPivot.name == "Camera Effect Pivot" &&
                    effectPivot.parent == cameraBase &&
                    camera.transform.parent == effectPivot,
                "DemoPlay camera base motion and additive Effect pivot are " +
                "not isolated in the required hierarchy.");
            Require(ReferenceEquals(
                    GetFieldValue(demo, "chartSession"),
                    chartSession),
                "DemoPlay still bypasses the prepared gameplay chart session.");
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void GameScene_WiresFullSongAndFlow()
    {
        const string scenePath = "Assets/Scenes/Game.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(scenePath,
                OpenSceneMode.Additive);
        try
        {
            GameFlowController flow = FindSingleInScene<GameFlowController>(
                scene);
            DemoPlayController presenter =
                FindSingleInScene<DemoPlayController>(scene);
            GameplayChartSessionController charts =
                FindSingleInScene<GameplayChartSessionController>(scene);
            GameplaySessionState state =
                FindSingleInScene<GameplaySessionState>(scene);
            NoteJudgementSystem judgement =
                FindSingleInScene<NoteJudgementSystem>(scene);
            GameManager manager = FindSingleInScene<GameManager>(scene);
            Test oldUi = FindSingleInScene<Test>(scene);
            Require(flow && flow.gameObject.activeInHierarchy &&
                flow.enabled && oldUi && !oldUi.enabled &&
                !(bool)GetFieldValue(presenter, "playOnReady"),
                "Game scene still starts as the old DemoPlay harness.");
            Require(ReferenceEquals(GetFieldValue(flow, "gameManager"),
                    manager) &&
                ReferenceEquals(GetFieldValue(flow, "notePresenter"),
                    presenter) &&
                ReferenceEquals(GetFieldValue(flow, "chartSession"),
                    charts) &&
                ReferenceEquals(GetFieldValue(flow, "sessionState"),
                    state) &&
                ReferenceEquals(GetFieldValue(flow, "judgementSystem"),
                    judgement),
                "Game menu, playback, judgement and result state are disconnected.");
            var catalog = (MusicCatalog)GetFieldValue(presenter,
                "musicCatalog");
            Require(catalog && catalog.Songs.Count >= 2 &&
                (string)GetFieldValue(presenter, "defaultMusicId") == "i" &&
                (string)GetFieldValue(presenter, "defaultDifficultyId") == "hard",
                "Game scene does not resolve a selected song from the catalog.");
            foreach (MusicCatalogEntry songEntry in catalog.Songs)
            {
                Require(songEntry.SongData && songEntry.AudioClip,
                    "A playable catalog song is missing data or audio.");
                SongContent song = SongContentCodec.Parse(songEntry.SongData.text);
                Require(song.MusicId == songEntry.MusicId,
                    "Catalog and song data IDs differ.");
                foreach (MusicDifficultyEntry difficulty in songEntry.Difficulties)
                {
                    Require(difficulty.RuntimePackage,
                        "A selected difficulty has no runtime package.");
                    PreparedGameplayChart prepared = GameplayChartPreparation.Prepare(
                        difficulty.RuntimePackage.text);
                    Require(prepared.Metadata.MusicId == songEntry.MusicId &&
                        prepared.Metadata.DifficultyId == difficulty.DifficultyId &&
                        prepared.Snapshot.Notes.Count > 0 &&
                        song.FindChart(difficulty.DifficultyId) != null,
                        "Catalog selection does not match a playable chart.");
                }
            }
            var profiles = (NoteJudgeWindowProfile[])GetFieldValue(
                judgement, "noteWindowProfiles");
            Require(profiles.Length == 5 &&
                profiles[3] &&
                profiles[3].NoteKind == ChartNoteKind.LongScratch,
                "Long Scratch does not have a separate window asset.");
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void CheckCameraValidation(EffectRegistry registry,
        EffectRegistration registration, double offsetX, double roll,
        bool expected)
    {
        string json = "{\"durationMs\":400,\"offsetX\":" + Invariant(offsetX) +
            ",\"rollDegrees\":" + Invariant(roll) + "}";
        bool editorAccepts = ChartEffectJsonCodec.TryDecode("camera.offset",
            "", "", json, registry, out _, out _);
        bool runtimeAccepts = registration.ValidateParameters(
            new CameraEffectParameters(400, offsetX, 0, roll)) == null;
        Require(editorAccepts == expected && runtimeAccepts == expected,
            $"Camera validation mismatch: offset={offsetX}, roll={roll}, " +
            $"expected={expected}, editor={editorAccepts}, runtime={runtimeAccepts}.");
    }

    private static void CheckSampleValidation(EffectRegistry registry,
        MusicGimmickCommandRegistration registration, double multiplier,
        bool expected)
    {
        string json = "{\"minimumHealth\":30,\"damageMultiplier\":" +
            Invariant(multiplier) + "}";
        bool editorAccepts = ChartEffectJsonCodec.TryDecode("music.call",
            "begin-section", "sample", json, registry, out _, out _);
        bool runtimeAccepts = registration.ValidateParameters(
            new SampleSectionParameters(30, multiplier)) == null;
        Require(editorAccepts == expected && runtimeAccepts == expected,
            $"Sample validation mismatch: multiplier={multiplier}, expected={expected}, " +
            $"editor={editorAccepts}, runtime={runtimeAccepts}.");
    }

    private static ChartHolder EffectHolder(double x) => new ChartHolder(0, 0)
    { isEffect = true, effectId = "fx_sample", effectTypeId = "camera.offset", effectCommandId = "", effectParametersJson = CameraJson(x) };
    private static string CameraJson(double x) => "{\"durationMs\":400,\"offsetX\":" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
    private static void WritePair(string path, string music, string revision, double x)
    {
        BuildPairText(music, revision, x, out string chartText,
            out string parameterText);
        ChartEffectFileStore.Save(path, chartText, parameterText);
    }

    private static void BuildPairText(string music, string revision, double x,
        out string chartText, out string parameterText)
    {
        var holders = new[] { EffectHolder(x) };
        var metadata = new ChartEffectDocumentState.Metadata(music, "default", "", revision);
        XElement legacy = ChartEffectParameterCodec.ReadObject(
            ChartFileCodec.Serialize(holders, 120, 0, metadata));
        legacy.Element("formatVersion").Value = "8";
        legacy.Element("notes").Name = "events";
        XElement dictionary = legacy.Element("eventDictionary");
        dictionary.Name = "effectDefinitions";
        foreach (XElement entry in dictionary.Elements())
            entry.Element("parameters")?.Remove();
        legacy.Add(new XElement("hasEffectParameters",
            new XAttribute("type", "boolean"), "true"));
        chartText = ChartEffectParameterCodec.WriteObject(legacy);
        parameterText = ChartEffectFileStore.SerializeParameters(holders,
            metadata);
    }

    private static void MakeCameraOffsetSemanticallyInvalid(
        string parameterPath,
        double originalOffset)
    {
        string text = File.ReadAllText(parameterPath);
        var pattern = new Regex(
            "(\\\"offsetX\\\"\\s*:\\s*)([-+0-9.eE]+)",
            RegexOptions.CultureInvariant);
        Match match = pattern.Match(text);
        Require(match.Success &&
                double.TryParse(match.Groups[2].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double parsedOffset) &&
                parsedOffset == originalOffset,
            "The synthetic sidecar could not be changed into an invalid camera offset.");
        string invalid = text.Substring(0, match.Index) +
            match.Groups[1].Value + "10001" +
            text.Substring(match.Index + match.Length);
        File.WriteAllText(parameterPath, invalid,
            new System.Text.UTF8Encoding(false));
    }

    private static void RestoreRecentChartPath(Type recentFiles, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Invoke(recentFiles.GetMethod("ForgetChartPath"), null);
        }
        else
        {
            Invoke(recentFiles.GetMethod("RememberChartPath"), null,
                new object[] { path });
        }
    }

    private static double GetCameraOffset(ChartFile chart) =>
        ((CameraEffectParameters)ChartEffectJsonCodec.BuildParameterMap(
            chart.chartDatas, "")["fx_sample"]).OffsetX;

    private static string Invariant(double value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void SetField(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(
            instance.GetType().Name, name);
        field.SetValue(instance, value);
    }

    private static object GetFieldValue(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().Name, name);
        }
        return field.GetValue(instance);
    }

    private static void SetProperty(
        object instance,
        string name,
        object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic);
        MethodInfo setter = property?.GetSetMethod(true);
        if (setter == null)
        {
            throw new MissingMemberException(instance.GetType().Name, name);
        }
        Invoke(setter, instance, new[] { value });
    }

    private static T FindSingleInScene<T>(Scene scene)
        where T : Component
    {
        var matches = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            matches.AddRange(root.GetComponentsInChildren<T>(true));
        }

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one {typeof(T).Name} in {scene.path}; found " +
                $"{matches.Count}.");
        }
        return matches[0];
    }

    private static ChartDocumentNote Note(string id, int lane, long time)
    {
        int position = checked((int)(time * 12L / 5L));
        Require(position * 5L == time * 12L,
            "Fixture note time cannot be represented exactly.");
        return new ChartDocumentNote(id, ChartNoteKind.Tap, lane, position);
    }

    private static bool InitializeJudgement(NoteJudgementSystem system,
        IReadOnlyList<ChartDocumentNote> notes, double offsetMs = 0d)
    {
        var document = new ChartDocument(4800, 4, 120);
        foreach (ChartDocumentNote note in notes) document.Notes.Add(note);
        ChartCompileResult compiled = ChartCompiler.Compile(document);
        return compiled.Succeeded && system.Initialize(compiled.Snapshot,
            offsetMs);
    }
    private static void Enqueue(NoteJudgementSystem system, int lane, double chartTime, long sequence)
    {
        var queue = (IList)typeof(NoteJudgementSystem).GetField("pendingInputs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(system);
        Type inputType = typeof(NoteJudgementSystem).GetNestedType("QueuedInput", BindingFlags.NonPublic);
        queue.Add(Activator.CreateInstance(inputType, new object[] { lane, chartTime, sequence }));
    }
    private static void WithJudgement(Action<NoteJudgementSystem, GameRule> test)
    {
        var go = new GameObject("Effect baseline judgement (inactive)");
        go.SetActive(false);
        var config = ScriptableObject.CreateInstance<GameRuleConfig>();
        try
        {
            var rule = go.AddComponent<DefaultGameRule>();
            typeof(GameRule).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(rule, config);
            var system = go.AddComponent<NoteJudgementSystem>();
            typeof(NoteJudgementSystem).GetField("gameRule", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(system, rule);
            test(system, rule);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(config); }
    }
    private static void WithTemporaryDirectory(Action<string> test)
    {
        string parent = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EffectBaselineFixtures"));
        string directory = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally
        {
            string resolved = Path.GetFullPath(directory);
            if (Path.GetDirectoryName(resolved) == parent && Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
    private static object Invoke(MethodInfo method, object instance, object[] arguments = null)
    {
        if (method == null) throw new MissingMethodException("Baseline reflection target is missing.");
        try { return method.Invoke(instance, arguments); }
        catch (TargetInvocationException exception)
        { ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw(); throw; }
    }
    private static EffectRunner GetRuntimeRunner(object runtimeSession)
    {
        if (runtimeSession == null) throw new InvalidOperationException(
            "Expected an active Effect runtime session.");
        return (EffectRunner)runtimeSession.GetType().GetField("Runner",
            BindingFlags.Instance | BindingFlags.Public).GetValue(runtimeSession);
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private sealed class RecordingEffect : Effect
    {
        private readonly Action action;
        public RecordingEffect(Action action) { this.action = action; }
        protected override void OnStart(EffectExecutionContext context) { action(); Complete(); }
    }
    private sealed class ContextRecordingEffect : Effect
    {
        private readonly Action<EffectExecutionContext> action;
        public ContextRecordingEffect(Action<EffectExecutionContext> action)
        { this.action = action; }
        protected override void OnStart(EffectExecutionContext context)
        { action(context); Complete(); }
    }
}
