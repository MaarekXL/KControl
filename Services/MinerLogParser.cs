using System.Globalization;
using System.Text.RegularExpressions;

namespace KeryxControl.Services;

public sealed partial class MinerLogParser
{
    public MinerStats Parse(string line, MinerStats current)
    {
        var hash = Hashrate().Match(line);
        var accepted = Accepted().Match(line);
        var rejected = Rejected().Match(line);
        var value = current.HashrateMh;
        if (hash.Success && double.TryParse(hash.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            var converted = n * UnitFactor(hash.Groups[2].Value);
            if (double.IsFinite(converted) && converted >= 0) value = converted;
        }
        var acceptedValue = current.Accepted;
        var rejectedValue = current.Rejected;
        if (accepted.Success && int.TryParse(accepted.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var acceptedCount)) acceptedValue = acceptedCount;
        if (rejected.Success && int.TryParse(rejected.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var rejectedCount)) rejectedValue = rejectedCount;
        return current with
        {
            HashrateMh = value,
            Accepted = acceptedValue,
            Rejected = rejectedValue
        };
    }
    private static double UnitFactor(string unit) => unit.ToUpperInvariant() switch { "H" => 0.000001, "KH" => 0.001, "GH" => 1000, "TH" => 1_000_000, _ => 1 };
    [GeneratedRegex(@"(?i)(?:hashrate|speed|rate)[^0-9]*([0-9]+(?:[.,][0-9]+)?)\s*(H|KH|MH|GH|TH)(?:/s|s)?")]
    private static partial Regex Hashrate();
    [GeneratedRegex(@"(?i)(?:accepted|acc(?:epted)?)[^0-9]*([0-9]+)")]
    private static partial Regex Accepted();
    [GeneratedRegex(@"(?i)(?:rejected|rej(?:ected)?)[^0-9]*([0-9]+)")]
    private static partial Regex Rejected();
}
public sealed record MinerStats(double HashrateMh = 0, int Accepted = 0, int Rejected = 0);
