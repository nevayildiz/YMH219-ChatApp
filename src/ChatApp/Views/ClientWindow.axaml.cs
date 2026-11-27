using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ChatApp.Core;
using ChatApp.Core.Client;
using ChatApp.Core.Models;
using ChatApp.Core.Protocol;
using ChatApp.Core.Server;
using ChatApp.Models;

namespace ChatApp.Views;

public partial class ClientWindow : Window
{
    private readonly ChatClient _client = new();
    private readonly ObservableCollection<ChatMessage> _messages = new();
    private readonly ObservableCollection<UserEntry> _users = new();

    // Bağlantıyı kullanıcı mı kesti? Değilse kopma bir hata olarak gösterilir.
    private bool _userDisconnecting;

    public ClientWindow()
    {
        InitializeComponent();

        MessagesList.ItemsSource = _messages;
        UsersList.ItemsSource = _users;
        UserBox.Text = $"Kullanici{Random.Shared.Next(100, 1000)}";

        // Enter tuşu ile gönderme (Tunnel: TextBox olayı işlemeden önce yakalanır)
        MessageInput.AddHandler(KeyDownEvent, OnMessageKeyDown, RoutingStrategies.Tunnel);

        // ChatClient olayları arka plandan gelir → UI iş parçacığına aktarılır.
        _client.PacketReceived += p => Dispatcher.UIThread.Post(() => HandlePacket(p));
        _client.Disconnected += reason => Dispatcher.UIThread.Post(() => OnDisconnected(reason));

        Closing += (_, _) =>
        {
            _userDisconnecting = true;
            _ = _client.DisconnectAsync();
        };
    }

    // ---------- Bağlan / Kes ----------

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        HideError();

        var host = HostBox.Text?.Trim() ?? string.Empty;
        var userName = UserBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(host))
        {
            ShowError("Sunucu adresini girin (aynı bilgisayar için 127.0.0.1).");
            return;
        }
        if (!NetworkHelper.TryParsePort(PortBox.Text, out var port))
        {
            ShowError("Geçerli bir port girin (1–65535).");
            return;
        }
        if (string.IsNullOrEmpty(userName))
        {
            ShowError("Bir kullanıcı adı girin.");
            return;
        }
        if (userName.Length > ChatServer.MaxUserNameLength)
        {
            ShowError($"Kullanıcı adı en fazla {ChatServer.MaxUserNameLength} karakter olabilir.");
            return;
        }

        SetConnecting();
        try
        {
            await _client.ConnectAsync(host, port, userName);
            _userDisconnecting = false;
            SetConnected(true);
            AddMessage(ChatMessage.CreateInfo($"{host}:{port} sunucusuna \"{_client.UserName}\" olarak bağlandınız."));
            MessageInput.Focus();
        }
        catch (ChatException ex)
        {
            SetConnected(false);
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            SetConnected(false);
            ShowError($"Beklenmeyen hata: {ex.Message}");
        }
    }

    private async void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        _userDisconnecting = true;
        await _client.DisconnectAsync();
    }

    private void OnDisconnected(string reason)
    {
        SetConnected(false);
        _users.Clear();
        UpdateUsersHeader();
        AddMessage(ChatMessage.CreateInfo(reason));

        if (!_userDisconnecting)
            ShowError(reason);
    }

    // ---------- Mesaj gönderme ----------

    private void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = SendCurrentMessageAsync();
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e) => _ = SendCurrentMessageAsync();

    private async Task SendCurrentMessageAsync()
    {
        var text = MessageInput.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (!_client.IsConnected)
        {
            ShowError("Mesaj göndermek için önce sunucuya bağlanın.");
            return;
        }

        HideError();
        try
        {
            if (UsersList.SelectedItem is UserEntry { CanReceivePrivate: true } target)
                await _client.SendPrivateAsync(target.Name, text);
            else
                await _client.SendMessageAsync(text);

            MessageInput.Text = string.Empty;
        }
        catch (ChatException ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            MessageInput.Focus();
        }
    }

    // ---------- Geçmiş ----------

    private async void OnSaveHistoryClick(object? sender, RoutedEventArgs e)
    {
        HideError();
        if (_messages.Count == 0)
        {
            ShowError("Kaydedilecek mesaj yok.");
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Sohbet geçmişini kaydet",
                SuggestedFileName = ChatHistory.SuggestFileName(),
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Metin dosyası") { Patterns = new[] { "*.txt" } }
                }
            });
            if (file is null) return; // Kullanıcı vazgeçti.

            await using (var stream = await file.OpenWriteAsync())
                await ChatHistory.WriteAsync(stream, _messages.ToList(), _client.UserName);

            AddMessage(ChatMessage.CreateInfo($"Geçmiş kaydedildi: {file.Name}"));
        }
        catch (Exception ex)
        {
            ShowError($"Geçmiş kaydedilemedi: {ex.Message}");
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e) => _messages.Clear();

    // ---------- Gelen paketler ----------

    private void HandlePacket(ChatPacket p)
    {
        var me = _client.UserName;
        switch (p.Type)
        {
            case PacketType.Chat:
                var kind = p.From == me ? MessageKind.Outgoing : MessageKind.Incoming;
                AddMessage(new ChatMessage(kind, p.From ?? "?", p.Text ?? string.Empty, p.Timestamp));
                break;

            case PacketType.Private:
                var pkind = p.From == me ? MessageKind.PrivateOutgoing : MessageKind.PrivateIncoming;
                AddMessage(new ChatMessage(pkind, p.From ?? "?", p.Text ?? string.Empty, p.Timestamp, p.To));
                break;

            case PacketType.System:
                AddMessage(new ChatMessage(MessageKind.Info, string.Empty, p.Text ?? string.Empty, p.Timestamp));
                break;

            case PacketType.UserList:
                UpdateUsers(p.Users ?? new List<string>());
                break;
        }
    }

    private void UpdateUsers(IReadOnlyList<string> names)
    {
        var selectedName = (UsersList.SelectedItem as UserEntry)?.Name;
        var me = _client.UserName;

        _users.Clear();
        _users.Add(UserEntry.Everyone);
        foreach (var name in names)
            _users.Add(new UserEntry(name, IsEveryone: false, IsSelf: name == me));

        // Önceki seçimi koru; kişi ayrıldıysa "Herkes"e dön.
        UsersList.SelectedItem = _users.FirstOrDefault(u => !u.IsEveryone && u.Name == selectedName)
                                 ?? UserEntry.Everyone;
        UpdateUsersHeader();
    }

    private void OnUserSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        TargetText.Text = UsersList.SelectedItem is UserEntry { CanReceivePrivate: true } u
            ? $"Alıcı: {u.Name} (özel mesaj)"
            : "Alıcı: Herkes";
    }

    // ---------- Yardımcılar ----------

    private void AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        MessagesList.ScrollIntoView(_messages.Count - 1);
    }

    private void UpdateUsersHeader() =>
        UsersHeader.Text = $"Çevrimiçi ({_users.Count(u => !u.IsEveryone)})";

    private void SetConnecting()
    {
        ConnectButton.IsEnabled = false;
        StatusDot.Fill = Brushes.Orange;
        StatusText.Text = "Bağlanıyor...";
    }

    private void SetConnected(bool connected)
    {
        ConnectButton.IsEnabled = !connected;
        DisconnectButton.IsEnabled = connected;
        HostBox.IsEnabled = PortBox.IsEnabled = UserBox.IsEnabled = !connected;
        MessageInput.IsEnabled = SendButton.IsEnabled = connected;

        StatusDot.Fill = connected ? Brushes.SeaGreen : Brushes.Gray;
        StatusText.Text = connected
            ? $"Bağlı: {_client.UserName} @ {HostBox.Text}:{PortBox.Text}"
            : "Bağlı değil";
        Title = connected ? $"Chat İstemcisi — {_client.UserName}" : "Chat İstemcisi";
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void HideError() => ErrorText.IsVisible = false;
}
