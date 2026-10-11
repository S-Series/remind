using REmind.Charting;
using REmind.Data;
using REmind.Presentation;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChartPlacementController
{
    [Header("Central note prefabs")]
    [SerializeField] private GameObject centerTapNotePrefab;
    [SerializeField] private GameObject knobNotePrefab;
    [SerializeField] private GameObject chartPreviewCenterTapNotePrefab;
    [SerializeField] private GameObject chartPreviewKnobNotePrefab;
    [SerializeField] private Material knobPathMaterial;
    private KnobPathView knobPlacementPreview;
    private ChartNotePointKind currentKnobPoint = ChartNotePointKind.Start;
    private bool insertKnobPoint;
    private ChartKnobInterpolation currentKnobCurve;
    public static bool SnapKnobX { get; private set; } = true;
    private readonly List<KnobPathView> centerPaths = new List<KnobPathView>();
    private readonly List<PlayableNoteSnapshot> centerPathNotes = new List<PlayableNoteSnapshot>();

    public void SelectCenterTap() => SetCurrentNoteType(NoteType.CenterTap);
    public void SelectKnobStart() => SelectKnobPoint(ChartNotePointKind.Start);
    public void SelectKnobMid() => SelectKnobPoint(ChartNotePointKind.Mid);
    public void SelectKnobPath() => SelectKnobStart();
    public void SelectInsertKnobPoint()
    {
        SelectKnobMid();
        insertKnobPoint = true;
    }
    public void ToggleKnobXSnap() => SnapKnobX = !SnapKnobX;
    public void SelectKnobLinear() => SelectKnobCurve(ChartKnobInterpolation.Linear);
    public void SelectKnobSin() => SelectKnobCurve(ChartKnobInterpolation.Sin);
    public void SelectKnobCos() => SelectKnobCurve(ChartKnobInterpolation.Cos);

    public void ToggleKnobMode(bool curve)
    {
        if (!CanEditChart) return;
        if (curve) currentKnobCurve = NextKnobCurve(currentKnobCurve);
        else currentKnobPoint = currentKnobPoint == ChartNotePointKind.Start
            ? ChartNotePointKind.Mid : ChartNotePointKind.Start;
        insertKnobPoint = false;
        RefreshPreviewPosition();
    }

    internal static ChartKnobInterpolation NextKnobCurve(ChartKnobInterpolation curve) =>
        curve == ChartKnobInterpolation.Linear ? ChartKnobInterpolation.Sin :
        curve == ChartKnobInterpolation.Sin ? ChartKnobInterpolation.Cos : ChartKnobInterpolation.Linear;

    private void SelectKnobCurve(ChartKnobInterpolation curve)
    {
        currentKnobCurve = curve;
        RefreshPreviewPosition();
    }

    private void SelectKnobPoint(ChartNotePointKind kind)
    {
        insertKnobPoint = false;
        currentKnobPoint = kind;
        SetCurrentNoteType(NoteType.Knob);
    }

    private void PlaceCenterNote(ChartHolder holder, Vector3 position)
    {
        NoteType type = currentNoteType;
        double authoredX = position.x / PreviewMaxX;
        bool second = type == NoteType.Knob && holder.hasKnobPoint;
        if (type == NoteType.CenterTap && holder.hasCenterTap) return;
        if (second && (holder.secondKnobPoint != null || authoredX == holder.knobX))
        { Debug.LogWarning("A row supports two Knob points with different X positions.", this); return; }
        ChartNotePointKind kind = second ? ChartNotePointKind.Mid : currentKnobPoint;
        if (type == NoteType.Knob)
        {
            if (!second && insertKnobPoint)
            {
                if (!TryGetKnobSegment(holder.AbsoluteChartPosition, out ChartHolder previous, out ChartHolder next))
                { Debug.LogWarning("Click between two points of an existing Knob path.", this); return; }
                double ratio = (double)(holder.AbsoluteChartPosition - previous.AbsoluteChartPosition) /
                    (next.AbsoluteChartPosition - previous.AbsoluteChartPosition);
                double x = KnobPathEvaluator.Interpolate(previous.knobX, next.knobX, ratio,
                    next.knobInterpolation == ChartKnobInterpolation.Step ? ChartKnobInterpolation.Step : previous.knobCurve);
                if (System.Math.Abs(authoredX - x) > 0.15)
                { Debug.LogWarning("Click on the Knob segment to insert a point.", this); return; }
                position.x = (float)x * PreviewMaxX;
                authoredX = x;
                kind = ChartNotePointKind.Mid;
            }
            if (!ChartManager.ValidateKnobPoint(holder.AbsoluteChartPosition, kind, null, out string error, second))
            { Debug.LogWarning(error, this); return; }
        }
        GameObject prefab = GetNotePrefab(type);
        if (!prefab || !GetMakingNotePrefab(type))
        { Debug.LogWarning("Assign both central note prefabs before authoring.", this); return; }
        var transaction = ChartEditHistory.BeginChange(holder.AbsoluteChartPosition);
        GameObject[] objects = CreateCenterObjects(prefab, position, type);
        if (second)
        {
            holder.secondKnobPoint = new ChartHolder(holder.ChartNumber, holder.ChartPos);
            holder = holder.secondKnobPoint;
        }
        if (type == NoteType.CenterTap)
        { holder.hasCenterTap = true; holder.centerTapObjects = objects; }
        else
        {
            holder.hasKnobPoint = true;
            holder.knobPointKind = kind;
            holder.knobX = authoredX;
            holder.knobInterpolation = second ? ChartKnobInterpolation.Step : ChartKnobInterpolation.Linear;
            holder.knobCurve = currentKnobCurve;
            holder.knobPointObjects = objects;
            currentKnobPoint = ChartNotePointKind.Mid;
        }
        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(transaction);
    }

    private static bool TryGetKnobSegment(int position, out ChartHolder previous, out ChartHolder next)
    {
        previous = next = null;
        foreach (ChartHolder point in ChartHolder.KnobPoints(ChartManager.ChartHolders))
        {
            if (!point.hasKnobPoint) continue;
            if (point.AbsoluteChartPosition < position) previous = point;
            else { next = point; break; }
        }
        return previous != null && next != null && HasKnobStartBefore(previous) &&
            next.knobPointKind != ChartNotePointKind.Start && next.AbsoluteChartPosition > position;
    }

    internal static ChartHolder GetPreviousKnobPoint(int position, ChartHolder current = null)
    {
        ChartHolder previous = null;
        foreach (ChartHolder point in ChartHolder.KnobPoints(ChartManager.ChartHolders))
        {
            if (point == current || point.AbsoluteChartPosition > position ||
                (current == null && point.AbsoluteChartPosition == position)) break;
            if (point.hasKnobPoint) previous = point;
        }
        return previous;
    }

    private static bool HasKnobStartBefore(ChartHolder current)
    {
        bool found = false;
        foreach (ChartHolder point in ChartHolder.KnobPoints(ChartManager.ChartHolders))
        {
            if (point.knobPointKind == ChartNotePointKind.Start) found = true;
            if (point == current) return found;
            if (point.knobPointKind == ChartNotePointKind.End) found = false;
        }
        return false;
    }

    private GameObject[] CreateCenterObjects(GameObject prefab, Vector3 position, NoteType type)
    {
        GameObject making = Instantiate(GetMakingNotePrefab(type), middleNoteField, false);
        GameObject preview = Instantiate(prefab, chartPreviewNoteField, false);
        making.transform.localPosition = position;
        preview.transform.localPosition = position;
        PrepareDisplayOnlyNote(preview);
        InitializeSelectionTargets(new[] { making }, type);
        return new[] { making, preview };
    }

    private void RebuildCenterViews(ChartHolder holder)
    {
        if (holder.hasCenterTap && holder.centerTapObjects == null &&
            centerTapNotePrefab && chartPreviewCenterTapNotePrefab)
            holder.centerTapObjects = CreateCenterObjects(centerTapNotePrefab,
                new Vector3(0, holder.WorldY, 0), NoteType.CenterTap);
        if (holder.hasKnobPoint && holder.knobPointObjects == null &&
            knobNotePrefab && chartPreviewKnobNotePrefab)
            holder.knobPointObjects = CreateCenterObjects(knobNotePrefab,
                new Vector3((float)holder.knobX * PreviewMaxX, holder.WorldY, 0), NoteType.Knob);
        if (holder.secondKnobPoint != null) RebuildCenterViews(holder.secondKnobPoint);
    }

    public bool TryEditCenterNote(GameObject obj, int absolutePosition,
        ChartNotePointKind pointKind, double x, out string error,
        ChartKnobInterpolation? interpolation = null, ChartKnobInterpolation? curve = null)
    {
        error = null;
        if (!CanEditChart || !ChartManager.TryGetNotePosition(obj, out int sourcePosition))
        { error = "Stop preview and select a central note."; return false; }
        ChartManager.TryGetNoteData(obj, out ChartHolder source, out _, out NoteType sourceType,
            out _, out _);
        ChartHolder next = curve.HasValue && sourceType == NoteType.Knob
            ? ChartManager.GetNextKnobPoint(source) : null;
        var transaction = ChartEditHistory.BeginChange(sourcePosition, absolutePosition,
            next?.AbsoluteChartPosition ?? -1);
        if (!ChartManager.EditCenterNote(obj, absolutePosition, pointKind, x, out error, interpolation, curve)) return false;
        ChartManager.TryGetNoteData(obj, out ChartHolder holder, out _, out NoteType type,
            out _, out _);
        foreach (GameObject view in holder.GetCenterObjects(type))
            if (view) view.transform.localPosition = new Vector3(
                type == NoteType.CenterTap ? 0 : (float)x * PreviewMaxX, holder.WorldY, 0);
        ChartEditHistory.CommitChange(transaction);
        selectionController?.NotifySelectionChanged();
        return true;
    }

    private void HandleCenterChartChanged()
    {
        if (CanEditChart)
        {
            RefreshKnobPaths();
            RefreshPreviewPosition();
        }
    }

    private void RefreshKnobPlacementPreview()
    {
        if (knobPlacementPreview) knobPlacementPreview.gameObject.SetActive(false);
        if (currentNoteType != NoteType.Knob || !previewNote ||
            !isPreviewing || !isHovering || isDraggingNote || insertKnobPoint) return;

        Vector3 position = GetCurrentPreviewPosition();
        int absolutePosition = ChartHolder.WorldYToAbsolutePosition(position.y);
        ChartHolder row = ChartManager.GetHolder(absolutePosition);
        bool second = row?.hasKnobPoint == true;
        if (second && (row.secondKnobPoint != null || position.x == (float)row.knobX * PreviewMaxX)) return;
        ChartHolder previous = second ? row : currentKnobPoint == ChartNotePointKind.Start ? null :
            GetPreviousKnobPoint(absolutePosition);
        if (previous == null || !HasKnobStartBefore(previous) ||
            (!second && absolutePosition <= previous.AbsoluteChartPosition)) return;
        previewNote.transform.localPosition = position;

        var points = new[]
        {
            new KnobPathPoint((float)previous.knobX * PreviewMaxX, previous.WorldY),
            new KnobPathPoint(position.x, position.y, second ? ChartKnobInterpolation.Step : previous.knobCurve)
        };
        if (!knobPlacementPreview)
        {
            var obj = new GameObject("Knob Placement Path");
            obj.transform.SetParent(previewField, false);
            knobPlacementPreview = obj.AddComponent<KnobPathView>();
            SpriteRenderer icon = previewNote.GetComponentInChildren<SpriteRenderer>();
            knobPlacementPreview.Configure(points, GetVerticalDisplayScale(),
                icon ? icon.sortingLayerID : 0, icon ? icon.sortingOrder - 1 : 0,
                knobPathMaterial);
        }
        else knobPlacementPreview.SetPoints(points, GetVerticalDisplayScale());
        knobPlacementPreview.gameObject.SetActive(true);
    }

    internal void RefreshKnobPaths(PlayableChartSnapshot snapshot = null)
    {
        ClearKnobPaths();
        var points = new List<KnobPathPoint>();
        if (snapshot != null)
        {
            foreach (PlayableNoteSnapshot note in snapshot.Notes)
            {
                if (note.Kind != ChartNoteKind.Knob) continue;
                points = KnobPathView.BuildPlayablePoints(note, snapshot, PreviewMaxX);
                CreateKnobPathViews(points, note);
            }
            return;
        }
        var holders = new List<ChartHolder>();
        foreach (ChartHolder holder in ChartHolder.KnobPoints(ChartManager.ChartHolders))
        {
            if (!holder.hasKnobPoint) continue;
            if (holder.knobPointKind == ChartNotePointKind.Start)
            {
                BuildKnobPaths(holders, points);
                holders.Clear();
            }
            if (holders.Count > 0 || holder.knobPointKind == ChartNotePointKind.Start)
                holders.Add(holder);
            if (holder.knobPointKind == ChartNotePointKind.End)
            {
                BuildKnobPaths(holders, points);
                holders.Clear();
            }
        }
        BuildKnobPaths(holders, points);
    }

    private void BuildKnobPaths(List<ChartHolder> holders, List<KnobPathPoint> points)
    {
        if (holders.Count < 2) return;
        points.Clear();
        bool hasDistinctPoint = false;
        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder point = holders[i];
            Vector3 position = isDraggingNote && draggedNoteType == NoteType.Knob &&
                point.knobPointObjects != null && System.Array.IndexOf(point.knobPointObjects, dragAnchor) >= 0
                ? dragTargetPosition : new Vector3((float)point.knobX * PreviewMaxX, point.WorldY, 0);
            if (i > 0 && (position.x != points[0].X || position.y != points[0].Y))
                hasDistinctPoint = true;
            ChartKnobInterpolation incoming = i == 0 ? ChartKnobInterpolation.Linear :
                point.AbsoluteChartPosition == holders[i - 1].AbsoluteChartPosition ||
                point.knobInterpolation == ChartKnobInterpolation.Step ? ChartKnobInterpolation.Step : holders[i - 1].knobCurve;
            points.Add(new KnobPathPoint(position.x, position.y, incoming));
        }
        if (!hasDistinctPoint) return;
        CreateKnobPathViews(points, null);
    }

    private void CreateKnobPathViews(List<KnobPathPoint> points, PlayableNoteSnapshot note)
    {
        foreach (Transform field in new[] { middleNoteField, chartPreviewNoteField })
        {
            if (!field) continue;
            var obj = new GameObject("Knob Path");
            obj.transform.SetParent(field, false);
            var view = obj.AddComponent<KnobPathView>();
            SpriteRenderer source = chartPreviewKnobNotePrefab
                ? chartPreviewKnobNotePrefab.GetComponentInChildren<SpriteRenderer>() : null;
            view.Configure(points, GetVerticalDisplayScale(), source ? source.sortingLayerID : 0,
                source ? source.sortingOrder - 1 : 0, knobPathMaterial);
            centerPaths.Add(view);
            centerPathNotes.Add(note);
        }
    }

    internal void SetKnobPreviewTime(double timeMs)
    {
        for (int i = 0; i < centerPaths.Count; i++)
            if (centerPaths[i] && centerPathNotes[i] != null)
            {
                PlayableNoteSnapshot note = centerPathNotes[i];
                centerPaths[i].gameObject.SetActive(timeMs <= note.EndTimeMs.Value);
            }
    }

    private void ClearKnobPaths()
    {
        foreach (KnobPathView view in centerPaths)
            if (view) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
        centerPaths.Clear();
        centerPathNotes.Clear();
    }
}
