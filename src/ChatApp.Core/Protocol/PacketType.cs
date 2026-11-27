namespace ChatApp.Core.Protocol;

/// <summary>İstemci ile sunucu arasında gidip gelen paket türleri.</summary>
public enum PacketType
{
    /// <summary>İstemci → Sunucu: kullanıcı adıyla katılma isteği.</summary>
    Join,

    /// <summary>Sunucu → İstemci: katılma kabul edildi.</summary>
    Accepted,

    /// <summary>Sunucu → İstemci: katılma reddedildi (ör. kullanıcı adı dolu).</summary>
    Rejected,

    /// <summary>Herkese açık sohbet mesajı.</summary>
    Chat,

    /// <summary>Yalnızca tek bir kullanıcıya giden özel mesaj.</summary>
    Private,

    /// <summary>Sunucu → İstemci: çevrimiçi kullanıcı listesi.</summary>
    UserList,

    /// <summary>Sunucu → İstemci: bilgilendirme (katıldı/ayrıldı vb.).</summary>
    System,

    /// <summary>İstemci → Sunucu: bağlantıyı düzgün şekilde kapatma.</summary>
    Leave
}
