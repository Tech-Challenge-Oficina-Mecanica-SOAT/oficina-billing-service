using System.Text.Json;
using System.Text.Json.Serialization;

namespace OficinaBilling.Application.Common;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Criar();

    private static JsonSerializerOptions Criar()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web); // camelCase + case-insensitive
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}
