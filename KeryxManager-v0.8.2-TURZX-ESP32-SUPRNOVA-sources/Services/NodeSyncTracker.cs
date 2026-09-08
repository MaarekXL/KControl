using System.Text.RegularExpressions;

namespace KeryxControl.Services;

internal readonly record struct NodeSyncSnapshot(bool Known, bool Synchronized, int? Percent, int StabilitySecondsRemaining, bool Changed);

internal sealed partial class NodeSyncTracker
{
    private static readonly TimeSpan StableWindow = TimeSpan.FromSeconds(30);
    private DateTime _lastIbdAt;
    private DateTime? _completionCandidateAt;
    private bool _ibdActive;
    private bool _known;
    private bool _synchronized;
    private int? _percent;
    private int _stabilitySecondsRemaining;

    public NodeSyncTracker() => Reset(DateTime.UtcNow);

    public void Reset(DateTime now)
    {
        _lastIbdAt = DateTime.MinValue;
        _completionCandidateAt = null;
        _ibdActive = false;
        _known = false;
        _synchronized = false;
        _percent = null;
        _stabilitySecondsRemaining = 0;
    }

    public NodeSyncSnapshot Observe(string line, DateTime now)
    {
        var before = Snapshot(false);
        var progress = IbdProgress().Match(line);
        if (progress.Success && int.TryParse(progress.Groups[1].Value, out var value))
        {
            MarkIbd(now, Math.Clamp(value, 0, 99));
        }
        else if (IbdStarted().IsMatch(line) || SyncingAhead().IsMatch(line))
        {
            MarkIbd(now, _percent is >= 0 and < 100 ? _percent : 0);
        }
        else if (IbdCompleted().IsMatch(line))
        {
            _known = true;
            _synchronized = false;
            _percent = 99;
            _completionCandidateAt = now;
            _lastIbdAt = now;
            _ibdActive = false;
        }
        else if (LiveBlock().IsMatch(line) && !_ibdActive && _completionCandidateAt is null)
        {
            // A node which was already synchronized when started may not emit
            // an IBD completion line. A live relay block starts the same
            // conservative stability window in that case.
            _known = true;
            _synchronized = false;
            _percent = 99;
            _completionCandidateAt = now;
        }

        PromoteStableCandidate(now);
        return Snapshot(before.Known != _known || before.Synchronized != _synchronized || before.Percent != _percent
            || before.StabilitySecondsRemaining != _stabilitySecondsRemaining);
    }

    public NodeSyncSnapshot Tick(DateTime now)
    {
        var before = Snapshot(false);
        PromoteStableCandidate(now);
        return Snapshot(before.Known != _known || before.Synchronized != _synchronized || before.Percent != _percent
            || before.StabilitySecondsRemaining != _stabilitySecondsRemaining);
    }

    private void MarkIbd(DateTime now, int? percent)
    {
        _known = true;
        _synchronized = false;
        _percent = percent;
        _lastIbdAt = now;
        _completionCandidateAt = null;
        _ibdActive = true;
        _stabilitySecondsRemaining = 0;
    }

    private void PromoteStableCandidate(DateTime now)
    {
        if (_synchronized)
        {
            _stabilitySecondsRemaining = 0;
            return;
        }
        if (_completionCandidateAt is not DateTime candidate)
        {
            _stabilitySecondsRemaining = 0;
            return;
        }
        var baseline = candidate;
        if (_lastIbdAt > baseline) baseline = _lastIbdAt;
        var remaining = StableWindow - (now - baseline);
        if (remaining > TimeSpan.Zero)
        {
            _stabilitySecondsRemaining = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            return;
        }

        _known = true;
        _synchronized = true;
        _percent = 100;
        _stabilitySecondsRemaining = 0;
    }

    private NodeSyncSnapshot Snapshot(bool changed) => new(_known, _synchronized, _percent, _stabilitySecondsRemaining, changed);

    [GeneratedRegex(@"(?i)\bIBD:\s*Processed.*?\((\d{1,3})%\)")]
    private static partial Regex IbdProgress();

    [GeneratedRegex(@"(?i)\bIBD started with peer\b")]
    private static partial Regex IbdStarted();

    [GeneratedRegex(@"(?i)\bsyncing ahead from current pruning point\b")]
    private static partial Regex SyncingAhead();

    [GeneratedRegex(@"(?i)\b(?:IBD with peer .* completed successfully|IBD complete|initial block download complete)\b")]
    private static partial Regex IbdCompleted();

    [GeneratedRegex(@"(?i)\bAccepted block [0-9a-f]{64} via relay\b")]
    private static partial Regex LiveBlock();
}
