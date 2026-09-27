using System.Drawing;
using Ladder;

namespace MusicBeePlugin;

/// <summary>
/// The panel and the comparison window both talk to this. A failed load refuses later
/// saves so a damaged ladder file is left on disk.
/// </summary>
internal sealed class SeekRequest : EventArgs
{
    public SeekRequest(string url, double fraction)
    {
        Url = url;
        Fraction = fraction;
    }

    public string Url { get; }

    public double Fraction { get; }
}

internal sealed class LadderSession
{
    private readonly string _path;
    private string[] _libraryUrls = new string[0];
    private bool _canSave;
    private bool _dirty;

    public LadderSession(string path)
    {
        _path = path;
        try
        {
            Controller = new ComparisonController(LadderFile.Load(path));
            _canSave = true;
        }
        catch (LadderFileException exception)
        {
            Controller = new ComparisonController(LadderBook.Empty);
            _canSave = false;
            LoadError = exception.Message;
            Note = exception.Message;
        }
    }

    public event EventHandler Changed;

    public event EventHandler PlaybackChanged;

    public event EventHandler<SeekRequest> SeekRequested;

    public event EventHandler NeedsQuestion;

    public event EventHandler<string> PlayRequested;

    public ComparisonController Controller { get; private set; }

    public string LoadError { get; }

    public string Note { get; private set; }

    public string PlayingUrl { get; private set; }

    public int LibraryCount => _libraryUrls.Length;

    public int PlacedCount { get; private set; }

    public void SetLibrary(IReadOnlyList<string> urls)
    {
        if (urls == null || urls.Count == 0)
        {
            _libraryUrls = new string[0];
        }
        else
        {
            _libraryUrls = new string[urls.Count];
            for (int i = 0; i < urls.Count; i++)
            {
                _libraryUrls[i] = urls[i];
            }
        }

        Recount();
    }

    public Func<string, Image> LoadArt { get; set; }

    public Func<TrackChoice> PlayingChoice { get; set; }

    public Func<TrackChoice> SelectedChoice { get; set; }

    public Func<string, IReadOnlyList<TrackChoice>> FindTracks { get; set; }

    public Action AbandonRating { get; set; }

    public void PickTrack(string url)
    {
        if (!string.IsNullOrEmpty(url) && TrackPicked != null)
        {
            TrackPicked(this, url);
        }
    }

    public event EventHandler<string> TrackPicked;

    private readonly Dictionary<string, Image> _art = new Dictionary<string, Image>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _positionMs = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _durationMs = new Dictionary<string, int>(StringComparer.Ordinal);

    public Image ArtFor(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        Image cached;
        if (_art.TryGetValue(url, out cached))
        {
            return cached;
        }

        Image loaded = null;
        try
        {
            if (LoadArt != null)
            {
                loaded = LoadArt(url);
            }
        }
        catch (Exception)
        {
            loaded = null;
        }

        _art[url] = loaded;
        return loaded;
    }

    public void NotePlayback(string url, int positionMs, int durationMs)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        if (positionMs < 0)
        {
            positionMs = 0;
        }

        int previousPosition;
        int previousDuration;
        bool unchanged = _positionMs.TryGetValue(url, out previousPosition)
            && previousPosition == positionMs
            && _durationMs.TryGetValue(url, out previousDuration)
            && previousDuration == durationMs
            && string.Equals(PlayingUrl, url, StringComparison.Ordinal);
        _positionMs[url] = positionMs;
        if (durationMs > 0)
        {
            _durationMs[url] = durationMs;
        }

        if (!string.Equals(PlayingUrl, url, StringComparison.Ordinal))
        {
            PlayingUrl = url;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        if (!unchanged)
        {
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void TryPlayback(string url, out int positionMs, out int durationMs)
    {
        positionMs = 0;
        durationMs = 0;
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        _positionMs.TryGetValue(url, out positionMs);
        _durationMs.TryGetValue(url, out durationMs);
    }

    public void RequestSeek(string url, double fraction)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        if (fraction < 0)
        {
            fraction = 0;
        }

        if (fraction > 1)
        {
            fraction = 1;
        }

        SeekRequested?.Invoke(this, new SeekRequest(url, fraction));
    }

    public void RequestPlay(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        PlayRequested?.Invoke(this, url);
    }

    public void SetPlaying(string url)
    {
        string next = url ?? "";
        if (string.Equals(PlayingUrl, next, StringComparison.Ordinal))
        {
            return;
        }

        PlayingUrl = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Place(TrackSnapshot focus)
    {
        if (!GuardWritable())
        {
            return;
        }

        LadderBook before = Controller.Book;
        Controller.Place(focus, DateTime.UtcNow);
        After(before);
    }

    public void PlacePlayingRequested()
    {
        PlaceUrlRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler PlaceUrlRequested;

    public void Sharpen()
    {
        if (!GuardWritable())
        {
            return;
        }

        Controller.Sharpen(DateTime.UtcNow);
        Note = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ShowPair(TrackSnapshot left, TrackSnapshot right)
    {
        if (!GuardWritable())
        {
            return;
        }

        Controller.ShowPair(left, right);
        Note = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Choose(ComparisonChoice choice)
    {
        if (!GuardWritable())
        {
            return;
        }

        LadderBook before = Controller.Book;
        Controller.Choose(choice, DateTime.UtcNow);
        After(before);
        AskIfIdle();
    }

    public void Skip()
    {
        if (!GuardWritable())
        {
            return;
        }

        LadderBook before = Controller.Book;
        Controller.Skip(DateTime.UtcNow);
        After(before);
        AskIfIdle();
    }

    public void Report(string note)
    {
        Note = note;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        if (!_dirty || !_canSave)
        {
            return;
        }

        LadderFile.Save(_path, Controller.Book);
        _dirty = false;
    }

    private bool GuardWritable()
    {
        if (_canSave)
        {
            return true;
        }

        Note = LoadError;
        Changed?.Invoke(this, EventArgs.Empty);
        return false;
    }

    private void After(LadderBook before)
    {
        if (!ReferenceEquals(before, Controller.Book))
        {
            _dirty = true;
        }

        Recount();
        Note = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Recount()
    {
        int placed = 0;
        for (int i = 0; i < _libraryUrls.Length; i++)
        {
            TrackState state = Controller.Book.Find(_libraryUrls[i]);
            if (state != null && state.ComparisonCount > 0)
            {
                placed++;
            }
        }

        PlacedCount = placed;
    }

    private void AskIfIdle()
    {
        if (Controller.Current == null)
        {
            NeedsQuestion?.Invoke(this, EventArgs.Empty);
        }
    }
}
