using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using DNSManagerGUI.Models;
using Microsoft.Win32;

namespace DNSManagerGUI.Services;

public static class RegistryStore
{
    public const string BaseKeyPath = @"Software\DnsManager\Profiles";
    public static string RegistryPathDisplay => @"HKCU\" + BaseKeyPath;

    public static int GetNextId()
    {
        using var baseKey = Registry.CurrentUser.CreateSubKey(BaseKeyPath, true)!;
        var ids = baseKey.GetSubKeyNames()
            .Select(n => int.TryParse(n, out var i) ? i : 0)
            .Where(i => i > 0)
            .ToList();
        return ids.Count == 0 ? 1 : ids.Max() + 1;
    }

    public static List<DnsProfile> LoadAll()
    {
        using var baseKey = Registry.CurrentUser.CreateSubKey(BaseKeyPath, true)!;
        var list = new List<DnsProfile>();
        foreach (var name in baseKey.GetSubKeyNames())
        {
            if (!int.TryParse(name, out var id)) continue;
            var p = ReadProfile(baseKey, id);
            if (p != null) list.Add(p);
        }
        return list;
    }

    public static DnsProfile? FindById(int id)
    {
        using var baseKey = Registry.CurrentUser.CreateSubKey(BaseKeyPath, true)!;
        return ReadProfile(baseKey, id);
    }

    public static void SaveProfile(DnsProfile profile)
    {
        using var baseKey = Registry.CurrentUser.CreateSubKey(BaseKeyPath, true)!;
        using var sub = baseKey.CreateSubKey(profile.Id.ToString(), true)!;
        sub.SetValue("Id", profile.Id, RegistryValueKind.DWord);
        sub.SetValue("Name", profile.Name ?? "unnamed", RegistryValueKind.String);
        sub.SetValue("PrimaryDns", profile.PrimaryDns ?? string.Empty, RegistryValueKind.String);
        sub.SetValue("SecondaryDns", profile.SecondaryDns ?? string.Empty, RegistryValueKind.String);
        sub.SetValue("CreatedAtUtc", profile.CreatedAtUtc.ToString("o"), RegistryValueKind.String);
        sub.SetValue("LastUsedAtUtc", profile.LastUsedAtUtc?.ToString("o") ?? string.Empty, RegistryValueKind.String);
    }

    public static bool DeleteProfile(int id)
    {
        using var baseKey = Registry.CurrentUser.CreateSubKey(BaseKeyPath, true)!;
        if (baseKey.GetSubKeyNames().Contains(id.ToString()))
        {
            baseKey.DeleteSubKeyTree(id.ToString(), false);
            return true;
        }
        return false;
    }

    public static bool RenameProfile(int id, string newName)
    {
        using var baseKey = Registry.CurrentUser.CreateSubKey(BaseKeyPath, true)!;
        using var sub = baseKey.OpenSubKey(id.ToString(), true);
        if (sub == null) return false;
        sub.SetValue("Name", newName, RegistryValueKind.String);
        return true;
    }

    private static DnsProfile? ReadProfile(RegistryKey baseKey, int id)
    {
        using var sub = baseKey.OpenSubKey(id.ToString(), false);
        if (sub == null) return null;
        var p = new DnsProfile
        {
            Id = id,
            Name = (string?)sub.GetValue("Name") ?? "unnamed",
            PrimaryDns = ((string?)sub.GetValue("PrimaryDns"))?.Trim(),
            SecondaryDns = string.IsNullOrWhiteSpace((string?)sub.GetValue("SecondaryDns")) ? null : ((string?)sub.GetValue("SecondaryDns"))?.Trim(),
            CreatedAtUtc = ParseOrDefault((string?)sub.GetValue("CreatedAtUtc")),
            LastUsedAtUtc = ParseNullable((string?)sub.GetValue("LastUsedAtUtc"))
        };
        return p;

        static DateTime ParseOrDefault(string? s) =>
            DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.MinValue;

        static DateTime? ParseNullable(string? s) =>
            DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }
}
