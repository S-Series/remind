using System.Collections.Generic;
using REmind.Charting;
using REmind.Data;
using REmind.Presentation;
using UnityEngine;

public sealed partial class ChartPlacementController
{
    private readonly List<GameObject> centerHoldPaths = new List<GameObject>();
    private readonly List<PlayableNoteSnapshot> centerHoldPathNotes = new List<PlayableNoteSnapshot>();

    private static ChartHolder GetOpenCenterHold(int position)
    {
        ChartHolder start = null;
        foreach (ChartHolder holder in ChartManager.ChartHolders)
        {
            if (holder.AbsoluteChartPosition >= position) break;
            if (holder.hasCenterHold)
                start = holder.centerHoldPointKind == ChartNotePointKind.Start ? holder : null;
        }
        return start;
    }

    private void PlaceCenterHold(ChartHolder holder, Vector3 position, ChartHolder start)
    {
        if (!centerHoldNotePrefab || !chartPreviewCenterHoldNotePrefab)
        { Debug.LogWarning("Assign both Long Floor prefabs before authoring.", this); return; }
        var transaction = ChartEditHistory.BeginChange(holder.AbsoluteChartPosition);
        position.x = 0;
        holder.centerHoldObjects = CreateCenterObjects(centerHoldNotePrefab, position, NoteType.CenterHold);
        holder.hasCenterHold = true;
        holder.centerHoldPointKind = start == null ? ChartNotePointKind.Start : ChartNotePointKind.End;
        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(transaction);
    }

    private void RefreshCenterHoldPaths(PlayableChartSnapshot snapshot)
    {
        ClearCenterHoldPaths();
        if (snapshot != null)
        {
            foreach (PlayableNoteSnapshot note in snapshot.Notes)
                if (note.Kind == ChartNoteKind.CenterHold)
                    CreateCenterHoldPaths((float)note.Points[0].FloorPosition,
                        (float)note.Points[note.Points.Count - 1].FloorPosition, note);
            return;
        }
        ChartHolder start = null;
        foreach (ChartHolder holder in ChartManager.ChartHolders)
        {
            if (!holder.hasCenterHold) continue;
            if (holder.centerHoldPointKind == ChartNotePointKind.Start) start = holder;
            else if (start != null && holder.AbsoluteChartPosition > start.AbsoluteChartPosition)
            {
                CreateCenterHoldPaths(EditingCenterHoldY(start), EditingCenterHoldY(holder), null);
                start = null;
            }
        }
    }

    private float EditingCenterHoldY(ChartHolder holder) =>
        isDraggingNote && draggedNoteType == NoteType.CenterHold && holder.centerHoldObjects != null &&
        System.Array.IndexOf(holder.centerHoldObjects, dragAnchor) >= 0 ? dragTargetPosition.y : holder.WorldY;

    private void CreateCenterHoldPaths(float startY, float endY, PlayableNoteSnapshot note)
    {
        foreach (Transform field in new[] { middleNoteField, chartPreviewNoteField })
        {
            GameObject prefab = field == middleNoteField ? chartPreviewCenterHoldNotePrefab : centerHoldNotePrefab;
            if (!field || !prefab) continue;
            GameObject segment = Instantiate(prefab, field, false);
            segment.name = "Long Floor Segment";
            segment.transform.localPosition = new Vector3(0, (startY + endY) * 0.5f, 0);
            PrepareDisplayOnlyNote(segment);
            FloorNoteGeometry.SetSegmentLength(segment, Mathf.Abs(endY - startY));
            centerHoldPaths.Add(segment);
            centerHoldPathNotes.Add(note);
        }
    }

    private void SetCenterHoldPreviewTime(double timeMs)
    {
        for (int i = 0; i < centerHoldPaths.Count; i++)
            if (centerHoldPaths[i] && centerHoldPathNotes[i] != null)
                centerHoldPaths[i].SetActive(timeMs <= centerHoldPathNotes[i].EndTimeMs.Value);
    }

    private void ClearCenterHoldPaths()
    {
        foreach (GameObject path in centerHoldPaths)
            if (path) { path.SetActive(false); Destroy(path); }
        centerHoldPaths.Clear();
        centerHoldPathNotes.Clear();
    }
}
