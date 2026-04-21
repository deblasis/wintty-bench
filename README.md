# wintty-bench

Cross-terminal benchmark harness for [wintty](https://github.com/deblasis/wintty) on Windows.

Measures throughput, startup, memory, VT correctness, resize reflow, and keystroke latency vs Windows Terminal and WezTerm.

## Status

MVP shipped. Plan 2A extends to 7 throughput cells (C1-C5, C10, C11) with mode-aware generated fixtures on the WSL side and schema v2 (`IterationSample` per iteration, `hung` flag, nullable p50 for degraded cells). Remaining KPIs and WT/WezTerm comparison come in later plans.

## Baseline (Plan 2A)

Captured 2026-04-21 against wintty `windows@30482d8` (CI mode, ~30% GHA-equivalent variance expected). Schema version 2.

| Cell | Shell | Workload | Fixture size | p50 throughput |
|------|------------------|--------------------------|--------------|----------------|
| C1   | pwsh-7.4         | vtebench dense_cells     | 2.57 MB      | 870,751 B/s    |
| C2a  | pwsh-7.4         | vtebench scrolling       | 200 KB       | ~2,104 B/s *   |
| C2b  | wsl-ubuntu-24.04 | vtebench scrolling       | 200 KB       | ~96,841 B/s *  |
| C3   | pwsh-7.4         | cjk_jp_mixed_1mb         | 1 MB         | 128,520 B/s    |
| C4   | wsl-ubuntu-24.04 | vtebench dense_cells     | 2.57 MB      | 998,836 B/s    |
| C5   | wsl-ubuntu-24.04 | vtebench unicode         | 138 KB       | 67,190 B/s     |
| C10  | wsl-ubuntu-24.04 | vtebench_cat_sustained   | 1 MB         | 23,142 B/s     |
| C11  | wsl-ubuntu-24.04 | filtered_random_sustained| 1 MB         | 59,103 B/s     |

\* C2a and C2b numbers are from the 2026-04-21 scrolling-degradation probe (4-iteration writer-variant harness), not a fresh 9-iteration full-harness run under the new IDs. Full-harness re-run pending.

C1, C4, C5 fixtures were replaced from upstream shell-script wrappers to the actual byte streams vtebench produces; the numbers above are the fresh re-run. C3, C10, C11 are current steady-state signal. Generators for all vtebench fixtures live in `scripts/fixtures/make-vtebench-fixtures.sh`; generators for C10 and C11 live in `scripts/fixtures/make-c1{0,1}.sh` and cache under `$HOME/.cache/wintty-bench/` on WSL with a content-hashed sidecar.

C2 was split into C2a (pwsh) and C2b (wsl) after a 2026-04-21 probe: the same 200 KB `y\n` scroll fixture hits ~2k B/s through pwsh-on-ConPTY and ~97k B/s through WSL `cat` on the same Wintty binary. The ~50x gap is the user-shell floor (three pwsh writer APIs -- `Write-Host`, `[Console]::Out.Write`, `Out-Host` -- all landed within ~8%), not a Wintty scroll-path cost.

Marketing-grade numbers coming in a later plan.

## Quick start

```powershell
dotnet run --project harness -- --mode=ci --cells=C1 --target=<path-to-Wintty.exe>
```

## Reproducing marketing numbers

Coming in a later iteration.
