namespace Ladder;

/// <summary>What an answer did to the current placement run. Sharpen questions leave this empty.</summary>
public readonly struct RunFinish
{
    private RunFinish(bool completed, TrackSnapshot? focus, TrackSnapshot? other)
    {
        Completed = completed;
        Focus = focus;
        Other = other;
    }

    public bool Completed { get; }

    public TrackSnapshot? Focus { get; }

    public TrackSnapshot? Other { get; }

    public static RunFinish None => new RunFinish(false, null, null);

    public static RunFinish Placed(TrackSnapshot focus)
    {
        return new RunFinish(true, focus, null);
    }

    public static RunFinish Paired(TrackSnapshot left, TrackSnapshot right)
    {
        return new RunFinish(true, left, right);
    }
}

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

    /// <summary>How far the current placement is, or null when the question is not placing one track.</summary>
    public double? PlacementProgress => _place is not null && !_place.IsFinished && !_place.NeedsAnotherTrack
        ? _place.Progress
        : (double?)null;

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

    public void ClearQuestion()
    {
        _place = null;
        _shown = null;
        _sharpen = null;
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

    public RunFinish Choose(ComparisonChoice choice, DateTime utc)
    {
        if (!Enum.IsDefined(typeof(ComparisonChoice), choice))
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }

        if (_place is not null && !_place.IsFinished)
        {
            TrackSnapshot focus = _place.Focus;
            _place = _place.Answer(ToPlaceAnswer(choice), utc);
            _book = _place.Book;
            if (_place.IsFinished)
            {
                _place = null;
                return RunFinish.Placed(focus);
            }

            return RunFinish.None;
        }

        if (_shown is not null)
        {
            TrackSnapshot left = _shown.Left;
            TrackSnapshot right = _shown.Right;
            _book = _book.Apply(new DuelRecord(utc, left, right, ToOutcome(choice), ComparisonMode.Place));
            _shown = null;
            return RunFinish.Paired(left, right);
        }

        ComparisonPrompt prompt = _sharpen ?? throw new InvalidOperationException("There is no question to answer.");
        _book = _book.Apply(new DuelRecord(utc, prompt.Left, prompt.Right, ToOutcome(choice), ComparisonMode.Sharpen));
        _sharpen = SharpenPicker.Choose(_book, utc, _skippedSharpen);
        return RunFinish.None;
    }

    public RunFinish Skip(DateTime utc)
    {
        if (_place is not null && !_place.IsFinished)
        {
            TrackSnapshot focus = _place.Focus;
            _place = _place.Skip();
            if (_place.IsFinished)
            {
                _place = null;
                return RunFinish.Placed(focus);
            }

            return RunFinish.None;
        }

        if (_shown is not null)
        {
            _skippedPairs.Add(SharpenPicker.PairKey(_shown.Left.Url, _shown.Right.Url));
            _shown = null;
            return RunFinish.None;
        }

        ComparisonPrompt prompt = _sharpen ?? throw new InvalidOperationException("There is no question to skip.");
        _skippedSharpen.Add(SharpenPicker.PairKey(prompt.Left.Url, prompt.Right.Url));
        _sharpen = SharpenPicker.Choose(_book, utc, _skippedSharpen);
        return RunFinish.None;
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
