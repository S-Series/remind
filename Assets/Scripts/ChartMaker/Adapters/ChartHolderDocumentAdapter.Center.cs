using System.Collections.Generic;
using REmind.Charting;
using UnityEngine;

public static partial class ChartHolderDocumentAdapter
{
    private static void AddCenterNotes(IReadOnlyList<ChartHolder> holders,
        ChartDocument document, Dictionary<string, ChartNoteViewBinding> bindings,
        List<CompileIssue> issues)
    {
        AddCenterHolds(holders, document, bindings, issues);
        List<ChartHolder> pending = null;
        int unconnectedIndex = 0;
        foreach (ChartHolder holder in holders)
            if (holder?.hasCenterTap == true)
                AddSingleNote(document, bindings, ChartNoteKind.CenterTap,
                    (int)ChartLane.Center, holder.AbsoluteChartPosition, holder.centerTapObjects, "center-tap");
        foreach (ChartHolder holder in ChartHolder.KnobPoints(holders))
        {
            if ((holder.knobPointKind != ChartNotePointKind.Start && holder.knobPointKind != ChartNotePointKind.Mid &&
                 holder.knobPointKind != ChartNotePointKind.End) ||
                double.IsNaN(holder.knobX) || double.IsInfinity(holder.knobX) || holder.knobX < -1 || holder.knobX > 1 ||
                (holder.knobInterpolation != ChartKnobInterpolation.Linear && holder.knobInterpolation != ChartKnobInterpolation.Step) ||
                (holder.knobCurve != ChartKnobInterpolation.Linear && !KnobPathEvaluator.IsCurve(holder.knobCurve)))
            {
                issues.Add(new CompileIssue("ADAPTER_KNOB_POINT", "Invalid Knob node data."));
                continue;
            }
            if (holder.knobPointKind == ChartNotePointKind.Start)
            {
                FlushPath();
                pending = new List<ChartHolder> { holder };
            }
            else if (pending != null)
            {
                pending.Add(holder);
                // Read-only legacy End support; current authoring stores Middle instead.
                if (holder.knobPointKind == ChartNotePointKind.End) FlushPath();
            }
            else AddUnconnectedView(holder);
        }
        FlushPath();

        // Unconnected icons still need the Preview scroll geometry, but no gameplay targets.
        void AddUnconnectedView(ChartHolder point)
        {
            bindings.Add("knob-unconnected:" + unconnectedIndex++,
                new ChartNoteViewBinding(point.knobPointObjects, point.knobPointObjects,
                    new[] { new ChartNoteViewPointBinding(point.AbsoluteChartPosition, point.knobPointObjects) }));
        }

        void FlushPath()
        {
            if (pending == null) return;
            if (pending.Count < 2)
            {
                AddUnconnectedView(pending[0]);
                pending = null;
                return;
            }
            int position = pending[pending.Count - 1].AbsoluteChartPosition;
            string id = CreateNoteId("knob", (int)ChartLane.Center,
                pending[0].AbsoluteChartPosition);
            var note = new ChartDocumentNote(id, ChartNoteKind.Knob,
                (int)ChartLane.Center, pending[0].AbsoluteChartPosition, position);
            note.Points.Clear();
            var views = new List<ChartNoteViewPointBinding>();
            var allObjects = new List<GameObject>();
            for (int i = 0; i < pending.Count; i++)
            {
                ChartHolder point = pending[i];
                double delta = i + 1 < pending.Count ? pending[i + 1].knobX - point.knobX : 0;
                var direction = delta < 0 ? ChartKnobDirection.Left : delta > 0
                    ? ChartKnobDirection.Right : ChartKnobDirection.None;
                note.Points.Add(new ChartNotePoint(point.AbsoluteChartPosition,
                    i == 0 ? ChartNotePointKind.Start : i == pending.Count - 1
                        ? ChartNotePointKind.End : ChartNotePointKind.Mid)
                {
                    KnobX = point.knobX, KnobDirection = direction,
                    KnobInterpolation = i == 0 ? ChartKnobInterpolation.Linear :
                        point.AbsoluteChartPosition == pending[i - 1].AbsoluteChartPosition ||
                        point.knobInterpolation == ChartKnobInterpolation.Step ? ChartKnobInterpolation.Step :
                        pending[i - 1].knobCurve
                });
                views.Add(new ChartNoteViewPointBinding(point.AbsoluteChartPosition,
                    point.knobPointObjects));
                if (point.knobPointObjects != null) allObjects.AddRange(point.knobPointObjects);
            }
            document.Notes.Add(note);
            bindings.Add(id, new ChartNoteViewBinding(pending[0].knobPointObjects,
                allObjects.ToArray(), views.ToArray()));
            pending = null;
        }
    }

    private static void AddCenterHolds(IReadOnlyList<ChartHolder> holders,
        ChartDocument document, Dictionary<string, ChartNoteViewBinding> bindings,
        List<CompileIssue> issues)
    {
        ChartHolder start = null;
        foreach (ChartHolder holder in holders)
        {
            if (holder?.hasCenterHold != true) continue;
            if (holder.hasCenterTap && holder.centerHoldPointKind == ChartNotePointKind.Start)
            {
                issues.Add(new CompileIssue("ADAPTER_CENTER_HOLD_SLOT", "Floor Tap and Long Floor Start cannot share the same position."));
                continue;
            }
            if (holder.centerHoldPointKind == ChartNotePointKind.Start && start == null)
            { start = holder; continue; }
            if (holder.centerHoldPointKind != ChartNotePointKind.End || start == null ||
                holder.AbsoluteChartPosition <= start.AbsoluteChartPosition)
            {
                issues.Add(new CompileIssue("ADAPTER_CENTER_HOLD_PAIR", "Long Floor requires an ordered Start/End pair."));
                continue;
            }
            string id = CreateNoteId("center-hold", (int)ChartLane.Center, start.AbsoluteChartPosition);
            var note = new ChartDocumentNote(id, ChartNoteKind.CenterHold, (int)ChartLane.Center,
                start.AbsoluteChartPosition, holder.AbsoluteChartPosition);
            document.Notes.Add(note);
            var objects = new List<GameObject>();
            if (start.centerHoldObjects != null) objects.AddRange(start.centerHoldObjects);
            if (holder.centerHoldObjects != null) objects.AddRange(holder.centerHoldObjects);
            bindings.Add(id, new ChartNoteViewBinding(start.centerHoldObjects, objects.ToArray(),
                new[] { new ChartNoteViewPointBinding(start.AbsoluteChartPosition, start.centerHoldObjects),
                    new ChartNoteViewPointBinding(holder.AbsoluteChartPosition, holder.centerHoldObjects) }));
            start = null;
        }
        if (start != null)
            issues.Add(new CompileIssue("ADAPTER_CENTER_HOLD_END", "Long Floor is missing its End point."));
    }
}
