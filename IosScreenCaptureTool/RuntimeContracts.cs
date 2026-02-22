internal sealed record BootstrapResult(bool Success, string? PythonPath, string? ErrorMessage);

internal sealed record DeviceListResult(bool Success, IReadOnlyList<string> Udids, string? ErrorMessage, string? Details);

internal sealed record StreamSetupResult(bool Success, StreamSession? Session, string? ErrorMessage, string? Details);

internal sealed record FrameCaptureResult(bool Success, string? ErrorMessage, string? Details);

internal sealed record TunnelStartResult(bool Success, TunnelSession? Session, string? ErrorMessage, string? Details);

internal sealed record DeveloperModeResult(bool IsReady, bool RebootRequired, string? ErrorMessage, string? Details);

internal readonly record struct CommandResult(int ExitCode, string StdOut, string StdErr, bool IsCommandNotFound)
{
    public static CommandResult CommandNotFound(string commandName) => new(127, string.Empty, $"Command '{commandName}' was not found.", true);
}

internal readonly record struct ElevationRelaunchResult(bool Started, bool UserCanceled, int ExitCode, string? ErrorMessage);

