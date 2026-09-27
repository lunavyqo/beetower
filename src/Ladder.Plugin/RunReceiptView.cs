using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MusicBeePlugin;

internal sealed class RunReceipt
{
    public string Url;
    public string Title;
    public string Detail;
    public string Score;
    public string OtherLine;
    public bool Placed;
}

internal sealed class RunReceiptView : UserControl
{
    private readonly Label _headline;
    private readonly Label _title;
    private readonly Label _detail;
    private readonly Label _score;
    private readonly Label _other;
    private readonly Button _again;
    private readonly Font _markFont = new Font("Segoe UI", 42f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _scorePlacedFont = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _scoreOpenFont = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Point);
    private Image _art;
    private Rectangle _artRect;
    private StageTheme _theme = StageTheme.Light();

    public RunReceiptView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        _headline = MakeLabel(new Font("Segoe UI", 26f, FontStyle.Bold, GraphicsUnit.Point), ContentAlignment.MiddleLeft);
        _title = MakeLabel(new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Point), ContentAlignment.MiddleLeft);
        _detail = MakeLabel(new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point), ContentAlignment.MiddleLeft);
        _score = MakeLabel(_scorePlacedFont, ContentAlignment.MiddleLeft);
        _other = MakeLabel(new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point), ContentAlignment.MiddleLeft);
        _again = new Button
        {
            Text = "Rate another song",
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point),
            UseVisualStyleBackColor = false,
        };
        _again.FlatAppearance.BorderSize = 0;
        _again.Click += (sender, args) =>
        {
            if (Dismissed != null)
            {
                Dismissed(this, EventArgs.Empty);
            }
        };
        Controls.Add(_headline);
        Controls.Add(_title);
        Controls.Add(_detail);
        Controls.Add(_score);
        Controls.Add(_other);
        Controls.Add(_again);
        Apply(_theme);
    }

    public event EventHandler Dismissed;

    public void Apply(StageTheme theme)
    {
        _theme = theme ?? StageTheme.Light();
        BackColor = _theme.Page;
        Color label = _theme.Text;
        Style(_headline, label);
        Style(_title, label);
        Style(_detail, _theme.Muted);
        Style(_score, label);
        Style(_other, _theme.Muted);
        _again.BackColor = _theme.Pick;
        _again.ForeColor = _theme.Pick.GetBrightness() > 0.6f ? Color.FromArgb(16, 16, 16) : Color.White;
        _again.FlatAppearance.MouseOverBackColor = _theme.PickHover;
        _again.FlatAppearance.MouseDownBackColor = _theme.PickHover;
        Invalidate(true);
    }

    public void Show(RunReceipt receipt, Image art)
    {
        receipt = receipt ?? new RunReceipt();
        _art = art;
        _headline.Text = "This run is finished.";
        _title.Text = receipt.Title ?? "";
        _detail.Text = receipt.Detail ?? "";
        _score.Text = receipt.Placed ? (receipt.Score ?? "") : "It has not joined the ladder.";
        _other.Text = receipt.OtherLine ?? "";
        _score.Font = receipt.Placed ? _scorePlacedFont : _scoreOpenFont;
        Style(_score, receipt.Placed ? _theme.Text : _theme.Muted);
        PerformLayout();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int width = Math.Min(560, Math.Max(240, ClientSize.Width - 80));
        int x = Math.Max(24, (ClientSize.Width - width) / 2);
        bool other = _other.Text.Length > 0;
        int art = _art == null && _title.Text.Length == 0 ? 0 : Math.Min(220, width);
        int block = 52 + 18 + art + 16 + 40 + 28 + 48 + 22 + (other ? 28 : 0) + 48;
        int y = Math.Max(28, (ClientSize.Height - block) / 2);
        _headline.SetBounds(x, y, width, 52);
        y += 70;
        _artRect = art == 0 ? Rectangle.Empty : new Rectangle(x, y, art, art);
        if (art > 0)
        {
            y += art + 18;
        }

        _title.SetBounds(x, y, width, 36);
        _detail.SetBounds(x, y + 36, width, 26);
        _score.SetBounds(x, y + 66, width, 40);
        y += 112;
        _other.Visible = other;
        if (other)
        {
            _other.SetBounds(x, y, width, 26);
            y += 32;
        }

        _again.SetBounds(x, y + 8, width, 48);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(_theme.Page);
        if (_artRect.Width <= 0)
        {
            return;
        }

        using (GraphicsPath path = Rounded(_artRect, 16))
        {
            graphics.SetClip(path);
            if (_art != null)
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(_art, Fit(_art.Size, _artRect));
            }
            else
            {
                using (Brush wash = new SolidBrush(_theme.Line))
                {
                    graphics.FillPath(wash, path);
                }

                string mark = _title.Text.Length == 0 ? "?" : _title.Text.Substring(0, 1);
                using (Brush brush = new SolidBrush(_theme.Text))
                using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.DrawString(mark, _markFont, brush, _artRect, format);
                }
            }

            graphics.ResetClip();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _markFont.Dispose();
            _scorePlacedFont.Dispose();
            _scoreOpenFont.Dispose();
        }
    }

    private static Label MakeLabel(Font font, ContentAlignment align)
    {
        return new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Font = font,
            TextAlign = align,
        };
    }

    private void Style(Label label, Color color)
    {
        label.ForeColor = color;
        label.BackColor = _theme.Page;
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
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
