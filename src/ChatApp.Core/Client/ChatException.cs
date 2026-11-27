namespace ChatApp.Core.Client;

/// <summary>Kullanıcıya doğrudan gösterilebilecek, anlaşılır mesajlı sohbet hatası.</summary>
public sealed class ChatException : Exception
{
    public ChatException(string message, Exception? inner = null) : base(message, inner) { }
}
