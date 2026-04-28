using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using WinttyBench.Capture;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Input;
using WinttyBench.Kpis;
using WinttyBench.Launchers;

namespace WinttyBench.Runners;

// Per-iteration: bring wintty foreground, snapshot baseline frame, capture
// QPC, inject one keystroke via SendInput, wait for the next WGC frame whose
// per-iter ROI shows a paint, record (frame.QPC - qpc0) in milliseconds.
//
// Single wintty launch reused across all iters (relaunch-per-iter would
// throw away the warm D3D pipeline cache, font shaping cache, and ConPTY
// relay buffers - exactly the steady-state we want to measure).
[SupportedOSPlatform("windows")]
public sealed class LatencyRunner : IKpiRunner
{
    private const int WallClockBudgetMsPerIter = 1000;
    private const int CooldownMs = 50;
    private const int Cols = 120;
    private const int Rows = 32;

    public async Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string winttyExe,
        FairnessProfile profile,
        FixtureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentException.ThrowIfNullOrEmpty(winttyExe);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolver);

        var measuredIters = cell.MeasuredItersOverride ?? profile.MeasuredIters;
        var totalIters = profile.WarmupIters + measuredIters;

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "latency-echo.ps1");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException(
                $"latency-echo.ps1 missing at {scriptPath}", scriptPath);

        // pwsh -File "C:\..." gets mangled by ghostty's config parser
        // (Plan 2B hit the same wall in StartupRunner.BuildPwshStartupCommand
        // when passing scripts through the `command =` config field). The
        // parser eats `$VAR` references, strips/normalizes quotes, and may
        // misinterpret backslashes. -EncodedCommand is base64 (alnum + /+=)
        // which every parsing layer passes through verbatim; pwsh decodes
        // it natively as UTF-16LE. Read the .ps1 contents at runtime so the
        // file remains the single source of truth and stays test-driven.
        var scriptText = File.ReadAllText(scriptPath);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(scriptText));
        var launcher = new WinttyLauncher();
        var shellCmd = string.Create(CultureInfo.InvariantCulture,
            $"pwsh -NoLogo -NoProfile -EncodedCommand {encoded}");

        var launch = launcher.Launch(new LaunchRequest(
            TargetExePath: winttyExe,
            ShellCommand: shellCmd,
            ConfigOverrides: cell.WinttyConfigOverrides,
            Cols: Cols,
            Rows: Rows));

        try
        {
            var hwnd = HwndLocator.WaitForWinttyHwnd(launch.Process.ProcessId, TimeSpan.FromSeconds(5));

            using var session = WgcSession.Open(hwnd);

            // Full-frame diff with a count threshold instead of a per-cell
            // ROI. Wintty's actual cell grid sits inside the client area
            // with unknown padding/borders that the cols/rows division
            // can't predict; per-cell ROI math fired all-Hung in the
            // 2026-04-28 smoke even though wintty was clearly painting `*`.
            // A glyph paint is dozens of brightness-flipped pixels (a `*`
            // is 30+ at 8x16); cursor blink contributes ~80-130 pixels in
            // the same area, so a threshold of 50 catches glyph paints
            // while still firing on cursor blink (acceptable: blink lands
            // anywhere, glyph lands first frame after the keystroke and
            // wins on average).
            const int MinChangedPixels = 50;
            var fullFrameRoi = new Roi(0, 0, session.ClientWidthPx, session.ClientHeightPx);

            var samples = new List<IterationSample>(measuredIters);

            for (var i = 0; i < totalIters; i++)
            {
                var isWarmup = i < profile.WarmupIters;

                SendInputProbe.EnsureForeground(hwnd);
                CapturedFrame baseline = await session.NextFrameAsync(CancellationToken.None);

                var qpc0 = SendInputProbe.Inject(SendInputInterop.VK_SPACE);

                // Discard any frame whose FrameArrived callback fired between
                // the baseline read and our Inject. Without this, the bounded
                // drop-oldest channel can return a stale frame and the diff
                // against baseline fires on cursor blink alone, recording
                // sub-frame or even negative latency. (PR # 9 review item # 2.)
                _ = session.DrainStaleFrames();

                var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(WallClockBudgetMsPerIter);
                var hung = true;
                double latencyMs = 0;

                while (DateTime.UtcNow < deadline)
                {
                    using var cts = new CancellationTokenSource(deadline - DateTime.UtcNow);
                    CapturedFrame frame;
                    try { frame = await session.NextFrameAsync(cts.Token); }
                    catch (OperationCanceledException) { break; }

                    if (RoiDiffer.IsChanged(
                            baseline.BgraPixels, frame.BgraPixels, fullFrameRoi, frame.Width,
                            minChangedPixels: MinChangedPixels))
                    {
                        var qpcDelta = frame.QpcSystemRelativeTime - qpc0;
                        latencyMs = qpcDelta * 1000.0 / session.QpcFrequency;
                        hung = false;
                        break;
                    }
                }

                if (!isWarmup)
                {
                    samples.Add(hung
                        ? new IterationSample(Value: null, Hung: true)
                        : new IterationSample(Value: latencyMs, Hung: false));
                }

                Thread.Sleep(CooldownMs);
            }

            return samples;
        }
        finally
        {
            launch.Dispose();
        }
    }
}
