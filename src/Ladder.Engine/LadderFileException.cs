namespace Ladder;

public sealed class LadderFileException : Exception
{
    public LadderFileException(string path, string message)
        : base(message)
    {
        FilePath = path;
    }

    public LadderFileException(string path, string message, Exception inner)
        : base(message, inner)
    {
        FilePath = path;
    }

    public string FilePath { get; }
}
