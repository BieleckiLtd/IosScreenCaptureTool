namespace IosScreenCaptureTool.Tests;

public class AnsiCleanerTests
{
    [Fact]
    public void Strip_RemovesSimpleColorCode()
    {
        string input = "\x1B[31mHello\x1B[0m";
        string result = AnsiCleaner.Strip(input);
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Strip_RemovesMultipleAnsiSequences()
    {
        string input = "\x1B[1m\x1B[32mBold Green\x1B[0m Normal";
        string result = AnsiCleaner.Strip(input);
        Assert.Equal("Bold Green Normal", result);
    }

    [Fact]
    public void Strip_ReturnsEmptyForNull()
    {
        string result = AnsiCleaner.Strip(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Strip_ReturnsEmptyForEmptyString()
    {
        string result = AnsiCleaner.Strip(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Strip_ReturnsOriginalWhenNoAnsiCodes()
    {
        string input = "plain text without codes";
        string result = AnsiCleaner.Strip(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Strip_HandlesResetCode()
    {
        string input = "\x1B[0m";
        string result = AnsiCleaner.Strip(input);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Strip_HandlesMultiParameterSequences()
    {
        string input = "\x1B[38;5;196mRed text\x1B[0m";
        string result = AnsiCleaner.Strip(input);
        Assert.Equal("Red text", result);
    }

    [Fact]
    public void Strip_PreservesSurroundingText()
    {
        string input = "before \x1B[33myellow\x1B[0m after";
        string result = AnsiCleaner.Strip(input);
        Assert.Equal("before yellow after", result);
    }
}
