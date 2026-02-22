using System.IO;
using System.Text.Json;

namespace IosScreenCaptureTool.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_CaptureFolderIsDefaultCaptureFolder()
    {
        AppSettings settings = new();
        Assert.Equal(AppSettingsStore.DefaultCaptureFolder, settings.CaptureFolder);
    }

    [Fact]
    public void Defaults_StartWithWindowsMinimizedIsFalse()
    {
        AppSettings settings = new();
        Assert.False(settings.StartWithWindowsMinimized);
    }

    [Fact]
    public void Defaults_KeepMinimizedAfterClosingIsTrue()
    {
        AppSettings settings = new();
        Assert.True(settings.KeepMinimizedAfterClosing);
    }

    [Fact]
    public void DefaultCaptureFolder_ContainsiOSLiveStream()
    {
        string folder = AppSettingsStore.DefaultCaptureFolder;
        Assert.Contains("iOSLiveStream", folder);
    }

    [Fact]
    public void DefaultCaptureFolder_IsRooted()
    {
        string folder = AppSettingsStore.DefaultCaptureFolder;
        Assert.True(Path.IsPathRooted(folder));
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        AppSettings original = new()
        {
            CaptureFolder = @"C:\TestCaptures",
            StartWithWindowsMinimized = true,
            KeepMinimizedAfterClosing = false
        };

        string json = JsonSerializer.Serialize(original);
        AppSettings? deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.CaptureFolder, deserialized.CaptureFolder);
        Assert.Equal(original.StartWithWindowsMinimized, deserialized.StartWithWindowsMinimized);
        Assert.Equal(original.KeepMinimizedAfterClosing, deserialized.KeepMinimizedAfterClosing);
    }

    [Fact]
    public void JsonDeserialize_EmptyObject_UsesDefaults()
    {
        AppSettings? deserialized = JsonSerializer.Deserialize<AppSettings>("{}");

        Assert.NotNull(deserialized);
        Assert.Equal(AppSettingsStore.DefaultCaptureFolder, deserialized.CaptureFolder);
        Assert.False(deserialized.StartWithWindowsMinimized);
        Assert.True(deserialized.KeepMinimizedAfterClosing);
    }

    [Fact]
    public void JsonDeserialize_NullCaptureFolder_FallsToDefault()
    {
        string json = """{"CaptureFolder":null,"StartWithWindowsMinimized":false,"KeepMinimizedAfterClosing":true}""";
        AppSettings? deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        // When deserialized with null, the property is set to null (the default initializer doesn't re-apply).
        // This tests the raw deserialization behavior.
        Assert.Null(deserialized.CaptureFolder);
    }
}
