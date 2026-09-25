using System.Text;

namespace Ladder;

/// <summary>
/// Version 1 ladder files. The duel list is the file. Strengths are rebuilt on load.
/// </summary>
public static class LadderFile
{
    public const int CurrentVersion = 1;

    public static LadderBook Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            return LadderBook.Empty;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException exception)
        {
            throw new LadderFileException(path, "The ladder file could not be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new LadderFileException(path, "The ladder file could not be read.", exception);
        }

        try
        {
            return LadderJson.Parse(text);
        }
        catch (FormatException exception)
        {
            throw new LadderFileException(path, exception.Message, exception);
        }
    }

    public static void Save(string path, LadderBook book)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A path is required.", nameof(path));
        }

        if (book is null)
        {
            throw new ArgumentNullException(nameof(book));
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = path + ".tmp";
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try
        {
            File.WriteAllText(temporary, LadderJson.Write(book), encoding);
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        catch (Exception exception)
        {
            TryDelete(temporary);
            if (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new LadderFileException(path, "The ladder file could not be saved.", exception);
            }

            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
