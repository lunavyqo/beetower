namespace MusicBeePlugin;

internal sealed class Meter : Control
{
    private double _fraction;

    public Meter()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Height = 8;
        Cursor = Cursors.Hand;
        TrackColor = Color.FromArgb(48, 48, 48);
        FillColor = Color.FromArgb(230, 230, 230);
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
        e.Graphics.Clear(BackColor);
        Rectangle track = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        using (Brush trackBrush = new SolidBrush(TrackColor))
        {
            e.Graphics.FillRectangle(trackBrush, track);
        }

        int fillWidth = (int)Math.Round(track.Width * _fraction);
        if (fillWidth <= 0)
        {
            return;
        }

        using (Brush fillBrush = new SolidBrush(FillColor))
        {
            e.Graphics.FillRectangle(fillBrush, new Rectangle(track.X, track.Y, fillWidth, track.Height));
        }
    }
}
