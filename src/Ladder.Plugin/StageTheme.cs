namespace MusicBeePlugin;

internal sealed class TrackChoice
{
    public string Url;
    public string Title;
    public string Artist;
    public string Source;
}

internal sealed class StageTheme
{
    public Color Page;
    public Color Card;
    public Color CardHover;
    public Color Text;
    public Color Muted;
    public Color Line;
    public Color Accent;
    public Color Pick;
    public Color PickHover;

    public static StageTheme Dark()
    {
        return new StageTheme
        {
            Page = Color.FromArgb(14, 14, 14),
            Card = Color.FromArgb(26, 26, 26),
            CardHover = Color.FromArgb(34, 34, 34),
            Text = Color.FromArgb(244, 244, 244),
            Muted = Color.FromArgb(154, 154, 154),
            Line = Color.FromArgb(48, 48, 48),
            Accent = Color.FromArgb(255, 255, 255),
            Pick = Color.FromArgb(244, 244, 244),
            PickHover = Color.FromArgb(255, 255, 255),
        };
    }

    public static StageTheme Light()
    {
        return new StageTheme
        {
            Page = Color.FromArgb(244, 244, 242),
            Card = Color.White,
            CardHover = Color.FromArgb(250, 250, 250),
            Text = Color.FromArgb(20, 20, 20),
            Muted = Color.FromArgb(110, 110, 110),
            Line = Color.FromArgb(226, 226, 226),
            Accent = Color.FromArgb(20, 20, 20),
            Pick = Color.FromArgb(22, 22, 22),
            PickHover = Color.FromArgb(50, 50, 50),
        };
    }

    public static StageTheme For(Color background)
    {
        return background.GetBrightness() > 0.55f ? Light() : Dark();
    }
}
