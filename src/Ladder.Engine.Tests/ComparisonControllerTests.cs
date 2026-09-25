namespace Ladder.Tests;

public sealed class ComparisonControllerTests
{
    private static readonly DateTime T0 = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Place_OnAnEmptyLadder_AsksForASecondTrack()
    {
        var controller = new ComparisonController(LadderBook.Empty);

        controller.Place(Snap("n"), T0);

        Assert.True(controller.NeedsAnotherTrack);
        Assert.Null(controller.Current);
        Assert.Empty(controller.Book.Duels);
    }

    [Fact]
    public void Place_ThenAnswers_EndsInASharpenQuestion()
    {
        LadderBook seed = LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.LeftWins));
        var controller = new ComparisonController(seed);
        controller.Place(Snap("n"), T0);

        int guard = 0;
        while (controller.Current is ComparisonPrompt prompt && prompt.Mode == ComparisonMode.Place)
        {
            Assert.Equal("n", prompt.Left.Url);
            controller.Choose(ComparisonChoice.Left, T0.AddMinutes(++guard));
            Assert.True(guard < 12);
        }

        Assert.NotNull(controller.Current);
        Assert.Equal(ComparisonMode.Sharpen, controller.Current!.Mode);
        Assert.True(controller.Book.Duels.Count > seed.Duels.Count);
    }

    [Fact]
    public void Choose_OnASharpenQuestion_RecordsTheLeftCardAsTheWinner()
    {
        var controller = new ComparisonController(
            LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.Draw)));
        controller.Sharpen(T0);
        ComparisonPrompt prompt = controller.Current!;
        int before = controller.Book.Duels.Count;

        controller.Choose(ComparisonChoice.Left, T0.AddMinutes(1));

        DuelRecord recorded = controller.Book.Duels[before];
        Assert.Equal(prompt.Left.Url, recorded.Left.Url);
        Assert.Equal(prompt.Right.Url, recorded.Right.Url);
        Assert.Equal(DuelOutcome.LeftWins, recorded.Outcome);
        Assert.Equal(ComparisonMode.Sharpen, recorded.Mode);
    }

    [Fact]
    public void Skip_DoesNotRecordASharpenDuelAndDoesNotAskThatPairNext()
    {
        LadderBook seed = LadderBook.Empty
            .Apply(Match("A", "B", DuelOutcome.Draw))
            .Apply(Match("C", "D", DuelOutcome.Draw, 1));
        var controller = new ComparisonController(seed);
        controller.Sharpen(T0);
        string skipped = SharpenPicker.PairKey(controller.Current!.Left.Url, controller.Current.Right.Url);

        controller.Skip(T0);

        Assert.Equal(seed.Duels.Count, controller.Book.Duels.Count);
        Assert.NotNull(controller.Current);
        Assert.NotEqual(skipped, SharpenPicker.PairKey(controller.Current!.Left.Url, controller.Current.Right.Url));
    }

    [Fact]
    public void Choose_WithoutAQuestion_Throws()
    {
        var controller = new ComparisonController(LadderBook.Empty);
        Assert.Throws<InvalidOperationException>(() => controller.Choose(ComparisonChoice.Same, T0));
    }

    private static TrackSnapshot Snap(string url)
    {
        return new TrackSnapshot(url, url, "Artist", "Album");
    }

    private static DuelRecord Match(string left, string right, DuelOutcome outcome, int minute = 0)
    {
        return new DuelRecord(
            T0.AddMinutes(minute),
            Snap(left),
            Snap(right),
            outcome,
            ComparisonMode.Place);
    }
}
