using Serilog;
using ILogger = DokanNet.Logging.ILogger;

namespace Kurome.Core.Filesystem;

public class KuromeDokanLogger : ILogger
{
    private readonly Serilog.ILogger _logger = Log.ForContext<KuromeDokanLogger>();
    public void Debug(string message, params object[] args)
    {
        _logger.Debug(message, args);
    }

    public void Info(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    public void Warn(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }

    public void Error(string message, params object[] args)
    {
        _logger.Error(message, args);
    }

    public void Fatal(string message, params object[] args)
    {
        _logger.Fatal(message, args);
    }

    public bool DebugEnabled => true;
}