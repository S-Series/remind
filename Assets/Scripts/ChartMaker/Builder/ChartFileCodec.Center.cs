using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using REmind.Charting;
using UnityEngine;

public static partial class ChartFileCodec
{
    private static bool HasCenterNotes(IReadOnlyList<ChartHolder> holders)
    {
        foreach (ChartHolder holder in holders)
            if (holder != null && (holder.hasCenterTap || holder.hasKnobPoint)) return true;
        return false;
    }

    // The legacy row normalizer has no columns for the v2 central notes.
    private static void CopyCenterData(IReadOnlyList<ChartHolder> source,
        IReadOnlyList<ChartHolder> destination)
    {
        var byPosition = new Dictionary<int, ChartHolder>();
        foreach (ChartHolder holder in destination)
            byPosition.Add(holder.AbsoluteChartPosition, holder);
        foreach (ChartHolder holder in source)
        {
            if (holder == null) continue;
            if (!holder.hasCenterTap && !holder.hasKnobPoint) continue;
            ChartHolder target = byPosition[holder.AbsoluteChartPosition];
            target.hasCenterTap = holder.hasCenterTap;
            target.hasKnobPoint = holder.hasKnobPoint;
            target.knobPointKind = holder.knobPointKind;
            target.knobX = holder.knobX;
            target.knobInterpolation = holder.knobInterpolation;
            target.knobCurve = holder.knobCurve;
            target.secondKnobPoint = holder.secondKnobPoint?.CloneData();
        }
    }

    private static XElement WriteCenterNotes(IReadOnlyList<ChartHolder> holders)
    {
        var entries = new List<CenterNoteRow>();
        foreach (ChartHolder holder in holders)
        {
            if (holder.hasCenterTap)
                entries.Add(new CenterNoteRow { position = holder.AbsoluteChartPosition,
                    kind = "tap" });
            foreach (ChartHolder point in ChartHolder.KnobPoints(new[] { holder }))
            {
                bool second = point != holder;
                ValidateKnobX(point.knobX);
                if (point.knobInterpolation != ChartKnobInterpolation.Linear &&
                    point.knobInterpolation != ChartKnobInterpolation.Step)
                    throw new FormatException("Knob interpolation must be linear or step.");
                if (second && (point.AbsoluteChartPosition != holder.AbsoluteChartPosition ||
                    point.knobX == holder.knobX || point.knobInterpolation != ChartKnobInterpolation.Step ||
                    point.secondKnobPoint != null))
                    throw new FormatException("Second Point must be a distinct horizontal Step at the same position.");
                string kind = point.knobPointKind switch
                {
                    ChartNotePointKind.Start => "knobStart",
                    ChartNotePointKind.Mid => "knobMiddle",
                    ChartNotePointKind.End => "knobMiddle",
                    _ => throw new FormatException("Knob nodes must be Start or Middle.")
                };
                entries.Add(new CenterNoteRow { position = holder.AbsoluteChartPosition,
                    kind = kind, x = point.knobX, second = second,
                    curve = point.knobCurve switch
                    {
                        ChartKnobInterpolation.Linear => "linear",
                        ChartKnobInterpolation.Sin => "sin",
                        ChartKnobInterpolation.Cos => "cos",
                        _ => throw new FormatException("Node curve must be linear, sin or cos.")
                    },
                    interpolation = point.knobInterpolation == ChartKnobInterpolation.Step ? "step" : "linear" });
            }
        }
        XElement root = ChartEffectJsonCodec.ReadObject(JsonUtility.ToJson(
            new CenterNoteRows { centerNotes = entries.ToArray() }));
        XElement result = ChartEffectJsonCodec.Member(root, "centerNotes");
        foreach (XElement entry in result.Elements())
        {
            if (entry.Element("second")?.Value == "false") entry.Element("second").Remove();
            if (ChartEffectJsonCodec.String(entry, "kind") == "tap")
            {
                entry.Element("interpolation")?.Remove();
                entry.Element("curve")?.Remove();
            }
        }
        result.Remove();
        return result;
    }

    private static ChartHolder[] ReadCenterNotes(string text, ChartHolder[] holders)
    {
        XElement root = ChartEffectJsonCodec.ReadObject(text);
        XElement entries = ChartEffectJsonCodec.Member(root, "centerNotes");
        ChartEffectJsonCodec.RequireType(entries, "array");
        var rows = new SortedDictionary<int, ChartHolder>();
        foreach (ChartHolder holder in holders) rows.Add(holder.AbsoluteChartPosition, holder);
        foreach (XElement entry in entries.Elements())
        {
            ChartEffectJsonCodec.RequireType(entry, "object");
            ChartEffectJsonCodec.Only(entry, "position", "kind", "x", "interpolation", "second", "curve");
            XElement positionValue = ChartEffectJsonCodec.Member(entry, "position");
            ChartEffectJsonCodec.RequireType(positionValue, "number");
            if (!int.TryParse(positionValue.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int position) || position < 0 ||
                position > ChartHolder.MaximumAbsolutePosition)
                throw new FormatException("Center note position must be an integer within the chart.");
            if (!rows.TryGetValue(position, out ChartHolder holder))
            {
                holder = new ChartHolder(position / ChartHolder.PositionUnitsPerMeasure,
                    position % ChartHolder.PositionUnitsPerMeasure);
                rows.Add(position, holder);
            }
            XElement secondValue = entry.Element("second");
            bool second = false;
            if (secondValue != null)
            {
                ChartEffectJsonCodec.RequireType(secondValue, "boolean");
                if (!bool.TryParse(secondValue.Value, out second))
                    throw new FormatException("Second Point marker must be boolean.");
            }
            string kind = ChartEffectJsonCodec.String(entry, "kind");
            if (kind == "tap")
            {
                if (second || entry.Element("interpolation") != null || entry.Element("curve") != null)
                    throw new FormatException("Center Tap cannot have Knob interpolation.");
                if (holder.hasCenterTap) throw new FormatException("Duplicate center Tap position.");
                holder.hasCenterTap = true;
                continue;
            }
            ChartHolder first = holder;
            if (second)
            {
                if (!holder.hasKnobPoint || holder.secondKnobPoint != null)
                    throw new FormatException("Second Point requires a First Point and no existing Second Point.");
                holder.secondKnobPoint = new ChartHolder(holder.ChartNumber, holder.ChartPos);
                holder = holder.secondKnobPoint;
            }
            else if (holder.hasKnobPoint) throw new FormatException("Duplicate Knob point position.");
            holder.knobPointKind = kind switch
            {
                "knobStart" => ChartNotePointKind.Start,
                "knobMiddle" => ChartNotePointKind.Mid,
                "knobMid" => ChartNotePointKind.Mid,
                "knobEnd" => ChartNotePointKind.Mid,
                _ => throw new FormatException("Unknown center note kind: " + kind)
            };
            XElement xValue = ChartEffectJsonCodec.Member(entry, "x");
            ChartEffectJsonCodec.RequireType(xValue, "number");
            if (!double.TryParse(xValue.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double x))
                throw new FormatException("Knob X must be finite and in [-1, 1].");
            ValidateKnobX(x);
            holder.hasKnobPoint = true;
            holder.knobX = x;
            holder.knobInterpolation = entry.Element("interpolation") == null
                ? second ? ChartKnobInterpolation.Step : ChartKnobInterpolation.Linear
                : ChartEffectJsonCodec.String(entry, "interpolation") switch
                {
                    "linear" => ChartKnobInterpolation.Linear,
                    "step" => ChartKnobInterpolation.Step,
                    _ => throw new FormatException("Knob interpolation must be linear or step.")
                };
            holder.knobCurve = entry.Element("curve") == null ? ChartKnobInterpolation.Linear :
                ChartEffectJsonCodec.String(entry, "curve") switch
                {
                    "linear" => ChartKnobInterpolation.Linear,
                    "sin" => ChartKnobInterpolation.Sin,
                    "cos" => ChartKnobInterpolation.Cos,
                    _ => throw new FormatException("Node curve must be linear, sin or cos.")
                };
            if (second && (holder.knobX == first.knobX ||
                holder.knobInterpolation != ChartKnobInterpolation.Step))
                throw new FormatException("Second Point must be a distinct horizontal Step in the same Knob path.");
        }
        return new List<ChartHolder>(rows.Values).ToArray();
    }

    private static void ValidateKnobX(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x < -1d || x > 1d)
            throw new FormatException("Knob X must be finite and in [-1, 1].");
    }

    [Serializable] private sealed class CenterNoteRows
    {
        public CenterNoteRow[] centerNotes;
    }

    [Serializable] private sealed class CenterNoteRow
    {
        public int position;
        public string kind;
        public double x;
        public string interpolation;
        public string curve;
        public bool second;
    }
}
