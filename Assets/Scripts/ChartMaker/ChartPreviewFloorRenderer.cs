using System.Collections.Generic;
using System;
using REmind.Charting;
using REmind.Common.UI;
using REmind.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartPreviewFloorRenderer : MonoBehaviour
{
    private const float DefaultHalfWidth = 15f;

    [SerializeField] private RectTransform previewLineField;
    [SerializeField] private RectTransform previewNoteField;
    [SerializeField] private FourPointGraphic sourceGraphic;
    [SerializeField] private float halfWidth = DefaultHalfWidth;

    private readonly List<FourPointGraphic> generatedGraphics =
        new List<FourPointGraphic>();
    private readonly List<ScratchMotionEffect> motionEffects =
        new List<ScratchMotionEffect>();
    private readonly List<float> segmentBoundaries = new List<float>();
    private readonly List<float> ribbonBoundaries = new List<float>();
    private readonly List<NoteLength.RibbonPoint> ribbonPoints =
        new List<NoteLength.RibbonPoint>();
    private ScratchMotionPath motionPath = ScratchMotionPath.Empty;
    private PlayableChartSnapshot sessionSnapshot;
    private PlayableChartSnapshot displaySnapshot;
    private float displayScale = 1f;

    public float DisplayScale => displayScale;

    private void OnEnable()
    {
        ChartManager.ChartChanged += Rebuild;
    }

    private void Start()
    {
        ResolvePreviewNoteField();

        if (!previewLineField || !previewNoteField || !sourceGraphic)
        {
            Debug.LogError(
                "ChartPreviewFloorRenderer requires the Preview LineField, " +
                "Preview NoteField, and its source FourPointGraphic.",
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
        ResolvePreviewNoteField();

        if (!previewLineField || !previewNoteField || !sourceGraphic)
        {
            return;
        }

        displaySnapshot = sessionSnapshot ?? CompilePreviewSnapshot();
        ClearGeneratedGraphics();
        CollectMotionEffects();
        motionPath = new ScratchMotionPath(motionEffects);

        float previewEndY = Mathf.Max(
            0f,
            sourceGraphic.GetPoint(1).y * displayScale);
        float previewStartY = Mathf.Clamp(
            sourceGraphic.GetPoint(0).y * displayScale,
            0f,
            previewEndY);
        CollectSegmentBoundaries(previewStartY, previewEndY);

        for (int i = 0; i < segmentBoundaries.Count - 1; i++)
        {
            float startY = segmentBoundaries[i];
            float endY = segmentBoundaries[i + 1];
            AddSegment(
                startY,
                endY,
                EvaluateCenterX(startY, includeInstantAtPosition: true),
                EvaluateCenterX(endY, includeInstantAtPosition: false));
        }

        ApplyPreviewNoteOffsets();
        ApplyPreviewLongTapRibbons();
        ApplyPreviewLongScratchRibbons();
    }

    public void SetSessionSnapshot(PlayableChartSnapshot snapshot)
    {
        sessionSnapshot = snapshot;
        Rebuild();
    }

    /// <summary>
    /// Preview 배속을 Transform Scale이 아닌 좌표와 경로 길이에 적용합니다.
    /// 노트 및 라인의 두께와 프리팹 Scale은 변경되지 않습니다.
    /// </summary>
    public void SetDisplayScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                "Preview display scale must be finite and greater than zero.");
        }

        if (Mathf.Approximately(displayScale, scale))
        {
            return;
        }

        displayScale = scale;
        Rebuild();
    }

    private PlayableChartSnapshot CompilePreviewSnapshot()
    {
        ChartCore chartCore = ChartCore.Instance;

        if (!chartCore)
        {
            return null;
        }

        ChartHolderDocumentBuildResult buildResult =
            ChartHolderDocumentAdapter.Build(
                ChartManager.ChartHolders,
                chartCore.Bpm,
                4);

        if (!buildResult.Succeeded)
        {
            return null;
        }

        ChartCompileResult compileResult = ChartCompiler.Compile(
            buildResult.Document,
            1d / ChartHolder.PositionUnitsPerWorldUnit);
        return compileResult.Succeeded
            ? compileResult.Snapshot
            : null;
    }

    private void ResolvePreviewNoteField()
    {
        if (previewNoteField || !previewLineField || !previewLineField.parent)
        {
            return;
        }

        previewNoteField =
            previewLineField.parent.Find("NoteField") as RectTransform;
    }

    private void CollectMotionEffects()
    {
        motionEffects.Clear();
        CollectLineMotionEffects(-1);
        CollectLineMotionEffects(-2);
    }

    private void CollectLineMotionEffects(int line)
    {
        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        ChartHolder pendingLongPoint = null;

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
                AddMotionEffect(holder, holder, line);

                continue;
            }

            ScratchPointType pointType =
                holder.GetScratchPointType(line);

            if (pendingLongPoint != null)
            {
                AddMotionEffect(pendingLongPoint, holder, line);
            }

            if (pointType == ScratchPointType.Start ||
                pointType == ScratchPointType.Mid)
            {
                pendingLongPoint = holder;
            }
            else
            {
                pendingLongPoint = null;

                if (pointType == ScratchPointType.End &&
                    holder.GetScratchMotion(line).MotionType ==
                    ScratchMotionType.Release)
                {
                    AddMotionEffect(holder, holder, line);
                }
            }
        }

        if (pendingLongPoint != null)
        {
            AddMotionEffect(pendingLongPoint, pendingLongPoint, line);
        }
    }

    private void AddMotionEffect(
        ChartHolder startHolder,
        ChartHolder endHolder,
        int line)
    {
        ScratchMotionData motion = startHolder.GetScratchMotion(line);
        NoteType noteType = startHolder.noteTypes[
            line == -1
                ? ChartHolder.MainLineCount
                : ChartHolder.MainLineCount + 1];
        ScratchPointType pointType =
            startHolder.GetScratchPointType(line);
        motion = ScratchMotionRules.NormalizeMotion(
            noteType,
            pointType,
            motion);

        if (motion.MotionType == ScratchMotionType.None)
        {
            return;
        }

        float direction = GetDisplayedScratchDirection(line);
        float startY = GetDisplayY(startHolder);
        float endY = ScratchMotionRules.UsesInstantTransition(
                motion.MotionType)
            ? GetInstantMotionEndDisplayY(startHolder)
            : GetDisplayY(endHolder);
        motionEffects.Add(new ScratchMotionEffect(
            startY,
            endY,
            direction *
                ScratchMotionRules.GetHorizontalDirectionMultiplier(
                    motion.MotionType) *
                ScratchMotionRules.MoveAmountToHorizontalUnits(
                    motion.MoveAmount),
            motion.MotionType));
    }

    private float GetDisplayedScratchDirection(int line)
    {
        float noteLocalX = ChartPlacementController.GetStoredLineX(line);
        Vector3 noteWorldPosition = previewNoteField.TransformPoint(
            new Vector3(noteLocalX, 0f, 0f));
        float lineFieldX = previewLineField.InverseTransformPoint(
            noteWorldPosition).x;

        if (!Mathf.Approximately(lineFieldX, 0f))
        {
            return Mathf.Sign(lineFieldX);
        }

        return line == -1 ? -1f : 1f;
    }

    private void CollectSegmentBoundaries(float startY, float endY)
    {
        segmentBoundaries.Clear();
        segmentBoundaries.Add(startY);
        segmentBoundaries.Add(endY);

        for (int i = 0; i < motionEffects.Count; i++)
        {
            ScratchMotionEffect effect = motionEffects[i];
            segmentBoundaries.Add(Mathf.Clamp(
                (float)effect.StartPosition,
                startY,
                endY));
            segmentBoundaries.Add(Mathf.Clamp(
                (float)effect.EndPosition,
                startY,
                endY));
        }

        segmentBoundaries.Sort();

        for (int i = segmentBoundaries.Count - 1; i > 0; i--)
        {
            if (Mathf.Approximately(
                    segmentBoundaries[i],
                    segmentBoundaries[i - 1]))
            {
                segmentBoundaries.RemoveAt(i);
            }
        }
    }

    private float EvaluateCenterX(
        float positionY,
        bool includeInstantAtPosition)
    {
        return motionPath.EvaluateOffset(
            positionY,
            includeInstantAtPosition);
    }

    /// <summary>
    /// 현재 Preview 필드에서 지정한 채보 Y의 라인 중심이 이동한 월드 오프셋을 반환합니다.
    /// </summary>
    public Vector3 EvaluateWorldCenterOffset(float positionY)
    {
        if (!previewLineField)
        {
            return Vector3.zero;
        }

        float centerX = EvaluateCenterX(
            positionY * displayScale,
            includeInstantAtPosition: true);
        return previewLineField.TransformVector(
            new Vector3(centerX, 0f, 0f));
    }

    /// <summary>
    /// 지정한 FloorPosition에서 Preview 라인 중심의 로컬 X를 반환합니다.
    /// Camera Note는 처리 시점의 이 값에 자신의 OffsetX를 더합니다.
    /// </summary>
    public float EvaluateCenterOffsetX(float positionY)
    {
        return EvaluateCenterX(
            positionY * displayScale,
            includeInstantAtPosition: true);
    }

    /// <summary>
    /// 일반 편집 Preview의 현재 채보 Y를 공용 Snapshot 시간으로 변환하여
    /// Camera Note 상태를 평가합니다.
    /// </summary>
    public bool TryEvaluateCameraMotionAtChartY(
        float chartY,
        out CameraMotionState cameraState,
        out float scratchTiltDegrees)
    {
        cameraState = default;
        scratchTiltDegrees = 0f;

        if (displaySnapshot == null ||
            float.IsNaN(chartY) ||
            float.IsInfinity(chartY))
        {
            return false;
        }

        int absolutePosition = Mathf.Clamp(
            ChartHolder.WorldYToAbsolutePosition(Mathf.Max(0f, chartY)),
            0,
            ChartHolder.MaximumAbsolutePosition);
        double chartTimeMs = displaySnapshot.TimingMap.TimeAtPosition(
            absolutePosition);
        cameraState = displaySnapshot.CameraMotionMap.EvaluateAtTime(
            chartTimeMs);
        scratchTiltDegrees = (float)displaySnapshot.ScratchCameraTiltMap
            .EvaluateAtTime(chartTimeMs);
        return true;
    }

    /// <summary>
    /// Camera Note가 기록한 FloorPosition의 라인 중심과 OffsetX를 합칩니다.
    /// </summary>
    public float EvaluateCameraReferenceX(CameraMotionState cameraState)
    {
        return cameraState.HasReference
            ? EvaluateCenterOffsetX(
                  (float)cameraState.ReferenceFloorPosition) +
              (float)cameraState.ReferenceOffsetX
            : 0f;
    }

    private void ApplyPreviewNoteOffsets()
    {
        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;

        for (int holderIndex = 0;
             holderIndex < holders.Count;
             holderIndex++)
        {
            ChartHolder holder = holders[holderIndex];
            holder.EnsureStorage();
            float displayY = GetDisplayY(holder);
            float offsetX = motionPath.EvaluateOffset(
                displayY,
                includeInstantAtPosition: true);

            for (int line = 1;
                 line <= ChartHolder.MainLineCount;
                 line++)
            {
                SetPreviewNoteGroupOffset(
                    holder.tapNoteObjectGroups[line - 1],
                    line,
                    offsetX,
                    displayY);
                SetPreviewNoteGroupOffset(
                    holder.airNoteObjectGroups[line - 1],
                    line,
                    offsetX,
                    displayY);
            }

            for (int scratchIndex = 0;
                 scratchIndex < ChartHolder.ScratchLineCount;
                 scratchIndex++)
            {
                int line = scratchIndex == 0 ? -1 : -2;
                SetPreviewNoteGroupOffset(
                    holder.scratchNoteObjectGroups[scratchIndex],
                    line,
                    offsetX,
                    displayY);
            }
        }
    }

    private void ApplyPreviewLongScratchRibbons()
    {
        ApplyPreviewLongScratchRibbons(-1);
        ApplyPreviewLongScratchRibbons(-2);
    }

    private void ApplyPreviewLongTapRibbons()
    {
        for (int line = 1; line <= ChartHolder.MainLineCount; line++)
        {
            ApplyPreviewLongTapRibbons(line);
        }
    }

    private void ApplyPreviewLongTapRibbons(int line)
    {
        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        GameObject[] pendingStartObjects = null;
        float pendingStartY = 0f;

        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];

            if (!holder.TryGetNote(
                    line,
                    out NoteType noteType,
                    out GameObject[] noteObjects) ||
                noteType != NoteType.LongTap)
            {
                continue;
            }

            if (pendingStartObjects == null)
            {
                pendingStartObjects = noteObjects;
                pendingStartY = GetDisplayY(holder);
                continue;
            }

            SetPreviewLongRibbon(
                pendingStartObjects,
                pendingStartY,
                GetDisplayY(holder));
            pendingStartObjects = null;
        }
    }

    private void ApplyPreviewLongScratchRibbons(int line)
    {
        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        GameObject[] pendingSegmentObjects = null;
        float pendingSegmentY = 0f;

        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];

            if (!holder.TryGetNote(
                    line,
                    out NoteType noteType,
                    out GameObject[] noteObjects) ||
                noteType != NoteType.LongScratch)
            {
                continue;
            }

            ScratchPointType pointType =
                holder.GetScratchPointType(line);

            if (pointType == ScratchPointType.Start)
            {
                pendingSegmentObjects = noteObjects;
                pendingSegmentY = GetDisplayY(holder);
                continue;
            }

            if (pendingSegmentObjects != null)
            {
                SetPreviewLongRibbon(
                    pendingSegmentObjects,
                    pendingSegmentY,
                    GetDisplayY(holder));
            }

            if (pointType == ScratchPointType.Mid)
            {
                pendingSegmentObjects = noteObjects;
                pendingSegmentY = GetDisplayY(holder);
            }
            else
            {
                pendingSegmentObjects = null;
            }
        }
    }

    private void SetPreviewLongRibbon(
        GameObject[] noteObjects,
        float startY,
        float endY)
    {
        if (noteObjects == null || endY - startY <= Mathf.Epsilon)
        {
            return;
        }

        CollectRibbonBoundaries(startY, endY);
        float startOffset = ConvertLineFieldOffsetToNoteField(
            motionPath.EvaluateOffset(
                startY,
                includeInstantAtPosition: true));

        for (int objectIndex = 0;
             objectIndex < noteObjects.Length;
             objectIndex++)
        {
            GameObject noteObject = noteObjects[objectIndex];

            if (!noteObject ||
                noteObject.transform.parent != previewNoteField ||
                !noteObject.TryGetComponent(out NoteLength noteLength))
            {
                continue;
            }

            float halfRibbonWidth = noteLength.DefaultHalfWidth;

            if (halfRibbonWidth <= Mathf.Epsilon)
            {
                continue;
            }

            ribbonPoints.Clear();

            for (int boundaryIndex = 0;
                 boundaryIndex < ribbonBoundaries.Count;
                 boundaryIndex++)
            {
                float boundaryY = ribbonBoundaries[boundaryIndex];
                bool isStartBoundary = Mathf.Approximately(
                    boundaryY,
                    startY);

                if (!isStartBoundary)
                {
                    AddRibbonPoint(
                        boundaryY,
                        startY,
                        startOffset,
                        halfRibbonWidth,
                        includeInstantAtPosition: false);
                }

                if (isStartBoundary ||
                    HasInstantMotionAt(boundaryY))
                {
                    AddRibbonPoint(
                        boundaryY,
                        startY,
                        startOffset,
                        halfRibbonWidth,
                        includeInstantAtPosition: true);
                }
            }

            noteLength.SetRibbon(
                endY - startY,
                ribbonPoints.ToArray());
        }
    }

    private void CollectRibbonBoundaries(float startY, float endY)
    {
        ribbonBoundaries.Clear();
        ribbonBoundaries.Add(startY);
        ribbonBoundaries.Add(endY);

        IReadOnlyList<ScratchMotionEffect> effects = motionPath.Effects;

        for (int i = 0; i < effects.Count; i++)
        {
            ScratchMotionEffect effect = effects[i];
            ribbonBoundaries.Add(Mathf.Clamp(
                (float)effect.StartPosition,
                startY,
                endY));
            ribbonBoundaries.Add(Mathf.Clamp(
                (float)effect.EndPosition,
                startY,
                endY));
        }

        ribbonBoundaries.Sort();

        for (int i = ribbonBoundaries.Count - 1; i > 0; i--)
        {
            if (Mathf.Approximately(
                    ribbonBoundaries[i],
                    ribbonBoundaries[i - 1]))
            {
                ribbonBoundaries.RemoveAt(i);
            }
        }
    }

    private void AddRibbonPoint(
        float boundaryY,
        float startY,
        float startOffset,
        float halfRibbonWidth,
        bool includeInstantAtPosition)
    {
        float offset = ConvertLineFieldOffsetToNoteField(
            motionPath.EvaluateOffset(
                boundaryY,
                includeInstantAtPosition)) - startOffset;

        if (ribbonPoints.Count > 0)
        {
            NoteLength.RibbonPoint previous =
                ribbonPoints[ribbonPoints.Count - 1];

            if (Mathf.Approximately(previous.y, boundaryY - startY) &&
                Mathf.Approximately(
                    (previous.x1 + previous.x2) * 0.5f,
                    offset))
            {
                return;
            }
        }

        ribbonPoints.Add(new NoteLength.RibbonPoint(
            offset - halfRibbonWidth,
            offset + halfRibbonWidth,
            boundaryY - startY));
    }

    private bool HasInstantMotionAt(float positionY)
    {
        IReadOnlyList<ScratchMotionEffect> effects = motionPath.Effects;

        for (int i = 0; i < effects.Count; i++)
        {
            ScratchMotionEffect effect = effects[i];
            bool isInstant =
                effect.EndPosition <= effect.StartPosition;

            if (isInstant && Mathf.Approximately(
                    (float)effect.StartPosition,
                    positionY))
            {
                return true;
            }
        }

        return false;
    }

    private float ConvertLineFieldOffsetToNoteField(float lineFieldOffsetX)
    {
        Vector3 worldOffset = previewLineField.TransformVector(
            new Vector3(lineFieldOffsetX, 0f, 0f));
        return previewNoteField.InverseTransformVector(worldOffset).x;
    }

    private void SetPreviewNoteGroupOffset(
        GameObject[] noteObjects,
        int line,
        float lineFieldOffsetX,
        float displayY)
    {
        if (noteObjects == null)
        {
            return;
        }

        float noteFieldOffsetX =
            ConvertLineFieldOffsetToNoteField(lineFieldOffsetX);
        float noteLocalX =
            ChartPlacementController.GetStoredLineX(line) +
            noteFieldOffsetX;

        for (int i = 0; i < noteObjects.Length; i++)
        {
            GameObject noteObject = noteObjects[i];

            if (!noteObject || noteObject.transform.parent != previewNoteField)
            {
                continue;
            }

            Vector3 localPosition = noteObject.transform.localPosition;
            localPosition.x = noteLocalX;
            localPosition.y = displayY;
            noteObject.transform.localPosition = localPosition;
        }
    }

    private float GetDisplayY(ChartHolder holder)
    {
        return GetDisplayYAtChartPosition(holder.AbsoluteChartPosition);
    }

    private float GetInstantMotionEndDisplayY(ChartHolder holder)
    {
        int transitionUnits =
            ChartHolder.PositionUnitsPerMeasure /
            ScratchMotionRules.InstantTransitionDivisionsPerMeasure;
        int endPosition = Mathf.Min(
            holder.AbsoluteChartPosition + transitionUnits,
            ChartHolder.MaximumAbsolutePosition);
        return GetDisplayYAtChartPosition(endPosition);
    }

    private float GetDisplayYAtChartPosition(int absoluteChartPosition)
    {
        float rawDisplayY = displaySnapshot != null
            ? (float)displaySnapshot.ScrollMap
                .FloorPositionAtChartPosition(
                    absoluteChartPosition)
            : ChartHolder.AbsolutePositionToWorldY(
                absoluteChartPosition);
        return rawDisplayY * displayScale;
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
        segmentObject.name = "Scratch Floor Segment";
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
                graphic.gameObject.SetActive(false);
                Destroy(graphic.gameObject);
            }
        }

        generatedGraphics.Clear();
    }

}
