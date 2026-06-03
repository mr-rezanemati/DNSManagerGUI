using System;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;

namespace DNSManagerGUI.Services;

public static class DnsConfigurator
{
    public static void SetDns(NetworkInterface nic, string primary, string? secondary)
    {
        var config = GetNetworkAdapterConfig(nic.Description);
        if (config == null)
            throw new Exception($"Could not find WMI configuration for adapter: {nic.Name}");

        try
        {
            var dnsServers = secondary != null
                ? new[] { primary, secondary }
                : new[] { primary };

            using var mo = new ManagementObject(config["__PATH"].ToString());
            var inParams = mo.GetMethodParameters("SetDNSServerSearchOrder");
            inParams["DNSServerSearchOrder"] = dnsServers;

            var outParams = mo.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
            var returnValue = (uint)outParams["ReturnValue"];

            if (returnValue != 0 && returnValue != 1)
                throw new Exception($"Failed to set DNS. WMI return code: {returnValue}");

            FlushDns();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error setting DNS: {ex.Message}", ex);
        }
    }

    public static void SetDhcp(NetworkInterface nic)
    {
        var config = GetNetworkAdapterConfig(nic.Description);
        if (config == null)
            throw new Exception($"Could not find WMI configuration for adapter: {nic.Name}");

        try
        {
            using var mo = new ManagementObject(config["__PATH"].ToString());
            var inParams = mo.GetMethodParameters("SetDNSServerSearchOrder");
            inParams["DNSServerSearchOrder"] = null;

            var outParams = mo.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
            var returnValue = (uint)outParams["ReturnValue"];

            if (returnValue != 0 && returnValue != 1)
                throw new Exception($"Failed to set DHCP DNS. WMI return code: {returnValue}");

            FlushDns();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error setting DHCP: {ex.Message}", ex);
        }
    }

    private static ManagementObject? GetNetworkAdapterConfig(string adapterDescription)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");

        foreach (ManagementObject obj in searcher.Get())
        {
            var description = obj["Description"]?.ToString();
            if (description?.Equals(adapterDescription, StringComparison.OrdinalIgnoreCase) == true)
                return obj;
        }
        return null;
    }

    private static void FlushDns()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            process?.WaitForExit(2000);
        }
        catch { /* ignore */ }
    }
}
