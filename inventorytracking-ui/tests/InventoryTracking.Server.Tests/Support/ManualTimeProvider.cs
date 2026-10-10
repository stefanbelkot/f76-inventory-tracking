namespace InventoryTracking.Server.Tests.Support;

/// <summary>A clock the test moves by hand, to check the one-hour and one-week rules without waiting.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
