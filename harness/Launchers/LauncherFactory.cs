using System;

namespace WinttyBench.Launchers;

public static class LauncherFactory
{
    // Singleton instances; launchers are stateless (config + env per
    // request). KpiRunnerFactory caches singletons with the same shape.
    private static readonly WinttyLauncher s_wintty = new();
    private static readonly WtLauncher s_wt = new();

    // Maps the CLI terminal name to its concrete ILauncher. Wintty is
    // the historical default. The two terminal-name namespaces are kept
    // separate intentionally: --terminals=wintty,wt,wezterm in CLI maps
    // to the launchers that have implementations; an unknown name
    // throws so typos surface at startup, not after iterations begin.
    public static ILauncher For(string terminalName) => terminalName switch
    {
        "wintty" => s_wintty,
        "wt"     => s_wt,
        _        => throw new ArgumentException($"Unknown terminal '{terminalName}'. Known: wintty, wt."),
    };
}
