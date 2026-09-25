namespace Ladder;

/// <summary>
/// Picks the local pair that would teach the ladder the most.
/// Questions stay inside a window of ten stronger neighbors.
/// </summary>
public static class SharpenPicker
{
    public const int NeighborWindow = 10;
    public const int RecentDuelCount = 24;
    public const double RecentPairWeight = 0.1;

    public static ComparisonPrompt? Choose(LadderBook book, DateTime utc, IReadOnlyCollection<string>? excludedPairKeys)
    {
        if (book is null)
        {
            throw new ArgumentNullException(nameof(book));
        }

        IReadOnlyList<RankedTrack> rank = book.Rank(utc);
        if (rank.Count < 2)
        {
            return null;
        }

        var recent = RecentKeys(book);
        string? bestKey = null;
        double bestInfo = 0;
        RankedTrack? bestStronger = null;
        RankedTrack? bestWeaker = null;
        for (int weakerIndex = 1; weakerIndex < rank.Count; weakerIndex++)
        {
            int firstStronger = Math.Max(0, weakerIndex - NeighborWindow);
            for (int strongerIndex = firstStronger; strongerIndex < weakerIndex; strongerIndex++)
            {
                RankedTrack stronger = rank[strongerIndex];
                RankedTrack weaker = rank[weakerIndex];
                string key = PairKey(stronger.Track.Url, weaker.Track.Url);
                if (IsExcluded(excludedPairKeys, key))
                {
                    continue;
                }

                double info = Information(stronger.Effective, weaker.Effective, recent.Contains(key));
                if (bestKey is null
                    || info > bestInfo
                    || (info == bestInfo && string.CompareOrdinal(key, bestKey) < 0))
                {
                    bestKey = key;
                    bestInfo = info;
                    bestStronger = stronger;
                    bestWeaker = weaker;
                }
            }
        }

        if (bestStronger is null || bestWeaker is null)
        {
            return null;
        }

        bool strongerOnLeft = book.Duels.Count % 2 == 0;
        TrackSnapshot left = Snapshot(strongerOnLeft ? bestStronger : bestWeaker);
        TrackSnapshot right = Snapshot(strongerOnLeft ? bestWeaker : bestStronger);
        return new ComparisonPrompt(left, right, ComparisonMode.Sharpen, null);
    }

    public static string PairKey(string leftUrl, string rightUrl)
    {
        if (string.IsNullOrEmpty(leftUrl))
        {
            throw new ArgumentException("A URL is required.", nameof(leftUrl));
        }

        if (string.IsNullOrEmpty(rightUrl))
        {
            throw new ArgumentException("A URL is required.", nameof(rightUrl));
        }

        if (string.Equals(leftUrl, rightUrl, StringComparison.Ordinal))
        {
            throw new ArgumentException("A pair needs two different tracks.");
        }

        if (string.CompareOrdinal(leftUrl, rightUrl) < 0)
        {
            return leftUrl + "\n" + rightUrl;
        }

        return rightUrl + "\n" + leftUrl;
    }

    internal static double Information(Strength left, Strength right, bool recent)
    {
        double expected = Glicko2.ExpectedScore(left, right);
        double info = (left.Deviation * left.Deviation + right.Deviation * right.Deviation) * expected * (1.0 - expected);
        if (recent)
        {
            info *= RecentPairWeight;
        }

        return info;
    }

    private static HashSet<string> RecentKeys(LadderBook book)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int start = Math.Max(0, book.Duels.Count - RecentDuelCount);
        for (int i = start; i < book.Duels.Count; i++)
        {
            DuelRecord duel = book.Duels[i];
            keys.Add(PairKey(duel.Left.Url, duel.Right.Url));
        }

        return keys;
    }

    private static bool IsExcluded(IReadOnlyCollection<string>? keys, string key)
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

    private static TrackSnapshot Snapshot(RankedTrack row)
    {
        TrackState track = row.Track;
        return new TrackSnapshot(track.Url, track.Title, track.Artist, track.Album);
    }
}
