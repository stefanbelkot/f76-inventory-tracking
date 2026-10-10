namespace InventoryTracking.Server.Instance;

/// <summary>
/// Holds the per-user named mutex that marks the running instance. It must be disposed on the thread that acquired it;
/// after that the next start begins a fresh instance.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex) => _mutex = mutex;

    /// <returns>The guard, or null when another instance of this user already runs.</returns>
    public static SingleInstanceGuard? TryAcquire(InstanceNames names)
    {
        var mutex = new Mutex(initiallyOwned: true, names.Mutex, out var createdNew);
        if (createdNew)
        {
            return new SingleInstanceGuard(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
