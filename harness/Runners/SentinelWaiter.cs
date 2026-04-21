namespace WinttyBench.Runners;

// Shared sentinel-file polling used by ThroughputRunner and StartupRunner.
// Extracted from MeasurementRunner so both runners use one loop.
public static class SentinelWaiter
{
    public static readonly TimeSpan DefaultExitTimeout = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(25);

    public static void WaitForSentinel(string sentinelPath, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(sentinelPath)) return;
            Thread.Sleep(DefaultPollInterval);
        }
        throw new TimeoutException(
            $"Sentinel '{sentinelPath}' not observed in {timeout}; shell did not finish.");
    }
}
