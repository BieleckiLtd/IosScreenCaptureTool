using System.IO.Pipes;
using System.Text.Json;

internal sealed record PipeCommandRequest(string? Command, string? OutputPath);

internal sealed record PipeCommandResponse(bool Success, string? Message);

internal static class RunningAppCommandClient
{
    public static async Task<int> CaptureFrameAsync(string pipeName, string outputPath)
    {
        string absoluteOutputPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(absoluteOutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(1500);

            using StreamReader reader = new(client, leaveOpen: true);
            using StreamWriter writer = new(client, leaveOpen: true) { AutoFlush = true };

            PipeCommandRequest request = new("capture-frame", absoluteOutputPath);
            await writer.WriteLineAsync(JsonSerializer.Serialize(request));

            string? responseLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                Console.Error.WriteLine("No response from running app.");
                return 1;
            }

            PipeCommandResponse? response = JsonSerializer.Deserialize<PipeCommandResponse>(responseLine);
            if (response is null)
            {
                Console.Error.WriteLine("Invalid response from running app.");
                return 1;
            }

            if (!response.Success)
            {
                Console.Error.WriteLine(response.Message ?? "Capture command failed.");
                return 1;
            }

            Console.WriteLine($"Frame captured to: {response.Message ?? absoluteOutputPath}");
            return 0;
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine("Running app was not found. Start iOS Screen Capture Tool first.");
            return 1;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Access denied while connecting to running app command pipe.");
            return 1;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Failed to communicate with running app: {exception.Message}");
            return 1;
        }
    }
}
