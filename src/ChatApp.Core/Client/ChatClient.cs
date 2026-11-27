using System.Net.Sockets;
using System.Text;
using ChatApp.Core.Protocol;

namespace ChatApp.Core.Client;

/// <summary>
/// TcpClient tabanlı sohbet istemcisi. Bağlandıktan sonra gelen paketleri arka planda okur
/// ve <see cref="PacketReceived"/> olayıyla bildirir.
/// </summary>
/// <remarks>Olaylar arka plan iş parçacığından tetiklenir.</remarks>
public sealed class ChatClient : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // 0 = bağlı değil, 1 = bağlı. Kopma olayının yalnızca bir kez tetiklenmesi için Interlocked kullanılır.
    private int _connected;

    public bool IsConnected => Volatile.Read(ref _connected) == 1;
    public string? UserName { get; private set; }

    public event Action<ChatPacket>? PacketReceived;

    /// <summary>Bağlantı kesildiğinde (kullanıcı, sunucu veya ağ kaynaklı) nedeniyle birlikte tetiklenir.</summary>
    public event Action<string>? Disconnected;

    /// <summary>Sunucuya bağlanır ve kullanıcı adıyla katılır. Başarısızlıkta <see cref="ChatException"/> fırlatır.</summary>
    public async Task ConnectAsync(string host, int port, string userName, CancellationToken ct = default)
    {
        if (IsConnected)
            throw new ChatException("Zaten bağlısınız.");

        var tcp = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ConnectTimeout);

        try
        {
            await tcp.ConnectAsync(host, port, timeout.Token);

            var stream = tcp.GetStream();
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var reader = new StreamReader(stream, utf8);
            var writer = new StreamWriter(stream, utf8) { AutoFlush = true, NewLine = "\n" };

            await writer.WriteLineAsync(PacketSerializer.Serialize(ChatPacket.Join(userName)));

            var reply = PacketSerializer.Deserialize(await reader.ReadLineAsync(timeout.Token));
            switch (reply?.Type)
            {
                case PacketType.Accepted:
                    break;
                case PacketType.Rejected:
                    throw new ChatException(reply!.Text ?? "Sunucu bağlantıyı reddetti.");
                default:
                    throw new ChatException("Sunucudan geçerli bir yanıt alınamadı. Doğru adrese bağlandığınızdan emin olun.");
            }

            _tcp = tcp;
            _reader = reader;
            _writer = writer;
            UserName = reply!.From ?? userName;
            _cts = new CancellationTokenSource();
            Volatile.Write(ref _connected, 1);

            _ = ReceiveLoopAsync(reader, _cts.Token);
        }
        catch (ChatException)
        {
            tcp.Dispose();
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            tcp.Dispose();
            throw new ChatException($"{host}:{port} adresine bağlanırken zaman aşımı oluştu.");
        }
        catch (SocketException ex)
        {
            tcp.Dispose();
            throw new ChatException(ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused =>
                    $"{host}:{port} bağlantıyı reddetti. Sunucunun çalıştığından ve portun doğru olduğundan emin olun.",
                SocketError.HostNotFound or SocketError.NoData =>
                    $"\"{host}\" adresi bulunamadı.",
                _ => $"Sunucuya bağlanılamadı: {ex.Message}"
            }, ex);
        }
        catch (IOException ex)
        {
            tcp.Dispose();
            throw new ChatException("Bağlantı kurulurken sunucu bağlantıyı kapattı.", ex);
        }
    }

    public Task SendMessageAsync(string text) =>
        SendAsync(ChatPacket.Chat(UserName ?? string.Empty, text));

    public Task SendPrivateAsync(string to, string text) =>
        SendAsync(ChatPacket.Private(UserName ?? string.Empty, to, text));

    /// <summary>Sunucuya ayrıldığını bildirip bağlantıyı kapatır.</summary>
    public async Task DisconnectAsync()
    {
        if (!IsConnected) return;

        try
        {
            // Sunucuya düzgün ayrılma bilgisi; başarısız olsa da bağlantı kapatılır.
            var send = SendAsync(ChatPacket.Leave(UserName ?? string.Empty));
            await Task.WhenAny(send, Task.Delay(500));
        }
        catch
        {
            /* yoksay */
        }

        Cleanup("Bağlantı kesildi.");
    }

    private async Task SendAsync(ChatPacket packet)
    {
        var writer = _writer;
        if (!IsConnected || writer is null)
            throw new ChatException("Sunucuya bağlı değilsiniz.");

        await _sendLock.WaitAsync();
        try
        {
            await writer.WriteLineAsync(PacketSerializer.Serialize(packet));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            Cleanup("Mesaj gönderilemedi, sunucu bağlantısı koptu.");
            throw new ChatException("Mesaj gönderilemedi, sunucu bağlantısı koptu.", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(StreamReader reader, CancellationToken ct)
    {
        string? reason = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                {
                    reason = "Sunucu bağlantıyı kapattı.";
                    break;
                }

                var packet = PacketSerializer.Deserialize(line);
                if (packet is not null)
                    PacketReceived?.Invoke(packet);
            }
        }
        catch (OperationCanceledException) { /* kullanıcı bağlantıyı kesti */ }
        catch (ObjectDisposedException) { /* kullanıcı bağlantıyı kesti */ }
        catch (IOException) { reason = "Sunucu ile bağlantı koptu."; }
        catch (SocketException) { reason = "Sunucu ile bağlantı koptu."; }
        finally
        {
            Cleanup(reason ?? "Bağlantı kesildi.");
        }
    }

    private void Cleanup(string reason)
    {
        if (Interlocked.Exchange(ref _connected, 0) == 0)
            return; // Zaten temizlendi; olay ikinci kez tetiklenmez.

        try { _cts?.Cancel(); } catch { /* yoksay */ }
        try { _tcp?.Close(); } catch { /* yoksay */ }
        _cts?.Dispose();
        _cts = null;
        _tcp = null;
        _reader = null;
        _writer = null;

        Disconnected?.Invoke(reason);
    }

    public void Dispose()
    {
        Cleanup("Bağlantı kesildi.");
        _sendLock.Dispose();
    }
}
