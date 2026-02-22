namespace IosScreenCaptureTool.Tests;

public class ParseResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        ParseResult result = ParseResult.Success(new CaptureOptions());
        Assert.False(result.HasError);
    }

    [Fact]
    public void Success_ErrorMessageIsNull()
    {
        ParseResult result = ParseResult.Success(new CaptureOptions());
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Error_HasError()
    {
        ParseResult result = ParseResult.Error("Something went wrong");
        Assert.True(result.HasError);
    }

    [Fact]
    public void Error_PreservesErrorMessage()
    {
        ParseResult result = ParseResult.Error("bad argument");
        Assert.Equal("bad argument", result.ErrorMessage);
    }

    [Fact]
    public void Error_OptionsIsNotNull()
    {
        ParseResult result = ParseResult.Error("error");
        Assert.NotNull(result.Options);
    }
}
