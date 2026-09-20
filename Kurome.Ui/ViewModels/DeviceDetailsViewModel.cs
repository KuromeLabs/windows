using System.Windows;
using Kurome.Ui.Models;
using Kurome.Ui.Services;
using ReactiveUI;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Kurome.Ui.ViewModels;

public class DeviceDetailsViewModel : ReactiveObject
{
    private readonly KuromeClient _client;
    private readonly IContentDialogService _contentDialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger = Log.ForContext<DeviceDetailsViewModel>();

    public DeviceDetailsViewModel(
        KuromeClient client,
        IContentDialogService contentDialogService,
        ISnackbarService snackbarService,
        INavigationService navigationService)
    {
        _client = client;
        _contentDialogService = contentDialogService;
        _snackbarService = snackbarService;
        _navigationService = navigationService;
    }

    private DeviceItem? _device;
    public DeviceItem? Device
    {
        get => _device;
        private set
        {
            this.RaiseAndSetIfChanged(ref _device, value);
            this.RaisePropertyChanged(nameof(HasDevice));
        }
    }

    public bool HasDevice => _device != null;

    public void Show(DeviceItem device) => Device = device;

    public void CopyId()
    {
        var device = Device;
        if (device == null) return;

        try
        {
            Clipboard.SetText(device.Id);
            _snackbarService.Show("Copied", "Device ID copied to the clipboard.",
                ControlAppearance.Success, new SymbolIcon { Symbol = SymbolRegular.Checkmark24 },
                TimeSpan.FromSeconds(2));
        }
        catch (Exception e)
        {
            _logger.Warning(e, "Could not copy the device ID to the clipboard");
        }
    }

    public async Task UnpairAsync()
    {
        var device = Device;
        if (device == null) return;

        var dialog = new ContentDialog(_contentDialogService.GetDialogHost())
        {
            Title = "Unpair this device?",
            Content = $"Kurome will forget {device.Name} and stop sharing its storage. " +
                      "The device is told to forget this computer too, so pairing again means " +
                      "confirming on both ends.",
            PrimaryButtonText = "Unpair",
            CloseButtonText = "Cancel"
        };

        var result = await dialog.ShowAsync(CancellationToken.None);
        if (result != ContentDialogResult.Primary) return;

        _client.Unpair(device.ToDeviceState());
        _navigationService.GoBack();
    }
}
