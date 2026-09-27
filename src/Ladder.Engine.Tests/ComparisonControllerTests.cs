namespace Ladder.Tests;

public sealed class ComparisonControllerTests
{
    private static readonly DateTime T0 = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PlacementProgress_StartsEmptyAndAdvancesAfterAnAnswer()
    {
        LadderBook seed = LadderBook.Empty
            .Apply(Match("A", "B", DuelOutcome.LeftWins))
            .Apply(Match("B", "C", DuelOutcome.LeftWins, 1))
            .Apply(Match("A", "C", DuelOutcome.LeftWins, 2));
        var controller = new ComparisonController(seed);
        controller.Place(Snap("n"), T0);

        Assert.Equal(0, controller.PlacementProgress.GetValueOrDefault(-1));
        controller.Choose(ComparisonChoice.Left, T0.AddMinutes(3));
        Assert.True(controller.PlacementProgress.GetValueOrDefault(0) > 0);
    }

    [Fact]
    public void Place_OnAnEmptyLadder_AsksForASecondTrack()
    {
        var controller = new ComparisonController(LadderBook.Empty);

        controller.Place(Snap("n"), T0);

        Assert.True(controller.NeedsAnotherTrack);
        Assert.Null(controller.Current);
        Assert.Null(controller.PlacementProgress);
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

        Assert.Null(controller.Current);
        Assert.True(controller.Book.Duels.Count > seed.Duels.Count);
    }

    [Fact]
    public void ShowPair_RecordsTheChoiceAndThenWaits()
    {
        var controller = new ComparisonController(LadderBook.Empty);
        controller.ShowPair(Snap("a"), Snap("b"));

        RunFinish finish = controller.Choose(ComparisonChoice.Right, T0);

        Assert.True(finish.Completed);
        Assert.Equal("a", finish.Focus!.Url);
        Assert.Equal("b", finish.Other!.Url);
        Assert.Null(controller.Current);
        Assert.Equal("b", controller.Book.Duels[0].Right.Url);
        Assert.Equal(DuelOutcome.RightWins, controller.Book.Duels[0].Outcome);
    }

    [Fact]
    public void Choose_OnASharpenQuestion_RecordsTheLeftCardAsTheWinner()
    {
        var controller = new ComparisonController(
            LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.Draw)));
        controller.Sharpen(T0);
        ComparisonPrompt prompt = controller.Current!;
        int before = controller.Book.Duels.Count;

        RunFinish finish = controller.Choose(ComparisonChoice.Left, T0.AddMinutes(1));

        Assert.False(finish.Completed);
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

        RunFinish finish = controller.Skip(T0);

        Assert.False(finish.Completed);
        Assert.Equal(seed.Duels.Count, controller.Book.Duels.Count);
        Assert.NotNull(controller.Current);
        Assert.NotEqual(skipped, SharpenPicker.PairKey(controller.Current!.Left.Url, controller.Current.Right.Url));
    }

    [Fact]
    public void Choose_SignalsTheFocusWhenItsPlacementRunFinishes()
    {
        var controller = new ComparisonController(LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.LeftWins)));
        controller.Place(Snap("n"), T0);

        bool sawQuestion = false;
        RunFinish last = RunFinish.None;
        while (controller.Current is ComparisonPrompt prompt && prompt.Mode == ComparisonMode.Place)
        {
            sawQuestion = true;
            last = controller.Choose(ComparisonChoice.Left, T0.AddMinutes(controller.Book.Duels.Count + 1));
        }

        Assert.True(sawQuestion);
        Assert.True(last.Completed);
        Assert.Equal("n", last.Focus!.Url);
        Assert.Null(last.Other);
        Assert.Null(controller.Current);
    }

    [Fact]
    public void Choose_DoesNotFinishALongerRunOnTheFirstAnswer()
    {
        LadderBook seed = LadderBook.Empty
            .Apply(Match("A", "B", DuelOutcome.LeftWins))
            .Apply(Match("B", "C", DuelOutcome.LeftWins, 1))
            .Apply(Match("C", "D", DuelOutcome.LeftWins, 2));
        var controller = new ComparisonController(seed);
        controller.Place(Snap("n"), T0);

        RunFinish first = controller.Choose(ComparisonChoice.Left, T0.AddMinutes(4));

        Assert.False(first.Completed);
        Assert.Equal(ComparisonMode.Place, controller.Current!.Mode);
    }

    [Fact]
    public void Skip_SignalsWhenThatSkipEndsTheRun()
    {
        var controller = new ComparisonController(LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.LeftWins)));
        controller.Place(Snap("A"), T0);

        RunFinish finish = controller.Skip(T0);

        Assert.True(finish.Completed);
        Assert.Equal("A", finish.Focus!.Url);
        Assert.Null(controller.Current);
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
