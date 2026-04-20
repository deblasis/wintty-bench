# wintty-bench

Cross-terminal benchmark harness for [wintty](https://github.com/deblasis/wintty) on Windows.

Measures throughput, startup, memory, VT correctness, resize reflow, and keystroke latency vs Windows Terminal and WezTerm.

## Status

MVP in progress: single harness binary, four throughput cells, CI-mode JSON output. Marketing-grade numbers, remaining KPIs, and published reports come in later iterations.

## Quick start

```powershell
dotnet run --project harness -- --mode=ci --cells=C1 --target=<path-to-Ghostty.exe>
```

## Reproducing marketing numbers

Coming in a later iteration.
