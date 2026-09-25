namespace Ladder;

/// <summary>Cached strength for one URL. Rebuildable by replaying duels.</summary>
public sealed class TrackState
{
    internal TrackState(
        string url,
        string title,
        string artist,
        string album,
        Strength strength,
        int comparisonCount,
        DateTime? lastComparedUtc)
    {
        Url = url;
        Title = title;
        Artist = artist;
        Album = album;
        Strength = strength;
        ComparisonCount = comparisonCount;
        LastComparedUtc = lastComparedUtc;
    }

    public string Url { get; }

    public string Title { get; }

    public string Artist { get; }

    public string Album { get; }

    /// <summary>Strength stored after the last answer, before calendar widening.</summary>
    public Strength Strength { get; }

    public int ComparisonCount { get; }

    public DateTime? LastComparedUtc { get; }

    internal TrackState After(TrackSnapshot snapshot, Strength strength, DateTime utc)
    {
        return new TrackState(
            Url,
            Prefer(snapshot.Title, Title),
            Prefer(snapshot.Artist, Artist),
            Prefer(snapshot.Album, Album),
            strength,
            ComparisonCount + 1,
            utc);
    }

    internal Strength EffectiveAt(DateTime utc)
    {
        double days = 0;
        if (LastComparedUtc is DateTime last)
        {
            days = (LadderTime.AsUtc(utc) - last).TotalDays;
        }

        return Glicko2.Widen(Strength, days);
    }

    private static string Prefer(string incoming, string previous)
    {
        return incoming.Length == 0 ? previous : incoming;
    }
}
