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
        public IList<ChartDocumentNote> Notes => notes;
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

    public sealed class ChartDocumentNote
    {
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
        }

        public string Id { get; set; }
        public ChartNoteKind Kind { get; set; }
        public int Lane { get; set; }
        public int StartPosition { get; set; }
        public int? EndPosition { get; set; }
    }
}
