using Kurome.Fbs;
using Microsoft.Extensions.Logging;

namespace Application.flatbuffers;

public class FlatBufferHelper
{
    private readonly ILogger<FlatBufferHelper> _logger;

    public FlatBufferHelper(ILogger<FlatBufferHelper> logger)
    {
        _logger = logger;
    }

    public bool TryGetFileResponseNode(Packet packet, out Node? response)
    {
        try
        {
            response = packet.Component?.FileResponse.Response?.Node;
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
            response = null;
        }

        return response != null;
    }


    public bool TryGetFileResponseRaw(Packet packet, out Raw? raw)
    {
        try
        {
            raw = packet.Component?.FileResponse.Response?.Raw;
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
            raw = null;
        }

        return raw != null;
    }

    public bool TryGetDeviceInfo(Packet packet, out DeviceInfo? deviceInfo)
    {
        try
        {
            deviceInfo = packet.Component?.DeviceResponse.Response?.DeviceInfo;
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
            deviceInfo = null;
        }

        return deviceInfo != null;
    }

    public Component DeviceInfoSpaceQuery()
    {
        return new Component(new DeviceQuery { Type = DeviceQueryType.GetSpace });
    }

    public Component CreateFileCommand(string fileName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Create { Path = fileName, Type = FileType.File })
        });
    }

    public Component CreateDirectoryCommand(string directoryName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Create { Path = directoryName, Type = FileType.Directory })
        });
    }

    public Component ReadFileQuery(string fileName, long offset, int length)
    {
        return new Component(new FileQuery
        {
            Type = FileQueryType.ReadFile, Path = fileName, Length = length, Offset = offset
        });
    }

    public Component WriteFileCommand(Memory<byte> buffer, string fileName, long offset)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Write
            {
                Path = fileName,
                Buffer = new Raw { Data = buffer, Length = buffer.Length, Offset = offset }
            })
        });
    }

    public Component DeleteCommand(string fileName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Delete { Path = fileName })
        });
    }

    public Component SetLengthCommand(string fileName, long length)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new SetAttributes
            {
                Path = fileName, Attributes = new Attributes { Length = length }
            })
        });
    }

    public Component SetFileTimeCommand(string fileName, long cTime, long laTime, long lwTime)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new SetAttributes
            {
                Path = fileName,
                Attributes = new Attributes { CreationTime = cTime, LastAccessTime = laTime, LastWriteTime = lwTime }
            })
        });
    }

    public Component RenameCommand(string fileName, string newFileName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Rename
            {
                OldPath = fileName, NewPath = newFileName
            })
        });
    }

    public Component GetDirectoryQuery(string path)
    {
        return new Component(new FileQuery
        {
            Type = FileQueryType.GetDirectory, Path = path
        });
    }
}