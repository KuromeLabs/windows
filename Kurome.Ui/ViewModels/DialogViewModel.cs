using System.Reactive.Linq;
using Kurome.Fbs.Ipc;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Services;
using ReactiveUI;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Kurome.Ui.ViewModels;

public partial class DialogViewModel : ReactiveObject
{
    private readonly IContentDialogService _contentDialogService;
    private readonly PipeService _pipeService;
    private CancellationTokenSource _cts = new();

    public DialogViewModel(IContentDialogService contentDialogService, PipeService pipeService)
    {
        _contentDialogService = contentDialogService;
        _pipeService = pipeService;
        
        _pipeService.IpcEventStreamObservable
            .Where(x => x.Component!.Value.Kind == Component.ItemKind.PairEvent)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async packet =>
            {
                var pairEvent = packet.Component!.Value.PairEvent;
                var state = pairEvent.DeviceState!;
                if (pairEvent.Value == PairEventType.PairRequestCancel)
                {
                    _cts.Cancel();
                    _cts = new CancellationTokenSource();
                    return;
                }
                var dialog = new IncomingPairDialog(_contentDialogService.GetDialogHost()!, state)
                {
                    Title = "Incoming Pair Request",
                    PrimaryButtonText = "Accept",
                    SecondaryButtonText = "",
                    CloseButtonText = "Reject"
                };
                try
                {
                    var result = await dialog.ShowAsync(_cts.Token);
                    if (result == ContentDialogResult.Primary)
                    {
                        _pipeService.AcceptPairingRequest(state);
                    }
                    else
                    {
                        _pipeService.RejectPairingRequest(state);
                    }
                }
                catch (Exception e)
                {
                    //task cancelled
                }
                
            });
    }
}