using System.Globalization;
using System.Text.RegularExpressions;

namespace KeryxControl.Services;

public sealed partial class MinerLogParser
{
    public MinerStats Parse(string line, MinerStats current)
    {
        var shareSummary = ShareSummary().Match(line);
        var poolCounterHandled = false;
        if (shareSummary.Success)
        {
            var acceptedShares = ParseCount(shareSummary.Groups["accepted"].Value);
            var rejectedShares = ParseCount(shareSummary.Groups["stale"].Value)
                + ParseCount(shareSummary.Groups["low"].Value)
                + ParseCount(shareSummary.Groups["duplicate"].Value);
            current = current with { Accepted = acceptedShares, Rejected = rejectedShares };
            poolCounterHandled = true;
        }
        else if (ShareAccepted().IsMatch(line))
        {
            current = current with { Accepted = current.Accepted + 1 };
            poolCounterHandled = true;
        }
        else if (ShareRejected().IsMatch(line))
        {
            current = current with { Rejected = current.Rejected + 1 };
            poolCounterHandled = true;
        }

        var hash = Hashrate().Match(line);
        var accepted = Accepted().Match(line);
        var acceptedPrefix = AcceptedPrefix().Match(line);
        var rejected = Rejected().Match(line);
        var rejectedPrefix = RejectedPrefix().Match(line);
        var value = current.HashrateMh;
        if (hash.Success && double.TryParse(hash.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            var converted = n * UnitFactor(hash.Groups[2].Value);
            if (double.IsFinite(converted) && converted >= 0) value = converted;
        }
        var acceptedValue = current.Accepted;
        var rejectedValue = current.Rejected;
        var acceptedText = accepted.Success ? accepted.Groups[1].Value : acceptedPrefix.Success ? acceptedPrefix.Groups[1].Value : "";
        var rejectedText = rejected.Success ? rejected.Groups[1].Value : rejectedPrefix.Success ? rejectedPrefix.Groups[1].Value : "";
        if (!poolCounterHandled && long.TryParse(acceptedText, NumberStyles.None, CultureInfo.InvariantCulture, out var acceptedCount)) acceptedValue = acceptedCount;
        if (!poolCounterHandled && long.TryParse(rejectedText, NumberStyles.None, CultureInfo.InvariantCulture, out var rejectedCount)) rejectedValue = rejectedCount;
        return current with
        {
            HashrateMh = value,
            Accepted = acceptedValue,
            Rejected = rejectedValue
        };
    }
    private static long ParseCount(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;
    private static double UnitFactor(string unit) => unit.ToUpperInvariant() switch { "H" => 0.000001, "KH" => 0.001, "GH" => 1000, "TH" => 1_000_000, _ => 1 };
    [GeneratedRegex(@"(?i)(?:hashrate|speed|rate)[^0-9]*([0-9]+(?:[.,][0-9]+)?)\s*(H|KH|MH|GH|TH)(?:/s|s)?")]
    private static partial Regex Hashrate();
    [GeneratedRegex(@"(?i)(?:accepted|acc(?:epted)?)[^0-9]*([0-9]+)")]
    private static partial Regex Accepted();
    [GeneratedRegex(@"(?i)\b([0-9]+)\s+accepted\s+blocks?\b")]
    private static partial Regex AcceptedPrefix();
    [GeneratedRegex(@"(?i)(?:rejected|rej(?:ected)?)[^0-9]*([0-9]+)")]
    private static partial Regex Rejected();
    [GeneratedRegex(@"(?i)\b([0-9]+)\s+rejected\s+blocks?\b")]
    private static partial Regex RejectedPrefix();
    [GeneratedRegex(@"(?i)\bShares:\s*(?:Accepted:\s*(?<accepted>\d+)\s*)?(?:Stale:\s*(?<stale>\d+)\s*)?(?:Low difficulty:\s*(?<low>\d+)\s*)?(?:Duplicate:\s*(?<duplicate>\d+)\s*)?Pending:\s*\d+")]
    private static partial Regex ShareSummary();
    [GeneratedRegex(@"(?i)\bShare accepted\b")]
    private static partial Regex ShareAccepted();
    [GeneratedRegex(@"(?i)\b(?:Share rejected by pool|Stale share|Duplicate share|Low difficulty share)\b")]
    private static partial Regex ShareRejected();
}
public sealed record MinerStats(double HashrateMh = 0, long Accepted = 0, long Rejected = 0);
