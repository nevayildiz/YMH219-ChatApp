using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ChatApp.Core.Protocol;

namespace ChatApp.Core.Server;

/// <summary>
/// TcpListener tabanlı sohbet sunucusu. Her istemci ayrı bir async görevde dinlenir;
/// herkese açık mesajlar tüm istemcilere, özel mesajlar yalnızca alıcıya iletilir.
/// </summary>
/// <remarks>
/// Olaylar arka plan iş parçacıklarından tetiklenir; arayüz tarafı Dispatcher ile
/// UI iş parçacığına geçmelidir.
/// </remarks>
public sealed class ChatServer
{
    public const int MaxUserNameLength = 20;

    private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();
    private readonly object _joinLock = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _listener is not null;
    public int Port { get; private set; }

    /// <summary>Kullanıcı adı kabul edilmiş, sohbete katılmış istemciler.</summary>
    public IReadOnlyCollection<ClientConnection> Clients => JoinedClients.ToList();

    private IEnumerable<ClientConnection> JoinedClients => _clients.Values.Where(c => c.IsJoined);

    public event Action<ClientConnection>? ClientJoined;
    public event Action<ClientConnection, string>? ClientLeft;
    public event Action<ClientConnection>? ClientUpdated;
    public event Action<string>? Log;

    /// <summary>Sunucuyu belirtilen portta başlatır. Port kullanımdaysa SocketException fırlatır.</summary>
    public void Start(int port)
    {
        if (IsRunning)
            throw new InvalidOperationException("Sunucu zaten çalışıyor.");

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start(); // Port doluysa burada SocketException oluşur, çağıran yakalar.

        _listener = listener;
        _cts = new CancellationTokenSource();
        Port = port;
        Log?.Invoke($"Sunucu {port} portunda dinlemeye başladı.");

        _ = AcceptLoopAsync(listener, _cts.Token);
    }

    /// <summary>Tüm istemcilere bilgi verip sunucuyu durdurur.</summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        await BroadcastAsync(ChatPacket.SystemInfo("Sunucu kapatıldı."));

        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* yoksay */ }
        _listener = null;

        foreach (var client in _clients.Values)
            client.Dispose(); // Okuma döngüleri hata alıp kendini temizler.

        _cts?.Dispose();
        _cts = null;
        Log?.Invoke("Sunucu durduruldu.");
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException ex)
            {
                if (ct.IsCancellationRequested) break;
                Log?.Invoke($"Bağlantı kabul edilemedi: {ex.Message}");
                continue;
            }

            _ = HandleClientAsync(tcp, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        ClientConnection conn;
        try
        {
            conn = new ClientConnection(tcp);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"İstemci bağlantısı hazırlanamadı: {ex.Message}");
            tcp.Dispose();
            return;
        }

        Log?.Invoke($"Yeni bağlantı: {conn.RemoteEndPoint}");
        var reason = "bağlantı kapandı";

        try
        {
            if (!await TryJoinAsync(conn, ct))
                return;

            while (!ct.IsCancellationRequested)
            {
                var line = await conn.ReadLineAsync(ct);
                if (line is null) break; // Karşı taraf bağlantıyı kapattı.

                var packet = PacketSerializer.Deserialize(line);
                if (packet is null)
                {
                    Log?.Invoke($"{conn.UserName}: çözümlenemeyen veri yok sayıldı.");
                    continue;
                }

                if (packet.Type == PacketType.Leave)
                {
                    reason = "ayrıldı";
                    break;
                }

                await HandlePacketAsync(conn, packet);
            }
        }
        catch (OperationCanceledException) { reason = "sunucu durduruldu"; }
        catch (IOException) { reason = "bağlantı koptu"; }
        catch (SocketException) { reason = "bağlantı koptu"; }
        catch (ObjectDisposedException) { reason = "bağlantı kapatıldı"; }
        catch (Exception ex)
        {
            reason = "beklenmeyen hata";
            Log?.Invoke($"{conn.RemoteEndPoint} için hata: {ex.Message}");
        }
        finally
        {
            var wasJoined = _clients.TryRemove(conn.Id, out _);
            conn.Dispose();

            if (wasJoined)
            {
                Log?.Invoke($"{conn.UserName} ({conn.RemoteEndPoint}) {reason}.");
                ClientLeft?.Invoke(conn, reason);

                if (!ct.IsCancellationRequested)
                {
                    await BroadcastAsync(ChatPacket.SystemInfo($"{conn.UserName} sohbetten ayrıldı."));
                    await BroadcastUserListAsync();
                }
            }
        }
    }

    /// <summary>İlk paketin geçerli bir Join olup olmadığını ve kullanıcı adının boşta olduğunu denetler.</summary>
    private async Task<bool> TryJoinAsync(ClientConnection conn, CancellationToken ct)
    {
        var first = PacketSerializer.Deserialize(await conn.ReadLineAsync(ct));
        if (first is not { Type: PacketType.Join } || string.IsNullOrWhiteSpace(first.From))
        {
            await conn.SendAsync(ChatPacket.Rejected("Geçersiz bağlantı isteği."), ct);
            Log?.Invoke($"{conn.RemoteEndPoint}: geçersiz katılma isteği, reddedildi.");
            return false;
        }

        var name = first.From.Trim();
        if (name.Length > MaxUserNameLength)
        {
            await conn.SendAsync(ChatPacket.Rejected($"Kullanıcı adı en fazla {MaxUserNameLength} karakter olabilir."), ct);
            return false;
        }

        bool taken;
        lock (_joinLock)
        {
            taken = _clients.Values.Any(c =>
                string.Equals(c.UserName, name, StringComparison.OrdinalIgnoreCase));
            if (!taken)
            {
                conn.UserName = name;
                _clients[conn.Id] = conn;
            }
        }

        if (taken)
        {
            await conn.SendAsync(ChatPacket.Rejected($"\"{name}\" kullanıcı adı zaten kullanımda."), ct);
            Log?.Invoke($"{conn.RemoteEndPoint}: \"{name}\" adı dolu olduğu için reddedildi.");
            return false;
        }

        await conn.SendAsync(ChatPacket.Accepted(name), ct);
        conn.IsJoined = true;
        Log?.Invoke($"{name} ({conn.RemoteEndPoint}) sohbete katıldı.");
        ClientJoined?.Invoke(conn);

        await BroadcastAsync(ChatPacket.SystemInfo($"{name} sohbete katıldı."));
        await BroadcastUserListAsync();
        return true;
    }

    private async Task HandlePacketAsync(ClientConnection conn, ChatPacket packet)
    {
        var text = packet.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (text.Length > PacketSerializer.MaxTextLength)
            text = text[..PacketSerializer.MaxTextLength];

        switch (packet.Type)
        {
            case PacketType.Chat:
                conn.MessageCount++;
                ClientUpdated?.Invoke(conn);
                await BroadcastAsync(ChatPacket.Chat(conn.UserName, text));
                break;

            case PacketType.Private:
                await HandlePrivateAsync(conn, packet.To, text);
                break;

            default:
                Log?.Invoke($"{conn.UserName}: beklenmeyen paket türü ({packet.Type}).");
                break;
        }
    }

    private async Task HandlePrivateAsync(ClientConnection sender, string? to, string text)
    {
        var target = JoinedClients.FirstOrDefault(c =>
            string.Equals(c.UserName, to, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            await SafeSendAsync(sender, ChatPacket.SystemInfo($"\"{to}\" adlı kullanıcı çevrimiçi değil."));
            return;
        }

        sender.MessageCount++;
        ClientUpdated?.Invoke(sender);

        var packet = ChatPacket.Private(sender.UserName, target.UserName, text);
        await SafeSendAsync(target, packet);
        if (target != sender)
            await SafeSendAsync(sender, packet); // Gönderen de kendi özel mesajını görsün.

        Log?.Invoke($"Özel mesaj: {sender.UserName} → {target.UserName}");
    }

    private Task BroadcastUserListAsync() =>
        BroadcastAsync(ChatPacket.UserList(JoinedClients.Select(c => c.UserName).OrderBy(n => n)));

    private Task BroadcastAsync(ChatPacket packet) =>
        Task.WhenAll(JoinedClients.Select(c => SafeSendAsync(c, packet)));

    /// <summary>
    /// Tek bir istemciye gönderim hatası diğerlerini etkilemesin diye hatalar burada yutulur;
    /// kopan istemci kendi okuma döngüsünde temizlenir.
    /// </summary>
    private static async Task SafeSendAsync(ClientConnection client, ChatPacket packet)
    {
        try
        {
            await client.SendAsync(packet);
        }
        catch
        {
            /* yoksay */
        }
    }
}
