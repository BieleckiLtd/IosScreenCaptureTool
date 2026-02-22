namespace IosScreenCaptureTool.Tests;

public class StreamSessionTests
{
    [Fact]
    public void StreamSession_ExposesHostAndPort()
    {
        using System.Diagnostics.Process dummyProcess = new();
        TunnelSession tunnel = new(dummyProcess, "fd00::1", 12345);
        StreamSession session = new(tunnel);

        Assert.Equal("fd00::1", session.Host);
        Assert.Equal(12345, session.Port);
    }

    [Fact]
    public void TunnelSession_ExposesHostAndPort()
    {
        using System.Diagnostics.Process dummyProcess = new();
        TunnelSession tunnel = new(dummyProcess, "127.0.0.1", 8080);

        Assert.Equal("127.0.0.1", tunnel.Host);
        Assert.Equal(8080, tunnel.Port);
    }
}
