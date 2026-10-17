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
/// Immutable ladder. Duels are replayed into a strongest-first order.
/// A song already on the ladder does not move when a new song is only compared with it.
/// The newer of two placed songs is the one that moves if their order contradicts the answer.
/// </summary>
public sealed class LadderBook
{
    private readonly DuelRecord[] _duels;
    private readonly Dictionary<string, TrackState> _tracks;
    private readonly TrackState[] _trackList;
    private readonly string[] _order;
    private readonly Dictionary<string, int> _joined;

    private LadderBook(
        DuelRecord[] duels,
        Dictionary<string, TrackState> tracks,
        string[] order,
        Dictionary<string, int> joined)
    {
        _duels = duels;
        _tracks = tracks;
        _order = order;
        _joined = joined;
        _trackList = new TrackState[tracks.Count];
        tracks.Values.CopyTo(_trackList, 0);
        Array.Sort(_trackList, (left, right) => string.CompareOrdinal(left.Url, right.Url));
    }

    public static LadderBook Empty { get; } = new(
        Array.Empty<DuelRecord>(),
        new Dictionary<string, TrackState>(StringComparer.Ordinal),
        Array.Empty<string>(),
        new Dictionary<string, int>(StringComparer.Ordinal));

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
        var order = new List<string>(_order);
        var joined = new Dictionary<string, int>(_joined, StringComparer.Ordinal);
        Score(tracks, order, joined, duel, _duels.Length);
        return new LadderBook(duels, tracks, order.ToArray(), joined);
    }

    public static LadderBook Replay(IReadOnlyList<DuelRecord> duels)
    {
        if (duels is null)
        {
            throw new ArgumentNullException(nameof(duels));
        }

        var tracks = new Dictionary<string, TrackState>(StringComparer.Ordinal);
        var order = new List<string>();
        var joined = new Dictionary<string, int>(StringComparer.Ordinal);
        var copy = new DuelRecord[duels.Count];
        for (int i = 0; i < duels.Count; i++)
        {
            DuelRecord duel = duels[i] ?? throw new ArgumentException("Duel is missing.", nameof(duels));
            Score(tracks, order, joined, duel, i);
            copy[i] = duel;
        }

        return new LadderBook(copy, tracks, order.ToArray(), joined);
    }

    public IReadOnlyList<RankedTrack> Rank(DateTime utc)
    {
        DateTime at = LadderTime.AsUtc(utc);
        var ranked = new RankedTrack[_order.Length];
        for (int i = 0; i < _order.Length; i++)
        {
            TrackState track = _tracks[_order[i]];
            Strength effective = track.EffectiveAt(at);
            Strength? nextWeaker = i + 1 < _order.Length ? _tracks[_order[i + 1]].EffectiveAt(at) : null;
            ranked[i] = new RankedTrack(i + 1, track, effective, RatingText.Gap(effective, nextWeaker));
        }

        return ranked;
    }

    private static void Score(
        Dictionary<string, TrackState> tracks,
        List<string> order,
        Dictionary<string, int> joined,
        DuelRecord duel,
        int stamp)
    {
        TrackState left = ExistingOrNew(tracks, duel.Left);
        TrackState right = ExistingOrNew(tracks, duel.Right);
        Strength leftBefore = left.EffectiveAt(duel.Utc);
        Strength rightBefore = right.EffectiveAt(duel.Utc);
        Strength leftAfter = Glicko2.Update(leftBefore, new[] { rightBefore }, new[] { duel.ScoreFor(left.Url) });
        Strength rightAfter = Glicko2.Update(rightBefore, new[] { leftBefore }, new[] { duel.ScoreFor(right.Url) });
        tracks[left.Url] = left.After(duel.Left, leftAfter, duel.Utc);
        tracks[right.Url] = right.After(duel.Right, rightAfter, duel.Utc);
        Place(order, joined, duel, stamp);
    }

    private static void Place(List<string> order, Dictionary<string, int> joined, DuelRecord duel, int stamp)
    {
        if (duel.Outcome == DuelOutcome.Draw)
        {
            SitBeside(order, joined, duel.Left.Url, duel.Right.Url, stamp);
            return;
        }

        string winner = duel.Outcome == DuelOutcome.LeftWins ? duel.Left.Url : duel.Right.Url;
        string loser = duel.Outcome == DuelOutcome.LeftWins ? duel.Right.Url : duel.Left.Url;
        int winnerAt = order.IndexOf(winner);
        int loserAt = order.IndexOf(loser);
        if (winnerAt < 0 && loserAt < 0)
        {
            order.Add(winner);
            order.Add(loser);
            joined[winner] = stamp;
            joined[loser] = stamp;
            return;
        }

        if (winnerAt < 0)
        {
            order.Insert(loserAt, winner);
            joined[winner] = stamp;
            return;
        }

        if (loserAt < 0)
        {
            order.Insert(winnerAt + 1, loser);
            joined[loser] = stamp;
            return;
        }

        if (winnerAt < loserAt)
        {
            return;
        }

        if (joined[winner] >= joined[loser])
        {
            order.RemoveAt(winnerAt);
            order.Insert(order.IndexOf(loser), winner);
            return;
        }

        order.RemoveAt(loserAt);
        order.Insert(order.IndexOf(winner) + 1, loser);
    }

    private static void SitBeside(List<string> order, Dictionary<string, int> joined, string left, string right, int stamp)
    {
        int leftAt = order.IndexOf(left);
        int rightAt = order.IndexOf(right);
        if (leftAt < 0 && rightAt < 0)
        {
            order.Add(left);
            order.Add(right);
            joined[left] = stamp;
            joined[right] = stamp;
            return;
        }

        if (leftAt < 0)
        {
            order.Insert(rightAt + 1, left);
            joined[left] = stamp;
            return;
        }

        if (rightAt < 0)
        {
            order.Insert(leftAt + 1, right);
            joined[right] = stamp;
        }
    }

    private static TrackState ExistingOrNew(Dictionary<string, TrackState> tracks, TrackSnapshot snapshot)
    {
        if (tracks.TryGetValue(snapshot.Url, out TrackState? existing))
        {
            return existing;
        }

        return new TrackState(snapshot.Url, string.Empty, string.Empty, string.Empty, Strength.Unrated, 0, null);
    }
}
