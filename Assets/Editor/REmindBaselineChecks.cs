using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using REmind.Charting;
using REmind.Data;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Input.Judgement;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Test-only bridge to Unity's predefined Assembly-CSharp. Test asmdefs cannot reference that
/// assembly directly; the NUnit wrapper calls this editor-only helper through one reflection boundary.
/// No scene, prefab, user chart, audio or project setting is changed by these checks.
/// </summary>
public static class REmindBaselineChecks
{
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
            Require(current.chartDatas[0].effectId == "fx_sample", "Effect identity changed across saves.");
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
            "{\"durationMs\":1e999}", "{\"durationMs\":1,\"durationMs\":2}", "{\"durationMs\":1,\"typo\":1}" })
            Require(!ChartEffectJsonCodec.TryDecode("camera.offset", "", "", json, out _, out _),
                "Malformed parameters were accepted: " + json);
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
            Require(system.Initialize(10, new[] { Note("tap", 0, 100) }), "Initialization failed.");
            var results = new List<NoteJudgementEvent>();
            system.NoteJudged += results.Add;
            Enqueue(system, 0, 100, 0);
            system.ProcessFrame(400);
            Require(results.Count == 1 && results[0].Result == JudgeResult.Perfect && !results[0].IsAutomaticMiss,
                "An on-time queued input became an automatic Miss when its frame was late.");
        });
    }

    private static void Judgement_OffsetsAndExactMissBoundary()
    {
        WithJudgement((system, rule) =>
        {
            system.Initialize(10, new[] { Note("tap", 0, 100) }, 100);
            system.SetUserOffsetMs(25);
            system.ProcessFrame(375); // 100 note + 100 chart offset + 25 input offset + 150 miss window.
            Require(system.PendingNoteCount == 1, "Automatic Miss must be strictly outside its window.");
            system.ProcessFrame(375.01);
            Require(system.PendingNoteCount == 0, "Automatic Miss did not advance beyond its boundary.");
            system.ResetJudgements(false);
            JudgeResult received = JudgeResult.None;
            system.NoteJudged += value => received = value.Result;
            Enqueue(system, 0, 125, 0); // queued chart time = input song time 225 - chart offset 100.
            system.ProcessFrame(300);
            Require(received == JudgeResult.Perfect, "Chart/input offsets were double-applied or reversed.");
        });
    }

    private static void Judgement_EffectBeforeSameTimeInput()
    {
        WithJudgement((system, rule) =>
        {
            system.Initialize(10, new[] { Note("tap", 0, 100) });
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
            Require(system.Initialize(10, new[]
            {
                Note("input-second", 0, 100),
                Note("input-first", 1, 100),
                Note("automatic", 2, 100)
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
                value.IsAutomaticMiss ? "automatic" : value.Note.Id);
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
            Require(system.Initialize(10, new[] { Note("tap", 0, 100) }),
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
        Vector2 applied = Vector2.zero;
        int applyCount = 0;
        var oldMixer = new EffectCameraMixer((offset, roll) =>
        {
            applied = offset;
            applyCount++;
        });
        IEffectCameraOffset oldHandle = oldMixer.CreateOffset("same-id");
        oldHandle.Set(2, 0, 0);
        oldMixer.Apply();
        oldMixer.Dispose();
        int countAfterDispose = applyCount;

        using var currentMixer = new EffectCameraMixer((offset, roll) =>
        {
            applied = offset;
            applyCount++;
        });
        currentMixer.CreateOffset("same-id").Set(4, 0, 0);
        currentMixer.Apply();
        oldMixer.Apply();
        Require(applied.x == 4 && applyCount == countAfterDispose + 1,
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
            Require(system.Initialize(10, Array.Empty<NoteData>()),
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
            var metadata = new ChartEffectDocumentState.Metadata(
                "song", "default", "sample", "revision");
            Require(controller.Prepare(snapshot, new[] { holder }, metadata, 0),
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

            var invalidHolder = new ChartHolder(0, 0)
            {
                isEffect = true,
                effectId = "call",
                effectTypeId = "unknown.effect.type",
                effectCommandId = "count-success",
                effectParametersJson = string.Empty
            };
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "song/default:.*unknown.effect.type",
                    System.Text.RegularExpressions.RegexOptions.Singleline));
            Require(!controller.Prepare(snapshot, new[] { invalidHolder }, metadata, 0),
                "Invalid Effect definitions unexpectedly replaced the prepared plan.");
            Require(!(bool)Invoke(starting, controller, new object[] { 0d }) &&
                    active.GetValue(controller) == null &&
                    pending.GetValue(controller) == null,
                "A failed Prepare left a stale Effect plan available for playback.");
            Require(controller.Prepare(snapshot, new[] { holder }, metadata, 0),
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
            system.Initialize(10, new[] { Note("a", 0, 0), Note("b", 1, 100), Note("c", 2, 200) });
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
                new RuleContext(100, 0, result.Note.Type, false, false, false, result.EffectiveHitTimeMs + result.OffsetMs)));
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
            system.Initialize(10, new[] { Note("late", 0, 200), Note("early", 1, 100) });
            system.SetAutoPlayEnabled(true);
            var ids = new List<string>();
            system.NoteJudged += value => ids.Add(value.Note.Id);
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
                Require(File.Exists(chartPath) && File.Exists(parameterPath) &&
                        !string.IsNullOrWhiteSpace(savedRevision) &&
                        !saver.HasUnsavedChanges,
                    "Save did not produce a clean matching chart/Effect pair.");
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
                PreparedEffectPlan plan = ChartEffectPreparation.Prepare(
                    compiled.Snapshot, ChartManager.ChartHolders,
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
        chartText = ChartFileCodec.Serialize(holders, 120, 0, metadata);
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

    private static NoteData Note(string id, int lane, long time) => (NoteData)Activator.CreateInstance(typeof(NoteData),
        BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { id, NoteType.Tap, lane, time, 0L, null }, null);
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
