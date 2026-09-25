namespace Ladder;

public sealed class RankedTrack
{
    internal RankedTrack(int rank, TrackState track, Strength effective, string gap)
    {
        Rank = rank;
        Track = track;
        Effective = effective;
        Gap = gap;
    }

    public int Rank { get; }

    public TrackState Track { get; }

    /// <summary>Stored rating with the deviation widened for time since the last answer.</summary>
    public Strength Effective { get; }

    public string ScoreText => RatingText.Format(Effective);

    /// <summary>Empty for the weakest track. Otherwise how it sits against the next-weaker one.</summary>
    public string Gap { get; }
}

/// <summary>
/// Immutable ladder. <see cref="Apply"/> scores one answer from the strengths
/// both tracks had before that answer. <see cref="Replay"/> rebuilds the cache from duels.
/// </summary>
public sealed class LadderBook
{
    private readonly DuelRecord[] _duels;
    private readonly Dictionary<string, TrackState> _tracks;
    private readonly TrackState[] _trackList;

    private LadderBook(DuelRecord[] duels, Dictionary<string, TrackState> tracks)
    {
        _duels = duels;
        _tracks = tracks;
        _trackList = new TrackState[tracks.Count];
        tracks.Values.CopyTo(_trackList, 0);
        Array.Sort(_trackList, (left, right) => string.CompareOrdinal(left.Url, right.Url));
    }

    public static LadderBook Empty { get; } = new(
        Array.Empty<DuelRecord>(),
        new Dictionary<string, TrackState>(StringComparer.Ordinal));

    public IReadOnlyList<DuelRecord> Duels => Array.AsReadOnly(_duels);

    public IReadOnlyList<TrackState> Tracks => Array.AsReadOnly(_trackList);

    public TrackState? Find(string url)
    {
        if (url is null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (_tracks.TryGetValue(url, out TrackState? state))
        {
            return state;
        }

        return null;
    }

    public LadderBook Apply(DuelRecord duel)
    {
        if (duel is null)
        {
            throw new ArgumentNullException(nameof(duel));
        }

        var tracks = new Dictionary<string, TrackState>(_tracks, StringComparer.Ordinal);
        var duels = new DuelRecord[_duels.Length + 1];
        Array.Copy(_duels, duels, _duels.Length);
        duels[_duels.Length] = duel;
        Score(tracks, duel);
        return new LadderBook(duels, tracks);
    }

    public static LadderBook Replay(IReadOnlyList<DuelRecord> duels)
    {
        if (duels is null)
        {
            throw new ArgumentNullException(nameof(duels));
        }

        var tracks = new Dictionary<string, TrackState>(StringComparer.Ordinal);
        var copy = new DuelRecord[duels.Count];
        for (int i = 0; i < duels.Count; i++)
        {
            DuelRecord duel = duels[i] ?? throw new ArgumentException("Duel is missing.", nameof(duels));
            Score(tracks, duel);
            copy[i] = duel;
        }

        return new LadderBook(copy, tracks);
    }

    public IReadOnlyList<RankedTrack> Rank(DateTime utc)
    {
        DateTime at = LadderTime.AsUtc(utc);
        var ordered = new List<TrackState>(_tracks.Count);
        foreach (TrackState track in _tracks.Values)
        {
            if (track.ComparisonCount > 0)
            {
                ordered.Add(track);
            }
        }

        ordered.Sort(CompareStrongestFirst);
        var ranked = new RankedTrack[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            TrackState track = ordered[i];
            Strength effective = track.EffectiveAt(at);
            Strength? nextWeaker = i + 1 < ordered.Count ? ordered[i + 1].EffectiveAt(at) : null;
            ranked[i] = new RankedTrack(i + 1, track, effective, RatingText.Gap(effective, nextWeaker));
        }

        return ranked;
    }

    private static void Score(Dictionary<string, TrackState> tracks, DuelRecord duel)
    {
        TrackState left = ExistingOrNew(tracks, duel.Left);
        TrackState right = ExistingOrNew(tracks, duel.Right);
        Strength leftBefore = left.EffectiveAt(duel.Utc);
        Strength rightBefore = right.EffectiveAt(duel.Utc);
        Strength leftAfter = Glicko2.Update(leftBefore, new[] { rightBefore }, new[] { duel.ScoreFor(left.Url) });
        Strength rightAfter = Glicko2.Update(rightBefore, new[] { leftBefore }, new[] { duel.ScoreFor(right.Url) });
        tracks[left.Url] = left.After(duel.Left, leftAfter, duel.Utc);
        tracks[right.Url] = right.After(duel.Right, rightAfter, duel.Utc);
    }

    private static TrackState ExistingOrNew(Dictionary<string, TrackState> tracks, TrackSnapshot snapshot)
    {
        if (tracks.TryGetValue(snapshot.Url, out TrackState? existing))
        {
            return existing;
        }

        return new TrackState(snapshot.Url, string.Empty, string.Empty, string.Empty, Strength.Unrated, 0, null);
    }

    private static int CompareStrongestFirst(TrackState left, TrackState right)
    {
        int byRating = right.Strength.Rating.CompareTo(left.Strength.Rating);
        if (byRating != 0)
        {
            return byRating;
        }

        int byCount = right.ComparisonCount.CompareTo(left.ComparisonCount);
        if (byCount != 0)
        {
            return byCount;
        }

        return string.CompareOrdinal(left.Url, right.Url);
    }
}
