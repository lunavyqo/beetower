namespace Ladder;

/// <summary>
/// Liked tracks that have not finished a placement run.
/// </summary>
public static class UnplacedLiked
{
    public static string[] Select(IReadOnlyList<string> liked, LadderBook book)
    {
        if (liked is null)
        {
            throw new ArgumentNullException(nameof(liked));
        }

        if (book is null)
        {
            throw new ArgumentNullException(nameof(book));
        }

        var kept = new List<string>(liked.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < liked.Count; i++)
        {
            string url = liked[i];
            if (string.IsNullOrEmpty(url) || !seen.Add(url))
            {
                continue;
            }

            TrackState? state = book.Find(url);
            if (state != null && state.ComparisonCount > 0)
            {
                continue;
            }

            kept.Add(url);
        }

        return kept.ToArray();
    }
}
