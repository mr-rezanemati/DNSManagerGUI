using Microsoft.UI.Xaml;
using DNSManagerGUI.Services;

namespace DNSManagerGUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Auto-elevate on startup: if not admin, request UAC and restart
        if (!AdminHelper.IsElevated())
        {
            if (AdminHelper.TryRelaunchAsAdmin())
            {
                // New elevated instance launched — exit this non-elevated one
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }
            // UAC was declined — continue as non-admin (limited functionality)
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
