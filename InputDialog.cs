using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DNSManagerGUI;

public static class InputDialog
{
    public static async Task<string?> ShowAsync(string title, string prompt, string defaultValue, Window window)
    {
        var textBox = new TextBox
        {
            Text = defaultValue,
            PlaceholderText = prompt,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = prompt },
                    textBox
                }
            },
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return textBox.Text.Trim();
        }
        return null;
    }
}
