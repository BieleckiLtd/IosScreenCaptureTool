namespace IosScreenCaptureTool.Tests;

public class CommandResultTests
{
    [Fact]
    public void CommandNotFound_SetsExitCode127()
    {
        CommandResult result = CommandResult.CommandNotFound("mycommand");
        Assert.Equal(127, result.ExitCode);
    }

    [Fact]
    public void CommandNotFound_SetsIsCommandNotFoundTrue()
    {
        CommandResult result = CommandResult.CommandNotFound("mycommand");
        Assert.True(result.IsCommandNotFound);
    }

    [Fact]
    public void CommandNotFound_IncludesCommandNameInStdErr()
    {
        CommandResult result = CommandResult.CommandNotFound("python");
        Assert.Contains("python", result.StdErr);
    }

    [Fact]
    public void CommandNotFound_StdOutIsEmpty()
    {
        CommandResult result = CommandResult.CommandNotFound("test");
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        CommandResult result = new(0, "output", "error", false);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("output", result.StdOut);
        Assert.Equal("error", result.StdErr);
        Assert.False(result.IsCommandNotFound);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        CommandResult a = new(0, "out", "err", false);
        CommandResult b = new(0, "out", "err", false);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentExitCode_AreNotEqual()
    {
        CommandResult a = new(0, "out", "err", false);
        CommandResult b = new(1, "out", "err", false);
        Assert.NotEqual(a, b);
    }
}
