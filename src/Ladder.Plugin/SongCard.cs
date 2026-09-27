using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Ladder;

namespace MusicBeePlugin;

internal struct CardLayout
{
    public int Width;
    public int Height;
    public int PickTop;
    public int PickHeight;
}

internal sealed class SongCard : Control
{
    private readonly Meter _meter;
    private readonly Font _titleFont = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _detailFont = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _scoreFont = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _pickFont = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _timeFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    private Image _art;
    private string _title = "";
    private string _detail = "";
    private string _score = "";
    private string _time = "0:00";
    private bool _playing;
    private bool _hover;
    private bool _artHover;
    private bool _pickHover;
    private Rectangle _artRect;
    private Rectangle _titleRect;
    private Rectangle _detailRect;
    private Rectangle _scoreRect;
    private Rectangle _timeRect;
    private Rectangle _pickRect;
    private StageTheme _theme = StageTheme.Dark();
    private bool _pickIsLight;

    public SongCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        _meter = new Meter();
        _meter.Scrubbed += (sender, fraction) =>
        {
            if (Scrubbed != null)
            {
                Scrubbed(this, fraction);
            }
        };
        Controls.Add(_meter);
        Apply(_theme);
    }

    public event EventHandler PlayRequested;

    public event EventHandler PreferRequested;

    public event EventHandler<double> Scrubbed;

    public Image Art
    {
        get { return _art; }
        set
        {
            _art = value;
            Invalidate();
        }
    }

    public string TitleText
    {
        get { return _title; }
        set
        {
            _title = value ?? "";
            Invalidate();
        }
    }

    public string DetailText
    {
        get { return _detail; }
        set
        {
            _detail = value ?? "";
            Invalidate();
        }
    }

    public string ScoreText
    {
        get { return _score; }
        set
        {
            _score = value ?? "";
            Invalidate();
        }
    }

    public string TimeText
    {
        get { return _time; }
        set
        {
            _time = value ?? "";
            if (Width > 8 && Height > 8)
            {
                LayoutParts();
            }

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

    public double Position
    {
        get { return _meter.Fraction; }
        set { _meter.Fraction = value; }
    }

    public void Apply(StageTheme theme)
    {
        _theme = theme ?? StageTheme.Dark();
        _pickIsLight = _theme.Pick.GetBrightness() > 0.6f;
        BackColor = _theme.Page;
        SyncMeter();
        Invalidate();
    }

    public CardLayout Measure(int maxWidth, int maxHeight)
    {
        Metrics metrics = MetricsFor();
        if (maxWidth < metrics.Pad * 2 + metrics.MinArt)
        {
            maxWidth = metrics.Pad * 2 + metrics.MinArt;
        }

        if (maxHeight < metrics.Chrome + metrics.MinArt)
        {
            maxHeight = metrics.Chrome + metrics.MinArt;
        }

        int art = Math.Min(maxWidth - metrics.Pad * 2, maxHeight - metrics.Chrome);
        return new CardLayout
        {
            Width = art + metrics.Pad * 2,
            Height = art + metrics.Chrome,
            PickTop = metrics.Pad + art + metrics.AfterArt + metrics.TitleH + metrics.DetailH + metrics.ScoreH + metrics.BeforeTransport + metrics.TransportH + metrics.BeforePick,
            PickHeight = metrics.PickH,
        };
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width < 8 || Height < 8)
        {
            return;
        }

        LayoutParts();
        using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), Scale(18)))
        {
            Region previous = Region;
            Region = new Region(path);
            if (previous != null)
            {
                previous.Dispose();
            }
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        SyncMeter();
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _artHover = false;
        _pickHover = false;
        SyncMeter();
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool overPick = _pickRect.Contains(e.Location);
        bool overArt = _artRect.Contains(e.Location);
        if (overPick == _pickHover && overArt == _artHover)
        {
            return;
        }

        _pickHover = overPick;
        _artHover = overArt;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _meter.Bounds.Contains(e.Location))
        {
            return;
        }

        if (_pickRect.Contains(e.Location))
        {
            if (PreferRequested != null)
            {
                PreferRequested(this, EventArgs.Empty);
            }

            return;
        }

        if (PlayRequested != null)
        {
            PlayRequested(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(_theme.Page);
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = Rounded(bounds, Scale(18)))
        using (Brush fill = new SolidBrush(_hover ? _theme.CardHover : _theme.Card))
        using (Pen edge = new Pen(_theme.Line, 1f))
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(edge, path);
        }

        if (_art != null && _artRect.Width > 0)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using (GraphicsPath clip = Rounded(_artRect, Scale(12)))
            {
                graphics.SetClip(clip);
                graphics.DrawImage(_art, Fit(_art.Size, _artRect));
                graphics.ResetClip();
            }
        }
        else if (_title.Length > 0 && _artRect.Width > 0)
        {
            using (Brush wash = new SolidBrush(_theme.Line))
            using (GraphicsPath artPath = Rounded(_artRect, Scale(12)))
            {
                graphics.FillPath(wash, artPath);
            }

            using (Font mark = new Font("Segoe UI", Math.Max(28f, _artRect.Height / 5f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(_theme.Text))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString(_title.Substring(0, 1), mark, brush, _artRect, format);
            }
        }

        if (_playing && _artRect.Width > 0)
        {
            using (Pen ring = new Pen(_theme.Accent, Math.Max(2f, Scale(3))))
            using (GraphicsPath artPath = Rounded(_artRect, Scale(12)))
            {
                graphics.DrawPath(ring, artPath);
            }
        }

        DrawListenMark(graphics);

        bool unplaced = string.Equals(_score, RatingText.NotPlaced, StringComparison.Ordinal);
        using (Brush text = new SolidBrush(_theme.Text))
        using (Brush muted = new SolidBrush(_theme.Muted))
        using (StringFormat ellipsis = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
        using (StringFormat timeFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            graphics.DrawString(_title, _titleFont, text, _titleRect, ellipsis);
            graphics.DrawString(_detail, _detailFont, muted, _detailRect, ellipsis);
            graphics.DrawString(_score, _scoreFont, unplaced ? muted : text, _scoreRect, ellipsis);
            graphics.DrawString(_time, _timeFont, _playing ? text : muted, _timeRect, timeFormat);
        }

        if (_pickRect.Width <= 0 || _pickRect.Height <= 0)
        {
            return;
        }

        Color pickColor = _pickHover ? _theme.PickHover : _theme.Pick;
        Color pickText = _pickIsLight ? Color.FromArgb(16, 16, 16) : Color.White;
        using (Brush pick = new SolidBrush(pickColor))
        using (Brush label = new SolidBrush(pickText))
        using (GraphicsPath pickPath = Rounded(_pickRect, Scale(10)))
        using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.FillPath(pick, pickPath);
            graphics.DrawString("Pick this", _pickFont, label, _pickRect, center);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _detailFont.Dispose();
            _scoreFont.Dispose();
            _pickFont.Dispose();
            _timeFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawListenMark(Graphics graphics)
    {
        if (!_artHover || _artRect.Width < Scale(80))
        {
            return;
        }

        int size = Scale(56);
        Rectangle bubble = new Rectangle(
            _artRect.X + (_artRect.Width - size) / 2,
            _artRect.Y + (_artRect.Height - size) / 2,
            size,
            size);
        using (Brush wash = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
        {
            graphics.FillEllipse(wash, bubble);
        }

        using (Brush mark = new SolidBrush(Color.FromArgb(20, 20, 20)))
        {
            if (_playing)
            {
                int barWidth = Scale(5);
                int barHeight = Scale(18);
                int gap = Scale(6);
                int center = bubble.X + bubble.Width / 2;
                int barTop = bubble.Y + (bubble.Height - barHeight) / 2;
                graphics.FillRectangle(mark, center - gap / 2 - barWidth, barTop, barWidth, barHeight);
                graphics.FillRectangle(mark, center + gap / 2, barTop, barWidth, barHeight);
                return;
            }

            int tri = Scale(16);
            int left = bubble.X + (bubble.Width - tri) / 2 + Scale(2);
            int top = bubble.Y + (bubble.Height - tri) / 2;
            Point[] points = new Point[]
            {
                new Point(left, top),
                new Point(left, top + tri),
                new Point(left + tri, bubble.Y + bubble.Height / 2),
            };
            graphics.FillPolygon(mark, points);
        }
    }

    private void SyncMeter()
    {
        _meter.BackColor = _hover ? _theme.CardHover : _theme.Card;
        _meter.TrackColor = _theme.Page.GetBrightness() > 0.55f
            ? Color.FromArgb(214, 214, 214)
            : Color.FromArgb(68, 68, 68);
        _meter.FillColor = _theme.Accent;
        _meter.Invalidate();
    }

    private void LayoutParts()
    {
        Metrics metrics = MetricsFor();
        int inner = Math.Max(metrics.MinArt, Width - metrics.Pad * 2);
        int art = Math.Min(inner, Math.Max(metrics.MinArt, Height - metrics.Chrome));
        int x = metrics.Pad + Math.Max(0, (inner - art) / 2);
        _artRect = new Rectangle(x, metrics.Pad, art, art);
        int y = _artRect.Bottom + metrics.AfterArt;
        _titleRect = new Rectangle(x, y, art, metrics.TitleH);
        y += metrics.TitleH;
        _detailRect = new Rectangle(x, y, art, metrics.DetailH);
        y += metrics.DetailH;
        _scoreRect = new Rectangle(x, y, art, metrics.ScoreH);
        y += metrics.ScoreH + metrics.BeforeTransport;
        int timeWidth = TimeWidth(art);
        int gap = Scale(12);
        int barWidth = Math.Max(Scale(24), art - timeWidth - gap);
        _meter.SetBounds(x, y, barWidth, metrics.TransportH);
        _timeRect = new Rectangle(x + art - timeWidth, y, timeWidth, metrics.TransportH);
        _pickRect = new Rectangle(x, y + metrics.TransportH + metrics.BeforePick, art, metrics.PickH);
    }

    private int TimeWidth(int art)
    {
        int measured = TextRenderer.MeasureText("00:00  /  00:00", _timeFont, new Size(800, 80), TextFormatFlags.NoPadding).Width + Scale(6);
        int cap = Math.Max(Scale(72), art / 2);
        if (measured > cap)
        {
            measured = cap;
        }

        return measured;
    }

    private Metrics MetricsFor()
    {
        int pad = Scale(18);
        int afterArt = Scale(14);
        int titleH = LineHeight(_titleFont);
        int detailH = LineHeight(_detailFont);
        int scoreH = LineHeight(_scoreFont);
        int beforeTransport = Scale(10);
        int transportH = Math.Max(LineHeight(_timeFont), Scale(22));
        int beforePick = Scale(14);
        int pickH = Scale(44);
        int afterPick = Scale(18);
        return new Metrics
        {
            Pad = pad,
            AfterArt = afterArt,
            TitleH = titleH,
            DetailH = detailH,
            ScoreH = scoreH,
            BeforeTransport = beforeTransport,
            TransportH = transportH,
            BeforePick = beforePick,
            PickH = pickH,
            AfterPick = afterPick,
            MinArt = Scale(96),
            Chrome = pad + afterArt + titleH + detailH + scoreH + beforeTransport + transportH + beforePick + pickH + afterPick,
        };
    }

    private int LineHeight(Font font)
    {
        return TextRenderer.MeasureText("Ag", font, new Size(400, 200), TextFormatFlags.NoPadding).Height + Scale(4);
    }

    private int Scale(int pixels)
    {
        int dpi = DeviceDpi;
        if (dpi < 96)
        {
            dpi = 96;
        }

        return (int)Math.Round(pixels * (dpi / 96.0));
    }

    private struct Metrics
    {
        public int Pad;
        public int AfterArt;
        public int TitleH;
        public int DetailH;
        public int ScoreH;
        public int BeforeTransport;
        public int TransportH;
        public int BeforePick;
        public int PickH;
        public int AfterPick;
        public int MinArt;
        public int Chrome;
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        if (diameter > bounds.Width)
        {
            diameter = Math.Max(2, bounds.Width);
        }

        if (diameter > bounds.Height)
        {
            diameter = Math.Max(2, bounds.Height);
        }

        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Rectangle Fit(Size image, Rectangle bounds)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return bounds;
        }

        float scale = Math.Max(bounds.Width / (float)image.Width, bounds.Height / (float)image.Height);
        int width = Math.Max(1, (int)(image.Width * scale));
        int height = Math.Max(1, (int)(image.Height * scale));
        return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }
}
