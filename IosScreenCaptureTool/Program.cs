internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        return ProgramEntry.RunAsync(args).GetAwaiter().GetResult();
    }
}

internal static class ProgramEntry
{
    private const string commandPipeName = "IosScreenCaptureTool.CommandPipe.v1";
    private const string appTitle = "iOS Screen Capture Tool";

    public static async Task<int> RunAsync(string[] args)
    {
        ParseResult parseResult = ParseArguments(args);
        if (parseResult.HasError)
        {
            Console.Error.WriteLine($"Error: {parseResult.ErrorMessage}");
            PrintUsage();
            return 1;
        }

        if (parseResult.Options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(parseResult.Options.CaptureFrameOutputPath))
        {
            return await RunningAppCommandClient.CaptureFrameAsync(commandPipeName, parseResult.Options.CaptureFrameOutputPath!);
        }

        bool isSelfTestRun = !string.IsNullOrWhiteSpace(parseResult.Options.SelfTestOutputPath);

        if (OperatingSystem.IsWindows() && !WindowsElevation.IsAdministrator())
        {
            ElevationRelaunchResult relaunchResult = ElevationRelauncher.TryRelaunchAsAdministrator(args, waitForExit: isSelfTestRun);
            if (relaunchResult.Started)
            {
                return isSelfTestRun ? relaunchResult.ExitCode : 0;
            }

            if (!relaunchResult.UserCanceled)
            {
                const string fallbackMessage = "Failed to elevate process.";
                string message = string.IsNullOrWhiteSpace(relaunchResult.ErrorMessage)
                    ? fallbackMessage
                    : $"{fallbackMessage}{Environment.NewLine}{relaunchResult.ErrorMessage}";

                if (Environment.UserInteractive)
                {
                    System.Windows.MessageBox.Show(
                        message,
                        appTitle,
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                else
                {
                    Console.Error.WriteLine(message);
                }
            }

            return 1;
        }

        if (isSelfTestRun)
        {
            return await StreamSelfTest.RunAsync(parseResult.Options.SelfTestOutputPath!);
        }

        System.Windows.Application application = new()
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
        };

        MainWindow mainWindow = new(appTitle, commandPipeName, parseResult.Options.StartMinimized);
        application.Run(mainWindow);
        return 0;
    }

    private static ParseResult ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return ParseResult.Success(new CaptureOptions());
        }

        if (args.Length == 1 && (args[0] == "-h" || args[0] == "--help"))
        {
            return ParseResult.Success(new CaptureOptions { ShowHelp = true });
        }

        if (args.Length == 2 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
        {
            return ParseResult.Success(new CaptureOptions { SelfTestOutputPath = args[1] });
        }

        if (args.Length == 2 && string.Equals(args[0], "--capture-frame", StringComparison.Ordinal))
        {
            return ParseResult.Success(new CaptureOptions { CaptureFrameOutputPath = args[1] });
        }

        if (args.Length == 1 && string.Equals(args[0], "--start-minimized", StringComparison.Ordinal))
        {
            return ParseResult.Success(new CaptureOptions { StartMinimized = true });
        }

        return ParseResult.Error("Use GUI mode with no args, '--start-minimized', '--self-test <output-file>', or '--capture-frame <output-file>'.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("iOS Screen Capture Tool");
        Console.WriteLine("Usage:");
        Console.WriteLine("  IosScreenCaptureTool");
        Console.WriteLine("  IosScreenCaptureTool --start-minimized");
        Console.WriteLine("  IosScreenCaptureTool --self-test .\\screenshots\\stream-test.png");
        Console.WriteLine("  IosScreenCaptureTool --capture-frame .\\screenshots\\frame.png");
    }
}

internal sealed class CaptureOptions
{
    public bool ShowHelp { get; set; }

    public string? SelfTestOutputPath { get; set; }

    public string? CaptureFrameOutputPath { get; set; }

    public bool StartMinimized { get; set; }
}

internal readonly record struct ParseResult(CaptureOptions Options, string? ErrorMessage)
{
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public static ParseResult Success(CaptureOptions options) => new(options, null);

    public static ParseResult Error(string errorMessage) => new(new CaptureOptions(), errorMessage);
}
