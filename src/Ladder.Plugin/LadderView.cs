using System.Globalization;
using Ladder;

namespace MusicBeePlugin;

internal sealed class CoverBox : Control
{
    private Image _art;
    private bool _playing;
    private string _mark = "";

    public CoverBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(24, 24, 24);
    }

    public Image Art
    {
        get { return _art; }
        set
        {
            _art = value;
            Invalidate();
        }
    }

    public bool Playing
    {
        get { return _playing; }
        set
        {
            _playing = value;
            Invalidate();
        }
    }

    public string Mark
    {
        get { return _mark; }
        set
        {
            _mark = value ?? "";
            Invalidate();
        }
    }

    public Color FrameColor { get; set; } = Color.White;

    public event EventHandler Activate;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Activate?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        Rectangle box = ClientRectangle;
        box.Inflate(-1, -1);
        if (_playing)
        {
            using (Pen pen = new Pen(FrameColor, 4))
            {
                graphics.DrawRectangle(pen, 2, 2, Width - 6, Height - 6);
            }

            box.Inflate(-10, -10);
        }

        if (_art != null)
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(_art, Fit(_art.Size, box));
        }
        else if (_mark.Length > 0)
        {
            using (Font font = new Font(Font.FontFamily, Math.Max(28, box.Height / 5f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(FrameColor))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString(_mark.Substring(0, 1), font, brush, box, format);
            }
        }

        DrawPlay(graphics, box);
    }

    private void DrawPlay(Graphics graphics, Rectangle box)
    {
        int size = Math.Max(28, box.Width / 10);
        Rectangle badge = new Rectangle(box.X + 12, box.Bottom - size - 12, size, size);
        using (Brush wash = new SolidBrush(Color.FromArgb(_playing ? 230 : 160, 0, 0, 0)))
        {
            graphics.FillEllipse(wash, badge);
        }

        Point[] triangle =
        {
            new Point(badge.X + size / 3, badge.Y + size / 4),
            new Point(badge.X + size / 3, badge.Bottom - size / 4),
            new Point(badge.Right - size / 5, badge.Y + size / 2),
        };
        using (Brush brush = new SolidBrush(Color.White))
        {
            graphics.FillPolygon(brush, triangle);
        }
    }

    private static Rectangle Fit(Size image, Rectangle bounds)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return bounds;
        }

        float scale = Math.Min(bounds.Width / (float)image.Width, bounds.Height / (float)image.Height);
        int width = Math.Max(1, (int)(image.Width * scale));
        int height = Math.Max(1, (int)(image.Height * scale));
        return new Rectangle(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);
    }
}

internal sealed class LadderView : UserControl
{
    private readonly LadderSession _session;
    private readonly Label _status;
    private readonly CoverBox _leftCover;
    private readonly CoverBox _rightCover;
    private readonly Label _leftTitle;
    private readonly Label _rightTitle;
    private readonly Label _leftDetail;
    private readonly Label _rightDetail;
    private readonly Label _leftScore;
    private readonly Label _rightScore;
    private readonly Button _leftPrefer;
    private readonly Button _rightPrefer;
    private readonly Button _same;
    private readonly Button _skip;
    private readonly Label _hint;

    public LadderView(LadderSession session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        _session = session;
        DoubleBuffered = true;
        TabStop = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(18, 18, 18);
        ForeColor = Color.FromArgb(245, 245, 245);
        Font = new Font("Segoe UI", 10f);

        _status = MakeLabel(11f, FontStyle.Regular, true);
        _hint = MakeLabel(9f, FontStyle.Regular, true);
        _hint.Text = "Click a cover, or press the left and right arrows, to switch which song is playing.";

        _leftCover = new CoverBox();
        _rightCover = new CoverBox();
        _leftCover.Activate += (sender, args) => Play(true);
        _rightCover.Activate += (sender, args) => Play(false);

        _leftTitle = MakeLabel(18f, FontStyle.Bold, false);
        _rightTitle = MakeLabel(18f, FontStyle.Bold, false);
        _leftDetail = MakeLabel(11f, FontStyle.Regular, true);
        _rightDetail = MakeLabel(11f, FontStyle.Regular, true);
        _leftScore = MakeLabel(12f, FontStyle.Regular, false);
        _rightScore = MakeLabel(12f, FontStyle.Regular, false);

        _leftPrefer = MakeButton("Prefer this");
        _rightPrefer = MakeButton("Prefer this");
        _same = MakeButton("About the same");
        _skip = MakeButton("Skip");

        _leftPrefer.Click += (sender, args) => Choose(ComparisonChoice.Left);
        _rightPrefer.Click += (sender, args) => Choose(ComparisonChoice.Right);
        _same.Click += (sender, args) => Choose(ComparisonChoice.Same);
        _skip.Click += (sender, args) => Run(delegate { _session.Skip(); });

        Controls.AddRange(new Control[]
        {
            _status, _leftCover, _rightCover, _leftTitle, _rightTitle, _leftDetail, _rightDetail,
            _leftScore, _rightScore, _leftPrefer, _rightPrefer, _same, _skip, _hint,
        });

        _session.Changed += OnChanged;
        Disposed += (sender, args) => _session.Changed -= OnChanged;
        RefreshView();
    }

    public void ApplyPalette(Color background, Color text, Color muted, Color accent)
    {
        BackColor = background;
        ForeColor = text;
        _status.ForeColor = text;
        _hint.ForeColor = muted;
        _leftTitle.ForeColor = text;
        _rightTitle.ForeColor = text;
        _leftDetail.ForeColor = muted;
        _rightDetail.ForeColor = muted;
        _leftScore.ForeColor = text;
        _rightScore.ForeColor = text;
        _leftCover.BackColor = background;
        _rightCover.BackColor = background;
        _leftCover.FrameColor = accent;
        _rightCover.FrameColor = accent;
        Color buttonBack = Blend(background, text, 0.12f);
        foreach (Button button in new[] { _leftPrefer, _rightPrefer, _same, _skip })
        {
            button.BackColor = buttonBack;
            button.ForeColor = text;
            button.FlatAppearance.BorderColor = muted;
        }

        Invalidate(true);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.Left)
        {
            Play(true);
            return true;
        }

        if (keyData == Keys.Right)
        {
            Play(false);
            return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutStage();
    }

    private void OnChanged(object sender, EventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(RefreshView));
            return;
        }

        RefreshView();
    }

    private void RefreshView()
    {
        ComparisonPrompt prompt = _session.Controller.Current;
        bool asking = prompt != null;
        _leftCover.Visible = asking;
        _rightCover.Visible = asking;
        _leftTitle.Visible = asking;
        _rightTitle.Visible = asking;
        _leftDetail.Visible = asking;
        _rightDetail.Visible = asking;
        _leftScore.Visible = asking;
        _rightScore.Visible = asking;
        _leftPrefer.Visible = asking;
        _rightPrefer.Visible = asking;
        _same.Visible = asking;
        _skip.Visible = asking;
        _hint.Visible = asking;

        if (!string.IsNullOrEmpty(_session.LoadError))
        {
            _status.Text = _session.LoadError;
        }
        else if (!string.IsNullOrEmpty(_session.Note))
        {
            _status.Text = _session.Note;
        }
        else if (_session.Controller.NeedsAnotherTrack)
        {
            _status.Text = "Ladder needs at least two tracks in the library.";
        }
        else if (prompt != null && prompt.Mode == ComparisonMode.Place)
        {
            _status.Text = "Does the song on the left beat the one on the right?";
        }
        else if (prompt != null)
        {
            _status.Text = "Which of these do you prefer?";
        }
        else
        {
            _status.Text = "Place a track, or sharpen the pairs that are still close.";
        }

        if (asking)
        {
            IReadOnlyList<RankedTrack> rank = _session.Controller.Rank(DateTime.UtcNow);
            Fill(_leftCover, _leftTitle, _leftDetail, _leftScore, prompt.Left, rank);
            Fill(_rightCover, _rightTitle, _rightDetail, _rightScore, prompt.Right, rank);
        }

        LayoutStage();
    }

    private void Fill(CoverBox cover, Label title, Label detail, Label score, TrackSnapshot track, IReadOnlyList<RankedTrack> rank)
    {
        cover.Art = _session.ArtFor(track.Url);
        cover.Mark = DisplayTitle(track);
        cover.Playing = string.Equals(_session.PlayingUrl, track.Url, StringComparison.Ordinal);
        title.Text = DisplayTitle(track);
        detail.Text = JoinDetail(track.Artist, track.Album);
        score.Text = ScoreFor(track.Url, rank);
    }

    private static string ScoreFor(string url, IReadOnlyList<RankedTrack> rank)
    {
        foreach (RankedTrack row in rank)
        {
            if (!string.Equals(row.Track.Url, url, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrEmpty(row.Gap))
            {
                return "#" + row.Rank.ToString(CultureInfo.InvariantCulture) + "   " + row.ScoreText;
            }

            return "#" + row.Rank.ToString(CultureInfo.InvariantCulture) + "   " + row.ScoreText + "   " + row.Gap;
        }

        return RatingText.NotPlaced;
    }

    private void LayoutStage()
    {
        int pad = 28;
        int width = ClientSize.Width;
        int height = ClientSize.Height;
        if (width < 40 || height < 40)
        {
            return;
        }

        _status.SetBounds(pad, 16, width - pad * 2, 28);

        int footerTop = height - 36;

        if (!_leftCover.Visible)
        {
            return;
        }

        int center = 150;
        int textBlock = 132;
        int top = 52;
        int bottom = footerTop - 12;
        int side = Math.Min((width - pad * 2 - center - 48) / 2, bottom - top - textBlock);
        if (side < 96)
        {
            side = 96;
        }

        int group = side * 2 + center + 48;
        int x = Math.Max(pad, (width - group) / 2);
        int y = top + Math.Max(0, (bottom - top - textBlock - side) / 2);

        PlaceSide(_leftCover, _leftTitle, _leftDetail, _leftScore, _leftPrefer, x, y, side);
        PlaceSide(_rightCover, _rightTitle, _rightDetail, _rightScore, _rightPrefer, x + side + center + 48, y, side);

        int midX = x + side + 24;
        _same.SetBounds(midX, y + side / 2 - 20, center, 34);
        _skip.SetBounds(midX, y + side / 2 + 22, center, 34);
        _hint.SetBounds(pad, footerTop - 28, width - pad * 2, 22);
    }

    private static void PlaceSide(CoverBox cover, Label title, Label detail, Label score, Button prefer, int x, int y, int side)
    {
        cover.SetBounds(x, y, side, side);
        title.SetBounds(x, y + side + 12, side, 28);
        detail.SetBounds(x, y + side + 40, side, 22);
        score.SetBounds(x, y + side + 62, side, 22);
        prefer.SetBounds(x, y + side + 90, side, 36);
    }

    private void Play(bool left)
    {
        ComparisonPrompt prompt = _session.Controller.Current;
        if (prompt == null)
        {
            return;
        }

        _session.RequestPlay(left ? prompt.Left.Url : prompt.Right.Url);
    }

    private void Choose(ComparisonChoice choice)
    {
        Run(delegate { _session.Choose(choice); });
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            _status.Text = exception.Message;
        }
    }

    private Label MakeLabel(float size, FontStyle style, bool muted)
    {
        return new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            ForeColor = muted ? Color.FromArgb(170, 170, 170) : ForeColor,
            Font = new Font(Font.FontFamily, size, style, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private Button MakeButton(string text)
    {
        Button button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(36, 36, 36),
            ForeColor = ForeColor,
            Font = Font,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
        return button;
    }

    private static string DisplayTitle(TrackSnapshot track)
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
            return artist + "  ·  " + album;
        }

        if (hasArtist)
        {
            return artist;
        }

        return hasAlbum ? album : "";
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        int channel(byte start, byte end)
        {
            return start + (int)((end - start) * amount);
        }

        return Color.FromArgb(channel(from.R, to.R), channel(from.G, to.G), channel(from.B, to.B));
    }
}
