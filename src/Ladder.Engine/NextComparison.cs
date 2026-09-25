namespace Ladder;

public enum NextComparisonKind
{
    None = 0,
    Pair = 1,
    Place = 2,
    Sharpen = 3,
}

/// <summary>
/// Picks the next question from the library. The listener never chooses the two songs.
/// </summary>
public static class NextComparison
{
    public static NextComparisonPlan Plan(
        LadderBook book,
        IReadOnlyList<string> libraryUrls,
        string? playingUrl,
        IReadOnlyCollection<string>? skippedPairKeys)
    {
        if (book is null)
        {
            throw new ArgumentNullException(nameof(book));
        }

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Add(ordered, seen, playingUrl);
        if (libraryUrls != null)
        {
            for (int i = 0; i < libraryUrls.Count; i++)
            {
                Add(ordered, seen, libraryUrls[i]);
            }
        }

        var rated = new HashSet<string>(StringComparer.Ordinal);
        foreach (TrackState track in book.Tracks)
        {
            if (track.ComparisonCount > 0)
            {
                rated.Add(track.Url);
            }
        }

        var unrated = new List<string>();
        foreach (string url in ordered)
        {
            if (!rated.Contains(url))
            {
                unrated.Add(url);
            }
        }

        if (unrated.Count == 0)
        {
            if (rated.Count >= 2)
            {
                return NextComparisonPlan.Sharpen();
            }

            return NextComparisonPlan.None();
        }

        if (rated.Count == 0)
        {
            if (unrated.Count < 2)
            {
                return NextComparisonPlan.None();
            }

            string left = unrated[0];
            for (int i = 1; i < unrated.Count; i++)
            {
                string key = SharpenPicker.PairKey(left, unrated[i]);
                if (!Contains(skippedPairKeys, key))
                {
                    return NextComparisonPlan.Pair(left, unrated[i]);
                }
            }

            return NextComparisonPlan.None();
        }

        return NextComparisonPlan.Place(unrated[0]);
    }

    private static void Add(List<string> ordered, HashSet<string> seen, string? url)
    {
        if (url == null || url.Length == 0 || !seen.Add(url))
        {
            return;
        }

        ordered.Add(url);
    }

    private static bool Contains(IReadOnlyCollection<string>? keys, string key)
    {
        if (keys is null)
        {
            return false;
        }

        foreach (string candidate in keys)
        {
            if (string.Equals(candidate, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class NextComparisonPlan
{
    private NextComparisonPlan(NextComparisonKind kind, string? leftUrl, string? rightUrl)
    {
        Kind = kind;
        LeftUrl = leftUrl;
        RightUrl = rightUrl;
    }

    public NextComparisonKind Kind { get; }

    public string? LeftUrl { get; }

    public string? RightUrl { get; }

    public static NextComparisonPlan None()
    {
        return new NextComparisonPlan(NextComparisonKind.None, null, null);
    }

    public static NextComparisonPlan Pair(string leftUrl, string rightUrl)
    {
        return new NextComparisonPlan(NextComparisonKind.Pair, leftUrl, rightUrl);
    }

    public static NextComparisonPlan Place(string focusUrl)
    {
        return new NextComparisonPlan(NextComparisonKind.Place, focusUrl, null);
    }

    public static NextComparisonPlan Sharpen()
    {
        return new NextComparisonPlan(NextComparisonKind.Sharpen, null, null);
    }
}
