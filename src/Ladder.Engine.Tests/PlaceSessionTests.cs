namespace Ladder.Tests;

public sealed class PlaceSessionTests
{
    private static readonly DateTime T0 = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_WithNobodyElse_AsksNothingAndWritesNothing()
    {
        LadderBook book = LadderBook.Empty;
        PlaceSession session = PlaceSession.Start(book, Snap("n"), T0);

        Assert.True(session.NeedsAnotherTrack);
        Assert.True(session.IsFinished);
        Assert.Null(session.Current);
        Assert.Same(book, session.Book);
    }

    [Fact]
    public void Rank_IsWithheldUntilTheRunFinishes()
    {
        PlaceSession session = PlaceSession.Start(Trio(), Snap("n"), T0);
        session = session.Answer(PlaceAnswer.FocusWins, T0.AddMinutes(1));

        Assert.False(session.IsFinished);
        Assert.DoesNotContain(session.Book.Rank(T0), row => row.Track.Url == "n");

        while (!session.IsFinished)
        {
            session = session.Answer(PlaceAnswer.FocusWins, T0.AddMinutes(2));
        }

        Assert.Contains(session.Book.Rank(T0), row => row.Track.Url == "n");
    }

    [Fact]
    public void Answers_WalkTowardTheStrongEndThenStop()
    {
        PlaceSession session = PlaceSession.Start(Trio(), Snap("n"), T0);

        Assert.Equal("B", session.Current!.Right.Url);
        session = session.Answer(PlaceAnswer.FocusWins, T0.AddMinutes(1));
        Assert.Equal("A", session.Current!.Right.Url);
        session = session.Answer(PlaceAnswer.FocusWins, T0.AddMinutes(2));

        Assert.True(session.IsFinished);
        Assert.Equal(new[] { "B", "A" }, session.Book.Duels.Skip(3).Select(duel => duel.Right.Url).ToArray());
        Assert.All(session.Book.Duels.Skip(3), duel => Assert.Equal(ComparisonMode.Place, duel.Mode));
        Assert.Equal("n", session.Book.Rank(T0)[0].Track.Url);
    }

    [Fact]
    public void Answers_WalkTowardTheWeakEnd()
    {
        PlaceSession session = PlaceSession.Start(Trio(), Snap("n"), T0);

        session = session.Answer(PlaceAnswer.FocusLoses, T0.AddMinutes(1));
        Assert.Equal("C", session.Current!.Right.Url);
        session = session.Answer(PlaceAnswer.FocusLoses, T0.AddMinutes(2));

        Assert.True(session.IsFinished);
        Assert.Equal("n", session.Book.Rank(T0)[session.Book.Rank(T0).Count - 1].Track.Url);
    }

    [Fact]
    public void Draw_EndsTheSearchAndDoesNotAskThatPivotAgain()
    {
        PlaceSession session = PlaceSession.Start(Trio(), Snap("n"), T0);
        string pivot = session.Current!.Right.Url;

        session = session.Answer(PlaceAnswer.Draw, T0.AddMinutes(1));
        var followed = new List<string>();
        while (session.Current is ComparisonPrompt prompt)
        {
            followed.Add(prompt.Right.Url);
            Assert.Equal("n", prompt.Left.Url);
            Assert.Equal(ComparisonMode.Place, prompt.Mode);
            session = session.Answer(PlaceAnswer.FocusWins, T0.AddMinutes(2 + followed.Count));
        }

        Assert.DoesNotContain(pivot, followed);
        Assert.InRange(followed.Count, 0, 2);
        Assert.Equal(DuelOutcome.Draw, session.Book.Duels[3].Outcome);
    }

    [Fact]
    public void Skip_DropsThePivotAndRecordsNothing()
    {
        LadderBook book = Trio();
        PlaceSession session = PlaceSession.Start(book, Snap("n"), T0);

        session = session.Skip();

        Assert.Equal("A", session.Current!.Right.Url);
        Assert.Same(book, session.Book);
    }

    [Fact]
    public void Skip_AllTheWay_FinishesWithoutADuel()
    {
        LadderBook book = Trio();
        PlaceSession session = PlaceSession.Start(book, Snap("n"), T0);
        int guard = 0;
        while (!session.IsFinished)
        {
            session = session.Skip();
            Assert.True(++guard < 10);
        }

        Assert.Same(book, session.Book);
        Assert.False(session.NeedsAnotherTrack);
    }

    [Fact]
    public void Start_UsesTheCachedNameWhenTheIncomingTitleIsBlank()
    {
        PlaceSession session = PlaceSession.Start(Trio(), new TrackSnapshot("A", "", "", ""), T0);

        Assert.Equal("A-title", session.Focus.Title);
        Assert.Equal("B", session.Current!.Right.Url);
    }

    [Fact]
    public void Answer_WhenFinished_Throws()
    {
        PlaceSession session = PlaceSession.Start(LadderBook.Empty, Snap("n"), T0);
        Assert.Throws<InvalidOperationException>(() => session.Answer(PlaceAnswer.FocusWins, T0));
    }

    [Fact]
    public void EdgeQuestions_OrderTheCloserUnaskedNeighborFirst()
    {
        RankedTrack[] rank =
        {
            Row(1, "s", 1800),
            Row(2, "f", 1700),
            Row(3, "w", 1400),
        };

        Assert.Equal(new[] { "s", "w" }, Urls(PlaceEdges.Select(rank, "f", new HashSet<string>(StringComparer.Ordinal))));
        Assert.Equal(new[] { "w" }, Urls(PlaceEdges.Select(rank, "f", new HashSet<string>(StringComparer.Ordinal) { "s" })));
        Assert.Empty(PlaceEdges.Select(rank, "f", new HashSet<string>(StringComparer.Ordinal) { "s", "w" }));
        Assert.Empty(PlaceEdges.Select(rank, "missing", new HashSet<string>(StringComparer.Ordinal)));
    }

    private static string[] Urls(IReadOnlyList<TrackSnapshot> edges)
    {
        return edges.Select(edge => edge.Url).ToArray();
    }

    private static RankedTrack Row(int rank, string url, double rating)
    {
        var state = new TrackState(url, url + "-title", "Artist", "Album", new Strength(rating, 40, 0.06), 4, T0);
        return new RankedTrack(rank, state, state.Strength, string.Empty);
    }

    private static LadderBook Trio()
    {
        return LadderBook.Empty
            .Apply(Match("A", "B", DuelOutcome.LeftWins))
            .Apply(Match("A", "C", DuelOutcome.LeftWins, 1))
            .Apply(Match("B", "C", DuelOutcome.LeftWins, 2));
    }

    private static DuelRecord Match(string left, string right, DuelOutcome outcome, int minute = 0)
    {
        return new DuelRecord(
            T0.AddMinutes(minute),
            new TrackSnapshot(left, left + "-title", "Artist", "Album"),
            new TrackSnapshot(right, right + "-title", "Artist", "Album"),
            outcome,
            ComparisonMode.Place);
    }

    private static TrackSnapshot Snap(string url)
    {
        return new TrackSnapshot(url, url + "-title", "Artist", "Album");
    }
}
