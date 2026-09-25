using System.Globalization;
using System.Text;

namespace Ladder;

internal static class LadderJson
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public static string Write(LadderBook book)
    {
        var builder = new StringBuilder();
        builder.Append("{\n  \"version\": ").Append(LadderFile.CurrentVersion).Append(",\n  \"duels\": [");
        IReadOnlyList<DuelRecord> duels = book.Duels;
        for (int i = 0; i < duels.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            WriteDuel(builder, duels[i]);
        }

        if (duels.Count > 0)
        {
            builder.Append("\n  ");
        }

        builder.Append("]\n}\n");
        return builder.ToString();
    }

    public static LadderBook Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text.Substring(1);
        }

        var reader = new Reader(text);
        int? version = null;
        List<DuelRecord>? duels = null;
        reader.Expect('{');
        if (reader.Peek() != '}')
        {
            while (true)
            {
                string name = reader.String();
                reader.Expect(':');
                if (name == "version")
                {
                    if (version is not null)
                    {
                        throw new FormatException("The version field is repeated.");
                    }

                    version = reader.Int();
                }
                else if (name == "duels")
                {
                    if (duels is not null)
                    {
                        throw new FormatException("The duels field is repeated.");
                    }

                    duels = reader.Duels();
                }
                else
                {
                    throw new FormatException("Unknown field \"" + name + "\".");
                }

                if (reader.Peek() != ',')
                {
                    break;
                }

                reader.Expect(',');
            }
        }

        reader.Expect('}');
        reader.ExpectEnd();
        if (version is null)
        {
            throw new FormatException("The version field is missing.");
        }

        if (version.Value != LadderFile.CurrentVersion)
        {
            throw new FormatException("Ladder file version " + version.Value + " is not supported.");
        }

        if (duels is null)
        {
            throw new FormatException("The duels field is missing.");
        }

        return LadderBook.Replay(duels);
    }

    private static void WriteDuel(StringBuilder builder, DuelRecord duel)
    {
        builder.Append("\n    {\n");
        Field(builder, "utc", duel.Utc.ToString(TimeFormat, CultureInfo.InvariantCulture), comma: true);
        Field(builder, "leftUrl", duel.Left.Url, comma: true);
        Field(builder, "leftTitle", duel.Left.Title, comma: true);
        Field(builder, "leftArtist", duel.Left.Artist, comma: true);
        Field(builder, "leftAlbum", duel.Left.Album, comma: true);
        Field(builder, "rightUrl", duel.Right.Url, comma: true);
        Field(builder, "rightTitle", duel.Right.Title, comma: true);
        Field(builder, "rightArtist", duel.Right.Artist, comma: true);
        Field(builder, "rightAlbum", duel.Right.Album, comma: true);
        Field(builder, "outcome", Outcome(duel.Outcome), comma: true);
        Field(builder, "mode", Mode(duel.Mode), comma: false);
        builder.Append("    }");
    }

    private static void Field(StringBuilder builder, string name, string value, bool comma)
    {
        builder.Append("      \"").Append(name).Append("\": ");
        WriteString(builder, value);
        builder.Append(comma ? ",\n" : "\n");
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    private static string Outcome(DuelOutcome outcome)
    {
        switch (outcome)
        {
            case DuelOutcome.LeftWins:
                return "left";
            case DuelOutcome.RightWins:
                return "right";
            case DuelOutcome.Draw:
                return "draw";
            default:
                throw new InvalidOperationException("Unknown duel outcome.");
        }
    }

    private static string Mode(ComparisonMode mode)
    {
        switch (mode)
        {
            case ComparisonMode.Place:
                return "place";
            case ComparisonMode.Sharpen:
                return "sharpen";
            default:
                throw new InvalidOperationException("Unknown comparison mode.");
        }
    }

    private sealed class Reader
    {
        private readonly string _text;
        private int _index;

        public Reader(string text)
        {
            _text = text;
        }

        public char Peek()
        {
            Skip();
            if (_index >= _text.Length)
            {
                throw new FormatException("The ladder file ended early.");
            }

            return _text[_index];
        }

        public void Expect(char token)
        {
            if (Peek() != token)
            {
                throw new FormatException("Expected '" + token + "'.");
            }

            _index++;
        }

        public void ExpectEnd()
        {
            Skip();
            if (_index != _text.Length)
            {
                throw new FormatException("Unexpected data after the ladder file.");
            }
        }

        public string String()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (_index < _text.Length)
            {
                char character = _text[_index++];
                if (character == '"')
                {
                    return builder.ToString();
                }

                if (character == '\\')
                {
                    builder.Append(Escape());
                    continue;
                }

                if (character < ' ')
                {
                    throw new FormatException("A string contains a raw control character.");
                }

                builder.Append(character);
            }

            throw new FormatException("A string is missing its closing quote.");
        }

        public int Int()
        {
            Skip();
            int start = _index;
            if (_index < _text.Length && _text[_index] == '-')
            {
                _index++;
            }

            int digits = _index;
            while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
            {
                _index++;
            }

            if (digits == _index)
            {
                throw new FormatException("Expected a number.");
            }

            return int.Parse(_text.Substring(start, _index - start), CultureInfo.InvariantCulture);
        }

        public List<DuelRecord> Duels()
        {
            Expect('[');
            var duels = new List<DuelRecord>();
            if (Peek() == ']')
            {
                Expect(']');
                return duels;
            }

            while (true)
            {
                duels.Add(Duel());
                if (Peek() != ',')
                {
                    break;
                }

                Expect(',');
            }

            Expect(']');
            return duels;
        }

        private DuelRecord Duel()
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            Expect('{');
            if (Peek() != '}')
            {
                while (true)
                {
                    string name = String();
                    if (fields.ContainsKey(name))
                    {
                        throw new FormatException("The field \"" + name + "\" is repeated.");
                    }

                    Expect(':');
                    fields.Add(name, String());
                    if (Peek() != ',')
                    {
                        break;
                    }

                    Expect(',');
                }
            }

            Expect('}');
            string utc = Required(fields, "utc");
            string leftUrl = Required(fields, "leftUrl");
            string leftTitle = Required(fields, "leftTitle");
            string leftArtist = Required(fields, "leftArtist");
            string leftAlbum = Required(fields, "leftAlbum");
            string rightUrl = Required(fields, "rightUrl");
            string rightTitle = Required(fields, "rightTitle");
            string rightArtist = Required(fields, "rightArtist");
            string rightAlbum = Required(fields, "rightAlbum");
            string outcome = Required(fields, "outcome");
            string mode = Required(fields, "mode");
            if (fields.Count > 0)
            {
                foreach (string name in fields.Keys)
                {
                    throw new FormatException("Unknown field \"" + name + "\".");
                }
            }

            return new DuelRecord(
                Time(utc),
                new TrackSnapshot(leftUrl, leftTitle, leftArtist, leftAlbum),
                new TrackSnapshot(rightUrl, rightTitle, rightArtist, rightAlbum),
                ParseOutcome(outcome),
                ParseMode(mode));
        }

        private static string Required(Dictionary<string, string> fields, string name)
        {
            string? value;
            if (!fields.TryGetValue(name, out value))
            {
                throw new FormatException("The field \"" + name + "\" is missing.");
            }

            fields.Remove(name);
            return value;
        }

        private static DateTime Time(string text)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(
                text,
                TimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed))
            {
                throw new FormatException("The time \"" + text + "\" is not a UTC timestamp.");
            }

            return parsed;
        }

        private static DuelOutcome ParseOutcome(string text)
        {
            switch (text)
            {
                case "left":
                    return DuelOutcome.LeftWins;
                case "right":
                    return DuelOutcome.RightWins;
                case "draw":
                    return DuelOutcome.Draw;
                default:
                    throw new FormatException("Unknown outcome \"" + text + "\".");
            }
        }

        private static ComparisonMode ParseMode(string text)
        {
            switch (text)
            {
                case "place":
                    return ComparisonMode.Place;
                case "sharpen":
                    return ComparisonMode.Sharpen;
                default:
                    throw new FormatException("Unknown mode \"" + text + "\".");
            }
        }

        private char Escape()
        {
            if (_index >= _text.Length)
            {
                throw new FormatException("A string escape was cut off.");
            }

            char character = _text[_index++];
            switch (character)
            {
                case '"':
                case '\\':
                case '/':
                    return character;
                case 'b':
                    return '\b';
                case 'f':
                    return '\f';
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                case 'u':
                    if (_index + 4 > _text.Length)
                    {
                        throw new FormatException("A unicode escape was cut off.");
                    }

                    int code;
                    if (!int.TryParse(
                        _text.Substring(_index, 4),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out code))
                    {
                        throw new FormatException("A unicode escape is not hexadecimal.");
                    }

                    _index += 4;
                    return (char)code;
                default:
                    throw new FormatException("Unknown string escape.");
            }
        }

        private void Skip()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }
    }
}
