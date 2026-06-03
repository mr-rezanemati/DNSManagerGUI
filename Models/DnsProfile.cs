using System;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml.Media;

namespace DNSManagerGUI.Models;

public sealed class DnsProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "unnamed";
    public string? PrimaryDns { get; set; }
    public string? SecondaryDns { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }

    [JsonIgnore]
    public string SecondaryDnsDisplay => SecondaryDns ?? "—";

    [JsonIgnore]
    public string LastUsedDisplay => LastUsedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Never";
}

public class DnsProfilesExport
{
    public DateTime ExportedAtUtc { get; set; }
    public string Version { get; set; } = "1.0";
    public List<DnsProfileExport> Profiles { get; set; } = new();
}

public class DnsProfileExport
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PrimaryDns { get; set; }
    public string? SecondaryDns { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}

public class BenchmarkResult
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public double LatencyMs { get; set; }
    public bool Success { get; set; }

    public string LatencyDisplay => Success ? $"{LatencyMs:F1} ms" : "timeout";

    public SolidColorBrush LatencyColor => Success
        ? (LatencyMs < 20
            ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xA6, 0xE3, 0xA1))
            : LatencyMs < 50
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF9, 0xE2, 0xAF))
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF3, 0x8B, 0xA8)))
        : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF3, 0x8B, 0xA8));
}
