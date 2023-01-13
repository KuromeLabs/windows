using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;

namespace KuromeUI.Views;

public partial class MainView : UserControl
{
    private NavigationView _navigationView;

    public MainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _navigationView = this.FindControl<NavigationView>("NavView")!;
        var testItems = new List<NavigationViewItem>
        {
            new()
            {
                Content = "Devices",
                Classes = { "Nav" },
                Icon = new IconSourceElement { IconSource = (IconSource)this.FindResource("HomeIcon") }
            },
            new()
            {
                Content = "Settings",
                Classes = { "Nav" },
                Icon = new IconSourceElement { IconSource = (IconSource)this.FindResource("HomeIcon") }
            }
        };
        // _navigationView.Resources["NavigationViewExpandedPaneBackground"] = new SolidColorBrush(new Color(255, 255, 255, 255));
        _navigationView.MenuItems = testItems;
    }
}