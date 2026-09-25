namespace Ladder.Tests;

public sealed class Glicko2Tests
{
    [Fact]
    public void PaperExample_MatchesPublishedResult()
    {
        var player = new Strength(1500, 200, 0.06);
        Strength[] opponents =
        [
            new Strength(1400, 30, 0.06),
            new Strength(1550, 100, 0.06),
            new Strength(1700, 300, 0.06),
        ];
        double[] scores = [1, 0, 0];

        Strength updated = Glicko2.Update(player, opponents, scores, tau: 0.5);

        Assert.InRange(updated.Rating, 1464.04, 1464.08);
        Assert.InRange(updated.Deviation, 151.50, 151.55);
        Assert.InRange(updated.Volatility, 0.05998, 0.06000);
    }

    [Fact]
    public void ExpectedScore_MatchesThePaperTable()
    {
        var player = new Strength(1500, 200, 0.06);
        var first = new Strength(1400, 30, 0.06);
        var second = new Strength(1550, 100, 0.06);
        var third = new Strength(1700, 300, 0.06);

        Assert.InRange(Glicko2.ExpectedScore(player, first), 0.638, 0.640);
        Assert.InRange(Glicko2.ExpectedScore(player, second), 0.431, 0.433);
        Assert.InRange(Glicko2.ExpectedScore(player, third), 0.302, 0.304);
    }

    [Fact]
    public void WinAndLoss_MoveEqualPlayersInOppositeDirections()
    {
        Strength winner = Glicko2.Update(Strength.Unrated, [Strength.Unrated], [1]);
        Strength loser = Glicko2.Update(Strength.Unrated, [Strength.Unrated], [0]);

        Assert.True(winner.Rating > Glicko2.InitialRating);
        Assert.True(loser.Rating < Glicko2.InitialRating);
        Assert.InRange(Math.Abs((winner.Rating - Glicko2.InitialRating) - (Glicko2.InitialRating - loser.Rating)), 0, 0.001);
        Assert.True(winner.Deviation < Glicko2.InitialDeviation);
        Assert.True(loser.Deviation < Glicko2.InitialDeviation);
    }

    [Fact]
    public void Draw_KeepsEqualPlayersNearTheirRating()
    {
        Strength updated = Glicko2.Update(Strength.Unrated, [Strength.Unrated], [0.5]);

        Assert.InRange(updated.Rating, 1499.99, 1500.01);
        Assert.True(updated.Deviation < Glicko2.InitialDeviation);
    }

    [Fact]
    public void ManyDraws_DoNotPushDeviationBelowTheFloor()
    {
        var certain = new Strength(1500, Glicko2.DeviationFloor, 0.06);
        Strength[] opponents = new Strength[200];
        double[] scores = new double[200];
        for (int i = 0; i < opponents.Length; i++)
        {
            opponents[i] = certain;
            scores[i] = 0.5;
        }

        Strength updated = Glicko2.Update(certain, opponents, scores);

        Assert.Equal(Glicko2.DeviationFloor, updated.Deviation);
    }

    [Fact]
    public void WidenDeviation_ReturnsAConfidentTrackToUnratedAfterTwoYears()
    {
        Assert.Equal(50, Glicko2.WidenDeviation(50, 0));
        Assert.Equal(50, Glicko2.WidenDeviation(50, -3));
        Assert.InRange(Glicko2.WidenDeviation(50, Glicko2.DaysToForget), 349.999, 350);
        Assert.Equal(Glicko2.DeviationCap, Glicko2.WidenDeviation(50, 5000));
    }

    [Fact]
    public void Widen_KeepsRatingAndVolatility()
    {
        var strength = new Strength(1620, 40, 0.05);

        Strength widened = Glicko2.Widen(strength, 100);

        Assert.Equal(1620, widened.Rating);
        Assert.Equal(0.05, widened.Volatility);
        Assert.True(widened.Deviation > 40);
        Assert.True(widened.Deviation <= Glicko2.DeviationCap);
    }

    [Fact]
    public void Update_RejectsAScoreOutsideTheUnitInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Glicko2.Update(Strength.Unrated, [Strength.Unrated], [1.5]));
    }
}
