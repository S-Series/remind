using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using REmind.Charting;

/// <summary>File/editor boundary only. A session receives typed, immutable parameters.</summary>
public static class ChartEffectJsonCodec
{
    public delegate object ParameterDecoder(string json);
    private static readonly EffectRegistry BuiltInRegistry =
        EffectRegistry.CreateDefault();

    public static void Register(string typeId, string commandId,
        string gimmickId, ParameterDecoder decoder, string defaultJson) =>
        ChartEffectParameterCodec.Register(typeId, commandId, gimmickId,
            json => decoder(json), defaultJson);

    public static string GetDefaultJson(string typeId, string commandId,
        string gimmickId) => ChartEffectParameterCodec.GetDefaultJson(
            typeId, commandId, gimmickId);

    public static bool TryDecode(string typeId, string commandId,
        string gimmickId, string json, out object parameters,
        out string error) => ChartEffectParameterCodec.TryDecode(
            typeId, commandId, gimmickId, json, out parameters, out error);

    public static bool TryDecode(string typeId, string commandId,
        string gimmickId, string json, EffectRegistry registry,
        out object parameters, out string error) =>
        ChartEffectParameterCodec.TryDecode(typeId, commandId, gimmickId,
            json, registry, out parameters, out error);

    public static Dictionary<string, object> BuildParameterMap(
        IReadOnlyList<ChartHolder> holders, string gimmickId,
        EffectRegistry registry = null)
    {
        registry ??= BuiltInRegistry;
        Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (ChartHolder holder in holders)
        {
            if (holder == null || !holder.isEffect) continue;
            if (string.IsNullOrWhiteSpace(holder.effectId) || result.ContainsKey(holder.effectId))
                throw new FormatException("Effect IDs must be nonempty and unique: " + holder.effectId);
            if (!TryDecode(holder.effectTypeId, holder.effectCommandId, gimmickId,
                holder.effectParametersJson, registry, out object parameters,
                out string error))
                throw new FormatException($"Effect '{holder.effectId}' ({holder.effectTypeId}/{holder.effectCommandId}): {error}");
            result.Add(holder.effectId, parameters);
        }
        return result;
    }

    /// <summary>
    /// Transitional authoring adapter: converts current ChartHolder parameter
    /// JSON into the immutable shared Effect plan consumed by both Preview and
    /// the DemoPlay integration harness.
    /// </summary>
    public static PreparedEffectPlan PreparePlan(
        PlayableChartSnapshot snapshot,
        IReadOnlyList<ChartHolder> holders,
        string gimmickId,
        EffectRegistry registry = null)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        registry ??= EffectRegistry.CreateDefault();
        Dictionary<string, object> parameters = BuildParameterMap(
            holders,
            gimmickId,
            registry);
        EffectPreparationResult result = EffectPreparation.Prepare(
            snapshot.EffectEvents,
            parameters,
            registry,
            gimmickId);
        if (result.Succeeded)
        {
            return result.Plan;
        }

        var errors = new StringBuilder();
        for (int i = 0; i < result.Issues.Count; i++)
        {
            CompileIssue issue = result.Issues[i];
            if (issue.Severity == CompileIssueSeverity.Error)
            {
                errors.AppendLine(issue.Code + ": " + issue.Message);
            }
        }

        throw new FormatException(errors.ToString());
    }

    /// <summary>
    /// Validate every Effect whose implementation is currently available. Unknown
    /// legacy/plugin definitions remain round-trippable, but known malformed data
    /// cannot be written as though it were playable.
    /// </summary>
    public static void ValidateKnownParameters(
        IReadOnlyList<ChartHolder> holders, string gimmickId,
        EffectRegistry registry = null)
    {
        if (holders == null) throw new ArgumentNullException(nameof(holders));
        registry ??= BuiltInRegistry;
        foreach (ChartHolder holder in holders)
        {
            if (holder == null || !holder.isEffect ||
                string.IsNullOrWhiteSpace(holder.effectTypeId) ||
                !registry.TryGetEffect(holder.effectTypeId, out _))
                continue;

            // A chart that depends on an unavailable song/plugin must remain
            // preservable. Once its registration is present, the same typed rules
            // as runtime preparation apply.
            if (holder.effectTypeId == CallMusicGimmickEffect.TypeId &&
                !registry.TryGetGimmick(gimmickId, out _))
                continue;

            if (!TryDecode(holder.effectTypeId, holder.effectCommandId,
                    gimmickId, holder.effectParametersJson, registry,
                    out _, out string error))
                throw new FormatException(
                    $"Effect '{holder.effectId}' ({holder.effectTypeId}/" +
                    $"{holder.effectCommandId}): {error}");
        }
    }

    internal static XElement ReadObject(string json) =>
        ChartEffectParameterCodec.ReadObject(json);
    internal static void Only(XElement data, params string[] names) =>
        ChartEffectParameterCodec.Only(data, names);
    internal static XElement Member(XElement data, string name) =>
        ChartEffectParameterCodec.Member(data, name);
    internal static void RequireType(XElement member, string type) =>
        ChartEffectParameterCodec.RequireType(member, type);
    internal static string String(XElement data, string name,
        bool allowEmpty = false) =>
        ChartEffectParameterCodec.String(data, name, allowEmpty);
    internal static double Number(XElement data, string name,
        double? defaultValue = null) =>
        ChartEffectParameterCodec.Number(data, name, defaultValue);
    internal static string WriteObject(XElement element) =>
        ChartEffectParameterCodec.WriteObject(element);
    internal static XElement JsonString(string name, string value) =>
        ChartEffectParameterCodec.JsonString(name, value);
    internal static XElement JsonNumber(string name, int value) =>
        ChartEffectParameterCodec.JsonNumber(name, value);
}
