using System.Text.Json;
using System.Text.Json.Serialization;
using WpfPilot.Mcp.Core.Serialization;

namespace WpfPilot.Mcp.Core;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            IncludeFields = false
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.Converters.Add(new ElementSelectorJsonConverter());
        return o;
    }
}
