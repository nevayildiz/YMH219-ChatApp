using System.Net.Sockets;
using System.Text;
using ChatApp.Core.Protocol;

namespace ChatApp.Core.Server;

/// <summary>
/// Sunucu tarafında bağlı tek bir istemciyi temsil eder: soket, okuyucu/yazıcı ve temel bilgiler.
/// </summary>
public sealed class ClientConnection : IDisposable
{
    private readonly TcpClient _tcp;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    // Aynı istemciye iki farklı görevden aynı anda yazılırsa satırlar karışmasın diye kilit.
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public ClientConnection(TcpClient tcp)
    {
        _tcp = tcp;
        var stream = tcp.GetStream();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _reader = new StreamReader(stream, utf8);
        _writer = new StreamWriter(stream, utf8) { AutoFlush = true, NewLine = "\n" };
        RemoteEndPoint = tcp.Client.RemoteEndPoint?.ToString() ?? "bilinmiyor";
    }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Join paketi kabul edildikten sonra atanır.</summary>
    public string UserName { get; internal set; } = string.Empty;

    /// <summary>İstemcinin IP:port bilgisi.</summary>
    public string RemoteEndPoint { get; }

    public DateTime ConnectedAt { get; } = DateTime.Now;

    /// <summary>Katılma onayı (Accepted) gönderildikten sonra true olur; yayınlar yalnızca bunlara gider.</summary>
    public bool IsJoined { get; internal set; }

    /// <summary>Bu istemcinin gönderdiği mesaj sayısı.</summary>
    public int MessageCount { get; internal set; }

    public async Task<string?> ReadLineAsync(CancellationToken ct) =>
        await _reader.ReadLineAsync(ct);

    public async Task SendAsync(ChatPacket packet, CancellationToken ct = default)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            await _writer.WriteLineAsync(PacketSerializer.Serialize(packet));
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Kapanış sırasında oluşabilecek hatalar önemsizdir; yutulur.
        try { _tcp.Close(); } catch { /* yoksay */ }
        try { _reader.Dispose(); } catch { /* yoksay */ }
        try { _writer.Dispose(); } catch { /* yoksay */ }
        _sendLock.Dispose();
    }
}
