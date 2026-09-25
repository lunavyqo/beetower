namespace Ladder;

/// <summary>
/// A track's estimated strength on the Glicko scale.
/// </summary>
public sealed class Strength
{
    public Strength(double rating, double deviation, double volatility)
    {
        if (!IsFinite(rating))
        {
            throw new ArgumentOutOfRangeException(nameof(rating), rating, "Rating must be finite.");
        }

        if (!IsFinite(deviation) || deviation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviation), deviation, "Deviation must be positive and finite.");
        }

        if (!IsFinite(volatility) || volatility <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volatility), volatility, "Volatility must be positive and finite.");
        }

        Rating = rating;
        Deviation = deviation;
        Volatility = volatility;
    }

    /// <summary>Higher means the listener prefers this track.</summary>
    public double Rating { get; }

    /// <summary>Uncertainty of <see cref="Rating"/>. The 95% interval is the rating ± twice this value.</summary>
    public double Deviation { get; }

    /// <summary>How quickly the rating is allowed to change when results are surprising.</summary>
    public double Volatility { get; }

    public static Strength Unrated { get; } = new(Glicko2.InitialRating, Glicko2.InitialDeviation, Glicko2.InitialVolatility);

    internal static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
