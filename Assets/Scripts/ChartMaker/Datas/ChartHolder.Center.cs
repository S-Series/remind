using System;
using System.Collections.Generic;
using REmind.Charting;
using REmind.Data;
using UnityEngine;

public partial class ChartHolder
{
    public bool hasCenterTap;
    public bool hasKnobPoint;
    public ChartNotePointKind knobPointKind = ChartNotePointKind.Start;
    public double knobX;
    public ChartKnobInterpolation knobInterpolation;
    public ChartKnobInterpolation knobCurve;
    // The second slot contains only Knob data at this row's position.
    public ChartHolder secondKnobPoint;

    internal static IEnumerable<ChartHolder> KnobPoints(
        IEnumerable<ChartHolder> rows)
    {
        foreach (ChartHolder row in rows)
        {
            if (row == null || !row.hasKnobPoint) continue;
            yield return row;
            if (row.secondKnobPoint != null) yield return row.secondKnobPoint;
        }
    }

    internal bool IsSecondKnobObject(GameObject obj) =>
        secondKnobPoint != null && FindNoteObject(secondKnobPoint.knobPointObjects, obj);

    internal void RemoveKnobPoint(ChartHolder point)
    {
        if (point == secondKnobPoint) { secondKnobPoint = null; return; }
        if (secondKnobPoint == null) { ClearCenterNote(NoteType.Knob); return; }
        ChartHolder second = secondKnobPoint;
        secondKnobPoint = null;
        knobPointKind = second.knobPointKind;
        knobX = second.knobX;
        knobCurve = second.knobCurve;
        knobInterpolation = second.knobInterpolation;
        knobPointObjects = second.knobPointObjects;
    }

    [NonSerialized] public GameObject[] centerTapObjects;
    [NonSerialized] public GameObject[] knobPointObjects;

    public GameObject[] GetCenterObjects(NoteType type) =>
        type == NoteType.CenterTap ? centerTapObjects : knobPointObjects;

    internal NoteType FindCenterNote(GameObject obj)
    {
        if (!obj) return NoteType.Unknown;
        if (hasCenterTap && FindNoteObject(centerTapObjects, obj))
            return NoteType.CenterTap;
        if (hasKnobPoint && (FindNoteObject(knobPointObjects, obj) || IsSecondKnobObject(obj)))
            return NoteType.Knob;
        return NoteType.Unknown;
    }

    internal bool DeleteCenterNote(GameObject obj)
    {
        NoteType type = FindCenterNote(obj);
        if (type == NoteType.Unknown) return false;
        if (type == NoteType.Knob)
        {
            ChartHolder point = IsSecondKnobObject(obj) ? secondKnobPoint : this;
            DestroyNoteObjects(point.knobPointObjects);
            RemoveKnobPoint(point);
        }
        else
        {
            DestroyNoteObjects(centerTapObjects);
            ClearCenterNote(type);
        }
        return true;
    }

    internal void ClearCenterNote(NoteType type)
    {
        if (type == NoteType.CenterTap)
        {
            hasCenterTap = false;
            centerTapObjects = null;
        }
        else
        {
            hasKnobPoint = false;
            knobPointObjects = null;
            knobX = 0;
            knobCurve = ChartKnobInterpolation.Linear;
            knobInterpolation = ChartKnobInterpolation.Linear;
            knobPointKind = ChartNotePointKind.Start;
        }
    }
}
