using DokanNet;
using Microsoft.Extensions.Logging;
using FileAccess = DokanNet.FileAccess;

namespace Infrastructure.Dokany;

public partial class KuromeOperations
{
    protected NtStatus Trace(string driveLetter, string method, string? fileName, IDokanFileInfo info, NtStatus result,
        params object[]? parameters)

    {
        var extraParameters = parameters != null && parameters.Length > 0
            ? " " + string.Join(", ", parameters.Select(x => x.ToString()))
            : string.Empty;
        _logger.LogDebug("{0} {1}[\"{2}\" | {3}{4}] -> {5}", driveLetter, method, fileName, info.ToString(),
            extraParameters, result.ToString());
        return result;
    }

    private NtStatus Trace(string driveLetter, string method, string fileName, IDokanFileInfo info,
        FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes,
        NtStatus result)
    {
        _logger.LogDebug(
            "{driveLetter} {method}[\"{fileName}\", {info}, [{access}], [{share}], [{mode}], [{options}], [{attributes}]] -> {result}",
            driveLetter, method, fileName, info, access, share, mode, options, attributes, result);
        return result;
    }
}