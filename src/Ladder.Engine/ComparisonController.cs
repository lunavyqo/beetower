namespace Ladder;

public enum ComparisonChoice
{
    Left = 0,
    Right = 1,
    Same = 2,
}

/// <summary>
/// Drives place mode, then sharpen mode, over one ladder.
/// The left card winning is always <see cref="ComparisonChoice.Left"/>, including while placing,
/// because the track being placed stays on the left.
/// </summary>
public sealed class ComparisonController
{
    private readonly HashSet<string> _skippedSharpen = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _skippedPairs = new HashSet<string>(StringComparer.Ordinal);
    private LadderBook _book;
    private PlaceSession? _place;
    private ComparisonPrompt? _shown;
    private ComparisonPrompt? _sharpen;

    public ComparisonController(LadderBook book)
    {
        _book = book ?? throw new ArgumentNullException(nameof(book));
    }

    public LadderBook Book => _book;

    public ComparisonPrompt? Current
    {
        get
        {
            if (_place is not null && !_place.IsFinished)
            {
                return _place.Current;
            }

            if (_shown is not null)
            {
                return _shown;
            }

            return _sharpen;
        }
    }

    public IReadOnlyCollection<string> SkippedPairKeys => _skippedPairs;

    public bool NeedsAnotherTrack => _place is not null && _place.NeedsAnotherTrack && _place.IsFinished;

    public void Place(TrackSnapshot focus, DateTime utc)
    {
        if (focus is null)
        {
            throw new ArgumentNullException(nameof(focus));
        }

        _skippedSharpen.Clear();
        _sharpen = null;
        _shown = null;
        _place = PlaceSession.Start(_book, focus, utc);
    }

    public void ShowPair(TrackSnapshot left, TrackSnapshot right)
    {
        if (left is null)
        {
            throw new ArgumentNullException(nameof(left));
        }

        if (right is null)
        {
            throw new ArgumentNullException(nameof(right));
        }

        _place = null;
        _sharpen = null;
        _shown = new ComparisonPrompt(left, right, ComparisonMode.Place, left.Url);
    }

    public void Sharpen(DateTime utc)
    {
        if (_sharpen is null)
        {
            _skippedSharpen.Clear();
        }

        _place = null;
        _shown = null;
        _sharpen = SharpenPicker.Choose(_book, utc, _skippedSharpen);
    }

    public void Choose(ComparisonChoice choice, DateTime utc)
    {
        if (!Enum.IsDefined(typeof(ComparisonChoice), choice))
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }

        if (_place is not null && !_place.IsFinished)
        {
            _place = _place.Answer(ToPlaceAnswer(choice), utc);
            _book = _place.Book;
            if (_place.IsFinished)
            {
                _place = null;
            }

            return;
        }

        if (_shown is not null)
        {
            _book = _book.Apply(new DuelRecord(utc, _shown.Left, _shown.Right, ToOutcome(choice), ComparisonMode.Place));
            _shown = null;
            return;
        }

        ComparisonPrompt prompt = _sharpen ?? throw new InvalidOperationException("There is no question to answer.");
        _book = _book.Apply(new DuelRecord(utc, prompt.Left, prompt.Right, ToOutcome(choice), ComparisonMode.Sharpen));
        _sharpen = SharpenPicker.Choose(_book, utc, _skippedSharpen);
    }

    public void Skip(DateTime utc)
    {
        if (_place is not null && !_place.IsFinished)
        {
            _place = _place.Skip();
            if (_place.IsFinished)
            {
                _place = null;
            }

            return;
        }

        if (_shown is not null)
        {
            _skippedPairs.Add(SharpenPicker.PairKey(_shown.Left.Url, _shown.Right.Url));
            _shown = null;
            return;
        }

        ComparisonPrompt prompt = _sharpen ?? throw new InvalidOperationException("There is no question to skip.");
        _skippedSharpen.Add(SharpenPicker.PairKey(prompt.Left.Url, prompt.Right.Url));
        _sharpen = SharpenPicker.Choose(_book, utc, _skippedSharpen);
    }

    public IReadOnlyList<RankedTrack> Rank(DateTime utc)
    {
        return _book.Rank(utc);
    }

    private static PlaceAnswer ToPlaceAnswer(ComparisonChoice choice)
    {
        switch (choice)
        {
            case ComparisonChoice.Left:
                return PlaceAnswer.FocusWins;
            case ComparisonChoice.Right:
                return PlaceAnswer.FocusLoses;
            default:
                return PlaceAnswer.Draw;
        }
    }

    private static DuelOutcome ToOutcome(ComparisonChoice choice)
    {
        switch (choice)
        {
            case ComparisonChoice.Left:
                return DuelOutcome.LeftWins;
            case ComparisonChoice.Right:
                return DuelOutcome.RightWins;
            default:
                return DuelOutcome.Draw;
        }
    }
}
