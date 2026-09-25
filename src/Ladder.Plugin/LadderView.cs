using System.Globalization;
using Ladder;

namespace MusicBeePlugin;

internal sealed class LadderView : UserControl
{
    private readonly LadderSession _session;
    private readonly Label _status;
    private readonly Label _bandHint;
    private readonly TableLayoutPanel _topCard;
    private readonly TableLayoutPanel _bottomCard;
    private readonly Label _topCaption;
    private readonly Label _topTitle;
    private readonly Label _topDetail;
    private readonly Label _topScore;
    private readonly Button _topChoose;
    private readonly Label _bottomCaption;
    private readonly Label _bottomTitle;
    private readonly Label _bottomDetail;
    private readonly Label _bottomScore;
    private readonly Button _bottomChoose;
    private readonly Button _same;
    private readonly Button _skip;
    private readonly Button _placePlaying;
    private readonly Button _sharpen;
    private readonly ListView _ranks;

    public LadderView(LadderSession session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        _session = session;
        AutoScaleMode = AutoScaleMode.Font;
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        _status = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "Ladder",
        };
        _bandHint = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = "The number is an estimate. ± is how far it can still move.",
        };

        _topCard = Card(out _topCaption, out _topTitle, out _topDetail, out _topScore, out _topChoose);
        _bottomCard = Card(out _bottomCaption, out _bottomTitle, out _bottomDetail, out _bottomScore, out _bottomChoose);
        _topChoose.Click += (sender, args) => Choose(ComparisonChoice.Left);
        _bottomChoose.Click += (sender, args) => Choose(ComparisonChoice.Right);

        _same = new Button { Text = "About the same", AutoSize = true, Margin = new Padding(0, 8, 8, 8) };
        _skip = new Button { Text = "Skip", AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
        _same.Click += (sender, args) => Choose(ComparisonChoice.Same);
        _skip.Click += (sender, args) => Run(delegate { _session.Skip(); });

        var middle = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
        };
        middle.Controls.Add(_same);
        middle.Controls.Add(_skip);

        _placePlaying = new Button { Text = "Place the playing track", AutoSize = true, Margin = new Padding(0, 0, 8, 8) };
        _sharpen = new Button { Text = "Sharpen close pairs", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        _placePlaying.Click += (sender, args) => _session.PlacePlayingRequested();
        _sharpen.Click += (sender, args) => Run(delegate { _session.Sharpen(); });

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
        };
        actions.Controls.Add(_placePlaying);
        actions.Controls.Add(_sharpen);

        _ranks = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            HideSelection = false,
            MultiSelect = false,
        };
        _ranks.Columns.Add("Rank", 52);
        _ranks.Columns.Add("Title", 180);
        _ranks.Columns.Add("Score", 110);
        _ranks.Columns.Add("Against the next", 160);
        _ranks.DoubleClick += RankDoubleClick;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(_status, 0, 0);
        table.Controls.Add(_topCard, 0, 1);
        table.Controls.Add(middle, 0, 2);
        table.Controls.Add(_bottomCard, 0, 3);
        table.Controls.Add(actions, 0, 4);
        table.Controls.Add(_bandHint, 0, 5);
        table.Controls.Add(_ranks, 0, 6);
        Controls.Add(table);

        _session.Changed += OnChanged;
        Disposed += (sender, args) => _session.Changed -= OnChanged;
        RefreshView();
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
        _topCard.Visible = asking;
        _bottomCard.Visible = asking;
        _same.Enabled = asking;
        _skip.Enabled = asking;
        _same.Parent.Visible = asking;

        if (!string.IsNullOrEmpty(_session.LoadError))
        {
            _status.Text = _session.LoadError + " The file was left unchanged.";
        }
        else if (!string.IsNullOrEmpty(_session.Note))
        {
            _status.Text = _session.Note;
        }
        else if (_session.Controller.NeedsAnotherTrack)
        {
            _status.Text = "Ladder needs a second track before it can ask which you prefer.";
        }
        else if (prompt != null && prompt.Mode == ComparisonMode.Place)
        {
            _status.Text = "Placing \"" + DisplayTitle(prompt.Left) + "\". Does it beat the other track?";
        }
        else if (prompt != null)
        {
            _status.Text = "Which of these do you prefer? Close calls are what settle the order.";
        }
        else
        {
            _status.Text = "Place a track, or sharpen the pairs that are still close.";
        }

        IReadOnlyList<RankedTrack> rank = _session.Controller.Rank(DateTime.UtcNow);
        if (asking)
        {
            bool placing = prompt.Mode == ComparisonMode.Place;
            Fill(_topCaption, _topTitle, _topDetail, _topScore, prompt.Left, placing ? "Placing" : "First track", rank);
            Fill(_bottomCaption, _bottomTitle, _bottomDetail, _bottomScore, prompt.Right, placing ? "Compared with" : "Second track", rank);
        }
        _ranks.BeginUpdate();
        _ranks.Items.Clear();
        foreach (RankedTrack row in rank)
        {
            var item = new ListViewItem(row.Rank.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(DisplayTitle(row.Track));
            item.SubItems.Add(row.ScoreText);
            item.SubItems.Add(row.Gap);
            item.Tag = row.Track.Url;
            _ranks.Items.Add(item);
        }

        _ranks.EndUpdate();
    }

    private static void Fill(
        Label caption,
        Label title,
        Label detail,
        Label score,
        TrackSnapshot track,
        string captionText,
        IReadOnlyList<RankedTrack> rank)
    {
        caption.Text = captionText;
        title.Text = DisplayTitle(track);
        detail.Text = JoinDetail(track.Artist, track.Album);
        score.Text = ScoreFor(track.Url, rank);
    }

    private static string ScoreFor(string url, IReadOnlyList<RankedTrack> rank)
    {
        foreach (RankedTrack row in rank)
        {
            if (string.Equals(row.Track.Url, url, StringComparison.Ordinal))
            {
                return row.ScoreText;
            }
        }

        return RatingText.NotPlaced;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (_status == null || _bandHint == null || _topTitle == null || _bottomTitle == null)
        {
            return;
        }

        int width = Math.Max(80, ClientSize.Width - 24);
        var limit = new Size(width, 0);
        _status.MaximumSize = limit;
        _bandHint.MaximumSize = limit;
        _topTitle.MaximumSize = limit;
        _topDetail.MaximumSize = limit;
        _bottomTitle.MaximumSize = limit;
        _bottomDetail.MaximumSize = limit;
    }

    private static TableLayoutPanel Card(
        out Label caption,
        out Label title,
        out Label detail,
        out Label score,
        out Button choose)
    {
        caption = Line(FontStyle.Regular, SystemColors.GrayText);
        title = Line(FontStyle.Bold, SystemColors.ControlText);
        detail = Line(FontStyle.Regular, SystemColors.ControlText);
        score = Line(FontStyle.Regular, SystemColors.ControlText);
        choose = new Button
        {
            Text = "This one",
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 4, 0, 0),
        };

        var card = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(0, 8, 0, 0),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.Controls.Add(caption, 0, 0);
        card.Controls.Add(title, 0, 1);
        card.Controls.Add(detail, 0, 2);
        card.Controls.Add(score, 0, 3);
        card.Controls.Add(choose, 0, 4);
        return card;
    }

    private static Label Line(FontStyle style, Color color)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.MessageBoxFont, style),
            ForeColor = color,
            MaximumSize = new Size(420, 0),
        };
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
            return artist + " — " + album;
        }

        if (hasArtist)
        {
            return artist;
        }

        return hasAlbum ? album : "";
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

    private void RankDoubleClick(object sender, EventArgs args)
    {
        if (_ranks.SelectedItems.Count == 0)
        {
            return;
        }

        string url = _ranks.SelectedItems[0].Tag as string;
        if (!string.IsNullOrEmpty(url))
        {
            RankChosen?.Invoke(this, url);
        }
    }

    public event EventHandler<string> RankChosen;
}
