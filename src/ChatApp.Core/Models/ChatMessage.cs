namespace ChatApp.Core.Models;

public enum MessageKind
{
    Incoming,
    Outgoing,
    PrivateIncoming,
    PrivateOutgoing,
    Info
}

/// <summary>İstemci penceresinde gösterilen (ve geçmiş dosyasına yazılan) tek bir sohbet satırı.</summary>
public sealed class ChatMessage
{
    public ChatMessage(MessageKind kind, string sender, string text, DateTime time, string? recipient = null)
    {
        Kind = kind;
        Sender = sender;
        Text = text;
        Time = time;
        Recipient = recipient;
    }

    public MessageKind Kind { get; }
    public string Sender { get; }
    public string? Recipient { get; }
    public string Text { get; }
    public DateTime Time { get; }

    public bool IsOutgoing => Kind is MessageKind.Outgoing or MessageKind.PrivateOutgoing;
    public bool IsPrivate => Kind is MessageKind.PrivateIncoming or MessageKind.PrivateOutgoing;
    public bool IsInfo => Kind == MessageKind.Info;

    public string TimeText => Time.ToString("HH:mm:ss");

    public string Header => Kind switch
    {
        MessageKind.Outgoing => "Siz",
        MessageKind.Incoming => Sender,
        MessageKind.PrivateOutgoing => $"Siz → {Recipient} (özel)",
        MessageKind.PrivateIncoming => $"{Sender} → Siz (özel)",
        _ => "Bilgi"
    };

    /// <summary>Bilgi satırları için tek satırlık gösterim.</summary>
    public string InfoLine => $"{TimeText} · {Text}";

    public static ChatMessage CreateInfo(string text) =>
        new(MessageKind.Info, string.Empty, text, DateTime.Now);

    public override string ToString() => $"[{Time:dd.MM.yyyy HH:mm:ss}] {Header}: {Text}";
}
