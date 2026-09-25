namespace Ladder;

public enum DuelOutcome
{
    LeftWins = 0,
    RightWins = 1,
    Draw = 2,
}

public enum ComparisonMode
{
    Place = 0,
    Sharpen = 1,
}

/// <summary>One stored answer. Duels are the source of truth for a ladder.</summary>
public sealed class DuelRecord
{
    public DuelRecord(DateTime utc, TrackSnapshot left, TrackSnapshot right, DuelOutcome outcome, ComparisonMode mode)
    {
        if (left is null)
        {
            throw new ArgumentNullException(nameof(left));
        }

        if (right is null)
        {
            throw new ArgumentNullException(nameof(right));
        }

        if (string.Equals(left.Url, right.Url, StringComparison.Ordinal))
        {
            throw new ArgumentException("A track cannot be compared with itself.");
        }

        if (!Enum.IsDefined(typeof(DuelOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (!Enum.IsDefined(typeof(ComparisonMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        Utc = LadderTime.AsUtc(utc);
        Left = left;
        Right = right;
        Outcome = outcome;
        Mode = mode;
    }

    public DateTime Utc { get; }

    public TrackSnapshot Left { get; }

    public TrackSnapshot Right { get; }

    public DuelOutcome Outcome { get; }

    public ComparisonMode Mode { get; }

    public double ScoreFor(string url)
    {
        if (string.Equals(url, Left.Url, StringComparison.Ordinal))
        {
            return ScoreForLeft;
        }

        if (string.Equals(url, Right.Url, StringComparison.Ordinal))
        {
            return 1.0 - ScoreForLeft;
        }

        throw new ArgumentException("That URL is not in this duel.", nameof(url));
    }

    private double ScoreForLeft
    {
        get
        {
            switch (Outcome)
            {
                case DuelOutcome.LeftWins:
                    return 1;
                case DuelOutcome.RightWins:
                    return 0;
                case DuelOutcome.Draw:
                    return 0.5;
                default:
                    throw new InvalidOperationException("Unknown duel outcome.");
            }
        }
    }
}
