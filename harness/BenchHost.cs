using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;

namespace WinttyBench;

public sealed record ParsedArgs(
    string Mode,
    IReadOnlyList<string> Cells,
    string TargetExePath,
    string? ReleaseTag);

public static class BenchHost
{
    private static readonly HashSet<string> ValidModes = ["ci", "marketing"];

    public static ParsedArgs ParseArgs(IReadOnlyList<string> args)
    {
        string? mode = null;
        string? cells = null;
        string? target = null;
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
                case "release-tag": releaseTag = value; break;
                default: throw new ArgumentException($"Unknown flag --{key}");
            }
        }

        if (mode is null) throw new ArgumentException("--mode is required");
        if (!ValidModes.Contains(mode))
            throw new ArgumentException($"Unknown mode '{mode}'. Valid: {string.Join(", ", ValidModes)}");
        if (cells is null) throw new ArgumentException("--cells is required");
        if (target is null) throw new ArgumentException("--target is required");
        if (mode == "marketing" && releaseTag is null)
            throw new ArgumentException("--release-tag is required in marketing mode");

        var cellList = ResolveCells(cells);

        return new ParsedArgs(mode, cellList, target, releaseTag);
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

    public static int Run(IReadOnlyList<string> args)
    {
        try
        {
            var parsed = ParseArgs(args);
            var profile = parsed.Mode == "ci" ? FairnessProfile.Ci() : FairnessProfile.Marketing();
            var env = EnvProbe.Capture(parsed.TargetExePath);
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

            foreach (var cellId in parsed.Cells)
            {
                var cell = StarredCells.All.Single(c => c.Id == cellId);
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"[{cellId}] {cell.Shell} x {cell.Workload} ({cell.Kpi})"));

                var run = MeasurementRunner.RunThroughputAsync(cell, parsed.TargetExePath, profile, resolver).GetAwaiter().GetResult();
                var samples = run.Samples;
                var trimmed = profile.Discarded.Contains("last")
                    ? ThroughputKpi.TrimFirstAndLast(samples)
                    : samples.Skip(profile.Discarded.Count).ToArray();

                var kpiResult = new ThroughputKpi().ComputeFromSamples(trimmed, run.FixtureBytes);

                var envelope = new ResultEnvelope(
                    SchemaVersion: 2,
                    RunId: runId,
                    Mode: parsed.Mode,
                    ReleaseTag: parsed.ReleaseTag,
                    Env: env,
                    Fairness: profile.ToCapture(),
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

                var outPath = Path.Combine(outDir, string.Create(CultureInfo.InvariantCulture, $"{cell.Id}.json"));
                File.WriteAllText(outPath,
                    System.Text.Json.JsonSerializer.Serialize(envelope,
                        ResultSchemaContext.Default.ResultEnvelope));
                var p50Display = kpiResult.ValueP50.HasValue
                    ? string.Create(CultureInfo.InvariantCulture, $"{kpiResult.ValueP50.Value:N0} B/s")
                    : "degraded";
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"[{cellId}] wrote {outPath} (p50 = {p50Display})"));
            }

            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Arg error: {ex.Message}");
            return 2;
        }
    }
}
