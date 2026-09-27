namespace Ladder;

public enum PlaceAnswer
{
    FocusWins = 0,
    FocusLoses = 1,
    Draw = 2,
}

/// <summary>
/// Places one track by halving a frozen weakest-to-strongest list, then asking
/// the songs that ended up directly beside it. Each answer is a real duel.
/// </summary>
public sealed class PlaceSession
{
    private readonly TrackSnapshot[] _opponents;
    private readonly int _low;
    private readonly int _high;
    private readonly bool _binary;
    private readonly TrackSnapshot[] _edges;
    private readonly HashSet<string> _asked;
    private readonly DateTime _clock;
    private readonly int _initialOpponents;
    private readonly LadderBook _basis;
    private readonly DuelRecord[] _pending;

    private PlaceSession(
        LadderBook basis,
        DuelRecord[] pending,
        bool commit,
        TrackSnapshot focus,
        TrackSnapshot[] opponents,
        int low,
        int high,
        bool binary,
        TrackSnapshot[] edges,
        HashSet<string> asked,
        DateTime clock,
        int initialOpponents,
        bool needsAnotherTrack)
    {
        _basis = basis;
        _pending = pending;
        Book = commit ? Commit(basis, pending) : basis;
        Focus = focus;
        _opponents = opponents;
        _low = low;
        _high = high;
        _binary = binary;
        _edges = edges;
        _asked = asked;
        _clock = clock;
        _initialOpponents = initialOpponents;
        NeedsAnotherTrack = needsAnotherTrack;
    }

    /// <summary>Share of the expected questions for this placement that already have an answer. 0 is the first question, 1 is finished.</summary>
    public double Progress
    {
        get
        {
            int expected = ExpectedQuestions(_initialOpponents);
            if (expected <= 0)
            {
                return NeedsAnotherTrack ? 0 : 1;
            }

            double value = _asked.Count / (double)expected;
            if (value < 0)
            {
                return 0;
            }

            if (value > 1)
            {
                return 1;
            }

            return value;
        }
    }

    private static int ExpectedQuestions(int opponents)
    {
        if (opponents <= 0)
        {
            return 0;
        }

        if (opponents == 1)
        {
            return 1;
        }

        int steps = 0;
        int value = opponents - 1;
        while (value > 0)
        {
            value >>= 1;
            steps++;
        }

        return steps + 2;
    }

    public LadderBook Book { get; }

    public TrackSnapshot Focus { get; }

    /// <summary>True when the ladder has nobody for this track to meet yet.</summary>
    public bool NeedsAnotherTrack { get; }

    public ComparisonPrompt? Current
    {
        get
        {
            if (_binary)
            {
                if (_high <= _low)
                {
                    return null;
                }

                return Prompt(_opponents[Mid]);
            }

            if (_edges.Length == 0)
            {
                return null;
            }

            return Prompt(_edges[0]);
        }
    }

    public bool IsFinished => Current is null;

    public static PlaceSession Start(LadderBook book, TrackSnapshot focus, DateTime utc)
    {
        if (book is null)
        {
            throw new ArgumentNullException(nameof(book));
        }

        if (focus is null)
        {
            throw new ArgumentNullException(nameof(focus));
        }

        DateTime clock = LadderTime.AsUtc(utc);
        TrackSnapshot shown = Merge(focus, book.Find(focus.Url));
        IReadOnlyList<RankedTrack> rank = book.Rank(clock);
        var opponents = new List<TrackSnapshot>(rank.Count);
        for (int i = rank.Count - 1; i >= 0; i--)
        {
            TrackState track = rank[i].Track;
            if (!string.Equals(track.Url, shown.Url, StringComparison.Ordinal))
            {
                opponents.Add(new TrackSnapshot(track.Url, track.Title, track.Artist, track.Album));
            }
        }

        var asked = new HashSet<string>(StringComparer.Ordinal);
        if (opponents.Count == 0)
        {
            return new PlaceSession(
                book,
                Array.Empty<DuelRecord>(),
                false,
                shown,
                Array.Empty<TrackSnapshot>(),
                0,
                0,
                binary: false,
                edges: Array.Empty<TrackSnapshot>(),
                asked,
                clock,
                0,
                needsAnotherTrack: true);
        }

        return new PlaceSession(
            book,
            Array.Empty<DuelRecord>(),
            false,
            shown,
            opponents.ToArray(),
            0,
            opponents.Count,
            binary: true,
            edges: Array.Empty<TrackSnapshot>(),
            asked,
            clock,
            opponents.Count,
            needsAnotherTrack: false);
    }

    public PlaceSession Answer(PlaceAnswer answer, DateTime utc)
    {
        if (!Enum.IsDefined(typeof(PlaceAnswer), answer))
        {
            throw new ArgumentOutOfRangeException(nameof(answer));
        }

        ComparisonPrompt prompt = Current ?? throw new InvalidOperationException("There is no question to answer.");
        DateTime clock = LadderTime.AsUtc(utc);
        DuelOutcome outcome;
        switch (answer)
        {
            case PlaceAnswer.FocusWins:
                outcome = DuelOutcome.LeftWins;
                break;
            case PlaceAnswer.FocusLoses:
                outcome = DuelOutcome.RightWins;
                break;
            default:
                outcome = DuelOutcome.Draw;
                break;
        }

        DuelRecord[] pending = Append(_pending, new DuelRecord(clock, prompt.Left, prompt.Right, outcome, ComparisonMode.Place));
        var asked = new HashSet<string>(_asked, StringComparer.Ordinal);
        asked.Add(prompt.Right.Url);
        if (!_binary)
        {
            TrackSnapshot[] rest = WithoutFirst(_edges);
            return new PlaceSession(
                _basis,
                pending,
                rest.Length == 0,
                Focus,
                _opponents,
                _low,
                _high,
                binary: false,
                edges: rest,
                asked,
                clock,
                _initialOpponents,
                needsAnotherTrack: false);
        }

        int low = _low;
        int high = _high;
        int[] neighbors;
        if (answer == PlaceAnswer.Draw)
        {
            neighbors = new[] { Mid + 1, Mid - 1 };
            return FinishBinary(_basis, pending, asked, clock, neighbors, _opponents, low, high);
        }

        if (answer == PlaceAnswer.FocusWins)
        {
            low = Mid + 1;
        }
        else
        {
            high = Mid;
        }

        if (low >= high)
        {
            return FinishBinary(_basis, pending, asked, clock, new[] { low, low - 1 }, _opponents, low, high);
        }

        return new PlaceSession(
            _basis,
            pending,
            false,
            Focus,
            _opponents,
            low,
            high,
            binary: true,
            edges: Array.Empty<TrackSnapshot>(),
            asked,
            clock,
            _initialOpponents,
            needsAnotherTrack: false);
    }

    public PlaceSession Skip()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("There is no question to skip.");
        }

        if (!_binary)
        {
            TrackSnapshot[] rest = WithoutFirst(_edges);
            return new PlaceSession(
                _basis,
                _pending,
                rest.Length == 0,
                Focus,
                _opponents,
                _low,
                _high,
                binary: false,
                edges: rest,
                _asked,
                _clock,
                _initialOpponents,
                needsAnotherTrack: false);
        }

        int mid = Mid;
        var opponents = new TrackSnapshot[_opponents.Length - 1];
        Array.Copy(_opponents, 0, opponents, 0, mid);
        Array.Copy(_opponents, mid + 1, opponents, mid, _opponents.Length - mid - 1);
        int low = _low > mid ? _low - 1 : _low;
        int high = _high > mid ? _high - 1 : _high;
        if (low >= high)
        {
            return FinishBinary(_basis, _pending, _asked, _clock, new[] { low, low - 1 }, opponents, low, high);
        }

        return new PlaceSession(
            _basis,
            _pending,
            false,
            Focus,
            opponents,
            low,
            high,
            binary: true,
            edges: Array.Empty<TrackSnapshot>(),
            _asked,
            _clock,
            _initialOpponents,
            needsAnotherTrack: false);
    }

    private int Mid => _low + (_high - _low) / 2;

    private ComparisonPrompt Prompt(TrackSnapshot opponent)
    {
        return new ComparisonPrompt(Focus, opponent, ComparisonMode.Place, Focus.Url);
    }

    private PlaceSession FinishBinary(
        LadderBook basis,
        DuelRecord[] pending,
        HashSet<string> asked,
        DateTime clock,
        int[] neighborIndexes,
        TrackSnapshot[] opponents,
        int low,
        int high)
    {
        TrackSnapshot[] edges = Neighbors(opponents, neighborIndexes, asked);
        return new PlaceSession(
            basis,
            pending,
            edges.Length == 0,
            Focus,
            opponents,
            low,
            high,
            binary: false,
            edges,
            asked,
            clock,
            _initialOpponents,
            needsAnotherTrack: false);
    }

    private static TrackSnapshot[] Neighbors(TrackSnapshot[] opponents, int[] indexes, HashSet<string> asked)
    {
        var chosen = new List<TrackSnapshot>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < indexes.Length; i++)
        {
            int index = indexes[i];
            if (index < 0 || index >= opponents.Length)
            {
                continue;
            }

            TrackSnapshot opponent = opponents[index];
            if (asked.Contains(opponent.Url) || !seen.Add(opponent.Url))
            {
                continue;
            }

            chosen.Add(opponent);
        }

        return chosen.ToArray();
    }

    private static DuelRecord[] Append(DuelRecord[] pending, DuelRecord duel)
    {
        var next = new DuelRecord[pending.Length + 1];
        Array.Copy(pending, next, pending.Length);
        next[pending.Length] = duel;
        return next;
    }

    private static LadderBook Commit(LadderBook basis, DuelRecord[] pending)
    {
        LadderBook book = basis;
        for (int i = 0; i < pending.Length; i++)
        {
            book = book.Apply(pending[i]);
        }

        return book;
    }

    private static TrackSnapshot[] WithoutFirst(TrackSnapshot[] edges)
    {
        var rest = new TrackSnapshot[edges.Length - 1];
        Array.Copy(edges, 1, rest, 0, rest.Length);
        return rest;
    }

    private static TrackSnapshot Merge(TrackSnapshot incoming, TrackState? cached)
    {
        if (cached is null)
        {
            return incoming;
        }

        return new TrackSnapshot(
            incoming.Url,
            incoming.Title.Length > 0 ? incoming.Title : cached.Title,
            incoming.Artist.Length > 0 ? incoming.Artist : cached.Artist,
            incoming.Album.Length > 0 ? incoming.Album : cached.Album);
    }
}
