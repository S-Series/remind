using System.Collections.Generic;
using REmind.Common.UI;
using REmind.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartPreviewFloorRenderer : MonoBehaviour
{
    private const float DefaultHalfWidth = 15f;
    private const float DefaultMaximumOffsetUnits = 400f;

    [SerializeField] private RectTransform previewLineField;
    [SerializeField] private FourPointGraphic sourceGraphic;
    [SerializeField] private float halfWidth = DefaultHalfWidth;
    [SerializeField] private float maximumOffsetUnits =
        DefaultMaximumOffsetUnits;

    private readonly List<FourPointGraphic> generatedGraphics =
        new List<FourPointGraphic>();
    private readonly List<MotionInterval> motionIntervals =
        new List<MotionInterval>();

    private void OnEnable()
    {
        ChartManager.ChartChanged += Rebuild;
    }

    private void Start()
    {
        if (!previewLineField || !sourceGraphic)
        {
            Debug.LogError(
                "ChartPreviewFloorRenderer requires the Preview LineField " +
                "and its source FourPointGraphic.",
                this);
            enabled = false;
            return;
        }

        sourceGraphic.enabled = false;
        Rebuild();
    }

    private void OnDisable()
    {
        ChartManager.ChartChanged -= Rebuild;
    }

    public void Rebuild()
    {
        if (!previewLineField || !sourceGraphic)
        {
            return;
        }

        ClearGeneratedGraphics();
        CollectMotionIntervals();

        float previewEndY = Mathf.Max(
            0f,
            sourceGraphic.GetPoint(1).y);
        float cursorY = Mathf.Clamp(
            sourceGraphic.GetPoint(0).y,
            0f,
            previewEndY);
        float currentCenterX = 0f;

        for (int i = 0; i < motionIntervals.Count; i++)
        {
            MotionInterval interval = motionIntervals[i];
            float intervalStartY = Mathf.Clamp(
                interval.StartY,
                cursorY,
                previewEndY);

            AddSegment(
                cursorY,
                intervalStartY,
                currentCenterX,
                currentCenterX);

            float motionStartX = OffsetUnitsToPreviewX(
                interval.Motion.StartOffsetUnits);
            float motionEndX = OffsetUnitsToPreviewX(
                interval.Motion.EndOffsetUnits);
            float intervalEndY = Mathf.Clamp(
                interval.EndY,
                intervalStartY,
                previewEndY);

            if (intervalEndY > intervalStartY)
            {
                if (interval.Motion.MotionType ==
                    ScratchMotionType.Gradual)
                {
                    AddSegment(
                        intervalStartY,
                        intervalEndY,
                        motionStartX,
                        motionEndX);
                }
                else
                {
                    AddSegment(
                        intervalStartY,
                        intervalEndY,
                        motionEndX,
                        motionEndX);
                }
            }

            currentCenterX = motionEndX;
            cursorY = intervalEndY;

            if (cursorY >= previewEndY)
            {
                break;
            }
        }

        AddSegment(
            cursorY,
            previewEndY,
            currentCenterX,
            currentCenterX);
    }

    private void CollectMotionIntervals()
    {
        motionIntervals.Clear();
        CollectLineMotionIntervals(-1);
        CollectLineMotionIntervals(-2);
        motionIntervals.Sort(CompareMotionIntervals);
    }

    private void CollectLineMotionIntervals(int line)
    {
        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        ChartHolder pendingLongStart = null;

        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];

            if (!holder.TryGetNote(
                    line,
                    out NoteType noteType,
                    out _) ||
                !noteType.IsScratch())
            {
                continue;
            }

            if (noteType == NoteType.Scratch)
            {
                if (IsPowered(holder, line))
                {
                    AddMotionInterval(
                        holder,
                        holder,
                        holder.GetScratchMotion(line));
                }

                continue;
            }

            if (pendingLongStart == null)
            {
                pendingLongStart = holder;
                continue;
            }

            bool startPowered = IsPowered(pendingLongStart, line);
            bool endPowered = IsPowered(holder, line);

            if (startPowered || endPowered)
            {
                ChartHolder motionHolder = startPowered
                    ? pendingLongStart
                    : holder;
                AddMotionInterval(
                    pendingLongStart,
                    holder,
                    motionHolder.GetScratchMotion(line));
            }

            pendingLongStart = null;
        }

        if (pendingLongStart != null && IsPowered(pendingLongStart, line))
        {
            AddMotionInterval(
                pendingLongStart,
                pendingLongStart,
                pendingLongStart.GetScratchMotion(line));
        }
    }

    private void AddMotionInterval(
        ChartHolder startHolder,
        ChartHolder endHolder,
        ScratchMotionData motion)
    {
        motionIntervals.Add(new MotionInterval(
            startHolder.WorldY,
            endHolder.WorldY,
            motion));
    }

    private void AddSegment(
        float startY,
        float endY,
        float startCenterX,
        float endCenterX)
    {
        if (endY - startY <= Mathf.Epsilon)
        {
            return;
        }

        GameObject segmentObject = Instantiate(
            sourceGraphic.gameObject,
            previewLineField,
            false);
        segmentObject.name = "Powered Floor Segment";
        segmentObject.layer = previewLineField.gameObject.layer;

        FourPointGraphic segment =
            segmentObject.GetComponent<FourPointGraphic>();
        segment.enabled = true;
        segment.raycastTarget = false;
        segment.SetPoints(
            new Vector2(startCenterX - halfWidth, startY),
            new Vector2(endCenterX - halfWidth, endY),
            new Vector2(endCenterX + halfWidth, endY),
            new Vector2(startCenterX + halfWidth, startY));
        generatedGraphics.Add(segment);
    }

    private void ClearGeneratedGraphics()
    {
        for (int i = 0; i < generatedGraphics.Count; i++)
        {
            FourPointGraphic graphic = generatedGraphics[i];

            if (graphic)
            {
                Destroy(graphic.gameObject);
            }
        }

        generatedGraphics.Clear();
    }

    private float OffsetUnitsToPreviewX(int offsetUnits)
    {
        float safeMaximum = Mathf.Max(1f, maximumOffsetUnits);
        return Mathf.Clamp(
            offsetUnits / safeMaximum,
            -1f,
            1f) * halfWidth;
    }

    private static bool IsPowered(ChartHolder holder, int line)
    {
        int scratchIndex = ChartHolder.MainLineCount +
            (line == -1 ? 0 : 1);
        return holder.isPoweredNotes != null &&
            scratchIndex < holder.isPoweredNotes.Length &&
            holder.isPoweredNotes[scratchIndex];
    }

    private static int CompareMotionIntervals(
        MotionInterval left,
        MotionInterval right)
    {
        int startComparison = left.StartY.CompareTo(right.StartY);
        return startComparison != 0
            ? startComparison
            : left.EndY.CompareTo(right.EndY);
    }

    private readonly struct MotionInterval
    {
        public float StartY { get; }
        public float EndY { get; }
        public ScratchMotionData Motion { get; }

        public MotionInterval(
            float startY,
            float endY,
            ScratchMotionData motion)
        {
            StartY = startY;
            EndY = endY;
            Motion = motion;
        }
    }
}
