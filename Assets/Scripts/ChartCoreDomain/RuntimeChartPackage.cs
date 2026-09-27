using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace REmind.Charting
{
    /// <summary>Validated authoring output consumed by a play session.</summary>
    public sealed class RuntimeChartPackage
    {
        internal RuntimeChartPackage(PlayableChartSnapshot snapshot,
            string musicId, string difficultyId, string gimmickId,
            string revision, double baseBpm, double chartOffsetMs,
            IReadOnlyDictionary<string, string> effectParameterJson)
        {
            Snapshot = snapshot;
            MusicId = musicId;
            DifficultyId = difficultyId;
            GimmickId = gimmickId;
            Revision = revision;
            BaseBpm = baseBpm;
            ChartOffsetMs = chartOffsetMs;
            var copied = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in effectParameterJson)
                copied.Add(pair.Key, pair.Value);
            EffectParameterJson = new ReadOnlyDictionary<string, string>(
                copied);
        }

        public PlayableChartSnapshot Snapshot { get; }
        public string MusicId { get; }
        public string DifficultyId { get; }
        public string GimmickId { get; }
        public string Revision { get; }
        public double BaseBpm { get; }
        public double ChartOffsetMs { get; }
        public IReadOnlyDictionary<string, string> EffectParameterJson { get; }
    }

    /// <summary>
    /// A versioned, editor-independent package. Import recompiles the document
    /// before exposing a Snapshot so malformed or stale content cannot enter a
    /// session as validated data.
    /// </summary>
    public static class RuntimeChartPackageCodec
    {
        private const int CurrentVersion = 1;

        public static string Export(ChartDocument document, string musicId,
            string difficultyId, string gimmickId, string revision,
            double chartOffsetMs, double floorUnitsPerPosition,
            IReadOnlyDictionary<string, string> effectParameterJson)
        {
            ValidateMetadata(musicId, difficultyId, chartOffsetMs);
            ChartCompileResult compiled = ChartCompiler.Compile(document,
                floorUnitsPerPosition);
            if (!compiled.Succeeded)
                throw new FormatException(FirstError(compiled.Issues));

            var dto = new PackageDto
            {
                Version = CurrentVersion,
                MusicId = musicId,
                DifficultyId = difficultyId,
                GimmickId = gimmickId ?? string.Empty,
                Revision = revision ?? string.Empty,
                ChartOffsetMs = chartOffsetMs,
                FloorUnitsPerPosition = floorUnitsPerPosition,
                UnitsPerMeasure = document.PositionUnitsPerMeasure,
                BeatsPerMeasure = document.BeatsPerMeasure,
                BaseBpm = document.BaseBpm
            };
            foreach (ChartTimingEvent item in document.TimingEvents)
                dto.Timing.Add(new NumberEventDto { Position = item.Position,
                    Value = item.Bpm });
            foreach (ChartLineSpeedEvent item in document.LineSpeedEvents)
                dto.Speed.Add(new NumberEventDto { Position = item.Position,
                    Value = item.Multiplier });
            foreach (ChartCameraEvent item in document.CameraEvents)
                dto.Camera.Add(new CameraDto { Position = item.Position,
                    OffsetX = item.OffsetX, Spin = (int)item.SpinDirection });
            foreach (ChartScratchCameraTiltEvent item in
                document.ScratchCameraTiltEvents)
                dto.Tilt.Add(new TiltDto { Position = item.Position,
                    Lane = (int)item.Lane, Type = (int)item.EventType });
            foreach (ChartEffectEvent item in document.EffectEvents)
                dto.Effects.Add(new EffectDto { Position = item.Position,
                    Id = item.EffectId, Type = item.EffectTypeId,
                    Command = item.CommandId, Order = item.Order });
            foreach (ChartDocumentNote item in document.Notes)
            {
                var note = new NoteDto { Id = item.Id,
                    Kind = (int)item.Kind, Lane = item.Lane,
                    Start = item.StartPosition, End = item.EndPosition };
                foreach (ChartNotePoint point in item.Points)
                    note.Points.Add(new PointDto { Position = point.Position,
                        Kind = (int)point.Kind, Motion = (int)point.Motion,
                        Move = point.MoveAmount });
                dto.Notes.Add(note);
            }

            var required = new HashSet<string>(StringComparer.Ordinal);
            foreach (ChartEffectEvent item in document.EffectEvents)
                required.Add(item.EffectId);
            if (effectParameterJson != null)
                foreach (KeyValuePair<string, string> pair in effectParameterJson)
                {
                    if (!required.Remove(pair.Key))
                        throw new FormatException("Unexpected Effect parameter ID: " +
                            pair.Key);
                    dto.Parameters.Add(new ParameterDto { Id = pair.Key,
                        Json = pair.Value ?? string.Empty });
                }
            if (required.Count != 0)
                throw new FormatException("Effect parameters are missing.");

            using (var stream = new MemoryStream())
            {
                Serializer.WriteObject(stream, dto);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static RuntimeChartPackage Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Runtime chart package is empty.");
            PackageDto dto;
            try
            {
                using (var stream = new MemoryStream(
                    Encoding.UTF8.GetBytes(json)))
                    dto = (PackageDto)Serializer.ReadObject(stream);
            }
            catch (Exception exception) when (
                exception is SerializationException ||
                exception is ArgumentException)
            {
                throw new FormatException("Invalid runtime chart package.",
                    exception);
            }
            if (dto == null || dto.Version != CurrentVersion)
                throw new FormatException("Unsupported runtime chart package version.");
            ValidateMetadata(dto.MusicId, dto.DifficultyId,
                dto.ChartOffsetMs);
            if (dto.Timing == null || dto.Speed == null ||
                dto.Camera == null || dto.Tilt == null ||
                dto.Effects == null || dto.Notes == null ||
                dto.Parameters == null)
                throw new FormatException("Runtime chart package is incomplete.");

            var document = new ChartDocument(dto.UnitsPerMeasure,
                dto.BeatsPerMeasure, dto.BaseBpm);
            foreach (NumberEventDto item in dto.Timing)
                document.TimingEvents.Add(new ChartTimingEvent(
                    item.Position, item.Value));
            foreach (NumberEventDto item in dto.Speed)
                document.LineSpeedEvents.Add(new ChartLineSpeedEvent(
                    item.Position, item.Value));
            foreach (CameraDto item in dto.Camera)
                document.CameraEvents.Add(new ChartCameraEvent(item.Position,
                    item.OffsetX, (ChartCameraSpinDirection)item.Spin));
            foreach (TiltDto item in dto.Tilt)
                document.ScratchCameraTiltEvents.Add(
                    new ChartScratchCameraTiltEvent(item.Position,
                        (ChartLane)item.Lane,
                        (ChartScratchCameraTiltEventType)item.Type));
            foreach (EffectDto item in dto.Effects)
                document.EffectEvents.Add(new ChartEffectEvent(item.Position,
                    item.Id, item.Type, item.Command, item.Order));
            foreach (NoteDto item in dto.Notes)
            {
                if (item == null || item.Points == null)
                    throw new FormatException("Runtime note points are missing.");
                var note = new ChartDocumentNote(item.Id,
                    (ChartNoteKind)item.Kind, item.Lane, item.Start, item.End);
                note.Points.Clear();
                foreach (PointDto point in item.Points)
                    note.Points.Add(new ChartNotePoint(point.Position,
                        (ChartNotePointKind)point.Kind,
                        (ChartScratchMotionKind)point.Motion, point.Move));
                document.Notes.Add(note);
            }
            ChartCompileResult compiled = ChartCompiler.Compile(document,
                dto.FloorUnitsPerPosition);
            if (!compiled.Succeeded)
                throw new FormatException(FirstError(compiled.Issues));

            var parameters = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (ParameterDto item in dto.Parameters)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id) ||
                    !parameters.TryAdd(item.Id, item.Json ?? string.Empty))
                    throw new FormatException("Duplicate or invalid Effect parameter ID.");
            }
            if (parameters.Count != document.EffectEvents.Count)
                throw new FormatException("Effect parameter count does not match.");
            foreach (ChartEffectEvent item in document.EffectEvents)
                if (!parameters.ContainsKey(item.EffectId))
                    throw new FormatException("Effect parameters are missing for " +
                        item.EffectId);

            return new RuntimeChartPackage(compiled.Snapshot, dto.MusicId,
                dto.DifficultyId, dto.GimmickId ?? string.Empty,
                dto.Revision ?? string.Empty, dto.BaseBpm,
                dto.ChartOffsetMs, parameters);
        }

        private static DataContractJsonSerializer Serializer =>
            new DataContractJsonSerializer(typeof(PackageDto));

        private static void ValidateMetadata(string musicId,
            string difficultyId, double offsetMs)
        {
            if (string.IsNullOrWhiteSpace(musicId) ||
                string.IsNullOrWhiteSpace(difficultyId) ||
                double.IsNaN(offsetMs) || double.IsInfinity(offsetMs))
                throw new FormatException("Runtime chart metadata is invalid.");
        }

        private static string FirstError(IReadOnlyList<CompileIssue> issues)
        {
            foreach (CompileIssue issue in issues)
                if (issue.Severity == CompileIssueSeverity.Error)
                    return issue.Code + ": " + issue.Message;
            return "Runtime chart compilation failed.";
        }

        [DataContract]
        private sealed class PackageDto
        {
            [DataMember] public int Version;
            [DataMember] public string MusicId;
            [DataMember] public string DifficultyId;
            [DataMember] public string GimmickId;
            [DataMember] public string Revision;
            [DataMember] public double ChartOffsetMs;
            [DataMember] public double FloorUnitsPerPosition;
            [DataMember] public int UnitsPerMeasure;
            [DataMember] public int BeatsPerMeasure;
            [DataMember] public double BaseBpm;
            [DataMember] public List<NumberEventDto> Timing = new List<NumberEventDto>();
            [DataMember] public List<NumberEventDto> Speed = new List<NumberEventDto>();
            [DataMember] public List<CameraDto> Camera = new List<CameraDto>();
            [DataMember] public List<TiltDto> Tilt = new List<TiltDto>();
            [DataMember] public List<EffectDto> Effects = new List<EffectDto>();
            [DataMember] public List<NoteDto> Notes = new List<NoteDto>();
            [DataMember] public List<ParameterDto> Parameters = new List<ParameterDto>();
        }

        [DataContract] private sealed class NumberEventDto
        {
            [DataMember] public int Position;
            [DataMember] public double Value;
        }
        [DataContract] private sealed class CameraDto
        {
            [DataMember] public int Position;
            [DataMember] public double OffsetX;
            [DataMember] public int Spin;
        }
        [DataContract] private sealed class TiltDto
        {
            [DataMember] public int Position;
            [DataMember] public int Lane;
            [DataMember] public int Type;
        }
        [DataContract] private sealed class EffectDto
        {
            [DataMember] public int Position;
            [DataMember] public string Id;
            [DataMember] public string Type;
            [DataMember] public string Command;
            [DataMember] public int Order;
        }
        [DataContract] private sealed class NoteDto
        {
            [DataMember] public string Id;
            [DataMember] public int Kind;
            [DataMember] public int Lane;
            [DataMember] public int Start;
            [DataMember] public int? End;
            [DataMember] public List<PointDto> Points = new List<PointDto>();
        }
        [DataContract] private sealed class PointDto
        {
            [DataMember] public int Position;
            [DataMember] public int Kind;
            [DataMember] public int Motion;
            [DataMember] public int Move;
        }
        [DataContract] private sealed class ParameterDto
        {
            [DataMember] public string Id;
            [DataMember] public string Json;
        }
    }
}
