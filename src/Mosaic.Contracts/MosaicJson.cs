using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mosaic.Contracts;

// The one definition of the wire format. The API, the desktop and the tests all serialise through this.
public static class MosaicJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    // ASP.NET already builds its options from JsonSerializerDefaults.Web, so only the deltas apply.
    public static void ApplyTo(JsonSerializerOptions options)
    {
        // A null property is not written, so {"error":null,"traceId":null,"success":true} goes out as {"success":true}.
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApplyTo(options);
        return options;
    }
}