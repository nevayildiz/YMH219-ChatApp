using System.Collections.ObjectModel;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ChatApp.Core;
using ChatApp.Core.Server;
using ChatApp.Models;

namespace ChatApp.Views;

public partial class ServerWindow : Window
{
    private readonly ChatServer _server = new();
    private readonly ObservableCollection<ClientRow> _clients = new();
    private readonly ObservableCollection<string> _log = new();

    public ServerWindow()
    {
        InitializeComponent();

        ClientsList.ItemsSource = _clients;
        LogList.ItemsSource = _log;

        // Sunucu olayları arka plandan gelir → UI iş parçacığına aktarılır.
        _server.Log += msg => Dispatcher.UIThread.Post(() => AddLog(msg));
        _server.ClientJoined += c => Dispatcher.UIThread.Post(() =>
        {
            _clients.Add(new ClientRow(c));
            UpdateClientsHeader();
        });
        _server.ClientLeft += (c, _) => Dispatcher.UIThread.Post(() =>
        {
            var row = _clients.FirstOrDefault(r => r.Id == c.Id);
            if (row is not null) _clients.Remove(row);
            UpdateClientsHeader();
        });
        _server.ClientUpdated += c =>
        {
            var count = c.MessageCount;
            Dispatcher.UIThread.Post(() =>
            {
                var row = _clients.FirstOrDefault(r => r.Id == c.Id);
                if (row is not null) row.MessageCount = count;
            });
        };

        ShowLocalAddresses();
        Closing += (_, _) => _ = _server.StopAsync();
    }

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        HideError();

        if (!NetworkHelper.TryParsePort(PortBox.Text, out var port))
        {
            ShowError("Geçerli bir port girin (1–65535).");
            return;
        }

        try
        {
            _server.Start(port);
            SetRunning(true);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            ShowError($"{port} portu başka bir uygulama tarafından kullanılıyor. Farklı bir port deneyin.");
        }
        catch (Exception ex)
        {
            ShowError($"Sunucu başlatılamadı: {ex.Message}");
        }
    }

    private async void OnStopClick(object? sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        try
        {
            await _server.StopAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Sunucu durdurulurken hata oluştu: {ex.Message}");
        }
        finally
        {
            _clients.Clear();
            UpdateClientsHeader();
            SetRunning(false);
        }
    }

    private void OnClearLogClick(object? sender, RoutedEventArgs e) => _log.Clear();

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        PortBox.IsEnabled = !running;
        StatusDot.Fill = running ? Brushes.SeaGreen : Brushes.Gray;
        StatusText.Text = running ? $"Çalışıyor (port {_server.Port})" : "Durduruldu";
    }

    private void UpdateClientsHeader() =>
        ClientsHeader.Text = $"Bağlı İstemciler ({_clients.Count})";

    private void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.Add(entry);
        LogList.ScrollIntoView(_log.Count - 1);
    }

    private void ShowLocalAddresses()
    {
        var ips = NetworkHelper.GetLocalIPv4Addresses();
        IpText.Text = ips.Count == 0
            ? "Aynı bilgisayardan bağlanmak için: 127.0.0.1"
            : $"Aynı bilgisayar: 127.0.0.1   ·   Yerel ağ: {string.Join(", ", ips)}";
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
        AddLog("HATA: " + message);
    }

    private void HideError() => ErrorText.IsVisible = false;
}
