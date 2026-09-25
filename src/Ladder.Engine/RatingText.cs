using System.Globalization;

namespace Ladder;

public static class RatingText
{
    public const string NotPlaced = "not placed";
    public const string ClearOfNext = "clear of the next track";
    public const string OverlappingNext = "overlapping the next track";

    /// <summary>0 when a rating is as wide as a new track, 1 when it has reached the deviation floor.</summary>
    public static double Settled(double deviation)
    {
        double span = Glicko2.DeviationCap - Glicko2.DeviationFloor;
        if (span <= 0)
        {
            return 1;
        }

        double value = (Glicko2.DeviationCap - deviation) / span;
        if (value < 0)
        {
            return 0;
        }

        if (value > 1)
        {
            return 1;
        }

        return value;
    }

    public static string Format(Strength strength)
    {
        if (strength is null)
        {
            throw new ArgumentNullException(nameof(strength));
        }

        int rating = Round(strength.Rating);
        int band = Round(2 * strength.Deviation);
        return rating.ToString(CultureInfo.InvariantCulture) + " ± " + band.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Compares a stronger track with the next-weaker one, using calendar-widened strengths.
    /// </summary>
    public static string Gap(Strength stronger, Strength? nextWeaker)
    {
        if (stronger is null)
        {
            throw new ArgumentNullException(nameof(stronger));
        }

        if (nextWeaker is null)
        {
            return string.Empty;
        }

        double gap = stronger.Rating - nextWeaker.Rating;
        double limit = 2 * (stronger.Deviation + nextWeaker.Deviation);
        return gap > limit ? ClearOfNext : OverlappingNext;
    }

    private static int Round(double value)
    {
        return (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
