using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Media;
using FluentAvalonia.UI.Windowing;

namespace KuromeUI.Views;

public partial class MainWindow : AppWindow
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        MinWidth = 450;
        MinHeight = 400;
        TitleBar.ExtendsContentIntoTitleBar = true;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var theme = AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>();
        theme!.RequestedThemeChanged += OnRequestedThemeChanged;

        SetStyle(theme);
        var screen = Screens.ScreenFromVisual(this);
        if (screen == null) return;
        Width = screen.WorkingArea.Width switch
        {
            > 1280 => 1280,
            > 1000 => 1000,
            > 700 => 700,
            > 500 => 500,
            _ => 450
        };

        Height = screen.WorkingArea.Height switch
        {
            > 720 => 720,
            > 600 => 600,
            > 500 => 500,
            _ => 400
        };
    }

    protected override void OnRequestedThemeChanged(FluentAvaloniaTheme sender, RequestedThemeChangedEventArgs args)
    {
        base.OnRequestedThemeChanged(sender, args);
        SetStyle(sender);
    }

    private void SetStyle(FluentAvaloniaTheme theme)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        if (!IsWindows11 || theme.RequestedTheme == FluentAvaloniaTheme.HighContrastModeString)
        {
            SetValue(BackgroundProperty, AvaloniaProperty.UnsetValue);
            return;
        }
        TransparencyBackgroundFallback = Brushes.Transparent;
        TransparencyLevelHint = WindowTransparencyLevel.Mica;
        if (theme.RequestedTheme == FluentAvaloniaTheme.DarkModeString)
        {
            var color = this.TryFindResource("SolidBackgroundFillColorBase", out var value) ? (Color2)(Color)value : new Color2(32, 32, 32);
            color = color.LightenPercent(-0.8f);
            Background = new ImmutableSolidColorBrush(color, 0.78);
        }
        else if (theme.RequestedTheme == FluentAvaloniaTheme.LightModeString)
        {
            var color = this.TryFindResource("SolidBackgroundFillColorBase", out var value) ? (Color2)(Color)value! : new Color2(243, 243, 243);
            color = color.LightenPercent(0.5f);
            Background = new ImmutableSolidColorBrush(color, 0.9);
        }
    }
}