using System.Globalization;
using Ladder;

namespace MusicBeePlugin;

internal sealed class LadderView : UserControl
{
    private readonly LadderSession _session;
    private readonly Label _status;
    private readonly SongCard _left;
    private readonly SongCard _right;
    private readonly Button _same;
    private readonly Button _skip;
    private readonly Button _different;
    private readonly SongPicker _picker;
    private StageTheme _theme = StageTheme.Dark();

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
        Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = _theme.Page;

        _status = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _left = new SongCard();
        _right = new SongCard();
        _left.PlayRequested += (sender, args) => Play(true);
        _right.PlayRequested += (sender, args) => Play(false);
        _left.PreferRequested += (sender, args) => Choose(ComparisonChoice.Left);
        _right.PreferRequested += (sender, args) => Choose(ComparisonChoice.Right);
        _left.Scrubbed += (sender, fraction) => Scrub(true, fraction);
        _right.Scrubbed += (sender, fraction) => Scrub(false, fraction);

        _same = Quiet("About the same");
        _skip = Quiet("Skip");
        _different = Quiet("Pick a different song");
        _same.Click += (sender, args) => Choose(ComparisonChoice.Same);
        _skip.Click += (sender, args) => Run(delegate { _session.Skip(); });
        _different.Click += (sender, args) =>
        {
            if (_session.AbandonRating != null)
            {
                _session.AbandonRating();
            }
        };

        _picker = new SongPicker { Dock = DockStyle.Fill };
        _picker.Picked += (sender, url) => _session.PickTrack(url);
        _picker.QueryChanged += (sender, query) =>
        {
            if (_session.FindTracks == null)
            {
                return;
            }

            _picker.SetResults(_session.FindTracks(query));
        };

        Controls.Add(_status);
        Controls.Add(_left);
        Controls.Add(_right);
        Controls.Add(_same);
        Controls.Add(_skip);
        Controls.Add(_different);
        Controls.Add(_picker);

        Apply(_theme);
        _session.Changed += OnChanged;
        _session.PlaybackChanged += OnPlayback;
        Disposed += (sender, args) =>
        {
            _session.Changed -= OnChanged;
            _session.PlaybackChanged -= OnPlayback;
        };
        RefreshView();
    }

    public void ApplyPalette(Color background, Color text, Color muted, Color accent)
    {
        _theme = StageTheme.For(background);
        _theme.Text = text;
        _theme.Accent = accent.GetBrightness() < 0.2f && _theme.Page.GetBrightness() < 0.5f
            ? Color.White
            : accent;
        Apply(_theme);
    }

    private void Apply(StageTheme theme)
    {
        _theme = theme;
        BackColor = theme.Page;
        _status.ForeColor = theme.Muted;
        _status.BackColor = theme.Page;
        _left.Apply(theme);
        _right.Apply(theme);
        StyleQuiet(_same);
        StyleQuiet(_skip);
        StyleQuiet(_different);
        _picker.Apply(theme);
        Invalidate(true);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (_picker.Visible)
        {
            return base.ProcessCmdKey(ref message, keyData);
        }

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

    private void OnPlayback(object sender, EventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateClocks));
            return;
        }

        UpdateClocks();
    }

    private void RefreshView()
    {
        ComparisonPrompt prompt = _session.Controller.Current;
        bool asking = prompt != null;
        _status.Visible = asking;
        _left.Visible = asking;
        _right.Visible = asking;
        _same.Visible = asking;
        _skip.Visible = asking;
        _different.Visible = asking;
        _picker.Visible = !asking;
        if (!asking)
        {
            TrackChoice playing = _session.PlayingChoice == null ? null : _session.PlayingChoice();
            TrackChoice selected = _session.SelectedChoice == null ? null : _session.SelectedChoice();
            _picker.SetShortcuts(playing, selected, string.IsNullOrEmpty(_session.Note) ? "Pick the song you want to rate. Ladder chooses what to compare it with." : _session.Note);
            _picker.BringToFront();
        }
        else
        {
            IReadOnlyList<RankedTrack> rank = _session.Controller.Rank(DateTime.UtcNow);
            _status.Text = prompt.Mode == ComparisonMode.Place
                ? "Does the song you picked beat this one?"
                : "Which song do you pick?";
            Bind(_left, prompt.Left, rank);
            Bind(_right, prompt.Right, rank);
            UpdateClocks();
        }

        LayoutStage();
    }

    private void Bind(SongCard card, TrackSnapshot track, IReadOnlyList<RankedTrack> rank)
    {
        card.Art = _session.ArtFor(track.Url);
        card.TitleText = DisplayTitle(track);
        card.DetailText = JoinDetail(track.Artist, track.Album);
        card.ScoreText = ScoreFor(track.Url, rank);
        card.Playing = string.Equals(_session.PlayingUrl, track.Url, StringComparison.Ordinal);
    }

    private void UpdateClocks()
    {
        ComparisonPrompt prompt = _session.Controller.Current;
        if (prompt == null)
        {
            return;
        }

        ShowClock(_left, prompt.Left.Url);
        ShowClock(_right, prompt.Right.Url);
    }

    private void ShowClock(SongCard card, string url)
    {
        int positionMs;
        int durationMs;
        _session.TryPlayback(url, out positionMs, out durationMs);
        card.Position = durationMs <= 0 ? 0 : positionMs / (double)durationMs;
        card.TimeText = durationMs <= 0 ? FormatClock(positionMs) : FormatClock(positionMs) + "  /  " + FormatClock(durationMs);
        card.Playing = string.Equals(_session.PlayingUrl, url, StringComparison.Ordinal);
    }

    private void LayoutStage()
    {
        if (_picker.Visible)
        {
            _picker.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
            return;
        }

        int pad = 28;
        int width = ClientSize.Width;
        int height = ClientSize.Height;
        if (width < 80 || height < 80)
        {
            return;
        }

        _status.SetBounds(pad, 18, width - pad * 2, 28);
        int top = 58;
        int center = 132;
        int cardWidth = Math.Max(180, (width - pad * 2 - center - 36) / 2);
        int cardHeight = Math.Max(280, height - top - 28);
        int group = cardWidth * 2 + center + 36;
        int x = Math.Max(pad, (width - group) / 2);
        _left.SetBounds(x, top, cardWidth, cardHeight);
        _right.SetBounds(x + cardWidth + center + 36, top, cardWidth, cardHeight);
        int mid = x + cardWidth + 18;
        int midY = top + cardHeight / 2;
        _same.SetBounds(mid, midY - 28, center, 36);
        _skip.SetBounds(mid, midY + 16, center, 32);
        _different.SetBounds(pad, 18, 220, 28);
        _status.SetBounds(pad + 230, 18, width - pad * 2 - 230, 28);
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

    private void Scrub(bool left, double fraction)
    {
        ComparisonPrompt prompt = _session.Controller.Current;
        if (prompt == null)
        {
            return;
        }

        _session.RequestSeek(left ? prompt.Left.Url : prompt.Right.Url, fraction);
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

    private static string ScoreFor(string url, IReadOnlyList<RankedTrack> rank)
    {
        foreach (RankedTrack row in rank)
        {
            if (!string.Equals(row.Track.Url, url, StringComparison.Ordinal))
            {
                continue;
            }

            return "#" + row.Rank.ToString(CultureInfo.InvariantCulture) + "    " + row.ScoreText;
        }

        return RatingText.NotPlaced;
    }

    private static string DisplayTitle(TrackSnapshot track)
    {
        if (!string.IsNullOrEmpty(track.Title))
        {
            return track.Title;
        }

        string file = System.IO.Path.GetFileName(track.Url);
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

    private static string FormatClock(int milliseconds)
    {
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        int totalSeconds = milliseconds / 1000;
        return (totalSeconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (totalSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    private Button Quiet(string text)
    {
        Button button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void StyleQuiet(Button button)
    {
        button.BackColor = _theme.Page;
        button.ForeColor = _theme.Muted;
        button.FlatAppearance.MouseOverBackColor = _theme.Card;
        button.FlatAppearance.MouseDownBackColor = _theme.Pick;
    }
}
