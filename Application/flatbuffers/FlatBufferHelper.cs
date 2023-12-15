using Kurome.Fbs;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Application.flatbuffers;

public static class FlatBufferHelper
{
    private static readonly ILogger Logger = Log.ForContext(typeof(FlatBufferHelper));

    public static bool TryGetFileResponseNode(Packet packet, out Node? response)
    {
        try
        {
            response = packet.Component?.FileResponse.Response?.Node;
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
            response = null;
        }

        return response != null;
    }


    public static bool TryGetFileResponseRaw(Packet packet, out Raw? raw)
    {
        try
        {
            raw = packet.Component?.FileResponse.Response?.Raw;
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
            raw = null;
        }

        return raw != null;
    }

    public static bool TryGetDeviceInfo(Packet packet, out DeviceInfo? deviceInfo)
    {
        try
        {
            deviceInfo = packet.Component?.DeviceResponse.Response?.DeviceInfo;
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
            deviceInfo = null;
        }

        return deviceInfo != null;
    }

    public static Component DeviceInfoSpaceQuery()
    {
        return new Component(new DeviceQuery { Type = DeviceQueryType.GetSpace });
    }

    public static Component CreateFileCommand(string fileName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Create { Path = fileName, Type = FileType.File })
        });
    }

    public static Component CreateDirectoryCommand(string directoryName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Create { Path = directoryName, Type = FileType.Directory })
        });
    }

    public static Component ReadFileQuery(string fileName, long offset, int length)
    {
        return new Component(new FileQuery
        {
            Type = FileQueryType.ReadFile, Path = fileName, Length = length, Offset = offset
        });
    }

    public static Component WriteFileCommand(Memory<byte> buffer, string fileName, long offset)
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

    public static Component DeleteCommand(string fileName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Delete { Path = fileName })
        });
    }

    public static Component SetLengthCommand(string fileName, long length)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new SetAttributes
            {
                Path = fileName, Attributes = new Attributes { Length = length }
            })
        });
    }

    public static Component SetFileTimeCommand(string fileName, long cTime, long laTime, long lwTime)
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

    public static Component RenameCommand(string fileName, string newFileName)
    {
        return new Component(new FileCommand
        {
            Command = new FileCommandType(new Rename
            {
                OldPath = fileName, NewPath = newFileName
            })
        });
    }

    public static Component GetDirectoryQuery(string path)
    {
        return new Component(new FileQuery
        {
            Type = FileQueryType.GetDirectory, Path = path
        });
    }
}