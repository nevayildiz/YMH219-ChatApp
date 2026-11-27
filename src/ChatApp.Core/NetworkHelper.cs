using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ChatApp.Core;

public static class NetworkHelper
{
    /// <summary>
    /// Bu bilgisayarın yerel ağdaki IPv4 adreslerini döner. Başka bir bilgisayardan
    /// bağlanacak istemci bu adreslerden birini kullanır.
    /// </summary>
    public static IReadOnlyList<string> GetLocalIPv4Addresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Port metnini doğrular (1–65535).</summary>
    public static bool TryParsePort(string? text, out int port) =>
        int.TryParse(text?.Trim(), out port) && port is > 0 and <= 65535;
}
