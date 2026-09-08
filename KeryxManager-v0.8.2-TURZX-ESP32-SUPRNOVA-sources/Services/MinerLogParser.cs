using System.Globalization;
using System.Text.RegularExpressions;

namespace KeryxControl.Services;

public sealed partial class MinerLogParser
{
    public MinerStats Parse(string line, MinerStats current, bool isPool)
    {
        current = ParseHashrate(line, current);
        return isPool ? ParsePoolCounters(line, current) : ParseSoloCounters(line, current);
    }

    private static MinerStats ParseHashrate(string line, MinerStats current)
    {
        var match = Hashrate().Match(line);
        if (!match.Success
            || !double.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return current;

        var converted = value * UnitFactor(match.Groups["unit"].Value);
        return double.IsFinite(converted) && converted >= 0 ? current with { HashrateMh = converted } : current;
    }

    private static MinerStats ParsePoolCounters(string line, MinerStats current)
    {
        var summary = ShareSummary().Match(line);
        if (summary.Success)
        {
            var accepted = ParseCount(summary.Groups["accepted"].Value);
            var rejected = ParseCount(summary.Groups["stale"].Value)
                + ParseCount(summary.Groups["low"].Value)
                + ParseCount(summary.Groups["duplicate"].Value);
            return current with { Accepted = accepted, Rejected = rejected };
        }

        if (ShareAccepted().IsMatch(line)) return current with { Accepted = current.Accepted + 1 };
        if (ShareRejected().IsMatch(line)) return current with { Rejected = current.Rejected + 1 };
        return current;
    }

    private static MinerStats ParseSoloCounters(string line, MinerStats current)
    {
        // "Found a block" is deliberately not counted: the following submit
        // result is the event that determines the accepted/rejected counter.
        if (SoloAccepted().IsMatch(line)) return current with { Accepted = current.Accepted + 1 };
        if (SoloRejected().IsMatch(line)) return current with { Rejected = current.Rejected + 1 };
        return current;
    }

    private static long ParseCount(string value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;

    private static double UnitFactor(string unit) => unit.ToUpperInvariant() switch
    {
        "H" => 0.000001,
        "KH" => 0.001,
        "GH" => 1000,
        "TH" => 1_000_000,
        _ => 1
    };

    [GeneratedRegex(@"(?i)(?:hashrate|speed|rate)[^0-9]*(?<value>[0-9]+(?:[.,][0-9]+)?)\s*(?<unit>[KMGT]?H)(?:ash)?(?:/s|s)?")]
    private static partial Regex Hashrate();

    [GeneratedRegex(@"(?i)\bShares:\s*(?:Accepted:\s*(?<accepted>\d+)\s*)?(?:Stale:\s*(?<stale>\d+)\s*)?(?:Low difficulty:\s*(?<low>\d+)\s*)?(?:Duplicate:\s*(?<duplicate>\d+)\s*)?Pending:\s*\d+")]
    private static partial Regex ShareSummary();

    [GeneratedRegex(@"(?i)\bShare accepted\b")]
    private static partial Regex ShareAccepted();

    [GeneratedRegex(@"(?i)\b(?:Share rejected by pool|Stale share|Duplicate share|Low difficulty share)\b")]
    private static partial Regex ShareRejected();

    [GeneratedRegex(@"(?i)\bBlock submitted successfully\b")]
    private static partial Regex SoloAccepted();

    [GeneratedRegex(@"(?i)\b(?:Block rejected|Block submission failed|Failed to submit block)\b")]
    private static partial Regex SoloRejected();
}

public sealed record MinerStats(double HashrateMh = 0, long Accepted = 0, long Rejected = 0);
