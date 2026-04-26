// AeroDial — AboutDialog.cs
// Standalone About window, reachable from the system tray context menu.
// Reuses AboutContent so the content is never duplicated.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI;

namespace AeroDial.UI.Views;

public sealed class AboutDialog : Window
{
    private static AboutDialog? _instance;

    public AboutDialog()
    {
        Title = $"About AeroDial";
        Content = AboutContent.Build();
        ConfigureChrome();
    }

    public static void ShowOrActivate()
    {
        if (_instance is not null) { _instance.Activate(); return; }
        _instance = new AboutDialog();
        _instance.Closed += (_, _) => _instance = null;
        _instance.Activate();
    }

    private void ConfigureChrome()
    {
        AppWindow.Resize(new Windows.Graphics.SizeInt32(520, 580));
        AppWindow.IsShownInSwitchers = true;
        AppWindow.SetIcon(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "aerodial.ico"));

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.ButtonBackgroundColor         = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        var display = DisplayArea.Primary;
        int x = (display.WorkArea.Width  - 520) / 2;
        int y = (display.WorkArea.Height - 580) / 2;
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }
}
