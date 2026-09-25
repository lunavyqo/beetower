namespace Ladder;

/// <summary>
/// After binary search, the songs directly beside the focus may never have been pivots.
/// Ask those, closer rating first, skipping any URL this session already compared.
/// </summary>
internal static class PlaceEdges
{
    public static TrackSnapshot[] Select(
        IReadOnlyList<RankedTrack> rank,
        string focusUrl,
        IReadOnlyCollection<string> alreadyAsked)
    {
        int index = -1;
        for (int i = 0; i < rank.Count; i++)
        {
            if (string.Equals(rank[i].Track.Url, focusUrl, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return Array.Empty<TrackSnapshot>();
        }

        var neighbors = new List<RankedTrack>(2);
        if (index > 0)
        {
            neighbors.Add(rank[index - 1]);
        }

        if (index + 1 < rank.Count)
        {
            neighbors.Add(rank[index + 1]);
        }

        double focusRating = rank[index].Track.Strength.Rating;
        var pending = new List<RankedTrack>(neighbors.Count);
        foreach (RankedTrack neighbor in neighbors)
        {
            if (!Contains(alreadyAsked, neighbor.Track.Url))
            {
                pending.Add(neighbor);
            }
        }

        pending.Sort((left, right) =>
        {
            int byDistance = Distance(left, focusRating).CompareTo(Distance(right, focusRating));
            if (byDistance != 0)
            {
                return byDistance;
            }

            return string.CompareOrdinal(left.Track.Url, right.Track.Url);
        });

        var snapshots = new TrackSnapshot[pending.Count];
        for (int i = 0; i < pending.Count; i++)
        {
            TrackState track = pending[i].Track;
            snapshots[i] = new TrackSnapshot(track.Url, track.Title, track.Artist, track.Album);
        }

        return snapshots;
    }

    private static double Distance(RankedTrack track, double focusRating)
    {
        return Math.Abs(track.Track.Strength.Rating - focusRating);
    }

    private static bool Contains(IReadOnlyCollection<string> urls, string url)
    {
        foreach (string candidate in urls)
        {
            if (string.Equals(candidate, url, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
