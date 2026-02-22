namespace IosScreenCaptureTool.Tests;

public class RuntimeContractsTests
{
    [Fact]
    public void BootstrapResult_SuccessCase()
    {
        BootstrapResult result = new(true, "/usr/bin/python3", null);

        Assert.True(result.Success);
        Assert.Equal("/usr/bin/python3", result.PythonPath);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void BootstrapResult_FailureCase()
    {
        BootstrapResult result = new(false, null, "Python not found");

        Assert.False(result.Success);
        Assert.Null(result.PythonPath);
        Assert.Equal("Python not found", result.ErrorMessage);
    }

    [Fact]
    public void DeviceListResult_SuccessWithDevices()
    {
        string[] udids = ["UDID-001", "UDID-002"];
        DeviceListResult result = new(true, udids, null, null);

        Assert.True(result.Success);
        Assert.Equal(2, result.Udids.Count);
        Assert.Equal("UDID-001", result.Udids[0]);
        Assert.Equal("UDID-002", result.Udids[1]);
    }

    [Fact]
    public void DeviceListResult_FailureWithErrorAndDetails()
    {
        DeviceListResult result = new(false, [], "Device enumeration failed", "Process exited with code 1");

        Assert.False(result.Success);
        Assert.Empty(result.Udids);
        Assert.Equal("Device enumeration failed", result.ErrorMessage);
        Assert.Equal("Process exited with code 1", result.Details);
    }

    [Fact]
    public void StreamSetupResult_SuccessWithNullSession()
    {
        StreamSetupResult result = new(false, null, "Tunnel failed", "Details here");

        Assert.False(result.Success);
        Assert.Null(result.Session);
        Assert.Equal("Tunnel failed", result.ErrorMessage);
    }

    [Fact]
    public void FrameCaptureResult_Success()
    {
        FrameCaptureResult result = new(true, null, null);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Details);
    }

    [Fact]
    public void FrameCaptureResult_Failure()
    {
        FrameCaptureResult result = new(false, "Capture failed", "timeout");

        Assert.False(result.Success);
        Assert.Equal("Capture failed", result.ErrorMessage);
        Assert.Equal("timeout", result.Details);
    }

    [Fact]
    public void TunnelStartResult_Failure()
    {
        TunnelStartResult result = new(false, null, "Tunnel not available", "stderr output");

        Assert.False(result.Success);
        Assert.Null(result.Session);
        Assert.Equal("Tunnel not available", result.ErrorMessage);
        Assert.Equal("stderr output", result.Details);
    }

    [Fact]
    public void ElevationRelaunchResult_UserCanceled()
    {
        ElevationRelaunchResult result = new(false, true, 1, null);

        Assert.False(result.Started);
        Assert.True(result.UserCanceled);
        Assert.Equal(1, result.ExitCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ElevationRelaunchResult_Success()
    {
        ElevationRelaunchResult result = new(true, false, 0, null);

        Assert.True(result.Started);
        Assert.False(result.UserCanceled);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ElevationRelaunchResult_FailedWithMessage()
    {
        ElevationRelaunchResult result = new(false, false, 1, "Access denied");

        Assert.False(result.Started);
        Assert.False(result.UserCanceled);
        Assert.Equal("Access denied", result.ErrorMessage);
    }

    [Fact]
    public void DeveloperModeResult_Ready()
    {
        DeveloperModeResult result = new(true, false, null, null);

        Assert.True(result.IsReady);
        Assert.False(result.RebootRequired);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void DeveloperModeResult_RebootRequired()
    {
        DeveloperModeResult result = new(false, true, null, null);

        Assert.False(result.IsReady);
        Assert.True(result.RebootRequired);
    }

    [Fact]
    public void DeveloperModeResult_CheckFailed()
    {
        DeveloperModeResult result = new(false, false, "Could not verify Developer Mode status.", "amfi not available");

        Assert.False(result.IsReady);
        Assert.False(result.RebootRequired);
        Assert.Equal("Could not verify Developer Mode status.", result.ErrorMessage);
        Assert.Equal("amfi not available", result.Details);
    }
}
