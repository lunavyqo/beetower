namespace Ladder.Tests;

public sealed class RatingTextTests
{
    [Fact]
    public void Format_RoundsThePaperExampleToAnIntegerBand()
    {
        var player = new Strength(1500, 200, 0.06);
        Strength updated = Glicko2.Update(
            player,
            new[]
            {
                new Strength(1400, 30, 0.06),
                new Strength(1550, 100, 0.06),
                new Strength(1700, 300, 0.06),
            },
            new[] { 1.0, 0.0, 0.0 });

        Assert.Equal("1464 ± 303", RatingText.Format(updated));
    }

    [Fact]
    public void Gap_RequiresTheRatingGapToExceedTwiceTheCombinedDeviation()
    {
        var tight = new Strength(1600, 10, 0.06);
        var close = new Strength(1560, 10, 0.06);
        var far = new Strength(1400, 10, 0.06);

        Assert.Equal(RatingText.OverlappingNext, RatingText.Gap(tight, close));
        Assert.Equal(RatingText.ClearOfNext, RatingText.Gap(tight, far));
        Assert.Equal(string.Empty, RatingText.Gap(tight, null));
    }

    [Fact]
    public void NotPlaced_IsTheLabelForATrackWithNoAnswers()
    {
        Assert.Equal("not placed", RatingText.NotPlaced);
    }
}
