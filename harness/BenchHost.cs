using WinttyBench.Cells;

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
            Console.WriteLine($"Mode: {parsed.Mode}");
            Console.WriteLine($"Cells: {string.Join(", ", parsed.Cells)}");
            Console.WriteLine($"Target: {parsed.TargetExePath}");
            if (parsed.ReleaseTag is not null)
                Console.WriteLine($"Release tag: {parsed.ReleaseTag}");

            // Task 12 wires up the actual measurement loop.
            Console.WriteLine("(measurement loop not yet implemented; see Plan Task 12)");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Arg error: {ex.Message}");
            return 2;
        }
    }
}
