namespace Ladder.Tests;

public sealed class LadderBookTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_UpdatesBothSidesFromTheStrengthsBeforeTheAnswer()
    {
        LadderBook book = LadderBook.Empty.Apply(Duel("a", "b", DuelOutcome.LeftWins, T0));

        Strength expectedLeft = Glicko2.Update(Strength.Unrated, new[] { Strength.Unrated }, new[] { 1.0 });
        Strength expectedRight = Glicko2.Update(Strength.Unrated, new[] { Strength.Unrated }, new[] { 0.0 });
        Close(expectedLeft, book.Find("a")!.Strength);
        Close(expectedRight, book.Find("b")!.Strength);
        Assert.Equal(1, book.Find("a")!.ComparisonCount);
        Assert.Equal(T0, book.Find("a")!.LastComparedUtc);
    }

    [Fact]
    public void Apply_UsesCalendarWidenedDeviationOnTheNextAnswer()
    {
        LadderBook first = LadderBook.Empty.Apply(Duel("a", "b", DuelOutcome.LeftWins, T0));
        DateTime later = T0.AddDays(730);
        Strength leftWide = Glicko2.Widen(first.Find("a")!.Strength, 730);
        Strength rightWide = Glicko2.Widen(first.Find("b")!.Strength, 730);

        LadderBook second = first.Apply(Duel("a", "b", DuelOutcome.Draw, later, ComparisonMode.Sharpen));

        Close(Glicko2.Update(leftWide, new[] { rightWide }, new[] { 0.5 }), second.Find("a")!.Strength);
        Close(Glicko2.Update(rightWide, new[] { leftWide }, new[] { 0.5 }), second.Find("b")!.Strength);
        Assert.Equal(later, second.Find("b")!.LastComparedUtc);
    }

    [Fact]
    public void Replay_ReproducesTheCacheIncludingQuietTimeBetweenAnswers()
    {
        LadderBook book = LadderBook.Empty
            .Apply(Duel("a", "b", DuelOutcome.LeftWins, T0))
            .Apply(Duel("b", "c", DuelOutcome.RightWins, T0.AddDays(40)))
            .Apply(Duel("a", "c", DuelOutcome.Draw, T0.AddDays(800), ComparisonMode.Sharpen));

        LadderBook replayed = LadderBook.Replay(book.Duels);

        Assert.Equal(book.Tracks.Count, replayed.Tracks.Count);
        foreach (TrackState track in book.Tracks)
        {
            TrackState again = replayed.Find(track.Url)!;
            Close(track.Strength, again.Strength);
            Assert.Equal(track.ComparisonCount, again.ComparisonCount);
            Assert.Equal(track.Title, again.Title);
            Assert.Equal(track.LastComparedUtc, again.LastComparedUtc);
        }
    }

    [Fact]
    public void Rank_OrdersTheWinnerAboveTheLoserAndReportsAnOverlapWhileBothAreNew()
    {
        LadderBook book = LadderBook.Empty.Apply(Duel("a", "b", DuelOutcome.LeftWins, T0));

        IReadOnlyList<RankedTrack> rank = book.Rank(T0);

        Assert.Equal(new[] { "a", "b" }, rank.Select(row => row.Track.Url).ToArray());
        Assert.Equal(1, rank[0].Rank);
        Assert.Equal(RatingText.OverlappingNext, rank[0].Gap);
        Assert.Equal(string.Empty, rank[1].Gap);
    }

    [Fact]
    public void Rank_CallsALargeSettledGapClear()
    {
        LadderBook book = LadderBook.Empty;
        for (int i = 0; i < 40; i++)
        {
            book = book.Apply(Duel("a", "b", DuelOutcome.LeftWins, T0.AddMinutes(i)));
        }

        RankedTrack strongest = book.Rank(T0.AddMinutes(40))[0];

        Assert.Equal("a", strongest.Track.Url);
        Assert.Equal(RatingText.ClearOfNext, strongest.Gap);
    }

    [Fact]
    public void Apply_KeepsAKnownNameWhenALaterSnapshotHasAnEmptyTitle()
    {
        var named = new TrackSnapshot("a", "First Title", "Ada", "Album");
        var blank = new TrackSnapshot("a", "", "", "");
        var other = new TrackSnapshot("b", "Other", "Bea", "Album");
        LadderBook book = LadderBook.Empty
            .Apply(new DuelRecord(T0, named, other, DuelOutcome.LeftWins, ComparisonMode.Place))
            .Apply(new DuelRecord(T0.AddDays(1), blank, other, DuelOutcome.Draw, ComparisonMode.Place));

        Assert.Equal("First Title", book.Find("a")!.Title);
        Assert.Equal("Ada", book.Find("a")!.Artist);
        Assert.Equal(string.Empty, book.Duels[1].Left.Title);
    }

    [Fact]
    public void Apply_ReplacesANameWhenTheNewSnapshotHasOne()
    {
        LadderBook book = LadderBook.Empty
            .Apply(Duel("a", "b", DuelOutcome.Draw, T0))
            .Apply(new DuelRecord(
                T0.AddDays(1),
                new TrackSnapshot("a", "Renamed", "Ada", "Album"),
                new TrackSnapshot("b", "Bee", "Bea", "Album"),
                DuelOutcome.LeftWins,
                ComparisonMode.Sharpen));

        Assert.Equal("Renamed", book.Find("a")!.Title);
    }

    [Fact]
    public void Duel_RejectsComparingATrackWithItself()
    {
        var track = new TrackSnapshot("a", "A", "Ada", "Album");
        Assert.Throws<ArgumentException>(() => new DuelRecord(T0, track, track, DuelOutcome.Draw, ComparisonMode.Place));
    }

    [Fact]
    public void AsUtc_TreatsAnUnspecifiedClockAsUtcAndConvertsLocal()
    {
        var unspecified = new DateTime(2024, 5, 1, 8, 0, 0, DateTimeKind.Unspecified);
        var local = new DateTime(2024, 5, 1, 8, 0, 0, DateTimeKind.Local);

        Assert.Equal(DateTimeKind.Utc, LadderTime.AsUtc(unspecified).Kind);
        Assert.Equal(unspecified.Ticks, LadderTime.AsUtc(unspecified).Ticks);
        Assert.Equal(local.ToUniversalTime(), LadderTime.AsUtc(local));
    }

    [Fact]
    public void Rank_WidensTheDisplayedBandWithoutChangingTheStoredRating()
    {
        LadderBook book = LadderBook.Empty.Apply(Duel("a", "b", DuelOutcome.LeftWins, T0));
        string atAnswer = book.Rank(T0)[0].ScoreText;
        string twoYearsLater = book.Rank(T0.AddDays(730))[0].ScoreText;

        Assert.Equal(book.Find("a")!.Strength.Rating, book.Rank(T0.AddDays(730))[0].Track.Strength.Rating);
        Assert.NotEqual(atAnswer, twoYearsLater);
    }

    private static DuelRecord Duel(
        string left,
        string right,
        DuelOutcome outcome,
        DateTime utc,
        ComparisonMode mode = ComparisonMode.Place)
    {
        return new DuelRecord(
            utc,
            new TrackSnapshot(left, left.ToUpperInvariant(), "Artist", "Album"),
            new TrackSnapshot(right, right.ToUpperInvariant(), "Artist", "Album"),
            outcome,
            mode);
    }

    private static void Close(Strength expected, Strength actual)
    {
        Assert.Equal(expected.Rating, actual.Rating);
        Assert.Equal(expected.Deviation, actual.Deviation);
        Assert.Equal(expected.Volatility, actual.Volatility);
    }
}
