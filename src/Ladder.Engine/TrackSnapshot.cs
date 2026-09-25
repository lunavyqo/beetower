namespace Ladder;

/// <summary>The URL MusicBee reported for a file, plus the names shown for it.</summary>
public sealed class TrackSnapshot
{
    public TrackSnapshot(string url, string? title, string? artist, string? album)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("A track URL is required.", nameof(url));
        }

        Url = url;
        Title = title ?? string.Empty;
        Artist = artist ?? string.Empty;
        Album = album ?? string.Empty;
    }

    public string Url { get; }

    public string Title { get; }

    public string Artist { get; }

    public string Album { get; }
}
