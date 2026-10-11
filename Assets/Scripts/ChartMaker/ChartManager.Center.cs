using REmind.Charting;
using REmind.Data;
using UnityEngine;

public static partial class ChartManager
{
    internal static bool EditCenterNote(GameObject obj, int position,
        ChartNotePointKind pointKind, double x, out string error,
        ChartKnobInterpolation? interpolation = null, ChartKnobInterpolation? curve = null)
    {
        error = null;
        if (!TryGetNoteData(obj, out ChartHolder source, out _, out NoteType type,
                out _, out _) || !type.IsCenter())
        { error = "Select a central note."; return false; }
        if (position < 0 || position > ChartHolder.MaximumAbsolutePosition ||
            double.IsNaN(x) || double.IsInfinity(x) || x < -1 || x > 1 ||
            (type == NoteType.Knob && pointKind != ChartNotePointKind.Start &&
             pointKind != ChartNotePointKind.Mid))
        { error = "Use a valid position, Knob Start/Middle and X in [-1, 1]."; return false; }
        ChartHolder sourceRow = GetHolder(source.AbsoluteChartPosition);
        bool isSecond = type == NoteType.Knob && sourceRow.secondKnobPoint == source;
        ChartHolder targetRow = GetHolder(position);
        if ((type == NoteType.CenterTap && targetRow != sourceRow && targetRow?.hasCenterTap == true) ||
            (type == NoteType.CenterHold && targetRow != sourceRow && targetRow?.hasCenterHold == true))
        { error = "The central note slot is occupied."; return false; }
        if ((type == NoteType.CenterTap && targetRow?.hasCenterHold == true &&
                targetRow.centerHoldPointKind == ChartNotePointKind.Start) ||
            (type == NoteType.CenterHold && source.centerHoldPointKind == ChartNotePointKind.Start &&
                targetRow?.hasCenterTap == true))
        { error = "Floor Tap and Long Floor Start cannot share the same position."; return false; }
        bool sameTime = position == source.AbsoluteChartPosition;
        if (type == NoteType.CenterHold)
        {
            pointKind = source.centerHoldPointKind;
            foreach (ChartHolder other in ChartHolders)
            {
                if (other == source || !other.hasCenterHold) continue;
                if ((other.AbsoluteChartPosition < source.AbsoluteChartPosition && position <= other.AbsoluteChartPosition) ||
                    (other.AbsoluteChartPosition > source.AbsoluteChartPosition && position >= other.AbsoluteChartPosition))
                { error = "Long Floor endpoints must stay in time order."; return false; }
            }
        }
        if (type == NoteType.Knob && !sameTime && targetRow?.hasKnobPoint == true)
        { error = "The central note slot is occupied."; return false; }
        if (type == NoteType.Knob)
        {
            foreach (ChartHolder other in ChartHolder.KnobPoints(ChartHolders))
            {
                if (other == source) continue;
                if (other.AbsoluteChartPosition == source.AbsoluteChartPosition)
                {
                    if ((!isSecond && position > other.AbsoluteChartPosition) ||
                        (isSecond && position < other.AbsoluteChartPosition))
                    { error = "Knob points must stay in time order without crossing another path."; return false; }
                    continue;
                }
                if ((other.AbsoluteChartPosition < source.AbsoluteChartPosition &&
                     position <= other.AbsoluteChartPosition) ||
                    (other.AbsoluteChartPosition > source.AbsoluteChartPosition &&
                     position >= other.AbsoluteChartPosition))
                { error = "Knob points must stay in time order without crossing another path."; return false; }
            }
            if (!ValidateKnobPoint(position, pointKind, source, out error, isSecond && sameTime)) return false;
            ChartHolder partner = isSecond ? sourceRow : sourceRow.secondKnobPoint;
            if (sameTime && partner != null && x == partner.knobX)
            { error = "First and Second Point must have different X positions."; return false; }
        }
        if (interpolation.HasValue && interpolation.Value != ChartKnobInterpolation.Linear &&
            interpolation.Value != ChartKnobInterpolation.Step)
        { error = "Choose Linear or Step interpolation."; return false; }
        ChartKnobInterpolation incoming = isSecond ? ChartKnobInterpolation.Step :
            interpolation ?? source.knobInterpolation;
        if (incoming != ChartKnobInterpolation.Linear && incoming != ChartKnobInterpolation.Step)
        { error = "Choose Linear or Step interpolation."; return false; }
        GameObject[] objects = source.GetCenterObjects(type);
        ChartKnobInterpolation outgoing = curve ?? source.knobCurve;
        if (outgoing != ChartKnobInterpolation.Linear && !KnobPathEvaluator.IsCurve(outgoing))
        { error = "Choose Linear, Sin or Cos."; return false; }
        targetRow ??= GetOrCreateHolder(position / ChartHolder.PositionUnitsPerMeasure,
            position % ChartHolder.PositionUnitsPerMeasure);
        ChartHolder target = isSecond && sameTime ? source : targetRow;
        if (type == NoteType.CenterTap)
        {
            source.ClearCenterNote(type);
            target.hasCenterTap = true;
            target.centerTapObjects = objects;
        }
        else if (type == NoteType.CenterHold)
        {
            source.ClearCenterNote(type);
            target.hasCenterHold = true;
            target.centerHoldPointKind = pointKind;
            target.centerHoldObjects = objects;
        }
        else
        {
            if (target != source)
            {
                if (isSecond) sourceRow.secondKnobPoint = null;
                else if (sourceRow.secondKnobPoint != null)
                {
                    ChartHolder remaining = sourceRow.secondKnobPoint;
                    sourceRow.secondKnobPoint = null;
                    sourceRow.knobPointKind = remaining.knobPointKind;
                    sourceRow.knobX = remaining.knobX;
                    sourceRow.knobCurve = remaining.knobCurve;
                    sourceRow.knobInterpolation = remaining.knobInterpolation;
                    sourceRow.knobPointObjects = remaining.knobPointObjects;
                }
                else source.ClearCenterNote(type);
            }
            target.hasKnobPoint = true;
            target.knobPointKind = pointKind;
            target.knobX = x;
            target.knobCurve = outgoing;
            target.knobInterpolation = incoming;
            target.knobPointObjects = objects;
            if (curve.HasValue)
            {
                ChartHolder next = GetNextKnobPoint(target);
                if (next != null && next.knobPointKind == ChartNotePointKind.Mid &&
                    next.AbsoluteChartPosition > target.AbsoluteChartPosition)
                    next.knobInterpolation = ChartKnobInterpolation.Linear;
            }
        }
        if (sourceRow != targetRow && !sourceRow.HasChartData) ChartHolderList.Remove(sourceRow);
        NotifyChartChanged();
        return true;
    }

    internal static ChartHolder GetNextKnobPoint(ChartHolder source)
    {
        bool found = false;
        foreach (ChartHolder point in ChartHolder.KnobPoints(ChartHolders))
        {
            if (found) return point;
            if (point == source) found = true;
        }
        return null;
    }

    internal static bool ValidateKnobPoint(int position, ChartNotePointKind kind,
        ChartHolder replaced, out string error, bool second = false)
    {
        error = null;
        if (kind != ChartNotePointKind.Start && kind != ChartNotePointKind.Mid)
        { error = "Choose Start or Middle."; return false; }
        ChartHolder row = GetHolder(position);
        if (second)
        {
            if (row == null || !row.hasKnobPoint ||
                (row.secondKnobPoint != null && row.secondKnobPoint != replaced))
            { error = "A row supports at most two Knob points."; return false; }
        }
        else if (row?.hasKnobPoint == true && row != replaced)
        { error = "Duplicate Knob point time."; return false; }
        return true;
    }
}
