namespace Ladder.Tests;

public sealed class SharpenPickerTests
{
    private static readonly DateTime T0 = new(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Choose_WithFewerThanTwoTracks_ReturnsNothing()
    {
        Assert.Null(SharpenPicker.Choose(LadderBook.Empty, T0, null));
    }

    [Fact]
    public void Choose_PutsTheStrongerTrackOnTheRightAfterAnOddNumberOfDuels()
    {
        LadderBook book = LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.LeftWins, 0));

        ComparisonPrompt prompt = SharpenPicker.Choose(book, T0, null)!;

        Assert.Equal("B", prompt.Left.Url);
        Assert.Equal("A", prompt.Right.Url);
        Assert.Equal(ComparisonMode.Sharpen, prompt.Mode);
        Assert.Null(prompt.FocusUrl);
    }

    [Fact]
    public void Choose_PrefersAnUnplayedPairAndBreaksThatTieByUrl()
    {
        LadderBook book = LadderBook.Empty
            .Apply(Match("A", "B", DuelOutcome.Draw, 0))
            .Apply(Match("C", "D", DuelOutcome.Draw, 1));

        ComparisonPrompt prompt = SharpenPicker.Choose(book, T0, null)!;

        Assert.Equal("A", prompt.Left.Url);
        Assert.Equal("C", prompt.Right.Url);
    }

    [Fact]
    public void Choose_SkipsAnExcludedPair()
    {
        LadderBook book = LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.LeftWins, 0));
        string key = SharpenPicker.PairKey("A", "B");

        Assert.Null(SharpenPicker.Choose(book, T0, new[] { key }));
    }

    [Fact]
    public void Choose_DoesNotCrossAGapOfMoreThanTenRanks()
    {
        LadderBook book = LadderBook.Empty;
        for (int i = 0; i < 11; i++)
        {
            book = book.Apply(Match(Name(i), Name(i + 1), DuelOutcome.LeftWins, i));
        }

        IReadOnlyList<RankedTrack> rank = book.Rank(T0);
        Assert.Equal(12, rank.Count);
        var inWindow = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rank.Count; i++)
        {
            int last = Math.Min(rank.Count - 1, i + SharpenPicker.NeighborWindow);
            for (int j = i + 1; j <= last; j++)
            {
                inWindow.Add(SharpenPicker.PairKey(rank[i].Track.Url, rank[j].Track.Url));
            }
        }

        Assert.Null(SharpenPicker.Choose(book, T0, inWindow));

        string kept = SharpenPicker.PairKey(rank[0].Track.Url, rank[1].Track.Url);
        inWindow.Remove(kept);
        ComparisonPrompt prompt = SharpenPicker.Choose(book, T0, inWindow)!;
        Assert.Equal(kept, SharpenPicker.PairKey(prompt.Left.Url, prompt.Right.Url));
    }

    [Fact]
    public void Choose_PrefersOneOldGameToAPairThatHasBeenSettled()
    {
        LadderBook book = LadderBook.Empty.Apply(Match("A", "B", DuelOutcome.LeftWins, 0));
        for (int i = 0; i < SharpenPicker.RecentDuelCount; i++)
        {
            book = book.Apply(Match("C", "D", DuelOutcome.LeftWins, 10 + i));
        }

        ComparisonPrompt prompt = SharpenPicker.Choose(book, T0.AddHours(1), null)!;
        Assert.Equal(SharpenPicker.PairKey("A", "B"), SharpenPicker.PairKey(prompt.Left.Url, prompt.Right.Url));
    }

    [Fact]
    public void Information_RisesWithUncertaintyAndShrinksWhenThePairWasJustAsked()
    {
        var wide = new Strength(1500, 200, 0.06);
        var narrow = new Strength(1500, 40, 0.06);
        var favorite = new Strength(1900, 40, 0.06);
        var underdog = new Strength(1100, 40, 0.06);

        double coinFlip = SharpenPicker.Information(wide, wide, recent: false);
        Assert.True(coinFlip > SharpenPicker.Information(narrow, narrow, recent: false));
        Assert.True(SharpenPicker.Information(narrow, narrow, recent: false) > SharpenPicker.Information(favorite, underdog, recent: false));
        Assert.Equal(coinFlip * SharpenPicker.RecentPairWeight, SharpenPicker.Information(wide, wide, recent: true), precision: 9);
    }

    private static string Name(int index)
    {
        return "t" + index.ToString("00");
    }

    private static DuelRecord Match(string left, string right, DuelOutcome outcome, int minute)
    {
        return new DuelRecord(
            T0.AddMinutes(minute),
            new TrackSnapshot(left, left, "Artist", "Album"),
            new TrackSnapshot(right, right, "Artist", "Album"),
            outcome,
            ComparisonMode.Sharpen);
    }
}
