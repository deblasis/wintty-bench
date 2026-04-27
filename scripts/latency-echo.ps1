# Deterministic single-byte echo for the wintty-bench latency KPI.
# PSReadLine is bypassed because we use [Console]::Read(), which is
# bare CRT-level stdin. Each iteration paints '*' at row 1 col (i+1)
# where i is the per-process iteration counter, wrapping to row 2
# after the row fills.
$ErrorActionPreference = 'Stop'
[Console]::Out.Write("`e[2J`e[1;1H")
$cols = 120
$i = 0
while ($true) {
    $b = [Console]::Read()
    if ($b -lt 0) { break }
    $col = ($i % $cols) + 1
    $row = [int]([math]::Floor($i / $cols)) + 1
    [Console]::Out.Write("`e[$row;${col}H*")
    [Console]::Out.Flush()
    $i++
}
