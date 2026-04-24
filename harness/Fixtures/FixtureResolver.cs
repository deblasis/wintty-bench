using WinttyBench.Cells;

namespace WinttyBench.Fixtures;

// Resolves a Cell + FairnessProfile to a concrete fixture path.
// Handles both Plan 1 static-path cells (returns path as-is, with WSL
// mount conversion for WSL cells) and Plan 2A generated-fixture cells
// (invokes the matching `scripts/fixtures/make-<key>.sh` on WSL if
// the cache is empty or the sidecar hash does not match).
//
// All relative paths (FixturePath, script paths) are resolved against the
// repo root. The resolver locates the repo root by walking up from
// AppContext.BaseDirectory until it finds a directory containing
// `scripts/fixtures`. Paths are resolved via Path.Combine(_repoRoot, ...)
// so Environment.CurrentDirectory is never touched.
public sealed class FixtureResolver
{
    private readonly WslFixtureCache _wsl;
    private readonly string _repoRoot;

    public FixtureResolver(WslFixtureCache wsl)
    {
        _wsl = wsl ?? throw new ArgumentNullException(nameof(wsl));
        _repoRoot = FindRepoRoot();
    }

    // Walk up from the executable's base directory until we find a
    // directory that contains `scripts/fixtures`. This works whether the
    // binary lives under bin/Debug/net10.0/ (test runs) or anywhere else.
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "scripts", "fixtures")))
                return dir.FullName;
            dir = dir.Parent;
        }
        // Fallback: use the current directory as-is (production runs from repo root).
        return Environment.CurrentDirectory;
    }

    public async Task<FixtureHandle> ResolveAsync(Cell cell, FairnessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(profile);

        if (cell.FixturePath is not null)
        {
            var abs = Path.GetFullPath(Path.Combine(_repoRoot, cell.FixturePath));
            if (!File.Exists(abs))
                throw new FileNotFoundException($"Static fixture not found: {abs}");
            var size = new FileInfo(abs).Length;
            var shellPath = cell.Shell == "wsl-ubuntu-24.04"
                ? WslPaths.ToWslMountPath(abs)
                : abs;
            return new FixtureHandle(shellPath, size);
        }

        var key = cell.FixtureKey
            ?? throw new InvalidOperationException($"Cell {cell.Id} has neither FixturePath nor FixtureKey");
        if (!profile.FixtureSizeBytesByKey.TryGetValue(key, out var targetSize))
            throw new InvalidOperationException(
                $"No fixture size configured for key '{key}' in profile; known keys: {string.Join(",", profile.FixtureSizeBytesByKey.Keys)}");

        var wslHome = await _wsl.HomeAsync();
        var wslFixture = $"{wslHome}/.cache/wintty-bench/{key}-{targetSize}.bin";
        var sidecar = $"{wslFixture}.sha256";
        var winSidecar = _wsl.ToWindowsPath(sidecar);
        var winFixture = _wsl.ToWindowsPath(wslFixture);

        var needsRegen = true;
        if (File.Exists(winFixture) && File.Exists(winSidecar))
        {
            var recorded = (await File.ReadAllTextAsync(winSidecar)).Trim();
            var actual = await _wsl.ComputeSha256Async(wslFixture);
            if (string.Equals(recorded, actual, StringComparison.OrdinalIgnoreCase))
                needsRegen = false;
        }

        if (needsRegen)
        {
            var scriptWinAbs = Path.GetFullPath(Path.Combine(_repoRoot, $"scripts/fixtures/make-{key}.sh"));
            if (!File.Exists(scriptWinAbs))
                throw new FileNotFoundException($"Generator script not found: {scriptWinAbs}");
            var scriptWsl = WslPaths.ToWslMountPath(scriptWinAbs);
            await _wsl.RunBashScriptAsync($"bash '{scriptWsl}' {targetSize}");
            var newHash = await _wsl.ComputeSha256Async(wslFixture);
            await _wsl.RunBashScriptAsync($"printf '{newHash}' > '{sidecar}'");
        }

        return new FixtureHandle(wslFixture, targetSize);
    }
}
