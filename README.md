# wintty-bench

Cross-terminal benchmark harness for [wintty](https://github.com/deblasis/wintty) on Windows.

Measures throughput, startup, memory, VT correctness, resize reflow, and keystroke latency vs Windows Terminal and WezTerm.

## Status

MVP shipped. Plan 2A extends to 7 throughput cells (C1-C5, C10, C11) with mode-aware generated fixtures on the WSL side and schema v2 (`IterationSample` per iteration, `hung` flag, nullable p50 for degraded cells). Remaining KPIs and WT/WezTerm comparison come in later plans.

## Baseline (Plan 2A)

Captured 2026-04-20 against wintty `windows@30482d8` (CI mode, ~30% GHA-equivalent variance expected). Schema version 2.

| Cell | Shell | Workload | Fixture size | p50 throughput |
|------|------------------|--------------------------|--------------|----------------|
| C1   | pwsh-7.4         | vtebench dense_cells     | 765 B        | 222 B/s        |
| C2   | pwsh-7.4         | vtebench scrolling       | 71 B         | 24 B/s         |
| C3   | pwsh-7.4         | cjk_jp_mixed_1mb         | 1 MB         | 128,520 B/s    |
| C4   | wsl-ubuntu-24.04 | vtebench dense_cells     | 765 B        | 307 B/s        |
| C5   | wsl-ubuntu-24.04 | vtebench unicode         | 84 B         | 33 B/s         |
| C10  | wsl-ubuntu-24.04 | vtebench_cat_sustained   | 1 MB         | 23,142 B/s     |
| C11  | wsl-ubuntu-24.04 | filtered_random_sustained| 1 MB         | 59,103 B/s     |

C1, C2, C4, C5 use sub-1 KB fixtures so those numbers are startup-dominated; treat C3, C10, C11 as steady-state signal. Generators for C10 and C11 live in `scripts/fixtures/`; first run on a machine generates + caches the fixture under `$HOME/.cache/wintty-bench/` on WSL (content-hashed sidecar, regenerate on mismatch).

Marketing-grade numbers coming in a later plan.

## Quick start

```powershell
dotnet run --project harness -- --mode=ci --cells=C1 --target=<path-to-Wintty.exe>
```

## Reproducing marketing numbers

Coming in a later iteration.
