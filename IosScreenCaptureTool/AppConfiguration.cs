using Microsoft.Win32;
using System.Text.Json;

internal sealed class AppSettings
{
    public string CaptureFolder { get; set; } = AppSettingsStore.DefaultCaptureFolder;

    public bool StartWithWindowsMinimized { get; set; }

    public bool KeepMinimizedAfterClosing { get; set; } = true;
}

internal static class AppSettingsStore
{
    public static string DefaultCaptureFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "iOSLiveStream");

    private static readonly string settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IosScreenCaptureTool");

    private static readonly string settingsPath = Path.Combine(settingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return new AppSettings();
            }

            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath));
            if (loaded is null)
            {
                return new AppSettings();
            }

            if (string.IsNullOrWhiteSpace(loaded.CaptureFolder))
            {
                loaded.CaptureFolder = DefaultCaptureFolder;
            }

            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(settingsDirectory);
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
    }
}

internal static class StartupRegistration
{
    private const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string valueName = "IosScreenCaptureTool";
    private const string startupArg = "--start-minimized";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: false);
        return runKey?.GetValue(valueName) is string currentValue
            && !string.IsNullOrWhiteSpace(currentValue);
    }

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using RegistryKey runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open startup registry key.");

        if (!enabled)
        {
            runKey.DeleteValue(valueName, throwOnMissingValue: false);
            return;
        }

        runKey.SetValue(valueName, BuildStartupCommand(), RegistryValueKind.String);
    }

    private static string BuildStartupCommand()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Unable to resolve process path for startup registration.");
        }

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

        args.Add(startupArg);
        return $"{QuoteArgument(processPath)} {string.Join(" ", args.Select(QuoteArgument))}";
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
