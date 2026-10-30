using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreePLAudit.Core;

/// <summary>Shared JSON settings for intake packages, reports and the reference library.</summary>
public static class AuditJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
