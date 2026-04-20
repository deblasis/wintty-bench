namespace WinttyBench.Fixtures;

// Result of fixture resolution for one cell.
// `ShellPath` is the path as it appears in the shell-script body:
// - For pwsh cells: absolute Windows path (e.g., C:\...\dense_cells.txt)
// - For WSL cells with a Plan 1 static fixture: /mnt/c/... path
// - For WSL cells with a Plan 2A generated fixture: /home/.../.cache/... path
public sealed record FixtureHandle(string ShellPath, long SizeBytes);
