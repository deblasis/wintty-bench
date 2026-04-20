# Deterministic generator for a 1 MB mixed Japanese fixture.
# Runs: hiragana + katakana + kanji + ASCII punctuation + newlines.
# Seeded RNG so output is bit-identical across runs.

param(
    [string]$OutputPath = "fixtures/cjk/jp-mixed-1mb.txt",
    [int]$TargetBytes = 1048576,
    [int]$Seed = 20260420
)

$ErrorActionPreference = "Stop"

# Unicode ranges
$hiragana = 0x3041..0x3096
$katakana = 0x30A1..0x30FA
$kanji    = 0x4E00..0x4FFF  # subset of CJK Unified (first 512 chars) to keep it reproducible
$ascii    = @(0x20, 0x21, 0x2C, 0x2E, 0x0A)  # space, !, comma, period, LF

$pool = @()
$pool += $hiragana
$pool += $katakana
$pool += $kanji
$pool += $ascii

$rng = [System.Random]::new($Seed)

$sb = [System.Text.StringBuilder]::new($TargetBytes)
$bytesWritten = 0
while ($bytesWritten -lt $TargetBytes) {
    $cp = $pool[$rng.Next(0, $pool.Length)]
    $ch = [char]::ConvertFromUtf32($cp)
    [void]$sb.Append($ch)
    $bytesWritten += [System.Text.Encoding]::UTF8.GetByteCount($ch)
}

[System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $bytesWritten bytes to $OutputPath"
