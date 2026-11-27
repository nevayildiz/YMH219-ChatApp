namespace ChatApp.Models;

/// <summary>İstemci penceresindeki "Çevrimiçi" listesinin bir öğesi.</summary>
public sealed record UserEntry(string Name, bool IsEveryone, bool IsSelf)
{
    public static readonly UserEntry Everyone = new(string.Empty, IsEveryone: true, IsSelf: false);

    /// <summary>Seçilince mesaj özel olarak bu kişiye gider (herkes ve kendisi hariç).</summary>
    public bool CanReceivePrivate => !IsEveryone && !IsSelf;

    public override string ToString() =>
        IsEveryone ? "Herkes (genel sohbet)" : IsSelf ? $"{Name} (siz)" : Name;
}
