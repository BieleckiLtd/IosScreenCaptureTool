namespace IosScreenCaptureTool.Tests;

public class CaptureOptionsTests
{
    [Fact]
    public void Defaults_ShowHelpIsFalse()
    {
        CaptureOptions options = new();
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Defaults_StartMinimizedIsFalse()
    {
        CaptureOptions options = new();
        Assert.False(options.StartMinimized);
    }

    [Fact]
    public void Defaults_SelfTestOutputPathIsNull()
    {
        CaptureOptions options = new();
        Assert.Null(options.SelfTestOutputPath);
    }

    [Fact]
    public void Defaults_CaptureFrameOutputPathIsNull()
    {
        CaptureOptions options = new();
        Assert.Null(options.CaptureFrameOutputPath);
    }

    [Fact]
    public void SetProperties_RoundTrips()
    {
        CaptureOptions options = new()
        {
            ShowHelp = true,
            StartMinimized = true,
            SelfTestOutputPath = "test.png",
            CaptureFrameOutputPath = "frame.png"
        };

        Assert.True(options.ShowHelp);
        Assert.True(options.StartMinimized);
        Assert.Equal("test.png", options.SelfTestOutputPath);
        Assert.Equal("frame.png", options.CaptureFrameOutputPath);
    }
}
