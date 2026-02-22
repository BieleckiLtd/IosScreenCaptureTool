using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

internal static class WindowsElevation
{
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

internal static class ElevationRelauncher
{
    public static ElevationRelaunchResult TryRelaunchAsAdministrator(string[] originalArgs, bool waitForExit)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ElevationRelaunchResult(false, false, 1, "Elevation relaunch is only supported on Windows.");
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return new ElevationRelaunchResult(false, false, 1, "Unable to locate current process path.");
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = processPath,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = BuildRelaunchArguments(processPath, originalArgs),
                WorkingDirectory = Environment.CurrentDirectory
            };

            Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return new ElevationRelaunchResult(false, false, 1, "Failed to start elevated process.");
            }

            using (process)
            {
                if (waitForExit && HasAssociatedProcess(process))
                {
                    process.WaitForExit();
                    return new ElevationRelaunchResult(true, false, process.ExitCode, null);
                }
            }

            return new ElevationRelaunchResult(true, false, 0, null);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new ElevationRelaunchResult(false, true, 1, null);
        }
        catch (Exception exception)
        {
            return new ElevationRelaunchResult(false, false, 1, exception.Message);
        }
    }

    private static string BuildRelaunchArguments(string processPath, string[] originalArgs)
    {
        string[] commandLineArgs = Environment.GetCommandLineArgs();
        bool isDotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);

        List<string> args = [];
        if (isDotnetHost && commandLineArgs.Length >= 2)
        {
            args.Add(commandLineArgs[1]);
        }

        args.AddRange(originalArgs);
        return string.Join(" ", args.Select(QuoteArgument));
    }

    private static bool HasAssociatedProcess(Process process)
    {
        try
        {
            _ = process.Id;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        string escaped = value.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
