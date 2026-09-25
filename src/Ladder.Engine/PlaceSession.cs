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

    private PlaceSession(
        LadderBook book,
        TrackSnapshot focus,
        TrackSnapshot[] opponents,
        int low,
        int high,
        bool binary,
        TrackSnapshot[] edges,
        HashSet<string> asked,
        DateTime clock,
        bool needsAnotherTrack)
    {
        Book = book;
        Focus = focus;
        _opponents = opponents;
        _low = low;
        _high = high;
        _binary = binary;
        _edges = edges;
        _asked = asked;
        _clock = clock;
        NeedsAnotherTrack = needsAnotherTrack;
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
                shown,
                Array.Empty<TrackSnapshot>(),
                0,
                0,
                binary: false,
                edges: Array.Empty<TrackSnapshot>(),
                asked,
                clock,
                needsAnotherTrack: true);
        }

        return new PlaceSession(
            book,
            shown,
            opponents.ToArray(),
            0,
            opponents.Count,
            binary: true,
            edges: Array.Empty<TrackSnapshot>(),
            asked,
            clock,
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

        LadderBook book = Book.Apply(new DuelRecord(clock, prompt.Left, prompt.Right, outcome, ComparisonMode.Place));
        var asked = new HashSet<string>(_asked, StringComparer.Ordinal);
        asked.Add(prompt.Right.Url);
        if (!_binary)
        {
            return new PlaceSession(
                book,
                Focus,
                _opponents,
                _low,
                _high,
                binary: false,
                edges: WithoutFirst(_edges),
                asked,
                clock,
                needsAnotherTrack: false);
        }

        int low = _low;
        int high = _high;
        bool searchOver = answer == PlaceAnswer.Draw;
        if (answer == PlaceAnswer.FocusWins)
        {
            low = Mid + 1;
        }
        else if (answer == PlaceAnswer.FocusLoses)
        {
            high = Mid;
        }

        if (searchOver || low >= high)
        {
            return FinishBinary(book, asked, clock);
        }

        return new PlaceSession(
            book,
            Focus,
            _opponents,
            low,
            high,
            binary: true,
            edges: Array.Empty<TrackSnapshot>(),
            asked,
            clock,
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
            return new PlaceSession(
                Book,
                Focus,
                _opponents,
                _low,
                _high,
                binary: false,
                edges: WithoutFirst(_edges),
                _asked,
                _clock,
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
            return FinishBinary(Book, _asked, _clock, opponents, low, high);
        }

        return new PlaceSession(
            Book,
            Focus,
            opponents,
            low,
            high,
            binary: true,
            edges: Array.Empty<TrackSnapshot>(),
            _asked,
            _clock,
            needsAnotherTrack: false);
    }

    private int Mid => _low + (_high - _low) / 2;

    private ComparisonPrompt Prompt(TrackSnapshot opponent)
    {
        return new ComparisonPrompt(Focus, opponent, ComparisonMode.Place, Focus.Url);
    }

    private PlaceSession FinishBinary(LadderBook book, HashSet<string> asked, DateTime clock)
    {
        return FinishBinary(book, asked, clock, _opponents, _low, _high);
    }

    private PlaceSession FinishBinary(
        LadderBook book,
        HashSet<string> asked,
        DateTime clock,
        TrackSnapshot[] opponents,
        int low,
        int high)
    {
        TrackSnapshot[] edges = PlaceEdges.Select(book.Rank(clock), Focus.Url, asked);
        return new PlaceSession(
            book,
            Focus,
            opponents,
            low,
            high,
            binary: false,
            edges,
            asked,
            clock,
            needsAnotherTrack: false);
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
