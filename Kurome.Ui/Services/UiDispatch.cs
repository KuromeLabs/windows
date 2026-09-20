using System.Windows;
using System.Windows.Threading;
using Serilog;

namespace Kurome.Ui.Services;

public static class UiDispatch
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UiDispatch));

    public static void Post(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher == null)
        {
            Logger.Debug("No dispatcher available; running inline on {Thread}",
                Environment.CurrentManagedThreadId);
            Run(action);
            return;
        }

        if (dispatcher.CheckAccess())
        {
            Run(action);
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => Run(action)));
    }

    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error while handling a dispatched UI update");
        }
    }
}
