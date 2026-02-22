internal static class PymobiledeviceBootstrapper
{
    public static async Task<BootstrapResult> EnsureAsync(CancellationToken cancellationToken)
    {
        string? pythonPath = await FindPythonPathAsync(cancellationToken);
        if (pythonPath is null && OperatingSystem.IsWindows())
        {
            CommandResult installResult = await InstallPythonAsync(cancellationToken);
            if (installResult.ExitCode != 0)
            {
                return new BootstrapResult(false, null, BuildFailure("Failed to install Python via winget.", installResult));
            }

            pythonPath = await FindPythonPathAsync(cancellationToken);
        }

        if (pythonPath is null)
        {
            return new BootstrapResult(false, null, "Python was not found and could not be installed automatically.");
        }

        CommandResult packageCheck = await CommandRunner.RunAsync(pythonPath, ["-m", "pip", "show", "pymobiledevice3"], cancellationToken);
        if (packageCheck.ExitCode != 0)
        {
            CommandResult packageInstall = await CommandRunner.RunAsync(pythonPath, ["-m", "pip", "install", "pymobiledevice3"], cancellationToken);
            if (packageInstall.ExitCode != 0)
            {
                return new BootstrapResult(false, null, BuildFailure("Failed to install pymobiledevice3.", packageInstall));
            }
        }

        CommandResult versionResult = await CommandRunner.RunAsync(pythonPath, ["-m", "pymobiledevice3", "version"], cancellationToken);
        if (versionResult.ExitCode != 0)
        {
            return new BootstrapResult(false, null, BuildFailure("pymobiledevice3 is not runnable.", versionResult));
        }

        return new BootstrapResult(true, pythonPath, null);
    }

    private static async Task<string?> FindPythonPathAsync(CancellationToken cancellationToken)
    {
        string localPython = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python312", "python.exe");
        if (File.Exists(localPython))
        {
            return localPython;
        }

        string[] candidates = ["python.exe", "python3.exe", "python", "python3"];
        foreach (string candidate in candidates)
        {
            string? resolved = await CommandLocator.TryResolveCommandPathAsync(candidate, cancellationToken);
            if (resolved is null)
            {
                continue;
            }

            CommandResult result = await CommandRunner.RunAsync(resolved, ["--version"], cancellationToken);
            string combined = $"{result.StdOut}\n{result.StdErr}";
            if (combined.Contains("Python", StringComparison.OrdinalIgnoreCase))
            {
                return resolved;
            }
        }

        return null;
    }

    private static async Task<CommandResult> InstallPythonAsync(CancellationToken cancellationToken)
    {
        string? wingetPath = await CommandLocator.TryResolveCommandPathAsync("winget.exe", cancellationToken);
        if (wingetPath is null)
        {
            return CommandResult.CommandNotFound("winget");
        }

        return await CommandRunner.RunAsync(
            wingetPath,
            [
                "install",
                "--id",
                "Python.Python.3.12",
                "--accept-package-agreements",
                "--accept-source-agreements",
                "--scope",
                "user",
                "--silent"
            ],
            cancellationToken);
    }

    private static string BuildFailure(string prefix, CommandResult result)
    {
        string details = BuildCommandDetails(result);
        return string.IsNullOrWhiteSpace(details) ? prefix : $"{prefix}{Environment.NewLine}{details}";
    }

    private static string BuildCommandDetails(CommandResult result)
    {
        string stdout = AnsiCleaner.Strip(result.StdOut).Trim();
        string stderr = AnsiCleaner.Strip(result.StdErr).Trim();
        return !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
    }
}
