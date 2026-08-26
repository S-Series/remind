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

        chartCore.TestPlaybackStarting += HandleTestPlaybackStarting;
        chartCore.TestMsChanged += HandleTestMsChanged;
        chartCore.TestPlaybackChanged += HandleTestPlaybackChanged;

        if (chartCore.IsTestPlaying && TryCompileSnapshot())
        {
            BeginTestView();
        }
        else
        {
            ResetTestView();
        }
    }

    private void OnDestroy()
    {
        RestoreAutoTestNotes(clearQueue: true);

        if (chartCore != null)
        {
            chartCore.TestPlaybackStarting -= HandleTestPlaybackStarting;
            chartCore.TestMsChanged -= HandleTestMsChanged;
            chartCore.TestPlaybackChanged -= HandleTestPlaybackChanged;
        }
    }

    private bool HandleTestPlaybackStarting(double _)
    {
        return TryCompileSnapshot();
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
            return false;
        }

        snapshot = compileResult.Snapshot;
        viewBindings = buildResult.ViewBindings;
        return true;
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

    private void BeginTestView()
    {
        if (snapshot == null && !TryCompileSnapshot())
        {
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
        ApplyCameraMotion(chartTimeMs);
        ChartPositionY = (float)snapshot.ScrollMap.FloorPositionAtTime(
            chartTimeMs);
        chartScroll.SetExternalChartY(ChartPositionY);
        CameraPositionY = chartScroll.CameraY;
        ProcessAutoTestNotes(chartTimeMs);
    }

    private void ApplyCameraMotion(double chartTimeMs)
    {
        CameraMotionState cameraState =
            snapshot.CameraMotionMap.EvaluateAtTime(chartTimeMs);
        float referenceX = 0f;

        if (cameraState.HasReference)
        {
            float lineX = previewFloorRenderer
                ? previewFloorRenderer.EvaluateCenterOffsetX(
                    (float)cameraState.ReferenceFloorPosition)
                : 0f;
            referenceX = lineX +
                (float)cameraState.ReferenceOffsetX;
        }

        ChartScroll.PreviewCameraRotationReferenceX = referenceX;
        ChartScroll.PreviewCameraSpinDegrees =
            (float)cameraState.SpinDegrees;
    }

    private void ResetTestView()
    {
        ChartPositionY = 0f;
        CameraPositionY = 0f;
        ChartScroll.PreviewCameraRotationReferenceX = 0f;
        ChartScroll.PreviewCameraSpinDegrees = 0f;

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

    private void ProcessAutoTestNotes(double chartTimeMs)
    {
        if (chartTimeMs + TargetTimeEpsilonMs < previousAutoTestTimeMs)
        {
            RestoreAutoTestNotes(clearQueue: false);
            nextAutoTestNoteIndex = 0;
        }

        previousAutoTestTimeMs = chartTimeMs;

        while (nextAutoTestNoteIndex < autoTestNotes.Count &&
               autoTestNotes[nextAutoTestNoteIndex].TargetTimeMs <=
               chartTimeMs + TargetTimeEpsilonMs)
        {
            double hitTimeMs =
                autoTestNotes[nextAutoTestNoteIndex].TargetTimeMs;
            int hitEffectLaneMask = 0;

            do
            {
                AutoTestNote note =
                    autoTestNotes[nextAutoTestNoteIndex];
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
