using System.Globalization;
using Ladder;

namespace MusicBeePlugin;

internal sealed class LadderBoard : UserControl
{
    private readonly Label _heading;
    private readonly Label _note;
    private readonly Label _empty;
    private readonly Button _back;
    private readonly ListBox _rows;
    private readonly Font _rankFont = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _detailFont = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
    private string _playingUrl = "";
    private StageTheme _theme = StageTheme.Light();

    public LadderBoard()
    {
        DoubleBuffered = true;
        _heading = new Label
        {
            Text = "The ladder",
            Font = new Font("Segoe UI", 26f, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = false,
            Height = 46,
        };
        _note = new Label
        {
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = false,
            Height = 28,
        };
        _empty = new Label
        {
            Text = "Nothing is on the ladder yet.",
            Font = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false,
        };
        _back = new Button
        {
            Text = "Rate a song",
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point),
            UseVisualStyleBackColor = false,
        };
        _back.FlatAppearance.BorderSize = 0;
        _back.Click += (sender, args) =>
        {
            if (Closed != null)
            {
                Closed(this, EventArgs.Empty);
            }
        };
        _rows = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 68,
            IntegralHeight = false,
            Font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
        };
        _rows.DrawItem += DrawRow;
        _rows.MouseClick += RowClicked;
        Controls.Add(_heading);
        Controls.Add(_note);
        Controls.Add(_back);
        Controls.Add(_empty);
        Controls.Add(_rows);
        Apply(_theme);
    }

    public event EventHandler Closed;

    public event EventHandler<string> PlayRequested;

    public void Apply(StageTheme theme)
    {
        _theme = theme ?? StageTheme.Light();
        BackColor = _theme.Page;
        _heading.ForeColor = _theme.Text;
        _heading.BackColor = _theme.Page;
        _note.ForeColor = _theme.Muted;
        _note.BackColor = _theme.Page;
        _empty.ForeColor = _theme.Muted;
        _empty.BackColor = _theme.Page;
        _back.BackColor = _theme.Page;
        _back.ForeColor = _theme.Text;
        _back.FlatAppearance.MouseOverBackColor = _theme.Card;
        _back.FlatAppearance.MouseDownBackColor = _theme.Line;
        _rows.BackColor = _theme.Page;
        _rows.ForeColor = _theme.Text;
        Invalidate(true);
    }

    public void ShowStandings(IReadOnlyList<RankedTrack> rank, string playingUrl, string backText)
    {
        _playingUrl = playingUrl ?? "";
        _back.Text = string.IsNullOrEmpty(backText) ? "Rate a song" : backText;
        int count = rank == null ? 0 : rank.Count;
        if (count == 0)
        {
            _note.Text = "Nothing is on the ladder yet.";
        }
        else if (count == 1)
        {
            _note.Text = "1 song, strongest first. Click it to play.";
        }
        else
        {
            _note.Text = count.ToString(CultureInfo.InvariantCulture) + " songs, strongest first. Click one to play.";
        }

        int top = _rows.TopIndex;
        _rows.BeginUpdate();
        _rows.Items.Clear();
        if (rank != null)
        {
            for (int i = 0; i < rank.Count; i++)
            {
                _rows.Items.Add(rank[i]);
            }
        }

        _rows.EndUpdate();
        if (top >= 0 && top < _rows.Items.Count)
        {
            _rows.TopIndex = top;
        }

        _rows.Visible = count > 0;
        _empty.Visible = count == 0;
        PerformLayout();
        _rows.Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int width = Math.Min(860, Math.Max(280, ClientSize.Width - 64));
        int x = Math.Max(24, (ClientSize.Width - width) / 2);
        int y = 28;
        int backW = Math.Min(220, width / 3);
        _heading.SetBounds(x, y, Math.Max(80, width - backW - 12), 46);
        _back.SetBounds(x + width - backW, y + 8, backW, 34);
        _note.SetBounds(x, y + 48, width, 28);
        int listY = y + 88;
        int listH = Math.Max(80, ClientSize.Height - listY - 24);
        _rows.SetBounds(x, listY, width, listH);
        _empty.SetBounds(x, listY, width, 48);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rankFont.Dispose();
            _detailFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RowClicked(object sender, MouseEventArgs e)
    {
        int index = _rows.IndexFromPoint(e.Location);
        if (index < 0 || PlayRequested == null)
        {
            return;
        }

        RankedTrack row = _rows.Items[index] as RankedTrack;
        if (row != null && !string.IsNullOrEmpty(row.Track.Url))
        {
            PlayRequested(this, row.Track.Url);
        }
    }

    private void DrawRow(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        RankedTrack row = _rows.Items[e.Index] as RankedTrack;
        if (row == null)
        {
            return;
        }

        bool hot = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        bool playing = string.Equals(row.Track.Url, _playingUrl, StringComparison.Ordinal);
        Graphics graphics = e.Graphics;
        using (Brush back = new SolidBrush(hot ? _theme.CardHover : _theme.Page))
        {
            graphics.FillRectangle(back, e.Bounds);
        }

        if (playing)
        {
            using (Brush mark = new SolidBrush(_theme.Accent))
            {
                graphics.FillRectangle(mark, new Rectangle(e.Bounds.X, e.Bounds.Y + 14, 4, e.Bounds.Height - 28));
            }
        }

        int rankWidth = 64;
        Rectangle rank = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, rankWidth, e.Bounds.Height);
        int textX = rank.Right + 8;
        int textW = Math.Max(20, e.Bounds.Right - 16 - textX);
        Rectangle title = new Rectangle(textX, e.Bounds.Y + 10, textW, 26);
        Rectangle detail = new Rectangle(textX, e.Bounds.Y + 36, textW, 20);
        TextFormatFlags line = TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.Left;
        TextRenderer.DrawText(graphics, "#" + row.Rank.ToString(CultureInfo.InvariantCulture), _rankFont, rank, playing ? _theme.Text : _theme.Muted, line);
        TextRenderer.DrawText(graphics, DisplayTitle(row.Track), _rows.Font, title, _theme.Text, line);
        TextRenderer.DrawText(graphics, JoinDetail(row.Track.Artist, row.Track.Album), _detailFont, detail, _theme.Muted, line);
        using (Pen rule = new Pen(_theme.Line))
        {
            graphics.DrawLine(rule, e.Bounds.X + 16, e.Bounds.Bottom - 1, e.Bounds.Right - 12, e.Bounds.Bottom - 1);
        }
    }

    private static string DisplayTitle(TrackState track)
    {
        if (!string.IsNullOrEmpty(track.Title))
        {
            return track.Title;
        }

        string file = Path.GetFileName(track.Url);
        return string.IsNullOrEmpty(file) ? track.Url : file;
    }

    private static string JoinDetail(string artist, string album)
    {
        bool hasArtist = !string.IsNullOrEmpty(artist);
        bool hasAlbum = !string.IsNullOrEmpty(album);
        if (hasArtist && hasAlbum)
        {
            return artist + "   ·   " + album;
        }

        return hasArtist ? artist : (hasAlbum ? album : "");
    }
}
