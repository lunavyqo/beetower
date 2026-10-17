namespace Ladder.Tests;

public sealed class UnplacedLikedTests
{
    private static readonly DateTime T0 = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Select_DropsSongsThatHaveAPlaceAndKeepsTheLovedOrder()
    {
        LadderBook book = LadderBook.Empty.Apply(new DuelRecord(
            T0,
            new TrackSnapshot("placed", "Placed", "A", "Album"),
            new TrackSnapshot("other", "Other", "B", "Album"),
            DuelOutcome.LeftWins,
            ComparisonMode.Place));

        string[] unrated = UnplacedLiked.Select(new[] { "loved", "placed", "loved", "", "new" }, book);

        Assert.Equal(new[] { "loved", "new" }, unrated);
    }

    [Fact]
    public void Select_KeepsEveryLikedSongWhenTheLadderIsEmpty()
    {
        string[] unrated = UnplacedLiked.Select(new[] { "a", "b" }, LadderBook.Empty);

        Assert.Equal(new[] { "a", "b" }, unrated);
    }
}
