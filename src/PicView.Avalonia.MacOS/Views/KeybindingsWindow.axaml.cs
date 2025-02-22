using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PicView.Core.Config;
using PicView.Core.Localization;

namespace PicView.Avalonia.MacOS.Views;

public partial class KeybindingsWindow : Window
{
    public KeybindingsWindow()
    {
        InitializeComponent();
        if (!SettingsHelper.Settings.Theme.Dark || SettingsHelper.Settings.Theme.GlassTheme)
        {
            WindowBorder.Background = Brushes.Transparent;
            XKeybindingsView.Background = Brushes.Transparent;
        }
        Loaded += (sender, e) =>
        {
            MinWidth = MaxWidth = Width;
            Title = $"{TranslationHelper.Translation.ApplicationShortcuts} - PicView";
        };
    }

    private void MoveWindow(object? sender, PointerPressedEventArgs e)
    {
        if (VisualRoot is null) { return; }

        var hostWindow = (Window)VisualRoot;
        hostWindow?.BeginMoveDrag(e);
    }
}