using System.ComponentModel;
using System.Runtime.CompilerServices;
using ChatApp.Core.Server;

namespace ChatApp.Models;

/// <summary>Sunucu penceresindeki "Bağlı İstemciler" listesinin bir satırı.</summary>
public sealed class ClientRow : INotifyPropertyChanged
{
    private int _messageCount;

    public ClientRow(ClientConnection connection)
    {
        Id = connection.Id;
        UserName = connection.UserName;
        EndPoint = connection.RemoteEndPoint;
        ConnectedAt = connection.ConnectedAt;
        _messageCount = connection.MessageCount;
    }

    public Guid Id { get; }
    public string UserName { get; }
    public string EndPoint { get; }
    public DateTime ConnectedAt { get; }

    public string ConnectedText => $"Bağlandı: {ConnectedAt:HH:mm:ss}";

    public int MessageCount
    {
        get => _messageCount;
        set
        {
            if (_messageCount == value) return;
            _messageCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MessageText));
        }
    }

    public string MessageText => $"{MessageCount} mesaj";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
