namespace Ladder;

/// <summary>
/// Glicko-2 updates and the calendar widening described in docs/rating-model.md.
/// </summary>
public static class Glicko2
{
    public const double Scale = 173.7178;
    public const double DefaultTau = 0.5;
    public const double InitialRating = 1500;
    public const double InitialDeviation = 350;
    public const double InitialVolatility = 0.06;
    public const double DeviationFloor = 30;
    public const double DeviationCap = 350;
    public const double ConfidentDeviation = 50;
    public const double DaysToForget = 730;
    public const double Convergence = 0.000001;

    /// <summary>
    /// Per-day term inside <c>sqrt(RD² + d · days)</c>. Chosen so a deviation of 50
    /// returns to 350 after <see cref="DaysToForget"/> days.
    /// </summary>
    public static double InactivityDriftPerDay { get; } =
        (DeviationCap * DeviationCap - ConfidentDeviation * ConfidentDeviation) / DaysToForget;

    public static double WidenDeviation(double deviation, double elapsedDays)
    {
        if (!Strength.IsFinite(deviation) || deviation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviation));
        }

        if (!Strength.IsFinite(elapsedDays))
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedDays));
        }

        if (elapsedDays <= 0)
        {
            return Math.Min(deviation, DeviationCap);
        }

        double widened = Math.Sqrt(deviation * deviation + InactivityDriftPerDay * elapsedDays);
        return Math.Min(widened, DeviationCap);
    }

    public static Strength Widen(Strength strength, double elapsedDays)
    {
        if (strength is null)
        {
            throw new ArgumentNullException(nameof(strength));
        }

        return new Strength(strength.Rating, WidenDeviation(strength.Deviation, elapsedDays), strength.Volatility);
    }

    /// <summary>Probability that <paramref name="player"/> beats <paramref name="opponent"/>.</summary>
    public static double ExpectedScore(Strength player, Strength opponent)
    {
        if (player is null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        if (opponent is null)
        {
            throw new ArgumentNullException(nameof(opponent));
        }
        double mu = ToMu(player.Rating);
        double muOpponent = ToMu(opponent.Rating);
        double g = Weight(ToPhi(opponent.Deviation));
        return 1.0 / (1.0 + Math.Exp(-g * (mu - muOpponent)));
    }

    public static Strength Update(
        Strength player,
        IReadOnlyList<Strength> opponents,
        IReadOnlyList<double> scores,
        double tau = DefaultTau)
    {
        if (player is null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        if (opponents is null)
        {
            throw new ArgumentNullException(nameof(opponents));
        }

        if (scores is null)
        {
            throw new ArgumentNullException(nameof(scores));
        }

        if (opponents.Count == 0)
        {
            throw new ArgumentException("An update needs at least one opponent.", nameof(opponents));
        }

        if (opponents.Count != scores.Count)
        {
            throw new ArgumentException("Each opponent needs one score.", nameof(scores));
        }

        if (!Strength.IsFinite(tau) || tau <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tau));
        }

        double mu = ToMu(player.Rating);
        double phi = ToPhi(player.Deviation);
        int count = opponents.Count;
        var weight = new double[count];
        var expected = new double[count];
        for (int i = 0; i < count; i++)
        {
            Strength opponent = opponents[i] ?? throw new ArgumentException("Opponent is missing.", nameof(opponents));
            double score = scores[i];
            if (!Strength.IsFinite(score) || score < 0 || score > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scores), score, "Scores are 0, 0.5, or 1.");
            }

            weight[i] = Weight(ToPhi(opponent.Deviation));
            expected[i] = 1.0 / (1.0 + Math.Exp(-weight[i] * (mu - ToMu(opponent.Rating))));
        }

        double varianceSum = 0;
        double outcomeSum = 0;
        for (int i = 0; i < count; i++)
        {
            varianceSum += weight[i] * weight[i] * expected[i] * (1.0 - expected[i]);
            outcomeSum += weight[i] * (scores[i] - expected[i]);
        }

        if (varianceSum <= 0)
        {
            throw new InvalidOperationException("The opponents produced no rating variance.");
        }

        double variance = 1.0 / varianceSum;
        double improvement = variance * outcomeSum;
        double volatility = SolveVolatility(player.Volatility, phi, variance, improvement, tau);
        double phiStar = Math.Sqrt(phi * phi + volatility * volatility);
        double phiPrime = 1.0 / Math.Sqrt(1.0 / (phiStar * phiStar) + 1.0 / variance);
        double muPrime = mu + phiPrime * phiPrime * outcomeSum;
        double rating = Scale * muPrime + InitialRating;
        double deviation = ClampDeviation(Scale * phiPrime);
        if (!Strength.IsFinite(rating) || !Strength.IsFinite(deviation) || !Strength.IsFinite(volatility))
        {
            throw new InvalidOperationException("Glicko-2 produced a non-finite strength.");
        }

        return new Strength(rating, deviation, volatility);
    }

    private static double SolveVolatility(double sigma, double phi, double variance, double improvement, double tau)
    {
        // Illinois algorithm from Glickman 2022, step 5.
        double a = Math.Log(sigma * sigma);
        double improvementSq = improvement * improvement;
        double phiSq = phi * phi;
        double tauSq = tau * tau;
        double left = a;
        double right;
        if (improvementSq > phiSq + variance)
        {
            right = Math.Log(improvementSq - phiSq - variance);
        }
        else
        {
            int k = 1;
            while (VolatilityObjective(a - k * tau, improvementSq, phiSq, variance, a, tauSq) < 0)
            {
                k++;
                if (k > 10000)
                {
                    throw new InvalidOperationException("Could not bracket the new volatility.");
                }
            }

            right = a - k * tau;
        }

        double leftValue = VolatilityObjective(left, improvementSq, phiSq, variance, a, tauSq);
        double rightValue = VolatilityObjective(right, improvementSq, phiSq, variance, a, tauSq);
        int guard = 0;
        while (Math.Abs(right - left) > Convergence)
        {
            if (++guard > 200)
            {
                throw new InvalidOperationException("Volatility solver did not converge.");
            }

            double next = left + (left - right) * leftValue / (rightValue - leftValue);
            double nextValue = VolatilityObjective(next, improvementSq, phiSq, variance, a, tauSq);
            if (nextValue * rightValue <= 0)
            {
                left = right;
                leftValue = rightValue;
            }
            else
            {
                leftValue /= 2.0;
            }

            right = next;
            rightValue = nextValue;
        }

        return Math.Exp(left / 2.0);
    }

    private static double VolatilityObjective(
        double x,
        double improvementSq,
        double phiSq,
        double variance,
        double logSigmaSq,
        double tauSq)
    {
        double expX = Math.Exp(x);
        double denominator = phiSq + variance + expX;
        return expX * (improvementSq - phiSq - variance - expX) / (2.0 * denominator * denominator)
            - (x - logSigmaSq) / tauSq;
    }

    private static double Weight(double phi)
    {
        return 1.0 / Math.Sqrt(1.0 + 3.0 * phi * phi / (Math.PI * Math.PI));
    }

    private static double ToMu(double rating) => (rating - InitialRating) / Scale;

    private static double ToPhi(double deviation) => deviation / Scale;

    private static double ClampDeviation(double deviation)
    {
        if (deviation < DeviationFloor)
        {
            return DeviationFloor;
        }

        if (deviation > DeviationCap)
        {
            return DeviationCap;
        }

        return deviation;
    }
}
