using System.IO.Pipes;

namespace InventoryTracking.Server.Instance;

/// <summary>Asks the running instance to open the browser.</summary>
public static class InstancePipeClient
{
    /// <returns>True when the running instance answered; false when it did not within <paramref name="timeout"/>.</returns>
    public static async Task<bool> RequestOpenAsync(string pipeName, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(cancellation.Token);
            await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(InstancePipeServer.OpenCommand.AsMemory(), cancellation.Token);
            using var reader = new StreamReader(pipe, leaveOpen: true);
            return await reader.ReadLineAsync(cancellation.Token) == InstancePipeServer.OkReply;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or TimeoutException)
        {
            return false;
        }
    }
}
