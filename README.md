# wintty-bench

Cross-terminal benchmark harness for [wintty](https://github.com/deblasis/wintty) on Windows.

Measures throughput, startup, memory, VT correctness, resize reflow, and keystroke latency vs Windows Terminal and WezTerm.

## Status

MVP shipped. Plan 2A extends to 7 throughput cells (C1-C5, C10, C11) with mode-aware generated fixtures on the WSL side and schema v2 (`IterationSample` per iteration, `hung` flag, nullable p50 for degraded cells). Plan 2B adds the first non-throughput KPI (`startup_seconds`, cell C8) on top of a refactored `IKpi` / `IKpiRunner` shape that makes further KPIs drop-in. Plan 2D adds keystroke-to-glyph latency (cell C13) via Windows.Graphics.Capture + SendInput. Plan 3a lands the terminal-axis infrastructure: `--terminals=wintty[,wt]` CLI selector, `ResultEnvelope` schema v3 with a `terminal` field, `LauncherFactory` dispatching to `WinttyLauncher` or `WtLauncher`, runners declaring `SupportedTerminals`, and a portable WT setup script at `scripts/setup-wt-portable.ps1`. `--terminals=wintty` (the default) preserves existing behavior exactly. **WT cells now run end-to-end** via Windows Terminal settings fragments under `%LOCALAPPDATA%\Microsoft\Windows Terminal\Fragments\wintty-bench\`; the original `WT_SETTINGS_PATH` handoff was silently ignored on unpackaged WT, and a per-iter source dir variant accumulated orphan profile entries in the user's `settings.json`. **WT throughput numbers are NOT directly comparable to wintty's**: WT does not back-pressure stdout the way wintty's ConPTY does, so the script's sentinel touch fires after buffer-drain rather than after render. The proper vtebench-style query-response timing fix is tracked in # 18. WezTerm (Plan 3b), WT C8 startup (3c), and WT C9 RSS (3d) remain deferred.

## Baseline (Plan 2A)

Captured 2026-04-21 against wintty `windows@30482d8` (CI mode, ~30% GHA-equivalent variance expected). Schema version 2.

| Cell | Shell | Workload | Fixture size | p50 throughput |
|------|------------------|--------------------------|--------------|----------------|
| C1   | pwsh-7.4         | vtebench dense_cells     | 2.57 MB      | 870,751 B/s    |
| C2a  | pwsh-7.4         | vtebench scrolling       | 200 KB       | 1,733 B/s      |
| C2b  | wsl-ubuntu-24.04 | vtebench scrolling       | 200 KB       | 73,384 B/s     |
| C3   | pwsh-7.4         | cjk_jp_mixed_1mb         | 1 MB         | 128,520 B/s    |
| C4   | wsl-ubuntu-24.04 | vtebench dense_cells     | 2.57 MB      | 998,836 B/s    |
| C5   | wsl-ubuntu-24.04 | vtebench unicode         | 138 KB       | 67,190 B/s     |
| C10  | wsl-ubuntu-24.04 | vtebench_cat_sustained   | 1 MB         | 23,142 B/s     |
| C11  | wsl-ubuntu-24.04 | filtered_random_sustained| 1 MB         | 59,103 B/s     |

C1, C4, C5 fixtures were replaced from upstream shell-script wrappers to the actual byte streams vtebench produces; the numbers above are the fresh re-run. C3, C10, C11 are current steady-state signal. Generators for all vtebench fixtures live in `scripts/fixtures/make-vtebench-fixtures.sh`; generators for C10 and C11 live in `scripts/fixtures/make-c1{0,1}.sh` and cache under `$HOME/.cache/wintty-bench/` on WSL with a content-hashed sidecar.

C2 was split into C2a (pwsh) and C2b (wsl) after a 2026-04-21 probe: the same 200 KB `y\n` scroll fixture hits ~2k B/s through pwsh-on-ConPTY and ~97k B/s through WSL `cat` on the same Wintty binary. The ~50x gap is the user-shell floor (three pwsh writer APIs -- `Write-Host`, `[Console]::Out.Write`, `Out-Host` -- all landed within ~8%), not a Wintty scroll-path cost.

Marketing-grade numbers coming in a later plan.

## Startup (Plan 2B)

Captured 2026-04-24 against wintty `windows@1c43011` (CI mode, 1 warmup + 9 measured iterations per cell).

| Cell | Shell    | Workload      | p50 startup | p95 startup |
|------|----------|---------------|-------------|-------------|
| C8   | pwsh-7.4 | shell_startup | 9.53 s      | 13.37 s     |

C8 measures cold `Wintty.exe` -> pwsh `$PROFILE` load -> first prompt ready, under the same `FairnessProfile.Ci()` shape used for throughput cells. `$PROFILE` is intentionally loaded (no `-NoProfile`) because the number is meant to reflect what a user with a loaded prompt (OhMyPosh, posh-git, etc.) experiences on a cold launch. The sentinel is a `function global:prompt` override that writes a marker file on first call and then exits pwsh; the runner passes it via `pwsh -NoLogo -NoExit -EncodedCommand <base64>` to sidestep `$`/quote/brace mangling across the config-parser / argv-split / pwsh-parser boundary.

The numbers above are from a developer box (Ryzen 7 PRO 5750G, 16 GB, Windows 11 26200) with a full OhMyPosh prompt and posh-git loaded, so they tilt toward the warm end of what users see. CI runners without prompt addons are expected to clock lower.

## Memory (Plan 2B)

Captured 2026-04-24 against wintty `windows@59d1f5d` (CI mode, 1 warmup + 9 measured iterations per cell).

| Cell | Shell            | Workload             | Fixture         | p50 peak RSS | p95 peak RSS |
|------|------------------|----------------------|-----------------|--------------|--------------|
| C9   | wsl-ubuntu-24.04 | rss_under_ingest_10s | C11 PRNG (1 MB) | 222.9 MB     | 223.5 MB     |

C9 measures `Wintty.exe` `WorkingSet64` sampled at 500 ms cadence over a 10 s WSL `cat`-driven ingest of the C11 filtered-random fixture. WSL is picked as the driver because it's faster and less variable than pwsh on ConPTY; the KPI targets the Wintty process itself, so shell choice only affects ingest speed, not what is measured. Raw iteration spread was ~2 MB (232.6-234.5 MB, 0.27% CV).

## Latency (Plan 2D)

Captured 2026-04-28 against wintty `windows@da77dd2` (CI mode, 1 warmup + 30 measured iterations per cell).

| Cell | Shell    | Workload                   | p50 latency | p95 latency |
|------|----------|----------------------------|-------------|-------------|
| C13  | pwsh-7.4 | latency_keystroke_to_glyph | 35.6 ms     | 44.7 ms     |

C13 measures keystroke-to-glyph latency: synthesize one `VK_SPACE` via `SendInput`, capture `Stopwatch.GetTimestamp()` as `t=0`, then time the next Windows.Graphics.Capture frame whose full-frame BT.709 luminance diff exceeds the 50-pixel threshold. The shell is a deterministic byte-echo loop (`scripts/latency-echo.ps1`) that does `[Console]::ReadKey($true)` per keystroke and writes `\e[<row>;<col>H*` to stdout; PSReadLine is bypassed so the number is "ConPTY + wintty render pipeline", not "+ PSReadLine". The script is passed via `pwsh -NoLogo -NoProfile -EncodedCommand <base64>` to sidestep `$`/quote/brace mangling across the config-parser / argv-split / pwsh-parser boundary, same approach as Plan 2B's StartupRunner.

Numbers are bounded below by the active monitor's frame interval; the developer box runs at 100 Hz (10 ms per frame) so the floor is ~5 ms. The JSON envelope's `env.display.refresh_hz` records the rate per run for cross-machine comparison. CI runners at 60 Hz will show a higher floor (~8.3 ms half-frame).

The runner uses a full-frame pixel diff rather than a per-cell ROI because wintty's actual cell grid sits inside the client area with unknown padding; client-area / cols-rows division does not align to where the script paints. The 50-pixel count threshold is well below a glyph's footprint (a `*` is 30+ luminance-flipped pixels alone, more with antialias halos and the implied erase of the prior cursor) and well above antialias jitter on otherwise-unchanged frames. Cursor blink lands in the same magnitude bucket, treated as a legitimate paint signal: blinks distribute uniformly within the 1 s deadline so they show up as occasional outliers on the high tail rather than systemic bias on p50.

## Quick start

```powershell
dotnet run --project harness -- --mode=ci --cells=C1 --target=<path-to-Wintty.exe>
```

## Reproducing marketing numbers

Coming in a later iteration.
