using Microsoft.Win32;
using System.Runtime.InteropServices;

internal static class WindowsThemeReader
{
    private const string personalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string appsUseLightThemeValueName = "AppsUseLightTheme";

    public static bool IsAppLightTheme()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(personalizeRegistryPath, writable: false);
            if (key?.GetValue(appsUseLightThemeValueName) is int value)
            {
                return value != 0;
            }
        }
        catch
        {
            // Fall back to a light palette if the key can't be read.
        }

        return true;
    }
}

internal static class WindowsWindowBackdrop
{
    private const int dwmWindowAttributeUseImmersiveDarkMode = 20;
    private const int dwmWindowAttributeUseImmersiveDarkModeLegacy = 19;
    private const int dwmWindowAttributeWindowCornerPreference = 33;
    private const int dwmWindowAttributeSystemBackdropType = 38;
    private const int dwmWindowCornerPreferenceRound = 2;
    private const int dwmSystemBackdropMainWindow = 2; // Mica
    private const int dwmSystemBackdropTransientWindow = 3; // Acrylic

    public static void Apply(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        int cornerPreference = dwmWindowCornerPreferenceRound;
        DwmSetWindowAttribute(hwnd, dwmWindowAttributeWindowCornerPreference, ref cornerPreference, sizeof(int));

        int backdropType = dwmSystemBackdropMainWindow;
        int result = DwmSetWindowAttribute(hwnd, dwmWindowAttributeSystemBackdropType, ref backdropType, sizeof(int));
        if (result != 0)
        {
            backdropType = dwmSystemBackdropTransientWindow;
            DwmSetWindowAttribute(hwnd, dwmWindowAttributeSystemBackdropType, ref backdropType, sizeof(int));
        }
    }

    public static void SetDarkTitleBar(IntPtr hwnd, bool useDarkTitleBar)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        int enabled = useDarkTitleBar ? 1 : 0;
        int result = DwmSetWindowAttribute(hwnd, dwmWindowAttributeUseImmersiveDarkMode, ref enabled, sizeof(int));
        if (result != 0)
        {
            DwmSetWindowAttribute(hwnd, dwmWindowAttributeUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
        }

        try
        {
            SetWindowTheme(hwnd, useDarkTitleBar ? "DarkMode_Explorer" : "Explorer", null);
        }
        catch
        {
            // Ignore if uxtheme is not available
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string? pszSubIdList);
}
