using System.Drawing;
using Ladder;

namespace MusicBeePlugin;

/// <summary>
/// The panel and the comparison window both talk to this. A failed load refuses later
/// saves so a damaged ladder file is left on disk.
/// </summary>
internal sealed class LadderSession
{
    private readonly string _path;
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

    public event EventHandler<string> PlayRequested;

    public ComparisonController Controller { get; private set; }

    public string LoadError { get; }

    public string Note { get; private set; }

    public string PlayingUrl { get; private set; }

    public Func<string, Image> LoadArt { get; set; }

    private readonly Dictionary<string, Image> _art = new Dictionary<string, Image>(StringComparer.Ordinal);

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

    public void Choose(ComparisonChoice choice)
    {
        if (!GuardWritable())
        {
            return;
        }

        LadderBook before = Controller.Book;
        Controller.Choose(choice, DateTime.UtcNow);
        After(before);
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

        Note = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
