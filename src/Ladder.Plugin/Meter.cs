using System.Drawing.Drawing2D;

namespace MusicBeePlugin;

internal sealed class Meter : Control
{
    private double _fraction;

    public Meter()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Height = 22;
        Cursor = Cursors.Hand;
        TrackColor = Color.FromArgb(214, 214, 214);
        FillColor = Color.FromArgb(20, 20, 20);
    }

    public event EventHandler<double> Scrubbed;

    public double Fraction
    {
        get { return _fraction; }
        set
        {
            if (value < 0)
            {
                value = 0;
            }

            if (value > 1)
            {
                value = 1;
            }

            _fraction = value;
            Invalidate();
        }
    }

    public Color TrackColor { get; set; }

    public Color FillColor { get; set; }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Scrub(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left)
        {
            Scrub(e.X);
        }
    }

    private void Scrub(int x)
    {
        if (Width <= 1)
        {
            return;
        }

        double fraction = x / (double)(Width - 1);
        if (fraction < 0)
        {
            fraction = 0;
        }

        if (fraction > 1)
        {
            fraction = 1;
        }

        Fraction = fraction;
        if (Scrubbed != null)
        {
            Scrubbed(this, fraction);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);
        int barHeight = Math.Min(6, Math.Max(4, Height / 4));
        int y = (Height - barHeight) / 2;
        Rectangle track = new Rectangle(0, y, Math.Max(0, Width - 1), barHeight);
        FillPill(graphics, track, TrackColor);
        int fillWidth = (int)Math.Round(track.Width * _fraction);
        if (fillWidth <= 0)
        {
            return;
        }

        if (fillWidth < barHeight)
        {
            fillWidth = barHeight;
        }

        if (fillWidth > track.Width)
        {
            fillWidth = track.Width;
        }

        FillPill(graphics, new Rectangle(track.X, track.Y, fillWidth, track.Height), FillColor);
    }

    private static void FillPill(Graphics graphics, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        int diameter = bounds.Height;
        if (diameter > bounds.Width)
        {
            diameter = bounds.Width;
        }

        using (GraphicsPath path = new GraphicsPath())
        using (Brush brush = new SolidBrush(color))
        {
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 90, 180);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 180);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }
    }
}
