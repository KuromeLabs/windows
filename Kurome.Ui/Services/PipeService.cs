using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using Kurome.Core;
using Kurome.Ui.ViewModels;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Kurome.Ui.Services;

public class PipeService : IHostedService
{
    private readonly DevicesViewModel _devicesViewModel;

    private NamedPipeClientStream _pipeClient = new(".", "KuromePipe", PipeDirection.InOut,
        PipeOptions.Asynchronous);


    private readonly ILogger _logger = Log.ForContext<PipeService>();

    public PipeService(DevicesViewModel devicesViewModel)
    {
        _devicesViewModel = devicesViewModel;
    }

    public async void Send(string message, CancellationToken cancellationToken)
    {
        if (!_pipeClient.IsConnected)
        {
            return;
        }

        var messageLength = Encoding.UTF8.GetByteCount(message);
        var buffer = new byte[4 + messageLength];
        Encoding.UTF8.GetBytes(message, buffer.AsSpan()[4..]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], messageLength);
        await _pipeClient.WriteAsync(buffer, cancellationToken);
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_pipeClient.IsConnected)
                    {
                        _pipeClient = new NamedPipeClientStream(".", "KuromePipe", PipeDirection.InOut,
                            PipeOptions.Asynchronous);
                        await _pipeClient.ConnectAsync(stoppingToken);
                    }

                    var buffer = new byte[4];
                    await _pipeClient.ReadExactlyAsync(buffer, stoppingToken);
                    var length = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                    buffer = new byte[length];
                    await _pipeClient.ReadExactlyAsync(buffer, stoppingToken);
                    var msg = Encoding.UTF8.GetString(buffer);
                    _logger.Information($"Received: {msg}");
                    ProcessMessage(msg);
                }
                catch (Exception e)
                {
                    await _pipeClient.DisposeAsync();
                    _logger.Error(e, "Error while reading from pipe");
                }
            }
        }, stoppingToken);

        return Task.CompletedTask;
    }

    private void ProcessMessage(string message)
    {
        var devices = JsonSerializer.Deserialize<List<Device>>(message);
        _devicesViewModel.SetDevices(devices);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}