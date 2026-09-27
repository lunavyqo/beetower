namespace MusicBeePlugin;

internal sealed class SongPicker : UserControl
{
    private readonly Label _heading;
    private readonly Label _note;
    private readonly Button _playing;
    private readonly Button _selected;
    private readonly TextBox _search;
    private readonly ListBox _results;
    private readonly System.Windows.Forms.Timer _debounce;
    private StageTheme _theme = StageTheme.Dark();

    public SongPicker()
    {
        DoubleBuffered = true;
        _heading = new Label
        {
            Text = "Rate a song",
            Font = new Font("Segoe UI", 26f, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = false,
            Height = 46,
        };
        _note = new Label
        {
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = false,
            Height = 44,
        };
        _playing = MakeChoice();
        _selected = MakeChoice();
        _search = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Point),
            Height = 36,
        };
        _results = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 52,
            IntegralHeight = false,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point),
        };
        _results.DrawItem += DrawResult;
        _results.MouseClick += ResultClicked;
        _playing.Click += (sender, args) => Choose(_playing.Tag as string);
        _selected.Click += (sender, args) => Choose(_selected.Tag as string);
        _debounce = new System.Windows.Forms.Timer { Interval = 180 };
        _debounce.Tick += (sender, args) =>
        {
            _debounce.Stop();
            if (QueryChanged != null)
            {
                QueryChanged(this, _search.Text);
            }
        };
        _search.TextChanged += (sender, args) =>
        {
            _debounce.Stop();
            _debounce.Start();
        };
        Controls.Add(_heading);
        Controls.Add(_note);
        Controls.Add(_playing);
        Controls.Add(_selected);
        Controls.Add(_search);
        Controls.Add(_results);
        Apply(_theme);
    }

    public event EventHandler<string> Picked;

    public event EventHandler<string> QueryChanged;

    public void Apply(StageTheme theme)
    {
        _theme = theme ?? StageTheme.Dark();
        BackColor = _theme.Page;
        _heading.ForeColor = _theme.Text;
        _heading.BackColor = _theme.Page;
        _note.ForeColor = _theme.Muted;
        _note.BackColor = _theme.Page;
        StyleChoice(_playing);
        StyleChoice(_selected);
        _search.BackColor = _theme.Card;
        _search.ForeColor = _theme.Text;
        _results.BackColor = _theme.Page;
        _results.ForeColor = _theme.Text;
        Invalidate(true);
    }

    public void SetShortcuts(TrackChoice playing, TrackChoice selected, string note)
    {
        _note.Text = note ?? "";
        ShowChoice(_playing, playing, "Now playing");
        bool same = playing != null && selected != null && string.Equals(playing.Url, selected.Url, StringComparison.Ordinal);
        ShowChoice(_selected, same ? null : selected, "Selected in the library");
        PerformLayout();
    }

    public void SetResults(IReadOnlyList<TrackChoice> results)
    {
        _results.BeginUpdate();
        _results.Items.Clear();
        if (results != null)
        {
            for (int i = 0; i < results.Count; i++)
            {
                _results.Items.Add(results[i]);
            }
        }

        _results.EndUpdate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        int width = Math.Min(560, Math.Max(200, ClientSize.Width - 80));
        int x = Math.Max(24, (ClientSize.Width - width) / 2);
        int y = 36;
        _heading.SetBounds(x, y, width, 46);
        _note.SetBounds(x, y + 48, width, 44);
        y += 104;
        if (_playing.Visible)
        {
            _playing.SetBounds(x, y, width, 58);
            y += 66;
        }

        if (_selected.Visible)
        {
            _selected.SetBounds(x, y, width, 58);
            y += 66;
        }

        _search.SetBounds(x, y, width, 32);
        y += 48;
        int listHeight = Math.Max(80, ClientSize.Height - y - 24);
        _results.SetBounds(x, y, width, listHeight);
    }

    private void ShowChoice(Button button, TrackChoice choice, string source)
    {
        if (choice == null || string.IsNullOrEmpty(choice.Url))
        {
            button.Visible = false;
            button.Tag = null;
            return;
        }

        button.Visible = true;
        button.Tag = choice.Url;
        button.Text = source + "\n" + choice.Title + (string.IsNullOrEmpty(choice.Artist) ? "" : "   ·   " + choice.Artist);
    }

    private void Choose(string url)
    {
        if (!string.IsNullOrEmpty(url) && Picked != null)
        {
            Picked(this, url);
        }
    }

    private void ResultClicked(object sender, MouseEventArgs e)
    {
        int index = _results.IndexFromPoint(e.Location);
        if (index < 0)
        {
            return;
        }

        TrackChoice choice = _results.Items[index] as TrackChoice;
        if (choice != null)
        {
            Choose(choice.Url);
        }
    }

    private void DrawResult(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        TrackChoice choice = _results.Items[e.Index] as TrackChoice;
        if (choice == null)
        {
            return;
        }

        bool hot = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using (Brush back = new SolidBrush(hot ? _theme.CardHover : _theme.Page))
        {
            e.Graphics.FillRectangle(back, e.Bounds);
        }

        Rectangle title = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, e.Bounds.Width - 16, 22);
        Rectangle artist = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 28, e.Bounds.Width - 16, 18);
        TextRenderer.DrawText(e.Graphics, choice.Title, _results.Font, title, _theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, choice.Artist ?? "", Font, artist, _theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }

    private Button MakeChoice()
    {
        Button button = new Button
        {
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            Padding = new Padding(16, 0, 12, 0),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void StyleChoice(Button button)
    {
        button.BackColor = _theme.Card;
        button.ForeColor = _theme.Text;
        button.FlatAppearance.MouseOverBackColor = _theme.CardHover;
        button.FlatAppearance.MouseDownBackColor = _theme.PickHover;
    }
}
