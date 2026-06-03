using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DNSManagerGUI.Services;

public static class NetworkAdapterFinder
{
    public static List<NetworkInterface> GetAllActive()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n =>
                n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();
    }

    public static List<NetworkInterface> GetFilteredAdapters()
    {
        return GetAllActive()
            .Where(n =>
                !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase) &&
                !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static NetworkInterface? GetActive(string? preferredName = null)
    {
        var nics = GetFilteredAdapters();

        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            var exact = nics.FirstOrDefault(n =>
                string.Equals(n.Name, preferredName, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
        }

        NetworkInterface? best = null;
        foreach (var nic in nics)
        {
            var props = nic.GetIPProperties();
            bool hasIpv4 = nic.Supports(NetworkInterfaceComponent.IPv4);
            bool hasGw = props.GatewayAddresses.Any(g =>
                g?.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork);
            if (hasIpv4 && hasGw)
                return nic;
            if (best == null && hasIpv4) best = nic;
        }
        return best ?? nics.FirstOrDefault();
    }

    public static List<string> GetDnsServers(NetworkInterface nic)
    {
        var props = nic.GetIPProperties();
        return props.DnsAddresses
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                       a.AddressFamily == AddressFamily.InterNetworkV6)
            .Select(a => a.ToString())
            .ToList();
    }

    public static string GetAdapterTypeDisplay(NetworkInterface nic)
    {
        return nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Ethernet => "Ethernet",
            NetworkInterfaceType.Wireless80211 => "Wi-Fi",
            _ => nic.NetworkInterfaceType.ToString()
        };
    }
}
