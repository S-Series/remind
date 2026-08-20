using System;
using System.Collections.Generic;
using REmind.Data;
using REmind.Gameplay.Effects;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class ChartTestPlay : MonoBehaviour
{
    private const double MillisecondsPerMinute = 60000d;
    private const double ChartPositionPerMeasure =
        ChartHolder.WorldUnitsPerMeasure;

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

    private ChartCore chartCore;
    private readonly List<AutoTestNote> autoTestNotes =
        new List<AutoTestNote>();
    private float scrollYBeforeTest;
    private float previousAutoTestPositionY = float.NegativeInfinity;
    private int nextAutoTestNoteIndex;
    private bool hasStoredScrollPosition;

    public float ChartPositionY { get; private set; }
    public float CameraPositionY { get; private set; }

    private void Start()
    {
        chartCore = ChartCore.Instance;

        if (chartCore == null)
        {
            Debug.LogError("ChartTestPlay requires ChartCore in the scene.", this);
            enabled = false;
            return;
        }

        if (moveCameraTransform == null)
        {
            Debug.LogError("ChartTestPlay requires a Move Transform.", this);
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
            Debug.LogError("ChartTestPlay requires ChartScroll in the scene.", this);
            enabled = false;
            return;
        }

        chartCore.TestMsChanged += HandleTestMsChanged;
        chartCore.BpmChanged += HandleBpmChanged;
        chartCore.TestPlaybackChanged += HandleTestPlaybackChanged;

        if (chartCore.IsTestPlaying)
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
            chartCore.TestMsChanged -= HandleTestMsChanged;
            chartCore.BpmChanged -= HandleBpmChanged;
            chartCore.TestPlaybackChanged -= HandleTestPlaybackChanged;
        }
    }

    private void HandleTestMsChanged(double timelineMs)
    {
        if (chartCore.IsTestPlaying)
        {
            ApplyTimelinePosition(timelineMs);
        }
    }

    private void HandleBpmChanged(double _)
    {
        if (chartCore.IsTestPlaying)
        {
            ApplyTimelinePosition(chartCore.TestMs);
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

    /// <summary>현재 편집 페이지보다 설정된 lead-in만큼 앞에서 테스트를 전환합니다.</summary>
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

        double chartMs =
            GuideGenerate.ReferenceY /
            ChartPositionPerMeasure *
            beatsPerMeasure *
            MillisecondsPerMinute /
            chartCore.Bpm;
        double audioMs = chartMs - chartCore.StartCorrectionMs;
        chartCore.StartTestPlay(Math.Max(0d, audioMs - currentPageLeadInMs));
    }

    /// <summary>편집 스크롤을 보존하고 테스트 재생의 원점에서 카메라 이동을 시작합니다.</summary>
    private void BeginTestView()
    {
        if (!hasStoredScrollPosition)
        {
            scrollYBeforeTest = chartScroll.ScrollY;
            hasStoredScrollPosition = true;
        }

        chartScroll.SetExternalTimelineControl(true);
        laneHitEffectPlayer?.ResetAll();
        BuildAutoTestQueue();
        PrepareAutoTestQueue(chartCore.TestMs);
        ApplyTimelinePosition(chartCore.TestMs);
    }

    /// <summary>테스트 재생 중의 타임라인 시간을 카메라와 가이드 위치에 반영합니다.</summary>
    private void ApplyTimelinePosition(double timelineMs)
    {
        double measureProgress = CalculateMeasureProgress(
            timelineMs + chartCore.StartCorrectionMs,
            chartCore.Bpm);
        ChartPositionY = (float)(measureProgress * ChartPositionPerMeasure);
        chartScroll.SetExternalChartY(ChartPositionY);
        CameraPositionY = chartScroll.CameraY;
        ProcessAutoTestNotes(ChartPositionY);
    }

    /// <summary>비테스트 카메라는 0으로 복귀시키고 선택 기준은 현재 스크롤에 맞춥니다.</summary>
    private void ResetTestView()
    {
        ChartPositionY = 0f;
        CameraPositionY = 0f;

        if (hasStoredScrollPosition)
        {
            chartScroll.SetScrollY(scrollYBeforeTest);
            hasStoredScrollPosition = false;
        }

        chartScroll.SetExternalTimelineControl(false);
        GuideGenerate.SetReferenceFromScrollY(chartScroll.ScrollY);
        RestoreAutoTestNotes(clearQueue: true);

        if (hitSource)
        {
            hitSource.Stop();
        }

        laneHitEffectPlayer?.ResetAll();
    }

    /// <summary>현재 ChartManager 데이터를 위치순 자동 처리 큐로 구성합니다.</summary>
    private void BuildAutoTestQueue()
    {
        RestoreAutoTestNotes(clearQueue: true);
        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        PendingLongAutoTestNote[] pendingLongNotes =
            new PendingLongAutoTestNote[ChartHolder.TotalLineCount];

        for (int holderIndex = 0; holderIndex < holders.Count; holderIndex++)
        {
            ChartHolder holder = holders[holderIndex];
            holder.EnsureStorage();

            for (int lineIndex = 0;
                 lineIndex < ChartHolder.MainLineCount;
                 lineIndex++)
            {
                if (holder.noteTypes[lineIndex] != NoteType.Unknown)
                {
                    AddAutoTestNoteOrLongEndpoint(
                        holder.noteTypes[lineIndex],
                        holder.WorldY,
                        holder.tapNoteObjectGroups[lineIndex],
                        lineIndex,
                        pendingLongNotes);
                }

                if (holder.airNoteValues[lineIndex] > 0)
                {
                    AddAutoTestNote(
                        holder.WorldY,
                        holder.airNoteObjectGroups[lineIndex],
                        1 << lineIndex);
                }
            }

            for (int scratchIndex = 0;
                 scratchIndex < ChartHolder.ScratchLineCount;
                 scratchIndex++)
            {
                int noteTypeIndex =
                    ChartHolder.MainLineCount + scratchIndex;

                if (holder.noteTypes[noteTypeIndex] != NoteType.Unknown)
                {
                    AddAutoTestNoteOrLongEndpoint(
                        holder.noteTypes[noteTypeIndex],
                        holder.WorldY,
                        holder.scratchNoteObjectGroups[scratchIndex],
                        noteTypeIndex,
                        pendingLongNotes);
                }
            }
        }

        for (int lineIndex = 0;
             lineIndex < pendingLongNotes.Length;
             lineIndex++)
        {
            PendingLongAutoTestNote pending = pendingLongNotes[lineIndex];

            if (pending != null)
            {
                AddAutoTestNote(
                    pending.PositionY,
                    pending.NoteObjects,
                    pending.HitEffectLaneMask);
            }
        }

        autoTestNotes.Sort(
            (left, right) => left.PositionY.CompareTo(right.PositionY));
        nextAutoTestNoteIndex = 0;
        previousAutoTestPositionY = float.NegativeInfinity;
    }

    private void AddAutoTestNote(
        float positionY,
        GameObject[] noteObjects,
        int hitEffectLaneMask,
        bool hideOnProcess = true)
    {
        if (noteObjects == null || noteObjects.Length == 0)
        {
            return;
        }

        autoTestNotes.Add(new AutoTestNote(
            positionY,
            noteObjects,
            hitEffectLaneMask,
            hideOnProcess));
    }

    private void AddAutoTestNoteOrLongEndpoint(
        NoteType noteType,
        float positionY,
        GameObject[] noteObjects,
        int lineIndex,
        PendingLongAutoTestNote[] pendingLongNotes)
    {
        int hitEffectLaneMask = 1 << lineIndex;

        if (!noteType.IsLong())
        {
            AddAutoTestNote(positionY, noteObjects, hitEffectLaneMask);
            return;
        }

        PendingLongAutoTestNote pending = pendingLongNotes[lineIndex];

        if (pending == null)
        {
            pendingLongNotes[lineIndex] = new PendingLongAutoTestNote(
                positionY,
                noteObjects,
                hitEffectLaneMask);
            return;
        }

        AddAutoTestNote(
            pending.PositionY,
            pending.NoteObjects,
            pending.HitEffectLaneMask,
            hideOnProcess: false);
        AddAutoTestNote(
            positionY,
            CombineNoteObjects(pending.NoteObjects, noteObjects),
            hitEffectLaneMask);
        pendingLongNotes[lineIndex] = null;
    }

    private static GameObject[] CombineNoteObjects(
        GameObject[] startObjects,
        GameObject[] endObjects)
    {
        int startCount = startObjects?.Length ?? 0;
        int endCount = endObjects?.Length ?? 0;
        GameObject[] combined = new GameObject[startCount + endCount];

        if (startCount > 0)
        {
            Array.Copy(startObjects, 0, combined, 0, startCount);
        }

        if (endCount > 0)
        {
            Array.Copy(endObjects, 0, combined, startCount, endCount);
        }

        return combined;
    }

    private void PrepareAutoTestQueue(double timelineMs)
    {
        float chartPositionY = (float)(CalculateMeasureProgress(
            timelineMs + chartCore.StartCorrectionMs,
            chartCore.Bpm) * ChartPositionPerMeasure);
        const float positionEpsilon = 0.001f;

        while (nextAutoTestNoteIndex < autoTestNotes.Count &&
               autoTestNotes[nextAutoTestNoteIndex].PositionY <
               chartPositionY - positionEpsilon)
        {
            if (hideProcessedNotes)
            {
                autoTestNotes[nextAutoTestNoteIndex].SetProcessed();
            }

            nextAutoTestNoteIndex++;
        }

        previousAutoTestPositionY = chartPositionY;
    }

    /// <summary>현재 재생 위치까지 도달한 채보 노트를 자동으로 처리합니다.</summary>
    private void ProcessAutoTestNotes(float chartPositionY)
    {
        const float positionEpsilon = 0.001f;

        if (chartPositionY + positionEpsilon < previousAutoTestPositionY)
        {
            RestoreAutoTestNotes(clearQueue: false);
            nextAutoTestNoteIndex = 0;
        }

        previousAutoTestPositionY = chartPositionY;

        while (nextAutoTestNoteIndex < autoTestNotes.Count &&
               autoTestNotes[nextAutoTestNoteIndex].PositionY <=
               chartPositionY + positionEpsilon)
        {
            float hitPositionY =
                autoTestNotes[nextAutoTestNoteIndex].PositionY;
            int hitEffectLaneMask = 0;

            do
            {
                AutoTestNote note =
                    autoTestNotes[nextAutoTestNoteIndex];
                hitEffectLaneMask |= note.HitEffectLaneMask;

                if (hideProcessedNotes)
                {
                    note.SetProcessed();
                }

                nextAutoTestNoteIndex++;
            }
            while (nextAutoTestNoteIndex < autoTestNotes.Count &&
                   Mathf.Approximately(
                       autoTestNotes[nextAutoTestNoteIndex].PositionY,
                       hitPositionY));

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

        for (int lane = 0; lane < ChartHolder.TotalLineCount; lane++)
        {
            if ((laneMask & (1 << lane)) != 0)
            {
                laneHitEffectPlayer.Play(lane);
            }
        }
    }

    private void RestoreAutoTestNotes(bool clearQueue)
    {
        for (int i = 0; i < autoTestNotes.Count; i++)
        {
            autoTestNotes[i].Restore();
        }

        nextAutoTestNoteIndex = 0;
        previousAutoTestPositionY = float.NegativeInfinity;

        if (clearQueue)
        {
            autoTestNotes.Clear();
        }
    }

    /// <summary>
    /// 음악 시간을 현재까지 경과한 마디 수로 변환합니다.
    /// </summary>
    private double CalculateMeasureProgress(double audioMs, double bpm)
    {
        double beatCount = audioMs * bpm / MillisecondsPerMinute;
        return beatCount / beatsPerMeasure;
    }

    private sealed class AutoTestNote
    {
        private readonly GameObject[] noteObjects;
        private readonly bool[] initialActiveStates;
        private readonly bool hideOnProcess;

        public AutoTestNote(
            float positionY,
            GameObject[] sourceObjects,
            int hitEffectLaneMask,
            bool hideOnProcess)
        {
            PositionY = positionY;
            HitEffectLaneMask = hitEffectLaneMask;
            this.hideOnProcess = hideOnProcess;
            noteObjects = (GameObject[])sourceObjects.Clone();
            initialActiveStates = new bool[noteObjects.Length];

            for (int i = 0; i < noteObjects.Length; i++)
            {
                initialActiveStates[i] =
                    noteObjects[i] && noteObjects[i].activeSelf;
            }
        }

        public float PositionY { get; }
        public int HitEffectLaneMask { get; }

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

    private sealed class PendingLongAutoTestNote
    {
        public float PositionY { get; }
        public GameObject[] NoteObjects { get; }
        public int HitEffectLaneMask { get; }

        public PendingLongAutoTestNote(
            float positionY,
            GameObject[] noteObjects,
            int hitEffectLaneMask)
        {
            PositionY = positionY;
            NoteObjects = noteObjects;
            HitEffectLaneMask = hitEffectLaneMask;
        }
    }
}
