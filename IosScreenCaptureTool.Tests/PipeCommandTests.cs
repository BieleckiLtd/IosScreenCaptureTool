using System.Text.Json;

namespace IosScreenCaptureTool.Tests;

public class PipeCommandTests
{
    [Fact]
    public void PipeCommandRequest_JsonRoundTrip()
    {
        PipeCommandRequest request = new("capture-frame", @"C:\output\frame.png");
        string json = JsonSerializer.Serialize(request);
        PipeCommandRequest? deserialized = JsonSerializer.Deserialize<PipeCommandRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("capture-frame", deserialized.Command);
        Assert.Equal(@"C:\output\frame.png", deserialized.OutputPath);
    }

    [Fact]
    public void PipeCommandRequest_NullValues_RoundTrip()
    {
        PipeCommandRequest request = new(null, null);
        string json = JsonSerializer.Serialize(request);
        PipeCommandRequest? deserialized = JsonSerializer.Deserialize<PipeCommandRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Command);
        Assert.Null(deserialized.OutputPath);
    }

    [Fact]
    public void PipeCommandResponse_SuccessRoundTrip()
    {
        PipeCommandResponse response = new(true, @"C:\output\frame.png");
        string json = JsonSerializer.Serialize(response);
        PipeCommandResponse? deserialized = JsonSerializer.Deserialize<PipeCommandResponse>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.Success);
        Assert.Equal(@"C:\output\frame.png", deserialized.Message);
    }

    [Fact]
    public void PipeCommandResponse_FailureRoundTrip()
    {
        PipeCommandResponse response = new(false, "Stream not active");
        string json = JsonSerializer.Serialize(response);
        PipeCommandResponse? deserialized = JsonSerializer.Deserialize<PipeCommandResponse>(json);

        Assert.NotNull(deserialized);
        Assert.False(deserialized.Success);
        Assert.Equal("Stream not active", deserialized.Message);
    }

    [Fact]
    public void PipeCommandResponse_NullMessage()
    {
        PipeCommandResponse response = new(true, null);
        string json = JsonSerializer.Serialize(response);
        PipeCommandResponse? deserialized = JsonSerializer.Deserialize<PipeCommandResponse>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.Success);
        Assert.Null(deserialized.Message);
    }

    [Fact]
    public void PipeCommandRequest_RecordEquality()
    {
        PipeCommandRequest a = new("capture-frame", "out.png");
        PipeCommandRequest b = new("capture-frame", "out.png");
        Assert.Equal(a, b);
    }

    [Fact]
    public void PipeCommandResponse_RecordEquality()
    {
        PipeCommandResponse a = new(true, "done");
        PipeCommandResponse b = new(true, "done");
        Assert.Equal(a, b);
    }
}
