using System.Diagnostics;
using System.Text;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
using WinttyBench.Launchers;

namespace WinttyBench.Runners;

// Measures "time to first prompt" for a shell spawned under Wintty. Each
// iteration: spawn Wintty -> pwsh overrides `function global:prompt` after
// $PROFILE load -> override writes a sentinel file on first prompt-ready
// and exits pwsh -> StartupRunner polls for the sentinel and stops its
// stopwatch. Hung iterations hit the SentinelWaiter timeout and record
// Hung: true. Profile load is intentionally measured (no -NoProfile) so
// the number reflects what the user experiences.
//
// Bounds: there is no per-run wall-clock ceiling. Worst case is
// (WarmupIters + MeasuredIters) * SentinelWaiter.DefaultExitTimeout.
// CI callers MUST size MeasuredIters so that hung-run worst-case fits
// the CI cell budget (15 min under FairnessProfile.Ci()).
public sealed class StartupRunner : IKpiRunner
{
    public Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string winttyExe,
        FairnessProfile profile,
        FixtureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentException.ThrowIfNullOrEmpty(winttyExe);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolver);

        var launcher = new WinttyLauncher();
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var samples = new List<IterationSample>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;
            var sentinelPath = Path.Combine(Path.GetTempPath(),
                $"wintty-bench-start-{Guid.NewGuid():N}.marker");
            if (File.Exists(sentinelPath)) File.Delete(sentinelPath);

            var shellCmd = BuildPwshStartupCommand(sentinelPath);

            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: winttyExe,
                ShellCommand: shellCmd,
                ConfigOverrides: cell.WinttyConfigOverrides,
                Cols: 120,
                Rows: 32));

            var sw = Stopwatch.StartNew();
            var hung = false;
            try
            {
                SentinelWaiter.WaitForSentinel(sentinelPath, SentinelWaiter.DefaultExitTimeout);
            }
            catch (TimeoutException)
            {
                hung = true;
            }
            finally
            {
                sw.Stop();
                launch.Dispose();
                try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); }
                catch (IOException) { /* best effort */ }
            }

            if (!isWarmup)
            {
                samples.Add(hung
                    ? new IterationSample(Value: null, Hung: true)
                    : new IterationSample(Value: sw.Elapsed.TotalSeconds, Hung: false));
            }
        }

        // NOTE: body is synchronous; see SentinelWaiter.WaitForSentinel note.
        // Do not await from a thread-pool context - the GetAwaiter().GetResult()
        // in BenchHost runs on a dedicated harness thread. Explicit type param
        // is required: FromResult's inference sees List<T>, not IReadOnlyList<T>.
        return Task.FromResult<IReadOnlyList<IterationSample>>(samples);
    }

    // Internal so harness.tests can drive this directly with Process.Start (no
    // Wintty round-trip) and assert the sentinel fires. That end-to-end check
    // is the only thing that catches pwsh arg-parsing / encoding regressions
    // before they turn into silent Hung iterations at bench time.
    internal static string BuildPwshStartupCommand(string sentinelPath)
    {
        // No -NoProfile: $PROFILE loads in the normal sequence and its cost is
        // part of the "time to first prompt" number we want to measure.
        // -NoExit is required: without it, pwsh runs the block and exits
        // before going interactive, so the `function global:prompt` override
        // is defined but never *called*. With -NoExit the flow is $PROFILE
        // -> run startup block (defines override) -> interactive loop ->
        // first prompt call -> our override fires sentinel + exits.
        //
        // -EncodedCommand (base64 UTF-16LE) not -Command, for a very specific
        // reason: the Wintty config file takes `command = <shell-string>`
        // which goes through multiple string-parsing layers (Ghostty config
        // parser, shell argv split, pwsh arg parser). Each of those can
        // mangle `$`, quotes, and braces differently. Observed symptom when
        // passing a raw -Command string: Ghostty's config parser ate the
        // `$env:WINTTY_BENCH_SENTINEL` tokens as variable references and
        // expanded them to empty, so pwsh received `-Command "='...'; ..."`
        // which is a syntax error and left pwsh stuck at a `>>` continuation
        // prompt. Base64 is pure ASCII with no `$`, no quotes, no braces -
        // every layer passes it through verbatim. pwsh decodes it natively.
        //
        // Exotic edge: if $PROFILE itself calls `exit`, pwsh terminates
        // before the prompt override is defined; the sentinel never fires
        // and the iteration records as Hung.
        // Double any apostrophe to escape: pwsh single-quoted string literals
        // use '' to encode a literal '.
        var escapedPath = sentinelPath.Replace("'", "''", StringComparison.Ordinal);
        var script =
            $"function global:prompt {{ New-Item -ItemType File -Force -Path '{escapedPath}' | Out-Null; exit }}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return $"pwsh -NoLogo -NoExit -EncodedCommand {encoded}";
    }
}
