using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

internal static class CommandLocator
{
    public static async Task<string?> TryResolveCommandPathAsync(string commandName, CancellationToken cancellationToken)
    {
        string locator = OperatingSystem.IsWindows() ? "where.exe" : "which";
        CommandResult lookupResult = await CommandRunner.RunAsync(locator, [commandName], cancellationToken);
        if (lookupResult.ExitCode != 0)
        {
            return null;
        }

        return lookupResult.StdOut
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }
}

internal static class CommandRunner
{
    public static async Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = new() { StartInfo = startInfo };
            process.Start();

            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            return new CommandResult(process.ExitCode, await stdOutTask, await stdErrTask, false);
        }
        catch (Win32Exception)
        {
            return CommandResult.CommandNotFound(fileName);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}

internal static partial class AnsiCleaner
{
    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiRegex();

    public static string Strip(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : AnsiRegex().Replace(value, string.Empty);
    }
}
