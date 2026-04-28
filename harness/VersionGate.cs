using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinttyBench;

public sealed class VersionMismatchException : Exception
{
    public VersionMismatchException(string message) : base(message) { }
}

public static class VersionGate
{
    // Parses --require-version values like "wt:1.18.3231.0,wezterm:20240829".
    // Returns a map from terminal name to required version. Empty/null input
    // returns an empty map (no gate). Malformed pairs throw ArgumentException
    // so the harness fails before launching anything.
    public static IReadOnlyDictionary<string, string> Parse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return new Dictionary<string, string>();

        var pins = new Dictionary<string, string>(StringComparer.Ordinal);
        var pairs = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var colon = pair.IndexOf(':');
            if (colon <= 0 || colon >= pair.Length - 1)
                throw new ArgumentException($"Malformed --require-version pair '{pair}'. Expected '<terminal>:<version>'.");

            var terminal = pair[..colon].Trim();
            var version = pair[(colon + 1)..].Trim();
            if (terminal.Length == 0 || version.Length == 0)
                throw new ArgumentException($"Malformed --require-version pair '{pair}'. Empty terminal or version.");

            pins[terminal] = version;
        }
        return pins;
    }

    // Throws VersionMismatchException listing every mismatch on the first
    // call. The exception message includes both pinned and detected for
    // each mismatch so a user sees the whole picture.
    public static void Verify(
        IReadOnlyDictionary<string, string> pins,
        IReadOnlyDictionary<string, string> detected)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(detected);

        var mismatches = new StringBuilder();
        foreach (var (terminal, requiredVersion) in pins)
        {
            if (!detected.TryGetValue(terminal, out var detectedVersion))
            {
                mismatches.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  {terminal}: required={requiredVersion}, detected=<not present>");
                continue;
            }
            if (!string.Equals(requiredVersion, detectedVersion, StringComparison.Ordinal))
            {
                mismatches.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  {terminal}: required={requiredVersion}, detected={detectedVersion}");
            }
        }

        if (mismatches.Length > 0)
        {
            throw new VersionMismatchException(
                "Version pinning mismatch:" + Environment.NewLine + mismatches);
        }
    }
}
