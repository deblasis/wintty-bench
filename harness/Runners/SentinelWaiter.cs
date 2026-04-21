namespace WinttyBench.Runners;

// Shared sentinel-file polling used by ThroughputRunner and StartupRunner.
public static class SentinelWaiter
{
    // Default for the `timeout` arg of WaitForSentinel — pass directly, e.g.
    // `SentinelWaiter.WaitForSentinel(path, SentinelWaiter.DefaultExitTimeout)`.
    public static readonly TimeSpan DefaultExitTimeout = TimeSpan.FromMinutes(2);

    // Default poll cadence; override per-call via the `pollInterval` arg when
    // a shorter loop is justified (tests) or a longer one reduces overhead.
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(25);

    public static void WaitForSentinel(
        string sentinelPath,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
    {
        var interval = pollInterval ?? DefaultPollInterval;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(sentinelPath)) return;
            Thread.Sleep(interval);
        }
        throw new TimeoutException(
            $"Sentinel '{sentinelPath}' not observed in {timeout}; shell did not finish.");
    }
}
