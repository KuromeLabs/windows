using System.Windows;
using Wpf.Ui.Appearance;

namespace Kurome.Ui.Services;

public class AppThemeService
{
    private readonly SettingsService _settings;
    private Window? _window;

    public AppThemeService(SettingsService settings)
    {
        _settings = settings;
    }

    public AppTheme Current
    {
        get => _settings.Current.Theme;
        set
        {
            if (_settings.Current.Theme == value) return;
            _settings.Current.Theme = value;
            _settings.Save();
            Apply();
        }
    }

    public void Attach(Window window)
    {
        _window = window;
        Apply();
    }

    public void Apply()
    {
        var theme = _settings.Current.Theme;

        if (_window != null)
        {
            if (theme == AppTheme.System) SystemThemeWatcher.Watch(_window);
            else SystemThemeWatcher.UnWatch(_window);
        }

        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case AppTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }
}
