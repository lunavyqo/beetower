namespace Ladder.Tests;

public sealed class LadderFileTests
{
    private static readonly DateTime T0 = new DateTime(2024, 5, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);

    [Fact]
    public void Load_MissingFile_IsAnEmptyLadderAndDoesNotCreateIt()
    {
        string path = NewPath();
        try
        {
            LadderBook book = LadderFile.Load(path);
            Assert.Same(LadderBook.Empty, book);
            Assert.False(File.Exists(path));
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void SaveAndLoad_ReplaysStrengthsAndEscapesWindowsPaths()
    {
        string path = NewPath();
        try
        {
            var left = new TrackSnapshot("C:\\Music\\a.flac", "Say \"hi\"", "Ada\tLovelace", "Al\nbum");
            var right = new TrackSnapshot("C:\\Music\\b.flac", "Second", "Bea", "Album");
            LadderBook book = LadderBook.Empty.Apply(
                new DuelRecord(T0, left, right, DuelOutcome.LeftWins, ComparisonMode.Place));

            LadderFile.Save(path, book);
            string json = File.ReadAllText(path);
            Assert.Contains("C:\\\\Music\\\\a.flac", json);
            Assert.Contains("Say \\\"hi\\\"", json);
            Assert.DoesNotContain("\"rating\"", json);

            LadderBook loaded = LadderFile.Load(path);
            TrackState again = loaded.Find(left.Url)!;
            Assert.Equal(book.Find(left.Url)!.Strength.Rating, again.Strength.Rating);
            Assert.Equal(book.Find(left.Url)!.Strength.Deviation, again.Strength.Deviation);
            Assert.Equal("Say \"hi\"", again.Title);
            Assert.Equal("Ada\tLovelace", again.Artist);
            Assert.Equal("Al\nbum", again.Album);
            Assert.Equal(T0, loaded.Duels[0].Utc);
            Assert.Equal(DateTimeKind.Utc, loaded.Duels[0].Utc.Kind);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void Save_ReplacesThePreviousFile()
    {
        string path = NewPath();
        try
        {
            LadderFile.Save(path, LadderBook.Empty.Apply(Sample(DuelOutcome.LeftWins)));
            LadderFile.Save(path, LadderBook.Empty.Apply(Sample(DuelOutcome.Draw)));

            Assert.Equal(DuelOutcome.Draw, LadderFile.Load(path).Duels[0].Outcome);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void Load_RejectsACorruptFileWithoutDeletingIt()
    {
        string path = NewPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "nope");

            LadderFileException error = Assert.Throws<LadderFileException>(() => LadderFile.Load(path));

            Assert.Equal(path, error.FilePath);
            Assert.Equal("nope", File.ReadAllText(path));
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void Load_RejectsAnUnknownVersionAndAnUnknownField()
    {
        string path = NewPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{\n  \"duels\": [],\n  \"version\": 2\n}\n");
            Assert.Throws<LadderFileException>(() => LadderFile.Load(path));

            File.WriteAllText(path, "{\n  \"version\": 1,\n  \"duels\": [],\n  \"extra\": 1\n}\n");
            Assert.Throws<LadderFileException>(() => LadderFile.Load(path));
        }
        finally
        {
            Delete(path);
        }
    }

    private static DuelRecord Sample(DuelOutcome outcome)
    {
        return new DuelRecord(
            T0,
            new TrackSnapshot("a", "A", "Ada", "Album"),
            new TrackSnapshot("b", "B", "Bea", "Album"),
            outcome,
            ComparisonMode.Sharpen);
    }

    private static string NewPath()
    {
        return Path.Combine(Path.GetTempPath(), "ladder-tests-" + Guid.NewGuid().ToString("N"), "ladder.json");
    }

    private static void Delete(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
