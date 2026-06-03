using System;
using System.Diagnostics;
using System.Security.Principal;

namespace DNSManagerGUI.Services;

public static class AdminHelper
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Attempts to relaunch the current process with admin privileges via UAC.
    /// Returns true if the new elevated process was started successfully.
    /// The caller should exit the current (non-elevated) process afterwards.
    /// </summary>
    public static bool TryRelaunchAsAdmin()
    {
        if (IsElevated()) return true;

        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exe)) return false;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Verb = "runas",
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        try
        {
            Process.Start(psi);
            return true;
        }
        catch
        {
            // User declined UAC or other error
            return false;
        }
    }

    /// <summary>
    /// Ensures the app is running with admin privileges.
    /// If not elevated, shows a UAC prompt and restarts the app as admin.
    /// If UAC is declined, returns false (current non-admin instance continues).
    /// </summary>
    public static bool EnsureElevatedAndRelaunch()
    {
        if (IsElevated()) return true;

        if (TryRelaunchAsAdmin())
        {
            // New elevated process launched — exit current non-elevated instance
            Process.GetCurrentProcess().Kill();
            return false; // never reached, but satisfies return type
        }

        // UAC was declined — continue as non-admin
        return false;
    }
}
