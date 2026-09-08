using System;
using System.Collections.Generic;
using REmind.Charting;
using REmind.Gameplay.Effects;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class ChartTestPlay : MonoBehaviour
{
    private const double TargetTimeEpsilonMs = 0.0001d;
    internal const string PreviewSetupMessagePrefix =
        "Preview needs setup:";

    [Header("Movement")]
    [FormerlySerializedAs("moveCameraTranform")]
    [SerializeField] private Transform moveCameraTransform;
    [SerializeField] private ChartScroll chartScroll;
    [SerializeField, Min(1)] private int beatsPerMeasure = 4;
    [SerializeField, Min(0f)] private float currentPageLeadInMs = 1000f;

    [Header("Auto Test")]
    [SerializeField] private AudioSource hitSource;
    [SerializeField] private LaneHitEffectPlayer laneHitEffectPlayer;
    [SerializeField] private bool hideProcessedNotes = true;
    [SerializeField] private ChartPreviewFloorRenderer previewFloorRenderer;

    [Header("Editor Guidance")]
    [SerializeField] private ChartNoteSelectionController selectionController;

    private ChartCore chartCore;
    private PlayableChartSnapshot snapshot;
    private IReadOnlyDictionary<string, ChartNoteViewBinding> viewBindings;
    private readonly List<AutoTestNote> autoTestNotes =
        new List<AutoTestNote>();
    private readonly Dictionary<Transform, Vector3> originalViewPositions =
        new Dictionary<Transform, Vector3>();
    private float scrollYBeforeTest;
    private double previousAutoTestTimeMs = double.NegativeInfinity;
    private int nextAutoTestNoteIndex;
    private bool hasStoredScrollPosition;
    [Header("Effect Preview (test state only)")]
    [SerializeField, Range(0, 100)] private float effectPreviewHealth = 100f;
    private PreparedEffectPlan effectPlan;
    private EffectRunner effectRunner;
    private EffectCameraMixer effectCamera;
    private EffectRuleService effectRules;
    private EffectTransitionMailbox effectTransitions;
    private long effectSessionId;
    private bool initialized;
    private bool eventsBound;
    public string LastEffectMessage { get; private set; }

    public float ChartPositionY { get; private set; }
    public float CameraPositionY { get; private set; }
    public PlayableChartSnapshot CurrentSnapshot => snapshot;

    private void Start()
    {
        chartCore = ChartCore.Instance;

        if (chartCore == null)
        {
            Debug.LogError(
                "ChartTestPlay requires ChartCore in the scene.",
                this);
            enabled = false;
            return;
        }

        if (moveCameraTransform == null)
        {
            Debug.LogError(
                "ChartTestPlay requires a Move Transform.",
                this);
            enabled = false;
            return;
        }

        if (!chartScroll)
        {
            chartScroll = FindFirstObjectByType<ChartScroll>();
        }

        if (!hitSource)
        {
            hitSource = GetComponent<AudioSource>();
        }

        if (!laneHitEffectPlayer)
        {
            laneHitEffectPlayer =
                FindFirstObjectByType<LaneHitEffectPlayer>();
        }

        if (!chartScroll)
        {
            Debug.LogError(
                "ChartTestPlay requires ChartScroll in the scene.",
                this);
            enabled = false;
            return;
        }

        if (!previewFloorRenderer)
        {
            previewFloorRenderer =
                FindFirstObjectByType<ChartPreviewFloorRenderer>();
        }

        ResolveSelectionController();

        initialized = true;
        BindEvents();

        SynchronizePlaybackView();
    }

    private void OnEnable()
    {
        // OnEnable precedes Start on the first activation. Re-enabling after Start
        // must restore subscriptions exactly once and rebuild from current state.
        if (!initialized) return;
        BindEvents();
        SynchronizePlaybackView();
    }

    private void OnDisable()
    {
        UnbindEvents();

        // This component owns the preview Effect session. Do not leave audio and
        // timeline callbacks running after its presenter has been disabled.
        if (initialized && chartCore && chartCore.IsTestPlaying)
        {
            chartCore.EndTestPlay();
        }

        if (initialized && chartScroll)
        {
            ResetTestView();
        }
        else
        {
            DisposeEffects();
            RestoreAutoTestNotes(clearQueue: true);
            snapshot = null;
            viewBindings = null;
        }
    }

    private void OnDestroy()
    {
        UnbindEvents();
        DisposeEffects();
        RestoreAutoTestNotes(clearQueue: true);
    }

    private void BindEvents()
    {
        if (eventsBound || !chartCore) return;
        chartCore.TestPlaybackStarting += HandleTestPlaybackStarting;
        chartCore.TestPlaybackStartAborted += HandleTestPlaybackStartAborted;
        chartCore.TestMsChanged += HandleTestMsChanged;
        chartCore.TestPlaybackChanged += HandleTestPlaybackChanged;
        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound) return;
        if (chartCore)
        {
            chartCore.TestPlaybackStarting -= HandleTestPlaybackStarting;
            chartCore.TestPlaybackStartAborted -= HandleTestPlaybackStartAborted;
            chartCore.TestMsChanged -= HandleTestMsChanged;
            chartCore.TestPlaybackChanged -= HandleTestPlaybackChanged;
        }
        eventsBound = false;
    }

    private void SynchronizePlaybackView()
    {
        if (chartCore.IsTestPlaying && TryCompileSnapshot())
        {
            BeginTestView();
        }
        else
        {
            ResetTestView();
        }
    }

    private bool HandleTestPlaybackStarting(double startSongTimeMs)
    {
        long attemptId = chartCore.CurrentTestPlaybackAttemptId;
        bool succeeded = attemptId != 0L && TryCompileSnapshot() &&
            PrepareEffects(startSongTimeMs, attemptId);
        if (!succeeded && chartCore &&
            !string.IsNullOrWhiteSpace(LastEffectMessage))
        {
            chartCore.SetTestPlaybackFailureReason(LastEffectMessage);
        }

        return succeeded;
    }

    private void HandleTestPlaybackStartAborted()
    {
        long attemptId = chartCore.CurrentTestPlaybackAttemptId;
        if (attemptId != 0L && effectSessionId == attemptId)
        {
            DisposeEffects();
        }
    }

    private void HandleTestMsChanged(double songTimeMs)
    {
        if (chartCore.IsTestPlaying)
        {
            ApplyTimelinePosition(songTimeMs);
        }
    }

    private void HandleTestPlaybackChanged(bool isPlaying)
    {
        if (isPlaying)
        {
            BeginTestView();
        }
        else
        {
            ResetTestView();
        }
    }

    /// <summary>
    /// 현재 편집 페이지의 절대 Position을 TimingMap으로 시간 변환한 뒤 lead-in 앞에서
    /// 테스트를 시작합니다.
    /// </summary>
    public void ToggleTestPlayFromCurrentPage()
    {
        if (!chartCore)
        {
            chartCore = ChartCore.Instance;
        }

        if (!chartCore)
        {
            Debug.LogWarning(
                "Current-page test playback requires ChartCore.",
                this);
            return;
        }

        if (chartCore.IsTestPlaying)
        {
            chartCore.EndTestPlay();
            return;
        }

        if (!TryCompileSnapshot())
        {
            return;
        }

        int chartPosition = ChartHolder.WorldYToAbsolutePosition(
            GuideGenerate.ReferenceY);
        double chartTimeMs = snapshot.TimingMap.TimeAtPosition(
            chartPosition);
        double songTimeMs =
            chartTimeMs - chartCore.StartCorrectionMs;
        chartCore.StartTestPlay(
            Math.Max(0d, songTimeMs - currentPageLeadInMs));
    }

    private bool TryCompileSnapshot()
    {
        if (TryGuideToUnresolvedEffect())
        {
            snapshot = null;
            viewBindings = null;
            effectPlan = null;
            return false;
        }

        ChartHolderDocumentBuildResult buildResult =
            ChartHolderDocumentAdapter.Build(
                ChartManager.ChartHolders,
                chartCore.Bpm,
                beatsPerMeasure);

        LogCompileIssues(buildResult.Issues);

        if (!buildResult.Succeeded)
        {
            snapshot = null;
            viewBindings = null;
            ReportCompileFailure(buildResult.Issues);
            return false;
        }

        double worldUnitsPerPosition =
            1d / ChartHolder.PositionUnitsPerWorldUnit;
        ChartCompileResult compileResult = ChartCompiler.Compile(
            buildResult.Document,
            worldUnitsPerPosition);
        LogCompileIssues(compileResult.Issues);

        if (!compileResult.Succeeded)
        {
            snapshot = null;
            viewBindings = null;
            ReportCompileFailure(compileResult.Issues);
            return false;
        }

        snapshot = compileResult.Snapshot;
        viewBindings = buildResult.ViewBindings;
        try
        {
            effectPlan = ChartEffectPreparation.Prepare(snapshot, ChartManager.ChartHolders,
                ChartEffectDocumentState.GimmickId);
            LastEffectMessage = null;
        }
        catch (Exception exception)
        {
            ReportEffectMessage(exception.Message);
            return false;
        }
        return true;
    }

    /// <summary>
    /// An unresolved Effect is valid editor data, but it cannot be executed. Catch
    /// this expected authoring state before compilation so Preview can guide the
    /// user without producing a red Console error or a compiler stack trace.
    /// </summary>
    private bool TryGuideToUnresolvedEffect()
    {
        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];
            if (holder == null || !holder.isEffect ||
                !string.IsNullOrWhiteSpace(holder.effectTypeId))
            {
                continue;
            }

            ResolveSelectionController();
            FocusEffect(holder);
            ReportEffectMessage(
                $"{PreviewSetupMessagePrefix} Effect at measure " +
                $"{holder.ChartNumber}, position {holder.ChartPos} has no type. " +
                "Select an Effect Type and click Apply before starting Preview.");
            return true;
        }

        return false;
    }

    private void ResolveSelectionController()
    {
        if (!selectionController)
        {
            selectionController =
                FindFirstObjectByType<ChartNoteSelectionController>();
        }
    }

    private void FocusEffect(ChartHolder holder)
    {
        if (!selectionController ||
            !holder.TryGetEffectNote(out GameObject[] noteObjects) ||
            noteObjects == null)
        {
            return;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            GameObject noteObject = noteObjects[i];
            if (!noteObject ||
                !noteObject.TryGetComponent(out ChartNoteSelectable _))
            {
                continue;
            }

            selectionController.SelectNoteObject(noteObject);
            return;
        }
    }

    internal static bool IsPreviewSetupGuidance(string message)
    {
        return !string.IsNullOrWhiteSpace(message) &&
            message.StartsWith(
                PreviewSetupMessagePrefix,
                StringComparison.Ordinal);
    }

    private bool PrepareEffects(double songTimeMs, long sessionId)
    {
        if (!DisposeEffects())
        {
            return false;
        }
        try
        {
            effectCamera = new EffectCameraMixer(chartScroll.SetEffectCameraOffset);
            effectRules = new EffectRuleService(null);
            // Preview only reports requests; it has no access to scenes/accounts/progress.
            effectTransitions = new EffectTransitionMailbox((music, difficulty) =>
                !string.IsNullOrWhiteSpace(music) && !string.IsNullOrWhiteSpace(difficulty));
            var session = new EffectSessionContext(ChartEffectDocumentState.MusicId,
                ChartEffectDocumentState.DifficultyId, EffectExecutionMode.Preview, effectCamera,
                new EffectTestGameState { CurrentHealth = effectPreviewHealth }, effectRules, effectTransitions);
            effectRunner = new EffectRunner(effectPlan, session, songTimeMs + chartCore.StartCorrectionMs);
            effectSessionId = sessionId;
            return true;
        }
        catch (Exception exception)
        {
            DisposeEffects();
            ReportEffectMessage(exception.Message);
            return false;
        }
    }

    private bool DisposeEffects()
    {
        // Detach the current generation before invoking user/plugin cleanup. A
        // re-entrant callback cannot observe or dispose a newly created session.
        EffectRunner runnerToDispose = effectRunner;
        EffectRuleService rulesToDispose = effectRules;
        EffectCameraMixer cameraToDispose = effectCamera;
        EffectTransitionMailbox transitionsToDispose = effectTransitions;
        effectSessionId = 0L;
        effectRunner = null;
        effectRules = null;
        effectCamera = null;
        effectTransitions = null;

        var failures = new List<Exception>();
        EffectExecutionFailure runnerFailureBeforeDispose =
            runnerToDispose?.Failure;
        DisposeEffectResource(runnerToDispose, failures);
        if (runnerToDispose?.Failure != null &&
            !ReferenceEquals(
                runnerFailureBeforeDispose,
                runnerToDispose.Failure))
        {
            // EffectRunner records cleanup failures instead of throwing them.
            // Surface a failure created by this Dispose call together with the
            // other preview-owned resource cleanup failures.
            failures.Add(runnerToDispose.Failure.Exception);
        }
        DisposeEffectResource(rulesToDispose, failures);
        DisposeEffectResource(cameraToDispose, failures);
        DisposeEffectResource(transitionsToDispose, failures);

        if (failures.Count > 0)
        {
            var cleanupFailure = new AggregateException(
                "Effect preview cleanup failed.", failures);
            LastEffectMessage = cleanupFailure.Message;
            Debug.LogException(cleanupFailure, this);
            return false;
        }

        return true;
    }

    private static void DisposeEffectResource(
        IDisposable resource,
        ICollection<Exception> failures)
    {
        if (resource == null) return;
        try
        {
            resource.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private void ReportEffectMessage(string message)
    {
        LastEffectMessage = message;
        // During a Core-owned start attempt, ChartCore publishes the final
        // rejection once all validators have rolled back. Avoid logging the same
        // expected authoring message twice.
        if (!chartCore || !chartCore.IsTestPlaybackStartInProgress)
        {
            Debug.LogWarning("Effect preview: " + message, this);
        }
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(LastEffectMessage))
            GUI.Box(new Rect(20, 20, Math.Min(720, Screen.width - 40), 100), LastEffectMessage);
    }

    private void LogCompileIssues(IReadOnlyList<CompileIssue> issues)
    {
        for (int i = 0; i < issues.Count; i++)
        {
            CompileIssue issue = issues[i];
            string message =
                $"Chart compile {issue.Code}: {issue.Message}";

            if (issue.Severity == CompileIssueSeverity.Error)
            {
                Debug.LogError(message, this);
            }
            else
            {
                Debug.LogWarning(message, this);
            }
        }
    }

    private void ReportCompileFailure(IReadOnlyList<CompileIssue> issues)
    {
        string message = "The chart could not be prepared for Preview.";
        if (issues != null)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                CompileIssue issue = issues[i];
                if (issue.Severity != CompileIssueSeverity.Error)
                {
                    continue;
                }

                message = $"Preview blocked ({issue.Code}): {issue.Message}";
                break;
            }
        }

        ReportEffectMessage(message);
    }

    private void BeginTestView()
    {
        long sessionId = chartCore.ActiveTestPlaybackSessionId;
        if (sessionId == 0L)
        {
            chartCore.EndTestPlay();
            return;
        }

        if (snapshot == null && !TryCompileSnapshot())
        {
            return;
        }

        if (effectRunner != null && effectSessionId != sessionId &&
            !DisposeEffects())
        {
            chartCore.EndTestPlay();
            return;
        }

        if (effectRunner == null &&
            !PrepareEffects(chartCore.TestMs, sessionId))
        {
            chartCore.EndTestPlay();
            return;
        }

        if (!hasStoredScrollPosition)
        {
            scrollYBeforeTest = chartScroll.ScrollY;
            hasStoredScrollPosition = true;
        }

        chartScroll.SetExternalTimelineControl(true);
        laneHitEffectPlayer?.ResetAll();
        ApplySnapshotViewGeometry();
        BuildAutoTestQueue();
        double chartTimeMs =
            chartCore.TestMs + chartCore.StartCorrectionMs;
        PrepareAutoTestQueue(chartTimeMs);
        ApplyTimelinePosition(chartCore.TestMs);
    }

    /// <summary>
    /// SongTimeMs를 현재 TimingPoint의 절대식으로 FloorPosition에 변환합니다.
    /// 카메라에는 프레임 이동량이나 BPM 보정값을 누적하지 않습니다.
    /// </summary>
    private void ApplyTimelinePosition(double songTimeMs)
    {
        if (snapshot == null)
        {
            return;
        }

        double chartTimeMs =
            songTimeMs + chartCore.StartCorrectionMs;
        long sessionId = effectSessionId;
        try
        {
            if (chartCore.HasReachedScheduledStart)
            {
                bool frameSucceeded = ProcessTestFrame(chartTimeMs);
                if (!IsCurrentEffectSession(sessionId))
                {
                    return;
                }

                if (!frameSucceeded)
                {
                    string message = effectRunner.Failure != null ? effectRunner.Failure.Message :
                        $"Preview transition requested: {effectTransitions.TargetMusicId}/{effectTransitions.TargetDifficultyId}. No real song/account change was performed.";
                    chartCore.EndTestPlay();
                    ReportEffectMessage(message);
                    return;
                }
            }
            effectCamera?.Apply();
            if (!IsCurrentEffectSession(sessionId))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            chartCore.EndTestPlay();
            ReportEffectMessage(exception.Message);
            return;
        }
        ApplyCameraMotion(chartTimeMs);
        ChartPositionY = (float)snapshot.ScrollMap.FloorPositionAtTime(
            chartTimeMs);
        chartScroll.SetExternalChartY(ChartPositionY);
        CameraPositionY = chartScroll.CameraY;
    }

    private bool IsCurrentEffectSession(long sessionId)
    {
        return sessionId != 0L && effectSessionId == sessionId &&
            chartCore.IsTestPlaying &&
            chartCore.ActiveTestPlaybackSessionId == sessionId &&
            snapshot != null;
    }

    private void ApplyCameraMotion(double chartTimeMs)
    {
        CameraMotionState cameraState =
            snapshot.CameraMotionMap.EvaluateAtTime(chartTimeMs);
        float referenceX = previewFloorRenderer
            ? previewFloorRenderer.EvaluateCameraReferenceX(cameraState)
            : 0f;

        ChartScroll.PreviewCameraRotationReferenceX = referenceX;
        ChartScroll.PreviewCameraSpinDegrees =
            (float)cameraState.SpinDegrees;
        ChartScroll.PreviewCameraScratchTiltDegrees =
            (float)snapshot.ScratchCameraTiltMap.EvaluateAtTime(
                chartTimeMs);
    }

    private void ResetTestView()
    {
        DisposeEffects();
        ChartPositionY = 0f;
        CameraPositionY = 0f;
        ChartScroll.PreviewCameraRotationReferenceX = 0f;
        ChartScroll.PreviewCameraSpinDegrees = 0f;
        ChartScroll.PreviewCameraScratchTiltDegrees = 0f;

        if (hasStoredScrollPosition)
        {
            chartScroll.SetScrollY(scrollYBeforeTest);
            hasStoredScrollPosition = false;
        }

        chartScroll.SetExternalTimelineControl(false);
        GuideGenerate.SetReferenceFromScrollY(chartScroll.ScrollY);
        RestoreAutoTestNotes(clearQueue: true);
        RestoreSnapshotViewGeometry();
        FindFirstObjectByType<ChartPreviewFloorRenderer>()
            ?.SetSessionSnapshot(null);
        snapshot = null;
        viewBindings = null;

        if (hitSource)
        {
            hitSource.Stop();
        }

        laneHitEffectPlayer?.ResetAll();
    }

    private void ApplySnapshotViewGeometry()
    {
        RestoreSnapshotViewGeometry();

        if (snapshot == null || viewBindings == null)
        {
            return;
        }

        foreach (ChartNoteViewBinding binding in viewBindings.Values)
        {
            for (int pointIndex = 0;
                 pointIndex < binding.Points.Count;
                 pointIndex++)
            {
                ChartNoteViewPointBinding point = binding.Points[pointIndex];
                float floorY = (float)snapshot.ScrollMap
                    .FloorPositionAtChartPosition(point.Position);
                float nextFloorY = pointIndex + 1 < binding.Points.Count
                    ? (float)snapshot.ScrollMap.FloorPositionAtChartPosition(
                        binding.Points[pointIndex + 1].Position)
                    : floorY;

                for (int objectIndex = 0;
                     objectIndex < point.NoteObjects.Length;
                     objectIndex++)
                {
                    GameObject noteObject = point.NoteObjects[objectIndex];

                    if (!noteObject)
                    {
                        continue;
                    }

                    Transform noteTransform = noteObject.transform;

                    if (!originalViewPositions.ContainsKey(noteTransform))
                    {
                        originalViewPositions.Add(
                            noteTransform,
                            noteTransform.localPosition);
                    }

                    Vector3 localPosition = noteTransform.localPosition;
                    localPosition.y = floorY;
                    noteTransform.localPosition = localPosition;

                    if (noteObject.TryGetComponent(
                            out NoteLength noteLength))
                    {
                        noteLength.SetStraightLength(
                            Mathf.Max(0f, nextFloorY - floorY));
                    }
                }
            }
        }

        FindFirstObjectByType<ChartPreviewFloorRenderer>()
            ?.SetSessionSnapshot(snapshot);
    }

    private void RestoreSnapshotViewGeometry()
    {
        foreach (KeyValuePair<Transform, Vector3> pair in
                 originalViewPositions)
        {
            if (pair.Key)
            {
                pair.Key.localPosition = pair.Value;
            }
        }

        originalViewPositions.Clear();

        for (int line = 1; line <= ChartHolder.MainLineCount; line++)
        {
            ChartManager.RefreshLongNoteLengths(line);
        }

        ChartManager.RefreshLongNoteLengths(-1);
        ChartManager.RefreshLongNoteLengths(-2);
    }

    /// <summary>
    /// Snapshot의 불변 판정 타깃으로 자동 테스트 큐를 구성합니다. ChartHolder 저장
    /// 배열은 어댑터 밖에서 해석하지 않습니다.
    /// </summary>
    private void BuildAutoTestQueue()
    {
        RestoreAutoTestNotes(clearQueue: true);

        for (int i = 0; i < snapshot.JudgementTargets.Count; i++)
        {
            JudgementTarget target = snapshot.JudgementTargets[i];
            GameObject[] noteObjects = Array.Empty<GameObject>();

            if (viewBindings != null &&
                viewBindings.TryGetValue(
                    target.NoteId,
                    out ChartNoteViewBinding binding))
            {
                noteObjects = target.Kind == JudgementTargetKind.HoldEnd
                    ? binding.AllObjects
                    : binding.StartObjects;
            }

            autoTestNotes.Add(new AutoTestNote(
                target.TargetTimeMs,
                noteObjects,
                target.Lane,
                target.Kind != JudgementTargetKind.HoldStart));
        }

        autoTestNotes.Sort(
            (left, right) =>
                left.TargetTimeMs.CompareTo(right.TargetTimeMs));
        nextAutoTestNoteIndex = 0;
        previousAutoTestTimeMs = double.NegativeInfinity;
    }

    private void PrepareAutoTestQueue(double chartTimeMs)
    {
        while (nextAutoTestNoteIndex < autoTestNotes.Count &&
               autoTestNotes[nextAutoTestNoteIndex].TargetTimeMs <
               chartTimeMs - TargetTimeEpsilonMs)
        {
            if (hideProcessedNotes)
            {
                autoTestNotes[nextAutoTestNoteIndex].SetProcessed();
            }

            nextAutoTestNoteIndex++;
        }

        previousAutoTestTimeMs = chartTimeMs;
    }

    /// <summary>
    /// Effect와 자동 판정 타깃을 하나의 시간축으로 처리합니다. 지연 프레임에서도
    /// Effect 시작에는 예약 시각이 아닌 실제 프레임 시각을 전달하고, 같은 시각에는
    /// Effect를 먼저 실행한 뒤 활성 Effect/Gimmick을 프레임 끝에서 한 번만 갱신합니다.
    /// </summary>
    private bool ProcessTestFrame(double chartTimeMs)
    {
        if (chartTimeMs + TargetTimeEpsilonMs < previousAutoTestTimeMs)
        {
            RestoreAutoTestNotes(clearQueue: false);
            nextAutoTestNoteIndex = 0;
        }

        previousAutoTestTimeMs = chartTimeMs;

        while (true)
        {
            double nextEffectTimeMs = effectRunner != null
                ? effectRunner.NextEventTimeMs
                : double.PositiveInfinity;
            double nextNoteTimeMs =
                nextAutoTestNoteIndex < autoTestNotes.Count
                    ? autoTestNotes[nextAutoTestNoteIndex].TargetTimeMs
                    : double.PositiveInfinity;
            bool effectIsDue = nextEffectTimeMs <= chartTimeMs;
            bool noteIsDue = nextNoteTimeMs <=
                chartTimeMs + TargetTimeEpsilonMs;

            if (!effectIsDue && !noteIsDue)
            {
                break;
            }

            if (effectIsDue &&
                (!noteIsDue || nextEffectTimeMs <=
                    nextNoteTimeMs + TargetTimeEpsilonMs))
            {
                if (!effectRunner.TriggerThrough(
                        nextEffectTimeMs,
                        chartTimeMs))
                {
                    return false;
                }

                continue;
            }

            // Auto-test notes keep their historical epsilon tolerance. If an
            // Effect shares that tolerated timestamp but is still fractionally
            // in the future, defer the note so the Effect remains first.
            if (!effectIsDue &&
                nextEffectTimeMs <= chartTimeMs + TargetTimeEpsilonMs &&
                Math.Abs(nextEffectTimeMs - nextNoteTimeMs) <=
                    TargetTimeEpsilonMs)
            {
                break;
            }

            ProcessNextAutoTestNoteTime();
        }

        return effectRunner == null || effectRunner.AdvanceTo(chartTimeMs);
    }

    private void ProcessNextAutoTestNoteTime()
    {
        double hitTimeMs =
            autoTestNotes[nextAutoTestNoteIndex].TargetTimeMs;
        int hitEffectLaneMask = 0;

        do
        {
            AutoTestNote note = autoTestNotes[nextAutoTestNoteIndex];
            hitEffectLaneMask |= 1 << note.Lane;

            if (hideProcessedNotes)
            {
                note.SetProcessed();
            }

            nextAutoTestNoteIndex++;
        }
        while (nextAutoTestNoteIndex < autoTestNotes.Count &&
               Math.Abs(
                   autoTestNotes[nextAutoTestNoteIndex].TargetTimeMs -
                   hitTimeMs) <= TargetTimeEpsilonMs);

        PlayHitSound();
        PlayHitEffects(hitEffectLaneMask);
    }

    private void PlayHitSound()
    {
        if (hitSource && hitSource.clip)
        {
            hitSource.PlayOneShot(hitSource.clip);
        }
    }

    private void PlayHitEffects(int laneMask)
    {
        if (!laneHitEffectPlayer)
        {
            return;
        }

        for (int lane = 0; lane < ChartLaneLayout.LaneCount; lane++)
        {
            if ((laneMask & (1 << lane)) != 0)
            {
                laneHitEffectPlayer.Play(GetPresentationEffectLane(lane));
            }
        }
    }

    private static int GetPresentationEffectLane(int inputLane)
    {
        if (inputLane >= (int)ChartLane.AirMain1 &&
            inputLane <= (int)ChartLane.AirMain4)
        {
            return inputLane - (int)ChartLane.AirMain1;
        }

        if (inputLane >= (int)ChartLane.GroundLeft)
        {
            return ChartHolder.MainLineCount +
                inputLane - (int)ChartLane.GroundLeft;
        }

        return inputLane;
    }

    private void RestoreAutoTestNotes(bool clearQueue)
    {
        for (int i = 0; i < autoTestNotes.Count; i++)
        {
            autoTestNotes[i].Restore();
        }

        nextAutoTestNoteIndex = 0;
        previousAutoTestTimeMs = double.NegativeInfinity;

        if (clearQueue)
        {
            autoTestNotes.Clear();
        }
    }

    private sealed class AutoTestNote
    {
        private readonly GameObject[] noteObjects;
        private readonly bool[] initialActiveStates;
        private readonly bool hideOnProcess;

        public AutoTestNote(
            double targetTimeMs,
            GameObject[] sourceObjects,
            int lane,
            bool hideOnProcess)
        {
            TargetTimeMs = targetTimeMs;
            Lane = lane;
            this.hideOnProcess = hideOnProcess;
            noteObjects = sourceObjects != null
                ? (GameObject[])sourceObjects.Clone()
                : Array.Empty<GameObject>();
            initialActiveStates = new bool[noteObjects.Length];

            for (int i = 0; i < noteObjects.Length; i++)
            {
                initialActiveStates[i] =
                    noteObjects[i] && noteObjects[i].activeSelf;
            }
        }

        public double TargetTimeMs { get; }
        public int Lane { get; }

        public void SetProcessed()
        {
            if (!hideOnProcess)
            {
                return;
            }

            for (int i = 0; i < noteObjects.Length; i++)
            {
                if (noteObjects[i])
                {
                    noteObjects[i].SetActive(false);
                }
            }
        }

        public void Restore()
        {
            for (int i = 0; i < noteObjects.Length; i++)
            {
                if (noteObjects[i])
                {
                    noteObjects[i].SetActive(initialActiveStates[i]);
                }
            }
        }
    }
}
