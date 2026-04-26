namespace WinttyBench.Capture;

// One captured frame, fully readback into a managed BGRA buffer. The
// QpcSystemRelativeTime is the WGC frame timestamp converted to QPC
// ticks (same clock as Stopwatch.GetTimestamp), which is what the
// LatencyRunner subtracts qpc0 from.
public sealed record CapturedFrame(
    byte[] BgraPixels,
    int Width,
    int Height,
    long QpcSystemRelativeTime);
