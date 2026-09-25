namespace Ladder;

/// <summary>One question. In place mode the focus track is <see cref="Left"/>.</summary>
public sealed class ComparisonPrompt
{
    public ComparisonPrompt(TrackSnapshot left, TrackSnapshot right, ComparisonMode mode, string? focusUrl)
    {
        if (left is null)
        {
            throw new ArgumentNullException(nameof(left));
        }

        if (right is null)
        {
            throw new ArgumentNullException(nameof(right));
        }

        if (!Enum.IsDefined(typeof(ComparisonMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (string.Equals(left.Url, right.Url, StringComparison.Ordinal))
        {
            throw new ArgumentException("A prompt needs two different tracks.");
        }

        Left = left;
        Right = right;
        Mode = mode;
        FocusUrl = focusUrl;
    }

    public TrackSnapshot Left { get; }

    public TrackSnapshot Right { get; }

    public ComparisonMode Mode { get; }

    /// <summary>The track being placed, or null when the question has no focus.</summary>
    public string? FocusUrl { get; }
}
