namespace ChatApp.Core.Protocol;

/// <summary>
/// Ağ üzerinden gönderilen tek bir paket. Her paket tek satırlık JSON olarak yazılır,
/// böylece StreamReader.ReadLineAsync ile paketler birbirinden kolayca ayrılır.
/// </summary>
public sealed class ChatPacket
{
    public PacketType Type { get; set; }

    /// <summary>Gönderen kullanıcı adı (sistem mesajlarında boş).</summary>
    public string? From { get; set; }

    /// <summary>Özel mesajlarda alıcı kullanıcı adı.</summary>
    public string? To { get; set; }

    /// <summary>Mesaj metni veya hata/bilgi açıklaması.</summary>
    public string? Text { get; set; }

    /// <summary>UserList paketinde çevrimiçi kullanıcılar.</summary>
    public List<string>? Users { get; set; }

    /// <summary>Mesajın zamanı (sunucu, aktarırken kendi saatini yazar).</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Okunabilirlik için fabrika metotları
    public static ChatPacket Join(string userName) => new() { Type = PacketType.Join, From = userName };
    public static ChatPacket Accepted(string userName) => new() { Type = PacketType.Accepted, From = userName };
    public static ChatPacket Rejected(string reason) => new() { Type = PacketType.Rejected, Text = reason };
    public static ChatPacket Chat(string from, string text) => new() { Type = PacketType.Chat, From = from, Text = text };
    public static ChatPacket Private(string from, string to, string text) =>
        new() { Type = PacketType.Private, From = from, To = to, Text = text };
    public static ChatPacket UserList(IEnumerable<string> users) =>
        new() { Type = PacketType.UserList, Users = users.ToList() };
    public static ChatPacket SystemInfo(string text) => new() { Type = PacketType.System, Text = text };
    public static ChatPacket Leave(string userName) => new() { Type = PacketType.Leave, From = userName };
}
