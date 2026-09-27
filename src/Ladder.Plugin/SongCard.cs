using System.Drawing.Drawing2D;

namespace MusicBeePlugin;

internal sealed class SongCard : Control
{
    private readonly Meter _meter;
    private readonly Font _titleFont = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _detailFont = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _scoreFont = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _pickFont = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _timeFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    private Image _art;
    private string _title = "";
    private string _detail = "";
    private string _score = "";
    private string _time = "0:00";
    private bool _playing;
    private bool _hover;
    private bool _pickHover;
    private Rectangle _artRect;
    private Rectangle _pickRect;
    private StageTheme _theme = StageTheme.Dark();

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
        BackColor = _theme.Page;
        _meter.BackColor = _theme.Card;
        _meter.TrackColor = _theme.Line;
        _meter.FillColor = _theme.Accent;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width < 8 || Height < 8)
        {
            return;
        }

        LayoutParts();
        using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), 22))
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
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pickHover = false;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool overPick = _pickRect.Contains(e.Location);
        if (overPick != _pickHover)
        {
            _pickHover = overPick;
            Invalidate();
        }
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

        if (_artRect.Contains(e.Location) && PlayRequested != null)
        {
            PlayRequested(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(_theme.Page);
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = Rounded(bounds, 22))
        using (Brush fill = new SolidBrush(_hover ? _theme.CardHover : _theme.Card))
        {
            graphics.FillPath(fill, path);
            if (_playing)
            {
                using (Pen pen = new Pen(_theme.Accent, 2f))
                {
                    graphics.DrawPath(pen, path);
                }
            }
        }

        if (_art != null && _artRect.Width > 0)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SetClip(Rounded(_artRect, 14));
            graphics.DrawImage(_art, Fit(_art.Size, _artRect));
            graphics.ResetClip();
        }
        else if (_title.Length > 0 && _artRect.Width > 0)
        {
            using (Brush wash = new SolidBrush(_theme.Line))
            using (GraphicsPath artPath = Rounded(_artRect, 14))
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

        float textLeft = 24;
        float textWidth = Math.Max(10, Width - 48);
        using (Brush text = new SolidBrush(_theme.Text))
        using (Brush muted = new SolidBrush(_theme.Muted))
        using (StringFormat ellipsis = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            float y = _artRect.Bottom + 16;
            graphics.DrawString(_title, _titleFont, text, new RectangleF(textLeft, y, textWidth, 30), ellipsis);
            graphics.DrawString(_detail, _detailFont, muted, new RectangleF(textLeft, y + 32, textWidth, 20), ellipsis);
            graphics.DrawString(_score, _scoreFont, muted, new RectangleF(textLeft, y + 54, textWidth, 18), ellipsis);
            graphics.DrawString(_time, _timeFont, muted, new RectangleF(textLeft, _meter.Bottom + 4, textWidth, 16), ellipsis);
        }

        using (Brush pick = new SolidBrush(_pickHover ? _theme.PickHover : _theme.Pick))
        using (Pen line = new Pen(_theme.Line))
        using (Brush label = new SolidBrush(_theme.Text))
        using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.FillRectangle(pick, _pickRect);
            graphics.DrawLine(line, _pickRect.Left + 18, _pickRect.Top, _pickRect.Right - 18, _pickRect.Top);
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

    private void LayoutParts()
    {
        int pad = 22;
        int pickHeight = 54;
        int textBlock = 108;
        int art = Math.Min(Width - pad * 2, Height - pad - textBlock - pickHeight);
        if (art < 48)
        {
            art = 48;
        }

        _artRect = new Rectangle((Width - art) / 2, pad, art, art);
        _meter.SetBounds(pad, _artRect.Bottom + 78, Math.Max(10, Width - pad * 2), 6);
        _pickRect = new Rectangle(0, Height - pickHeight, Width, pickHeight);
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

        float scale = Math.Min(bounds.Width / (float)image.Width, bounds.Height / (float)image.Height);
        int width = Math.Max(1, (int)(image.Width * scale));
        int height = Math.Max(1, (int)(image.Height * scale));
        return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }
}
