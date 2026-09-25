namespace Ladder.Tests;

public sealed class NextComparisonTests
{
    [Fact]
    public void Plan_WithAnEmptyLadder_PairsThePlayingTrackWithAnother()
    {
        NextComparisonPlan plan = NextComparison.Plan(
            LadderBook.Empty,
            new[] { "a", "b", "c" },
            "b",
            null);

        Assert.Equal(NextComparisonKind.Pair, plan.Kind);
        Assert.Equal("b", plan.LeftUrl);
        Assert.Equal("a", plan.RightUrl);
    }

    [Fact]
    public void Plan_SkipsAPairTheListenerAlreadyPassed()
    {
        string skipped = SharpenPicker.PairKey("b", "a");
        NextComparisonPlan plan = NextComparison.Plan(
            LadderBook.Empty,
            new[] { "a", "b", "c" },
            "b",
            new[] { skipped });

        Assert.Equal(NextComparisonKind.Pair, plan.Kind);
        Assert.Equal("b", plan.LeftUrl);
        Assert.Equal("c", plan.RightUrl);
    }

    [Fact]
    public void Plan_WithOneRatedTrack_PlacesTheNextUnratedTrack()
    {
        LadderBook book = LadderBook.Empty.Apply(new DuelRecord(
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new TrackSnapshot("a", "A", "Ada", "Album"),
            new TrackSnapshot("b", "B", "Bea", "Album"),
            DuelOutcome.LeftWins,
            ComparisonMode.Place));

        NextComparisonPlan plan = NextComparison.Plan(book, new[] { "a", "b", "c" }, null, null);

        Assert.Equal(NextComparisonKind.Place, plan.Kind);
        Assert.Equal("c", plan.LeftUrl);
    }

    [Fact]
    public void Plan_WhenEveryTrackIsRated_Sharpens()
    {
        LadderBook book = LadderBook.Empty.Apply(new DuelRecord(
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new TrackSnapshot("a", "A", "Ada", "Album"),
            new TrackSnapshot("b", "B", "Bea", "Album"),
            DuelOutcome.Draw,
            ComparisonMode.Place));

        NextComparisonPlan plan = NextComparison.Plan(book, new[] { "a", "b" }, null, null);

        Assert.Equal(NextComparisonKind.Sharpen, plan.Kind);
    }

    [Fact]
    public void Plan_WithASingleTrack_HasNothingToAsk()
    {
        NextComparisonPlan plan = NextComparison.Plan(LadderBook.Empty, new[] { "only" }, null, null);

        Assert.Equal(NextComparisonKind.None, plan.Kind);
    }
}
