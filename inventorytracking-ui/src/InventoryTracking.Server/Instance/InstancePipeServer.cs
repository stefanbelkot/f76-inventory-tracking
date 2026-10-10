using System.IO.Pipes;

namespace InventoryTracking.Server.Instance;

/// <summary>
/// Listens on the per-user pipe. A second start sends <c>open</c>; the running instance runs its callback
/// (open the browser on its own address) and answers <c>ok</c>.
/// </summary>
public sealed class InstancePipeServer : IDisposable
{
    public const string OpenCommand = "open";
    public const string OkReply = "ok";

    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    private readonly string _pipeName;
    private readonly Action _onOpenRequested;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public InstancePipeServer(string pipeName, Action onOpenRequested)
    {
        _pipeName = pipeName;
        _onOpenRequested = onOpenRequested;
        _loop = Task.Run(ListenAsync);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait(TimeSpan.FromSeconds(2));
        _stop.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                await ServeAsync(pipe);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                await Task.Delay(100, CancellationToken.None);
            }
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(ConnectionTimeout);
        try
        {
            using var reader = new StreamReader(pipe, leaveOpen: true);
            var command = await reader.ReadLineAsync(timeout.Token);
            if (command == OpenCommand)
            {
                _onOpenRequested();
                await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(OkReply.AsMemory(), timeout.Token);
                pipe.WaitForPipeDrain();
            }
        }
        catch (OperationCanceledException) when (!_stop.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
    }
}
