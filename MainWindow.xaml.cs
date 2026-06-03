using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using DNSManagerGUI.Models;
using DNSManagerGUI.Services;

namespace DNSManagerGUI;

public sealed partial class MainWindow : Window
{
    private readonly DnsResolver _resolver = new();
    private NetworkInterface? _currentAdapter;

    public MainWindow()
    {
        InitializeComponent();

        Title = "DNS Manager Pro";

        // Initialize data
        UpdateAdminStatus();
        RefreshDashboard();
        RefreshProfiles();
        LoadAdapters();
    }

    // ==================== Navigation ====================

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;

        PageDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        PageSetDns.Visibility = tag == "SetDns" ? Visibility.Visible : Visibility.Collapsed;
        PageProfiles.Visibility = tag == "Profiles" ? Visibility.Visible : Visibility.Collapsed;
        PageBenchmark.Visibility = tag == "Benchmark" ? Visibility.Visible : Visibility.Collapsed;
        PageResolver.Visibility = tag == "Resolver" ? Visibility.Visible : Visibility.Collapsed;
        PageImportExport.Visibility = tag == "ImportExport" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ==================== Admin Status & Elevation ====================

    private void UpdateAdminStatus()
    {
        if (AdminHelper.IsElevated())
        {
            AdminIndicator.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xA6, 0xE3, 0xA1));
            AdminStatusText.Text = "Admin ✓";
            AdminStatusText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xA6, 0xE3, 0xA1));
            BtnRestartAsAdmin.Visibility = Visibility.Collapsed;
        }
        else
        {
            AdminIndicator.Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF3, 0x8B, 0xA8));
            AdminStatusText.Text = "Not Admin";
            AdminStatusText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF3, 0x8B, 0xA8));
            BtnRestartAsAdmin.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Checks if running as admin. If not, automatically requests UAC elevation
    /// and restarts the app as admin. Returns true if already admin (proceed with action).
    /// Returns false if elevation was attempted (current instance will be killed).
    /// </summary>
    private async Task<bool> EnsureAdminAsync()
    {
        if (AdminHelper.IsElevated()) return true;

        // Auto-elevate: request UAC and restart
        SetLoading(true, "Requesting admin privileges...");

        if (AdminHelper.TryRelaunchAsAdmin())
        {
            // New elevated instance launched — kill this non-elevated one
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return false; // never reached
        }

        // UAC was declined
        SetLoading(false);
        await ShowMessageAsync(
            "Administrator privileges are required for this action.\n" +
            "You declined the UAC prompt. Click 'Restart as Admin' in the sidebar to retry.",
            "Admin Required");
        return false;
    }

    private void RestartAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (AdminHelper.TryRelaunchAsAdmin())
        {
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        else
        {
            // UAC declined — just update status
            StatusText.Text = "UAC declined — still running as non-admin";
        }
    }

    // ==================== Loading / Status ====================

    private void SetLoading(bool loading, string statusText = "")
    {
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = loading ? (string.IsNullOrEmpty(statusText) ? "Working..." : statusText) : "Ready";
    }

    // ==================== Dashboard ====================

    private void RefreshDashboard()
    {
        _currentAdapter = NetworkAdapterFinder.GetActive();

        if (_currentAdapter != null)
        {
            CurrentAdapterName.Text = _currentAdapter.Name;
            CurrentAdapterType.Text = NetworkAdapterFinder.GetAdapterTypeDisplay(_currentAdapter);
            CurrentAdapterStatus.Text = _currentAdapter.OperationalStatus == OperationalStatus.Up ? "Up" : "Down";

            var dnsServers = NetworkAdapterFinder.GetDnsServers(_currentAdapter);
            CurrentDnsList.ItemsSource = dnsServers.Count > 0 ? dnsServers : new List<string> { "(No DNS / DHCP)" };
        }
        else
        {
            CurrentAdapterName.Text = "Not found";
            CurrentAdapterType.Text = "—";
            CurrentAdapterStatus.Text = "—";
            CurrentDnsList.ItemsSource = new List<string> { "No active adapter found" };
        }

        var adapters = NetworkAdapterFinder.GetFilteredAdapters();
        AdapterCountText.Text = $"{adapters.Count} adapter(s)";
    }

    private async void TestCurrentDnsSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAdapter == null) return;

        var dnsServers = NetworkAdapterFinder.GetDnsServers(_currentAdapter);
        if (dnsServers.Count == 0)
        {
            await ShowMessageAsync("No DNS servers found.", "Speed Test");
            return;
        }

        SetLoading(true, "Testing DNS speed...");
        DnsSpeedCard.Visibility = Visibility.Visible;

        var results = new List<string>();
        foreach (var dns in dnsServers)
        {
            var latency = await _resolver.MeasureLatencyAsync(dns);
            results.Add(latency >= 0 ? $"{dns}: {latency:F1} ms" : $"{dns}: timeout/unreachable");
        }

        DnsSpeedResults.ItemsSource = results;
        SetLoading(false, "Speed test complete");
    }

    private async void ClearDns_Click(object sender, RoutedEventArgs e)
    {
        // Auto-elevate if not admin
        if (!await EnsureAdminAsync()) return;

        var adapter = _currentAdapter ?? NetworkAdapterFinder.GetActive();
        if (adapter == null)
        {
            await ShowMessageAsync("No network adapter found.", "Error");
            return;
        }

        try
        {
            SetLoading(true, "Clearing DNS...");
            DnsConfigurator.SetDhcp(adapter);
            RefreshDashboard();
            SetLoading(false, "DNS cleared");
            await ShowMessageAsync("DNS cleared successfully (DHCP).", "Success");
        }
        catch (Exception ex)
        {
            SetLoading(false);
            await ShowMessageAsync($"Error clearing DNS: {ex.Message}", "Error");
        }
    }

    // ==================== Set DNS ====================

    private void LoadAdapters()
    {
        var adapters = NetworkAdapterFinder.GetFilteredAdapters();
        SetDnsAdapterCombo.ItemsSource = adapters;
        SetDnsAdapterCombo.DisplayMemberPath = "Name";

        if (_currentAdapter != null)
        {
            SetDnsAdapterCombo.SelectedItem = adapters.FirstOrDefault(a => a.Name == _currentAdapter.Name);
        }
        else if (adapters.Count > 0)
        {
            SetDnsAdapterCombo.SelectedIndex = 0;
        }
    }

    private NetworkInterface? GetSelectedAdapter()
    {
        if (SetDnsAdapterCombo.SelectedItem is NetworkInterface selected)
            return selected;
        return _currentAdapter ?? NetworkAdapterFinder.GetActive();
    }

    private async void SetDns_Click(object sender, RoutedEventArgs e)
    {
        var primary = PrimaryDnsInput.Text.Trim();
        var secondary = SecondaryDnsInput.Text.Trim();
        var profileName = ProfileNameInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(primary))
        {
            await ShowMessageAsync("Please enter a primary DNS address.", "Invalid Input");
            return;
        }

        if (!IPAddress.TryParse(primary, out _))
        {
            await ShowMessageAsync("Primary DNS address is invalid.", "Invalid Input");
            return;
        }

        if (!string.IsNullOrWhiteSpace(secondary) && !IPAddress.TryParse(secondary, out _))
        {
            await ShowMessageAsync("Secondary DNS address is invalid.", "Invalid Input");
            return;
        }

        // Auto-elevate if not admin
        if (!await EnsureAdminAsync()) return;

        var adapter = GetSelectedAdapter();
        if (adapter == null)
        {
            await ShowMessageAsync("No network adapter found.", "Error");
            return;
        }

        try
        {
            SetLoading(true, $"Setting DNS on {adapter.Name}...");
            DnsConfigurator.SetDns(adapter, primary, string.IsNullOrWhiteSpace(secondary) ? null : secondary);

            // Save profile
            if (NoSaveCheck.IsChecked != true && !string.IsNullOrWhiteSpace(profileName))
            {
                var profile = new DnsProfile
                {
                    Id = RegistryStore.GetNextId(),
                    Name = profileName,
                    PrimaryDns = primary,
                    SecondaryDns = string.IsNullOrWhiteSpace(secondary) ? null : secondary,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastUsedAtUtc = DateTime.UtcNow
                };
                RegistryStore.SaveProfile(profile);
                RefreshProfiles();
            }

            // Test DNS
            SetLoading(true, "Testing DNS connection...");
            var testResult = await _resolver.TestDnsAsync(primary);

            SetDnsTestResult.Visibility = Visibility.Visible;
            if (testResult)
            {
                SetDnsTestIcon.Glyph = "\uE73E";
                SetDnsTestIcon.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xA6, 0xE3, 0xA1));
                SetDnsTestText.Text = $"DNS set successfully on \"{adapter.Name}\"\nDNS resolution test passed.";
            }
            else
            {
                SetDnsTestIcon.Glyph = "\uE7BA";
                SetDnsTestIcon.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF9, 0xE2, 0xAF));
                SetDnsTestText.Text = $"DNS set but resolution test failed.\nServer may not be reachable.";
            }

            RefreshDashboard();
            SetLoading(false, "DNS set");
        }
        catch (Exception ex)
        {
            SetLoading(false);
            await ShowMessageAsync($"Error setting DNS: {ex.Message}", "Error");
        }
    }

    private async void ClearDnsFromSetPage_Click(object sender, RoutedEventArgs e)
    {
        // Auto-elevate if not admin
        if (!await EnsureAdminAsync()) return;

        var adapter = GetSelectedAdapter();
        if (adapter == null) return;

        try
        {
            SetLoading(true, "Clearing DNS...");
            DnsConfigurator.SetDhcp(adapter);
            RefreshDashboard();
            SetLoading(false, "DNS cleared (DHCP)");
            await ShowMessageAsync("DNS cleared successfully (DHCP).", "Success");
        }
        catch (Exception ex)
        {
            SetLoading(false);
            await ShowMessageAsync($"Error: {ex.Message}", "Error");
        }
    }

    // Quick Select
    private void QuickSelectCloudflare(object sender, RoutedEventArgs e) { PrimaryDnsInput.Text = "1.1.1.1"; SecondaryDnsInput.Text = "1.0.0.1"; ProfileNameInput.Text = "Cloudflare"; }
    private void QuickSelectGoogle(object sender, RoutedEventArgs e) { PrimaryDnsInput.Text = "8.8.8.8"; SecondaryDnsInput.Text = "8.8.4.4"; ProfileNameInput.Text = "Google"; }
    private void QuickSelectOpenDns(object sender, RoutedEventArgs e) { PrimaryDnsInput.Text = "208.67.222.222"; SecondaryDnsInput.Text = "208.67.220.220"; ProfileNameInput.Text = "OpenDNS"; }
    private void QuickSelectQuad9(object sender, RoutedEventArgs e) { PrimaryDnsInput.Text = "9.9.9.9"; SecondaryDnsInput.Text = "149.112.112.112"; ProfileNameInput.Text = "Quad9"; }
    private void QuickSelectAdGuard(object sender, RoutedEventArgs e) { PrimaryDnsInput.Text = "94.140.14.14"; SecondaryDnsInput.Text = "94.140.15.15"; ProfileNameInput.Text = "AdGuard"; }
    private void QuickSelectCleanBrowsing(object sender, RoutedEventArgs e) { PrimaryDnsInput.Text = "185.228.168.9"; SecondaryDnsInput.Text = "185.228.169.9"; ProfileNameInput.Text = "CleanBrowsing"; }

    // ==================== Profiles ====================

    private void RefreshProfiles()
    {
        var profiles = RegistryStore.LoadAll();
        ProfilesListView.ItemsSource = profiles.OrderBy(p => p.Id).ToList();
        ProfilesRegistryPath.Text = $"Registry path: {RegistryStore.RegistryPathDisplay}";
    }

    private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesListView.SelectedItem is not DnsProfile profile)
        {
            await ShowMessageAsync("Please select a profile first.", "Not Selected");
            return;
        }

        // Auto-elevate if not admin
        if (!await EnsureAdminAsync()) return;

        var adapter = GetSelectedAdapter() ?? _currentAdapter;
        if (adapter == null)
        {
            await ShowMessageAsync("No network adapter found.", "Error");
            return;
        }

        try
        {
            SetLoading(true, $"Applying profile \"{profile.Name}\"...");
            DnsConfigurator.SetDns(adapter, profile.PrimaryDns!, profile.SecondaryDns);
            profile.LastUsedAtUtc = DateTime.UtcNow;
            RegistryStore.SaveProfile(profile);
            RefreshDashboard();
            RefreshProfiles();
            SetLoading(false, $"Profile \"{profile.Name}\" applied");
            await ShowMessageAsync($"Profile \"{profile.Name}\" applied successfully.", "Success");
        }
        catch (Exception ex)
        {
            SetLoading(false);
            await ShowMessageAsync($"Error: {ex.Message}", "Error");
        }
    }

    private async void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesListView.SelectedItem is not DnsProfile profile)
        {
            await ShowMessageAsync("Please select a profile first.", "Not Selected");
            return;
        }

        var newName = await InputDialog.ShowAsync("Rename Profile", "New name:", profile.Name, this);
        if (!string.IsNullOrWhiteSpace(newName))
        {
            RegistryStore.RenameProfile(profile.Id, newName);
            RefreshProfiles();
            StatusText.Text = $"Profile {profile.Id} renamed to \"{newName}\"";
        }
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesListView.SelectedItem is not DnsProfile profile)
        {
            await ShowMessageAsync("Please select a profile first.", "Not Selected");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Delete profile \"{profile.Name}\" (ID: {profile.Id})?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (RegistryStore.DeleteProfile(profile.Id))
            {
                RefreshProfiles();
                StatusText.Text = $"Profile {profile.Id} deleted";
            }
            else
            {
                await ShowMessageAsync($"Profile {profile.Id} not found.", "Error");
            }
        }
    }

    // ==================== Benchmark ====================

    private async void RunBenchmark_Click(object sender, RoutedEventArgs e)
    {
        RunBenchmarkBtn.IsEnabled = false;
        SetLoading(true, "Running DNS benchmark...");

        var commonDns = new Dictionary<string, string>
        {
            ["Google"] = "8.8.8.8",
            ["Cloudflare"] = "1.1.1.1",
            ["OpenDNS"] = "208.67.222.222",
            ["Quad9"] = "9.9.9.9",
            ["AdGuard"] = "94.140.14.14",
            ["CleanBrowsing"] = "185.228.168.9"
        };

        var results = new List<BenchmarkResult>();

        foreach (var kvp in commonDns)
        {
            var latency = await _resolver.MeasureLatencyAsync(kvp.Value);
            results.Add(new BenchmarkResult
            {
                Name = kvp.Key,
                Ip = kvp.Value,
                LatencyMs = latency,
                Success = latency >= 0
            });
            BenchmarkResults.ItemsSource = null;
            BenchmarkResults.ItemsSource = results.ToList();
        }

        var sorted = results.OrderBy(r => r.Success ? r.LatencyMs : double.MaxValue).ToList();
        BenchmarkResults.ItemsSource = sorted;

        RunBenchmarkBtn.IsEnabled = true;
        SetLoading(false, "Benchmark complete");
    }

    // ==================== Resolver ====================

    private async void Resolve_Click(object sender, RoutedEventArgs e)
    {
        var domain = ResolveDomainInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(domain))
        {
            await ShowMessageAsync("Please enter a domain name.", "Invalid Input");
            return;
        }

        var dnsServer = ResolveDnsServerInput.Text.Trim();
        SetLoading(true, $"Resolving {domain}...");

        var ips = await _resolver.ResolveAsync(domain, string.IsNullOrWhiteSpace(dnsServer) ? null : dnsServer);

        if (ips.Count == 0)
        {
            ResolveResults.ItemsSource = new List<string> { $"Could not resolve {domain}" };
        }
        else
        {
            ResolveResults.ItemsSource = ips.Select(ip => ip.ToString()).ToList();
        }

        SetLoading(false, $"Resolve complete — {ips.Count} address(es) found");
    }

    // ==================== Import / Export ====================

    private async void ExportBrowse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("JSON Files", new List<string> { ".json" });
        picker.SuggestedFileName = "dns-profiles.json";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            ExportPathInput.Text = file.Path;
        }
    }

    private async void ImportBrowse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".json");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            ImportPathInput.Text = file.Path;
        }
    }

    private void ExportSpecificId_Changed(object sender, RoutedEventArgs e)
    {
        ExportIdPanel.Visibility = ExportSpecificIdCheck.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var filePath = ExportPathInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            await ShowMessageAsync("Please specify a file path.", "Invalid Input");
            return;
        }

        try
        {
            if (ExportSpecificIdCheck.IsChecked == true)
            {
                var idText = ExportIdInput.Text.Trim();
                if (!int.TryParse(idText, out var id))
                {
                    await ShowMessageAsync("Invalid profile ID.", "Invalid Input");
                    return;
                }

                var profile = RegistryStore.FindById(id);
                if (profile == null)
                {
                    await ShowMessageAsync($"Profile id {id} not found.", "Not Found");
                    return;
                }

                ProfileExporter.ExportToFile(new[] { profile }, filePath);
                await ShowMessageAsync($"Profile \"{profile.Name}\" (id: {profile.Id}) exported to:\n{System.IO.Path.GetFullPath(filePath)}", "Success");
            }
            else
            {
                var profiles = RegistryStore.LoadAll();
                if (profiles.Count == 0)
                {
                    await ShowMessageAsync("No profiles to export.", "Empty");
                    return;
                }

                ProfileExporter.ExportToFile(profiles, filePath);
                await ShowMessageAsync($"{profiles.Count} profile(s) exported to:\n{System.IO.Path.GetFullPath(filePath)}", "Success");
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"Export error: {ex.Message}", "Error");
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var filePath = ImportPathInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            await ShowMessageAsync("Please specify a file path.", "Invalid Input");
            return;
        }

        if (!System.IO.File.Exists(filePath))
        {
            await ShowMessageAsync($"File not found: {filePath}", "Not Found");
            return;
        }

        try
        {
            var mergeMode = ImportMergeCheck.IsChecked == true;
            var profiles = ProfileExporter.ImportFromFile(filePath);

            if (profiles.Count == 0)
            {
                await ShowMessageAsync("No valid profiles found in file.", "Empty");
                return;
            }

            if (!mergeMode)
            {
                var existingProfiles = RegistryStore.LoadAll();
                foreach (var p in existingProfiles)
                {
                    RegistryStore.DeleteProfile(p.Id);
                }
            }

            int imported = 0;
            int skipped = 0;
            foreach (var profile in profiles)
            {
                if (mergeMode)
                {
                    var existing = RegistryStore.FindById(profile.Id);
                    if (existing != null)
                    {
                        skipped++;
                        continue;
                    }
                }

                profile.Id = RegistryStore.GetNextId();
                profile.CreatedAtUtc = DateTime.UtcNow;
                profile.LastUsedAtUtc = null;
                RegistryStore.SaveProfile(profile);
                imported++;
            }

            RefreshProfiles();

            string message = mergeMode
                ? $"Imported {imported} profile(s), skipped {skipped} duplicate(s)."
                : $"Imported {imported} profile(s).";

            await ShowMessageAsync(message, "Success");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"Import error: {ex.Message}", "Error");
        }
    }

    // ==================== Helpers ====================

    private async Task ShowMessageAsync(string message, string title)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
