using System.Text;

namespace ChatApp.Core.Models;

/// <summary>Sohbet geçmişini düz metin (.txt) olarak yazar.</summary>
public static class ChatHistory
{
    public static string SuggestFileName() => $"sohbet_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

    public static async Task WriteAsync(Stream stream, IEnumerable<ChatMessage> messages, string? userName)
    {
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync("YMH219 Chat Uygulaması — Sohbet Geçmişi");
        await writer.WriteLineAsync($"Kullanıcı : {userName ?? "-"}");
        await writer.WriteLineAsync($"Kayıt     : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        await writer.WriteLineAsync(new string('-', 50));

        foreach (var message in messages)
            await writer.WriteLineAsync(message.ToString());
    }
}
