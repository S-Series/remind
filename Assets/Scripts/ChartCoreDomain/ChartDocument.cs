using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    /// <summary>
    /// 최종 입력 레인 순서입니다. 채보 저장 방식이나 Unity 오브젝트 순서와 분리합니다.
    /// </summary>
    public enum ChartLane
    {
        Main1 = 0,
        Main2 = 1,
        Main3 = 2,
        Main4 = 3,
        AirMain1 = 4,
        AirMain2 = 5,
        AirMain3 = 6,
        AirMain4 = 7,
        GroundLeft = 8,
        GroundRight = 9
    }

    public static class ChartLaneLayout
    {
        public const int MainLaneCount = 4;
        public const int AirMainLaneCount = 4;
        public const int GroundSideLaneCount = 2;
        public const int LaneCount =
            MainLaneCount + AirMainLaneCount + GroundSideLaneCount;

        public static bool IsValid(int lane)
        {
            return lane >= 0 && lane < LaneCount;
        }
    }

    public enum ChartNoteKind
    {
        Tap = 0,
        Hold = 1,
        Scratch = 2,
        LongScratch = 3,
        Air = 4
    }

    public enum ChartNotePointKind
    {
        Tap = 0,
        Start = 1,
        Mid = 2,
        End = 3
    }

    public enum ChartScratchMotionKind
    {
        None = 0,
        Instant = 1,
        Gradual = 2,
        Release = 3
    }

    public static class ChartScratchMotionLimits
    {
        public const int MaximumMoveAmount = 99;
    }

    /// <summary>A stored note point, including the motion authored at a Scratch point.</summary>
    public sealed class ChartNotePoint
    {
        public ChartNotePoint(int position, ChartNotePointKind kind,
            ChartScratchMotionKind motion = ChartScratchMotionKind.None,
            int moveAmount = 0)
        {
            Position = position;
            Kind = kind;
            Motion = motion;
            MoveAmount = moveAmount;
        }

        public int Position { get; set; }
        public ChartNotePointKind Kind { get; set; }
        public ChartScratchMotionKind Motion { get; set; }
        public int MoveAmount { get; set; }
    }

    /// <summary>
    /// 편집 가능한 채보 모델입니다. Position이 편집 원본이며 컴파일 전에는 불완전한
    /// Long Note도 담을 수 있습니다.
    /// </summary>
    public sealed class ChartDocument
    {
        private readonly List<ChartTimingEvent> timingEvents =
            new List<ChartTimingEvent>();
        private readonly List<ChartDocumentNote> notes =
            new List<ChartDocumentNote>();
        private readonly List<ChartLineSpeedEvent> lineSpeedEvents =
            new List<ChartLineSpeedEvent>();
        private readonly List<ChartCameraEvent> cameraEvents =
            new List<ChartCameraEvent>();
        private readonly List<ChartEffectEvent> effectEvents =
            new List<ChartEffectEvent>();
        private readonly List<ChartScratchCameraTiltEvent>
            scratchCameraTiltEvents =
                new List<ChartScratchCameraTiltEvent>();

        public ChartDocument(
            int positionUnitsPerMeasure,
            int beatsPerMeasure,
            double baseBpm)
        {
            PositionUnitsPerMeasure = positionUnitsPerMeasure;
            BeatsPerMeasure = beatsPerMeasure;
            BaseBpm = baseBpm;
        }

        public int PositionUnitsPerMeasure { get; set; }
        public int BeatsPerMeasure { get; set; }
        public double BaseBpm { get; set; }
        public IList<ChartTimingEvent> TimingEvents => timingEvents;
        public IList<ChartLineSpeedEvent> LineSpeedEvents => lineSpeedEvents;
        public IList<ChartCameraEvent> CameraEvents => cameraEvents;
        public IList<ChartEffectEvent> EffectEvents => effectEvents;
        public IList<ChartScratchCameraTiltEvent> ScratchCameraTiltEvents =>
            scratchCameraTiltEvents;
        public IList<ChartDocumentNote> Notes => notes;
    }

    /// <summary>Passive editing data only. Runtime configuration lives outside ChartDocument.</summary>
    public sealed class ChartEffectEvent
    {
        public ChartEffectEvent(int position, string effectId, string effectTypeId,
            string commandId, int order)
        {
            Position = position;
            EffectId = effectId;
            EffectTypeId = effectTypeId;
            CommandId = commandId;
            Order = order;
        }

        public int Position { get; set; }
        public string EffectId { get; set; }
        public string EffectTypeId { get; set; }
        public string CommandId { get; set; }
        public int Order { get; set; }
    }

    public sealed class ChartTimingEvent
    {
        public ChartTimingEvent(int position, double bpm)
        {
            Position = position;
            Bpm = bpm;
        }

        public int Position { get; set; }
        public double Bpm { get; set; }
    }

    /// <summary>
    /// 판정 시간에는 관여하지 않고 FloorPosition 이동 기울기만 변경합니다.
    /// 현재 확정 범위에서는 1이 기본 속도이며 0보다 큰 배율만 허용합니다.
    /// 정지(0)와 역주행(음수)은 표시 및 편집 규칙을 별도로 결정한 뒤 지원합니다.
    /// </summary>
    public sealed class ChartLineSpeedEvent
    {
        public ChartLineSpeedEvent(int position, double multiplier)
        {
            Position = position;
            Multiplier = multiplier;
        }

        public int Position { get; set; }
        public double Multiplier { get; set; }
    }

    public enum ChartCameraSpinDirection
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    /// <summary>
    /// 처리 시점의 라인 중심 X에 OffsetX를 더해 Camera 기준 X를 갱신합니다.
    /// Left/Right는 해당 방향으로 한 바퀴 회전합니다.
    /// </summary>
    public sealed class ChartCameraEvent
    {
        public ChartCameraEvent(
            int position,
            double offsetX,
            ChartCameraSpinDirection spinDirection)
        {
            Position = position;
            OffsetX = offsetX;
            SpinDirection = spinDirection;
        }

        public int Position { get; set; }
        public double OffsetX { get; set; }
        public ChartCameraSpinDirection SpinDirection { get; set; }
    }

    /// <summary>
    /// Scratch Motion이 시작되거나 해제되는 순간의 카메라 롤 연출입니다. 방향은
    /// 최종 입력 레인으로 보존하고, 각도와 지속시간은 컴파일러가 결정합니다.
    /// </summary>
    public enum ChartScratchCameraTiltEventType
    {
        Instant = 0,
        Gradual = 1,
        Release = 2,
        ReverseInstant = 3
    }

    public sealed class ChartScratchCameraTiltEvent
    {
        public ChartScratchCameraTiltEvent(
            int position,
            ChartLane lane,
            ChartScratchCameraTiltEventType eventType =
                ChartScratchCameraTiltEventType.Instant)
        {
            Position = position;
            Lane = lane;
            EventType = eventType;
        }

        public int Position { get; set; }
        public ChartLane Lane { get; set; }
        public ChartScratchCameraTiltEventType EventType { get; set; }
    }

    public sealed class ChartDocumentNote
    {
        private readonly List<ChartNotePoint> points = new List<ChartNotePoint>();

        public ChartDocumentNote(
            string id,
            ChartNoteKind kind,
            int lane,
            int startPosition,
            int? endPosition = null)
        {
            Id = id;
            Kind = kind;
            Lane = lane;
            StartPosition = startPosition;
            EndPosition = endPosition;
            points.Add(new ChartNotePoint(startPosition,
                endPosition.HasValue ? ChartNotePointKind.Start : ChartNotePointKind.Tap));
            if (endPosition.HasValue)
                points.Add(new ChartNotePoint(endPosition.Value, ChartNotePointKind.End));
        }

        public string Id { get; set; }
        public ChartNoteKind Kind { get; set; }
        public int Lane { get; set; }
        public int StartPosition { get; set; }
        public int? EndPosition { get; set; }
        public IList<ChartNotePoint> Points => points;
    }
}
