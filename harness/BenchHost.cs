using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
using WinttyBench.Runners;

namespace WinttyBench;

public sealed record ParsedArgs(
    string Mode,
    IReadOnlyList<string> Cells,
    string? TargetExePath,
    string? TargetWtPath,
    IReadOnlyList<string> Terminals,
    string? RequireVersion,
    string? ReleaseTag);

public static class BenchHost
{
    private static readonly HashSet<string> ValidModes = ["ci", "marketing"];
    private static readonly HashSet<string> KnownTerminals = ["wintty", "wt"];

    public static ParsedArgs ParseArgs(IReadOnlyList<string> args)
    {
        string? mode = null;
        string? cells = null;
        string? target = null;
        string? targetWt = null;
        string? terminalsSpec = null;
        string? requireVersion = null;
        string? releaseTag = null;

        foreach (var arg in args)
        {
            var eq = arg.IndexOf('=');
            if (!arg.StartsWith("--", StringComparison.Ordinal) || eq < 0)
                throw new ArgumentException($"Malformed arg '{arg}'. Expected --key=value.");

            var key = arg.Substring(2, eq - 2);
            var value = arg[(eq + 1)..];
            switch (key)
            {
                case "mode": mode = value; break;
                case "cells": cells = value; break;
                case "target": target = value; break;
                case "target-wt": targetWt = value; break;
                case "terminals": terminalsSpec = value; break;
                case "require-version": requireVersion = value; break;
                case "release-tag": releaseTag = value; break;
                default: throw new ArgumentException($"Unknown flag --{key}");
            }
        }

        if (mode is null) throw new ArgumentException("--mode is required");
        if (!ValidModes.Contains(mode))
            throw new ArgumentException($"Unknown mode '{mode}'. Valid: {string.Join(", ", ValidModes)}");
        if (cells is null) throw new ArgumentException("--cells is required");
        if (mode == "marketing" && releaseTag is null)
            throw new ArgumentException("--release-tag is required in marketing mode");

        var terminals = ResolveTerminals(terminalsSpec);

        if (terminals.Contains("wintty") && target is null)
            throw new ArgumentException("--target=<Wintty.exe path> is required when 'wintty' is in --terminals");
        if (terminals.Contains("wt") && targetWt is null)
            throw new ArgumentException("--target-wt=auto|<wt.exe path> is required when 'wt' is in --terminals");

        var cellList = ResolveCells(cells);

        return new ParsedArgs(mode, cellList, target, targetWt, terminals, requireVersion, releaseTag);
    }

    private static string SanitizeForPath(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '+')
                chars[i] = '_';
        }
        return new string(chars);
    }

    private static string[] ResolveCells(string spec)
    {
        if (spec == "all")
            return StarredCells.All.Select(c => c.Id).ToArray();

        var ids = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = StarredCells.All.Select(c => c.Id).ToHashSet();
        foreach (var id in ids)
        {
            if (!valid.Contains(id))
                throw new ArgumentException($"Unknown cell '{id}'. Valid: {string.Join(", ", valid)}");
        }
        return ids;
    }

    private static string[] ResolveTerminals(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return ["wintty"];
        var parts = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (!KnownTerminals.Contains(p))
                throw new ArgumentException($"Unknown terminal '{p}'. Known: {string.Join(", ", KnownTerminals)}");
        }
        return parts;
    }

    private static string ResolveTargetExePath(string terminalName, ParsedArgs parsed) => terminalName switch
    {
        "wintty" => parsed.TargetExePath
            ?? throw new InvalidOperationException("Wintty target should have been validated at ParseArgs"),
        "wt" => Launchers.WtAutoResolver.Resolve(parsed.TargetWtPath
            ?? throw new InvalidOperationException("WT target should have been validated at ParseArgs")),
        _ => throw new ArgumentException($"Unknown terminal '{terminalName}'"),
    };

    public static int Run(IReadOnlyList<string> args)
    {
        try
        {
            var parsed = ParseArgs(args);
            var profile = parsed.Mode == "ci" ? FairnessProfile.Ci() : FairnessProfile.Marketing();

            // Capture env once per run; both terminals share environmental
            // facts (CPU/RAM/Windows build/display). When wintty is not in
            // play, ProbeWinttyVersion silently degrades to "unknown".
            var winttyTargetForEnv = parsed.TargetExePath ?? string.Empty;
            var env = EnvProbe.Capture(winttyTargetForEnv);
            var shortSha = env.WinttySha.Length >= 7 ? env.WinttySha[..7] : env.WinttySha;
            // Run ID is embedded in both filesystem paths and JSON; keep it
            // filesystem-safe on Windows (no ':' from ISO timestamps, no '+'
            // from semver build metadata).
            var fsSha = SanitizeForPath(shortSha);
            var runId = string.Create(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{fsSha}");

            var outDir = parsed.Mode == "ci"
                ? Path.Combine("results", "ci", runId)
                : Path.Combine("results", string.Create(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyy-MM-dd}-{SanitizeForPath(parsed.ReleaseTag!)}"));
            Directory.CreateDirectory(outDir);

            var resolver = new FixtureResolver(new WslFixtureCache());

            // VersionGate runs once before any iterations; gate failures
            // throw early so we never spawn launchers under a bad pin.
            if (!string.IsNullOrEmpty(parsed.RequireVersion))
            {
                var pins = VersionGate.Parse(parsed.RequireVersion);
                var detected = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["wt"] = env.WtVersion,
                };
                VersionGate.Verify(pins, detected);
            }

            foreach (var cellId in parsed.Cells)
            {
                var cell = StarredCells.All.Single(c => c.Id == cellId);
                var runner = KpiRunnerFactory.For(cell);

                foreach (var terminalName in parsed.Terminals)
                {
                    if (!runner.SupportedTerminals.Contains(terminalName))
                    {
                        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"[{cellId}/{terminalName}] skipped (runner does not support terminal)"));
                        continue;
                    }

                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"[{cellId}/{terminalName}] {cell.Shell} x {cell.Workload} ({cell.Kpi})"));

                    var targetForTerminal = ResolveTargetExePath(terminalName, parsed);

                    var samples = runner.RunAsync(cell, terminalName, targetForTerminal, profile, resolver).GetAwaiter().GetResult();

                    var trimmed = profile.Discarded.Contains("last")
                        ? KpiStats.TrimFirstAndLast(samples)
                        : samples.Skip(profile.Discarded.Count).ToArray();

                    var kpi = KpiFactory.For(cell.Kpi);
                    var kpiResult = kpi.ComputeFromSamples(trimmed);

                    var envelope = new ResultEnvelope(
                        SchemaVersion: 3,
                        RunId: runId,
                        Mode: parsed.Mode,
                        ReleaseTag: parsed.ReleaseTag,
                        Env: env,
                        Fairness: profile.ToCapture(),
                        Terminal: terminalName,
                        CellId: cell.Id,
                        Shell: cell.Shell,
                        Workload: cell.Workload,
                        Kpi: cell.Kpi,
                        ValueP50: kpiResult.ValueP50,
                        ValueP95: kpiResult.ValueP95,
                        ValueP99: kpiResult.ValueP99,
                        ValueStddev: kpiResult.ValueStddev,
                        RawIterations: kpiResult.RawIterations,
                        Source: kpiResult.Source,
                        Notes: "");

                    var outPath = Path.Combine(outDir, string.Create(CultureInfo.InvariantCulture,
                        $"{cell.Id}-{terminalName}.json"));
                    File.WriteAllText(outPath,
                        System.Text.Json.JsonSerializer.Serialize(envelope,
                            ResultSchemaContext.Default.ResultEnvelope));
                    var p50Display = kpiResult.ValueP50.HasValue
                        ? string.Create(CultureInfo.InvariantCulture, $"{kpiResult.ValueP50.Value:N0} {kpi.UnitHint}")
                        : "degraded";
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"[{cellId}/{terminalName}] wrote {outPath} (p50 = {p50Display})"));
                }
            }

            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Arg error: {ex.Message}");
            return 2;
        }
        catch (VersionMismatchException ex)
        {
            Console.Error.WriteLine($"Version gate: {ex.Message}");
            return 3;
        }
    }
}
