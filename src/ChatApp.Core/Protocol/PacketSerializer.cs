using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatApp.Core.Protocol;

/// <summary>Paketleri tek satırlık JSON metnine çevirir ve geri okur.</summary>
public static class PacketSerializer
{
    /// <summary>Bir mesaj metninin izin verilen en fazla uzunluğu.</summary>
    public const int MaxTextLength = 2000;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Paketi JSON'a çevirir. JSON içindeki satır sonları "\n" kaçış dizisi olarak yazıldığı
    /// için sonuç her zaman tek satırdır.
    /// </summary>
    public static string Serialize(ChatPacket packet) => JsonSerializer.Serialize(packet, Options);

    /// <summary>Satırı pakete çevirir; bozuk veri gelirse null döner (uygulama çökmez).</summary>
    public static ChatPacket? Deserialize(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ChatPacket>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
