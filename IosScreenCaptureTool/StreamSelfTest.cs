internal static class StreamSelfTest
{
    public static async Task<int> RunAsync(string outputPath)
    {
        if (OperatingSystem.IsWindows() && !WindowsElevation.IsAdministrator())
        {
            Console.Error.WriteLine("Self-test requires Administrator rights on Windows.");
            return 1;
        }

        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMinutes(2));

        BootstrapResult bootstrapResult = await PymobiledeviceBootstrapper.EnsureAsync(cancellationTokenSource.Token);
        if (!bootstrapResult.Success || string.IsNullOrWhiteSpace(bootstrapResult.PythonPath))
        {
            Console.Error.WriteLine(bootstrapResult.ErrorMessage ?? "Failed to prepare runtime dependencies.");
            return 1;
        }

        PymobiledeviceClient client = new(bootstrapResult.PythonPath);
        DeviceListResult deviceListResult = await client.GetUsbDeviceUdidsAsync(cancellationTokenSource.Token);
        if (!deviceListResult.Success)
        {
            Console.Error.WriteLine(deviceListResult.ErrorMessage ?? "Failed to list connected iOS devices.");
            if (!string.IsNullOrWhiteSpace(deviceListResult.Details))
            {
                Console.Error.WriteLine(deviceListResult.Details);
            }

            return 1;
        }

        if (deviceListResult.Udids.Count != 1)
        {
            Console.Error.WriteLine(deviceListResult.Udids.Count == 0
                ? "No connected iOS devices found."
                : "Multiple iOS devices are connected. Connect one device and retry.");
            return 1;
        }

        string absoluteOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath) ?? Directory.GetCurrentDirectory());

        StreamSetupResult setup = await client.OpenStreamSessionAsync(deviceListResult.Udids[0], cancellationTokenSource.Token);
        if (!setup.Success || setup.Session is null)
        {
            Console.Error.WriteLine(setup.ErrorMessage ?? "Failed to initialize stream session.");
            if (!string.IsNullOrWhiteSpace(setup.Details))
            {
                Console.Error.WriteLine(setup.Details);
            }

            return 1;
        }

        await using StreamSession session = setup.Session;
        FrameCaptureResult frameResult = await client.CaptureFrameAsync(session, absoluteOutputPath, cancellationTokenSource.Token);
        if (!frameResult.Success)
        {
            Console.Error.WriteLine(frameResult.ErrorMessage ?? "Frame capture failed.");
            if (!string.IsNullOrWhiteSpace(frameResult.Details))
            {
                Console.Error.WriteLine(frameResult.Details);
            }

            return 1;
        }

        Console.WriteLine($"Frame saved to: {absoluteOutputPath}");
        return 0;
    }
}
