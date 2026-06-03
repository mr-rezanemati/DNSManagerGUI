using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using DNSManagerGUI.Models;

namespace DNSManagerGUI.Services;

public static class ProfileExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void ExportToFile(IEnumerable<DnsProfile> profiles, string filePath)
    {
        var exportData = new DnsProfilesExport
        {
            ExportedAtUtc = DateTime.UtcNow,
            Version = "1.0",
            Profiles = profiles.Select(p => new DnsProfileExport
            {
                Id = p.Id,
                Name = p.Name,
                PrimaryDns = p.PrimaryDns,
                SecondaryDns = p.SecondaryDns,
                CreatedAtUtc = p.CreatedAtUtc,
                LastUsedAtUtc = p.LastUsedAtUtc
            }).ToList()
        };

        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static List<DnsProfile> ImportFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var importData = JsonSerializer.Deserialize<DnsProfilesExport>(json, JsonOptions);

        if (importData?.Profiles == null || importData.Profiles.Count == 0)
            return new List<DnsProfile>();

        return importData.Profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.PrimaryDns) && IsValidIp(p.PrimaryDns))
            .Select(p => new DnsProfile
            {
                Id = p.Id,
                Name = p.Name ?? "Imported Profile",
                PrimaryDns = p.PrimaryDns,
                SecondaryDns = string.IsNullOrWhiteSpace(p.SecondaryDns) ? null : p.SecondaryDns,
                CreatedAtUtc = p.CreatedAtUtc,
                LastUsedAtUtc = p.LastUsedAtUtc
            })
            .ToList();
    }

    private static bool IsValidIp(string ip)
    {
        return IPAddress.TryParse(ip, out _);
    }
}
