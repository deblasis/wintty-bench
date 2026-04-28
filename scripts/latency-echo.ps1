# Deterministic single-byte echo for the wintty-bench latency KPI.
# PSReadLine is bypassed because we use bare-Console reads. Each
# keystroke paints one '*' at row R col C, advancing left-to-right then
# wrapping to row 2.
#
# Branch on IsInputRedirected so the same script works in both modes:
#   - LatencyEchoScriptTests redirects stdin via Process.Start; the pipe
#     is uncooked so [Console]::Read() returns one byte at a time.
#   - Inside wintty, the child shell sees a real console (ConPTY).
#     [Console]::Read() would buffer until newline; ReadKey() reads raw
#     per-keystroke without requiring Enter.
$ErrorActionPreference = 'Stop'
[Console]::Out.Write("`e[2J`e[1;1H")
$cols = 120
$i = 0
$redirected = [Console]::IsInputRedirected
while ($true) {
    if ($redirected) {
        $b = [Console]::Read()
        if ($b -lt 0) { break }
    } else {
        # ReadKey($true) intercept: do NOT echo the typed character. We
        # paint our own '*' below so the bench's WGC capture sees a
        # deterministic glyph; without intercept, the host would echo the
        # raw user keystroke as well, doubling pixel deltas in the diff.
        [void][Console]::ReadKey($true)
    }
    $col = ($i % $cols) + 1
    $row = [int]([math]::Floor($i / $cols)) + 1
    [Console]::Out.Write("`e[$row;${col}H*")
    [Console]::Out.Flush()
    $i++
}
