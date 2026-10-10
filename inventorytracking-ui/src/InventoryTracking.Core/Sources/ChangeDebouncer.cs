namespace InventoryTracking.Core.Sources;

/// <summary>Collapses a burst of triggers into one callback after a quiet period.</summary>
internal sealed class ChangeDebouncer(TimeSpan delay, Action callback) : IDisposable
{
    private readonly Timer _timer = new(_ => callback(), null, Timeout.Infinite, Timeout.Infinite);

    public void Trigger() => _timer.Change(delay, Timeout.InfiniteTimeSpan);

    public void Dispose() => _timer.Dispose();
}
