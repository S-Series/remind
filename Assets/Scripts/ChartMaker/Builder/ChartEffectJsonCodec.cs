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
    private static readonly Dictionary<string, ParameterDecoder> CustomDecoders =
        new Dictionary<string, ParameterDecoder>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> CustomDefaults =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly EffectRegistry BuiltInRegistry =
        EffectRegistry.CreateDefault();

    // Register next to the corresponding runtime Effect/command registration.
    public static void Register(string typeId, string commandId, string gimmickId,
        ParameterDecoder decoder, string defaultJson)
    {
        string key = Key(typeId, commandId, gimmickId);
        CustomDecoders.Add(key, decoder ?? throw new ArgumentNullException(nameof(decoder)));
        CustomDefaults.Add(key, defaultJson ?? "");
    }

    private static string Key(string typeId, string commandId, string gimmickId) =>
        typeId + "|" + (typeId == "music.call" ? gimmickId + "|" + commandId : "");

    public static string GetDefaultJson(string typeId, string commandId, string gimmickId)
    {
        if (CustomDefaults.TryGetValue(Key(typeId, commandId, gimmickId), out string value)) return value;
        if (typeId == "camera.offset")
            return "{\"durationMs\":400,\"offsetX\":0,\"offsetY\":0,\"rollDegrees\":0}";
        if (typeId == "music.call" && gimmickId == "sample")
        {
            if (commandId == "begin-section") return "{\"minimumHealth\":30,\"damageMultiplier\":1.5}";
            if (commandId == "request-transition")
                return "{\"targetMusicId\":\"sample_song\",\"targetDifficultyId\":\"default\"}";
        }
        return "";
    }

    public static bool TryDecode(string typeId, string commandId, string gimmickId,
        string json, out object parameters, out string error)
    {
        return TryDecode(typeId, commandId, gimmickId, json,
            BuiltInRegistry, out parameters, out error);
    }

    public static bool TryDecode(string typeId, string commandId, string gimmickId,
        string json, EffectRegistry registry, out object parameters, out string error)
    {
        try
        {
            parameters = Decode(typeId, commandId, gimmickId, json, registry);
            error = null;
            return true;
        }
        catch (Exception exception) { parameters = null; error = exception.Message; return false; }
    }

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

    private static object Decode(string typeId, string commandId,
        string gimmickId, string json, EffectRegistry registry)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        if (CustomDecoders.TryGetValue(Key(typeId, commandId, gimmickId), out ParameterDecoder custom))
            return ValidateDecoded(typeId, commandId, gimmickId, custom(json),
                registry);
        if (typeId == "camera.offset")
        {
            if (!string.IsNullOrWhiteSpace(commandId)) throw new FormatException("Camera effect has no command ID.");
            XElement data = ReadObject(json);
            Only(data, "durationMs", "offsetX", "offsetY", "rollDegrees");
            double duration = Number(data, "durationMs");
            return ValidateDecoded(typeId, commandId, gimmickId,
                new CameraEffectParameters(duration,
                    Number(data, "offsetX", 0), Number(data, "offsetY", 0),
                    Number(data, "rollDegrees", 0)), registry);
        }
        if (typeId == "music.call" && gimmickId == "sample")
        {
            if (commandId == "begin-section")
            {
                XElement data = ReadObject(json);
                Only(data, "minimumHealth", "damageMultiplier");
                double minimumHealth = Number(data, "minimumHealth");
                double multiplier = Number(data, "damageMultiplier");
                return ValidateDecoded(typeId, commandId, gimmickId,
                    new SampleSectionParameters(minimumHealth, multiplier),
                    registry);
            }
            if (commandId == "request-transition")
            {
                XElement data = ReadObject(json);
                Only(data, "targetMusicId", "targetDifficultyId");
                return ValidateDecoded(typeId, commandId, gimmickId,
                    new SongTransitionParameters(
                        String(data, "targetMusicId", allowEmpty: true),
                        String(data, "targetDifficultyId", allowEmpty: true)),
                    registry);
            }
            if (commandId == "count-success" || commandId == "end-section")
            {
                if (!string.IsNullOrWhiteSpace(json)) Only(ReadObject(json));
                return ValidateDecoded(typeId, commandId, gimmickId, null,
                    registry);
            }
        }
        throw new FormatException("Unassigned/unregistered Effect or command. Select a registered type and command before playing.");
    }

    private static object ValidateDecoded(string typeId, string commandId,
        string gimmickId, object parameters, EffectRegistry registry)
    {
        string error = registry.ValidateParameters(typeId, commandId,
            gimmickId, parameters);
        if (error != null) throw new FormatException(error);
        return parameters;
    }

    internal static XElement ReadObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Required parameter object is missing.");
        try
        {
            using XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(
                Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max);
            XElement root = XDocument.Load(reader).Root;
            RequireType(root, "object");
            ValidateUniqueMembers(root);
            return root;
        }
        catch (Exception exception) when (!(exception is FormatException))
        { throw new FormatException("Invalid parameter JSON: " + exception.Message, exception); }
    }

    private static void ValidateUniqueMembers(XElement element)
    {
        if ((string)element.Attribute("type") == "object")
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement child in element.Elements())
                if (!keys.Add(child.Name.LocalName)) throw new FormatException("Duplicate JSON key: " + child.Name.LocalName);
        }
        foreach (XElement child in element.Elements()) ValidateUniqueMembers(child);
    }

    internal static void Only(XElement data, params string[] names)
    {
        HashSet<string> allowed = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (XElement child in data.Elements())
            if (!allowed.Contains(child.Name.LocalName)) throw new FormatException("Unknown JSON property: " + child.Name.LocalName);
    }

    internal static XElement Member(XElement data, string name)
    {
        XElement member = data.Element(name);
        if (member == null) throw new FormatException("Missing JSON property: " + name);
        return member;
    }

    internal static void RequireType(XElement member, string type)
    {
        if (member == null || (string)member.Attribute("type") != type)
            throw new FormatException($"JSON property '{member?.Name.LocalName}' must be {type}.");
    }

    internal static string String(XElement data, string name, bool allowEmpty = false)
    {
        XElement member = Member(data, name);
        RequireType(member, "string");
        if (!allowEmpty && string.IsNullOrWhiteSpace(member.Value)) throw new FormatException(name + " cannot be empty.");
        return member.Value;
    }

    internal static double Number(XElement data, string name, double? defaultValue = null)
    {
        XElement member = data.Element(name);
        if (member == null && defaultValue.HasValue) return defaultValue.Value;
        RequireType(member, "number");
        if (!double.TryParse(member.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            double.IsNaN(value) || double.IsInfinity(value)) throw new FormatException(name + " must be finite.");
        return value;
    }

    internal static string WriteObject(XElement element)
    {
        XElement root = new XElement(element) { Name = "root" };
        using MemoryStream stream = new MemoryStream();
        using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true))
        { root.WriteTo(writer); writer.Flush(); }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static XElement JsonString(string name, string value) =>
        new XElement(name, new XAttribute("type", "string"), value ?? "");
    internal static XElement JsonNumber(string name, int value) =>
        new XElement(name, new XAttribute("type", "number"), value.ToString(CultureInfo.InvariantCulture));
}
