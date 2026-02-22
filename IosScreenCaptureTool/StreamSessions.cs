using System.Diagnostics;

internal sealed class StreamSession(TunnelSession tunnelSession) : IAsyncDisposable
{
    public string Host { get; } = tunnelSession.Host;

    public int Port { get; } = tunnelSession.Port;

    public ValueTask DisposeAsync() => tunnelSession.DisposeAsync();
}

internal sealed class TunnelSession(Process process, string host, int port) : IAsyncDisposable
{
    public string Host { get; } = host;

    public int Port { get; } = port;

    public ValueTask DisposeAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // best effort
        }
        finally
        {
            process.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
