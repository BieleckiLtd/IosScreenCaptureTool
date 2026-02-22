using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed class PymobiledeviceClient(string pythonPath)
{
    public async Task<DeviceListResult> GetUsbDeviceUdidsAsync(CancellationToken cancellationToken)
    {
        CommandResult result = await RunPymobiledeviceAsync(["usbmux", "list", "-u"], cancellationToken);
        if (result.ExitCode != 0)
        {
            return new DeviceListResult(false, [], "Failed to list devices via pymobiledevice3 usbmux list.", BuildCommandDetails(result));
        }

        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(AnsiCleaner.Strip(result.StdOut));
            if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new DeviceListResult(false, [], "Unexpected usbmux list format.", AnsiCleaner.Strip(result.StdOut));
            }

            List<string> udids = [];
            foreach (JsonElement element in jsonDocument.RootElement.EnumerateArray())
            {
                string? udid = TryReadString(element, "UniqueDeviceID") ?? TryReadString(element, "Identifier");
                if (!string.IsNullOrWhiteSpace(udid))
                {
                    udids.Add(udid);
                }
            }

            return new DeviceListResult(true, udids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), null, null);
        }
        catch (Exception exception)
        {
            return new DeviceListResult(false, [], $"Failed to parse device list: {exception.Message}", AnsiCleaner.Strip(result.StdOut));
        }
    }

    public async Task<StreamSetupResult> OpenStreamSessionAsync(string udid, CancellationToken cancellationToken)
    {
        TunnelStartResult tunnelStartResult = await StartTunnelAsync(udid, cancellationToken);
        if (tunnelStartResult.Success && tunnelStartResult.Session is not null)
        {
            return new StreamSetupResult(true, new StreamSession(tunnelStartResult.Session), null, null);
        }

        return new StreamSetupResult(
            false,
            null,
            "Failed to open developer tunnel for live stream. Run as Administrator.",
            tunnelStartResult.Details);
    }

    public async Task<DeveloperModeResult> EnsureDeveloperModeAsync(string udid, CancellationToken cancellationToken)
    {
        CommandResult result = await RunPymobiledeviceAsync(
            ["amfi", "enable-developer-mode", "--udid", udid],
            cancellationToken);

        string output = $"{AnsiCleaner.Strip(result.StdOut)}\n{AnsiCleaner.Strip(result.StdErr)}";
        string normalized = output.ToLowerInvariant();

        if (result.ExitCode == 0)
        {
            bool rebootRequired = normalized.Contains("reboot") || normalized.Contains("restart");
            return new DeveloperModeResult(!rebootRequired, rebootRequired, null, null);
        }

        if (normalized.Contains("already") && normalized.Contains("enabled"))
        {
            return new DeveloperModeResult(true, false, null, null);
        }

        return new DeveloperModeResult(false, false,
            "Could not verify Developer Mode status.",
            BuildCommandDetails(result));
    }

    public async Task<FrameCaptureResult> CaptureFrameAsync(StreamSession session, string outputPath, CancellationToken cancellationToken)
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        CommandResult result = await RunPymobiledeviceAsync(
            [
                "developer",
                "dvt",
                "screenshot",
                "--rsd",
                session.Host,
                session.Port.ToString(CultureInfo.InvariantCulture),
                outputPath
            ],
            cancellationToken);

        if (File.Exists(outputPath))
        {
            return new FrameCaptureResult(true, null, null);
        }

        return new FrameCaptureResult(false, "Failed to capture frame.", BuildCommandDetails(result));
    }

    private async Task<CommandResult> RunPymobiledeviceAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        List<string> fullArguments = ["-m", "pymobiledevice3"];
        fullArguments.AddRange(arguments);
        return await CommandRunner.RunAsync(pythonPath, fullArguments, cancellationToken);
    }

    private async Task<TunnelStartResult> StartTunnelAsync(string udid, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = pythonPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pymobiledevice3");
        startInfo.ArgumentList.Add("lockdown");
        startInfo.ArgumentList.Add("start-tunnel");
        startInfo.ArgumentList.Add("--script-mode");
        startInfo.ArgumentList.Add("--udid");
        startInfo.ArgumentList.Add(udid);

        Process process = new() { StartInfo = startInfo };
        TaskCompletionSource<(string host, int port)> endpointSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> outputLines = [];
        List<string> errorLines = [];

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                return;
            }

            lock (outputLines)
            {
                outputLines.Add(eventArgs.Data);
            }

            if (TryParseHostPort(eventArgs.Data, out string host, out int port))
            {
                endpointSource.TrySetResult((host, port));
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                return;
            }

            lock (errorLines)
            {
                errorLines.Add(eventArgs.Data);
            }

            if (TryParseHostPort(eventArgs.Data, out string host, out int port))
            {
                endpointSource.TrySetResult((host, port));
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Task completed = await Task.WhenAny(endpointSource.Task, process.WaitForExitAsync(CancellationToken.None), Task.Delay(TimeSpan.FromSeconds(45), cancellationToken));
            if (completed == endpointSource.Task)
            {
                (string host, int port) = await endpointSource.Task;
                return new TunnelStartResult(true, new TunnelSession(process, host, port), null, null);
            }

            string details = BuildProcessOutput(outputLines, errorLines);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
            return new TunnelStartResult(false, null, "Tunnel startup failed.", details);
        }
        catch (Exception exception)
        {
            process.Dispose();
            return new TunnelStartResult(false, null, "Tunnel startup failed.", exception.Message);
        }
    }

    private static bool TryParseHostPort(string line, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        string[] tokens = AnsiCleaner.Strip(line)
            .Trim()
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 2)
        {
            return false;
        }

        if (Uri.CheckHostName(tokens[0]) == UriHostNameType.Unknown && !tokens[0].Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
        {
            return false;
        }

        host = tokens[0];
        return true;
    }

    private static string BuildCommandDetails(CommandResult result)
    {
        string stdout = AnsiCleaner.Strip(result.StdOut).Trim();
        string stderr = AnsiCleaner.Strip(result.StdErr).Trim();
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return stderr;
        }

        return stdout;
    }

    private static string BuildProcessOutput(IEnumerable<string> stdoutLines, IEnumerable<string> stderrLines)
    {
        string stdout = AnsiCleaner.Strip(string.Join(Environment.NewLine, stdoutLines)).Trim();
        string stderr = AnsiCleaner.Strip(string.Join(Environment.NewLine, stderrLines)).Trim();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return stderr;
        }

        if (string.IsNullOrWhiteSpace(stderr))
        {
            return stdout;
        }

        return $"{stdout}{Environment.NewLine}{stderr}";
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }
}
