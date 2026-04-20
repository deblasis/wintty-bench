# wintty-bench

Cross-terminal benchmark harness for [wintty](https://github.com/deblasis/wintty) on Windows.

Measures throughput, startup, memory, VT correctness, resize reflow, and keystroke latency vs Windows Terminal and WezTerm.

## Status

MVP in progress: single harness binary, four throughput cells, CI-mode JSON output. Marketing-grade numbers, remaining KPIs, and published reports come in later iterations.

## First baseline (MVP)

Captured 2026-04-20 against wintty `windows@30482d8` (CI mode, ~30% GHA-equivalent variance expected).

| Cell | Shell | Workload | p50 throughput |
|---|---|---|---|
| C1 | pwsh-7.4 | vtebench dense_cells | 251 B/s |
| C2 | pwsh-7.4 | vtebench scrolling | 25 B/s |
| C3 | pwsh-7.4 | cjk_jp_mixed_1mb | 151,910 B/s |
| C4 | wsl-ubuntu-24.04 | vtebench dense_cells | 328 B/s |

C1, C2, C4 use tiny fixtures (<1KB) so the numbers are startup-dominated; treat C3 (1MB CJK mix) as the only steady-state signal in this MVP. Fixture sizes and a second CJK cell (C3a/C3b knob A/B) land in Plan 2/Plan 4.

Marketing-grade numbers coming in Plan 4.

## Quick start

```powershell
dotnet run --project harness -- --mode=ci --cells=C1 --target=<path-to-Wintty.exe>
```

## Reproducing marketing numbers

Coming in a later iteration.
