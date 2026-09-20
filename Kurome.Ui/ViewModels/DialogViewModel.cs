using System.Collections.Concurrent;
using System.Reactive.Linq;
using Kurome.Fbs.Ipc;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Services;
using ReactiveUI;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Kurome.Ui.ViewModels;

public class DialogViewModel : ReactiveObject
{
    private readonly IContentDialogService _contentDialogService;
    private readonly KuromeClient _client;
    private readonly ILogger _logger = Log.ForContext<DialogViewModel>();

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();

    private readonly SemaphoreSlim _dialogGate = new(1, 1);

    public DialogViewModel(IContentDialogService contentDialogService, KuromeClient client)
    {
        _contentDialogService = contentDialogService;
        _client = client;

        client.PairEvents
            .Subscribe(OnPairEvent);
    }

    private void OnPairEvent(PairEvent pairEvent)
    {
        var state = pairEvent.DeviceState!;
        var id = state.Id!;

        switch (pairEvent.Value)
        {
            case PairEventType.PairRequest:
                if (_pending.ContainsKey(id)) return;
                _ = PromptAsync(state);
                break;

            case PairEventType.PairRequestCancel:
                if (_pending.TryRemove(id, out var cts))
                {
                    _logger.Information("Pair request from {Name} was withdrawn", state.Name);
                    cts.Cancel();
                    cts.Dispose();
                }

                break;
        }
    }

    private async Task PromptAsync(DeviceState state)
    {
        var id = state.Id!;
        var cts = new CancellationTokenSource();
        if (!_pending.TryAdd(id, cts))
        {
            cts.Dispose();
            return;
        }

        await _dialogGate.WaitAsync();
        try
        {
            if (cts.IsCancellationRequested) return;

            var dialog = new IncomingPairDialog(_contentDialogService.GetDialogHost()!, state)
            {
                Title = "Pair with this device?",
                PrimaryButtonText = "Pair",
                CloseButtonText = "Reject"
            };

            var result = await dialog.ShowAsync(cts.Token);
            if (result == ContentDialogResult.Primary) _client.AcceptPairing(state);
            else _client.RejectPairing(state);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to show the pair request for {Name}", state.Name);
        }
        finally
        {
            _dialogGate.Release();
            if (_pending.TryRemove(id, out var removed)) removed.Dispose();
        }
    }
}
