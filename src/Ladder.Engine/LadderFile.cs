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
            using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (StreamWriter writer = new StreamWriter(stream, encoding))
            {
                writer.Write(LadderJson.Write(book));
                writer.Flush();
                stream.Flush(true);
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            catch (IOException)
            {
                File.Copy(temporary, path, true);
                File.Delete(temporary);
            }
            catch (UnauthorizedAccessException)
            {
                File.Copy(temporary, path, true);
                File.Delete(temporary);
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
