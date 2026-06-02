using System.Text.Json;
using System.Text.Json.Serialization;
using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Core.Serialization;

/// <summary>
/// Deserializes <c>criteria</c> and legacy <c>element</c> into <see cref="ElementSelector.Criteria"/>.
/// When both keys are present, <c>criteria</c> wins. Serialization always emits <c>criteria</c>.
/// </summary>
public sealed class ElementSelectorJsonConverter : JsonConverter<ElementSelector>
{
    public override ElementSelector? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("ElementSelector must be a JSON object.");

        ElementCriteria? criteria = null;
        if (root.TryGetProperty("criteria", out var criteriaEl))
            criteria = DeserializeCriteria(criteriaEl, options);
        else if (root.TryGetProperty("element", out var elementEl))
            criteria = DeserializeCriteria(elementEl, options);

        var selector = new ElementSelector { Criteria = criteria };

        if (root.TryGetProperty("window", out var windowEl))
            selector.Window = Deserialize<WindowSelector>(windowEl, options);
        if (root.TryGetProperty("probe", out var probeEl))
            selector.Probe = Deserialize<ProbeCriteria>(probeEl, options);
        if (root.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
            selector.Path = pathEl.GetString();
        if (root.TryGetProperty("fallbacks", out var fallbacksEl))
            selector.Fallbacks = Deserialize<List<ElementCriteria>>(fallbacksEl, options);
        if (root.TryGetProperty("matchIndex", out var matchIndexEl) && matchIndexEl.TryGetInt32(out var matchIndex))
            selector.MatchIndex = matchIndex;

        return selector;
    }

    public override void Write(Utf8JsonWriter writer, ElementSelector value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Criteria is not null)
        {
            writer.WritePropertyName("criteria");
            JsonSerializer.Serialize(writer, value.Criteria, options);
        }
        if (value.Window is not null)
        {
            writer.WritePropertyName("window");
            JsonSerializer.Serialize(writer, value.Window, options);
        }
        if (value.Probe is not null)
        {
            writer.WritePropertyName("probe");
            JsonSerializer.Serialize(writer, value.Probe, options);
        }
        if (!string.IsNullOrEmpty(value.Path))
        {
            writer.WritePropertyName("path");
            writer.WriteStringValue(value.Path);
        }
        if (value.Fallbacks is { Count: > 0 })
        {
            writer.WritePropertyName("fallbacks");
            JsonSerializer.Serialize(writer, value.Fallbacks, options);
        }
        if (value.MatchIndex != 0)
        {
            writer.WritePropertyName("matchIndex");
            writer.WriteNumberValue(value.MatchIndex);
        }
        writer.WriteEndObject();
    }

    private static ElementCriteria? DeserializeCriteria(JsonElement el, JsonSerializerOptions options) =>
        el.ValueKind == JsonValueKind.Null ? null : Deserialize<ElementCriteria>(el, options);

    private static T? Deserialize<T>(JsonElement el, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<T>(el.GetRawText(), options);
}
