using System.Reflection;
using Ladder;

namespace MusicBeePlugin;

public partial class Plugin
{
    private MusicBeeApiInterface _api;
    private readonly PluginInfo _about = new PluginInfo();
    private LadderSession _session;
    private LadderView _panelView;
    private Form _window;
    private bool _menusAdded;

    static Plugin()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveSiblingAssembly;
    }

    public PluginInfo Initialise(IntPtr apiInterfacePtr)
    {
        _api = new MusicBeeApiInterface();
        _api.Initialise(apiInterfacePtr);
        Version version = typeof(Plugin).Assembly.GetName().Version ?? new Version(0, 1, 0);
        _about.PluginInfoVersion = PluginInfoVersion;
        _about.Name = "Ladder";
        _about.Description = "Rank tracks by asking which of two songs you prefer.";
        _about.Author = "MrBebra";
        _about.TargetApplication = "Ladder";
        _about.Type = PluginType.General;
        _about.VersionMajor = (short)version.Major;
        _about.VersionMinor = (short)version.Minor;
        _about.Revision = (short)version.Build;
        _about.MinInterfaceVersion = MinInterfaceVersion;
        _about.MinApiRevision = MinApiRevision;
        _about.ReceiveNotifications = ReceiveNotificationFlags.PlayerEvents;
        _about.ConfigurationPanelHeight = 0;
        return _about;
    }

    public bool Configure(IntPtr panelHandle)
    {
        return false;
    }

    public void SaveSettings()
    {
        Save();
    }

    public void Close(PluginCloseReason reason)
    {
        Save();
    }

    public void Uninstall()
    {
    }

    public void ReceiveNotification(string sourceFileUrl, NotificationType type)
    {
        if (type == NotificationType.PluginStartup)
        {
            EnsureSession();
            AddMenus();
        }
        else if (type == NotificationType.ShutdownStarted)
        {
            Save();
        }
        else if (type == NotificationType.TrackChanged || type == NotificationType.PlayStateChanged)
        {
            if (_session != null)
            {
                _session.SetPlaying(PlayingUrl());
            }
        }
    }

    public int OnDockablePanelCreated(Control panel)
    {
        EnsureSession();
        _panelView = CreateView();
        panel.Controls.Add(_panelView);
        return -1;
    }

    private void EnsureSession()
    {
        if (_session != null)
        {
            return;
        }

        string root = _api.Setting_GetPersistentStoragePath();
        string path = Path.Combine(root, "ladder", "ladder.json");
        _session = new LadderSession(path);
        _session.PlaceUrlRequested += PlacePlaying;
        _session.PlayRequested += OnPlayRequested;
        _session.LoadArt = LoadArt;
        _session.SetPlaying(PlayingUrl());
    }

    private void AddMenus()
    {
        if (_menusAdded)
        {
            return;
        }

        _menusAdded = true;
        _api.MB_AddMenuItem("context.Main/Ladder: Place selected track", null, PlaceSelected);
        _api.MB_AddMenuItem("context.Playlist/Ladder: Place selected track", null, PlaceSelected);
        _api.MB_AddMenuItem("context.NowPlayingList/Ladder: Place this track", null, PlaceSelected);
        _api.MB_AddMenuItem("mnuView/Ladder", null, OpenLadder);
        _api.MB_AddMenuItem("mnuTools/Ladder: Place the playing track", "Tools: Place the playing track", PlacePlaying);
        _api.MB_RegisterCommand("Ladder: Place the playing track", PlacePlaying);
    }

    private void OpenLadder(object sender, EventArgs args)
    {
        EnsureSession();
        Reveal();
    }

    private void PlaceSelected(object sender, EventArgs args)
    {
        EnsureSession();
        string url = SelectedUrl();
        if (string.IsNullOrEmpty(url))
        {
            _session.Report("Select a track, then place it.");
            Reveal();
            return;
        }

        _session.Place(SnapshotOf(url));
        Reveal();
    }

    private void PlacePlaying(object sender, EventArgs args)
    {
        EnsureSession();
        string url = PlayingUrl();
        if (string.IsNullOrEmpty(url))
        {
            _session.Report("Nothing is playing.");
            Reveal();
            return;
        }

        _session.Place(SnapshotOf(url));
        Reveal();
    }

    private void OnPlayRequested(object sender, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        if (string.Equals(PlayingUrl(), url, StringComparison.Ordinal))
        {
            _api.Player_PlayPause();
            return;
        }

        _api.NowPlayingList_PlayNow(url);
        _session.SetPlaying(url);
    }

    private Image LoadArt(string url)
    {
        PictureLocations locations;
        string pictureUrl;
        byte[] imageData;
        if (_api.Library_GetArtworkEx(url, 0, true, out locations, out pictureUrl, out imageData)
            && imageData != null
            && imageData.Length > 0)
        {
            return DecodeBytes(imageData);
        }

        if (!string.IsNullOrWhiteSpace(pictureUrl))
        {
            return DecodeArt(pictureUrl);
        }

        return DecodeArt(_api.Library_GetArtworkUrl(url, 0));
    }

    private static Image DecodeBytes(byte[] bytes)
    {
        using (MemoryStream stream = new MemoryStream(bytes))
        using (Image image = Image.FromStream(stream))
        {
            return new Bitmap(image);
        }
    }

    private static Image DecodeArt(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string text = raw.Trim();
        if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            text = new Uri(text).LocalPath;
        }

        if (File.Exists(text))
        {
            using (Image image = Image.FromFile(text))
            {
                return new Bitmap(image);
            }
        }

        int comma = text.IndexOf(',');
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
        {
            text = text.Substring(comma + 1);
        }

        try
        {
            return DecodeBytes(Convert.FromBase64String(text));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private string SelectedUrl()
    {
        string[] files;
        if (!_api.Library_QueryFilesEx("domain=SelectedFiles", out files) || files == null)
        {
            return null;
        }

        for (int i = 0; i < files.Length; i++)
        {
            if (!string.IsNullOrEmpty(files[i]))
            {
                return files[i];
            }
        }

        return null;
    }

    private string PlayingUrl()
    {
        string url = _api.NowPlaying_GetFileUrl();
        return string.IsNullOrEmpty(url) ? null : url;
    }

    private TrackSnapshot SnapshotOf(string url)
    {
        return new TrackSnapshot(url, Tag(url, MetaDataType.TrackTitle), Tag(url, MetaDataType.Artist), Tag(url, MetaDataType.Album));
    }

    private string Tag(string url, MetaDataType field)
    {
        string value = _api.Library_GetFileTag(url, field);
        return value ?? "";
    }

    private LadderView CreateView()
    {
        LadderView view = new LadderView(_session);
        view.Dock = DockStyle.Fill;
        ApplyPalette(view);
        return view;
    }

    private void ApplyPalette(LadderView view)
    {
        try
        {
            Color background = FromMusicBee(_api.Setting_GetSkinElementColour(
                SkinElement.SkinInputPanel,
                ElementState.ElementStateDefault,
                ElementComponent.ComponentBackground));
            Color text = FromMusicBee(_api.Setting_GetSkinElementColour(
                SkinElement.SkinInputPanel,
                ElementState.ElementStateDefault,
                ElementComponent.ComponentForeground));
            if (background.GetBrightness() < 0.08f && text.GetBrightness() < 0.08f)
            {
                return;
            }

            Color muted = Blend(text, background, 0.45f);
            Color accent = background.GetBrightness() < 0.5f ? Color.FromArgb(245, 245, 245) : Color.FromArgb(20, 20, 20);
            view.ApplyPalette(background, text, muted, accent);
        }
        catch (Exception)
        {
        }
    }

    private static Color FromMusicBee(int value)
    {
        Color color = (value & 0xFF000000) == 0
            ? Color.FromArgb(255, value & 255, (value >> 8) & 255, (value >> 16) & 255)
            : Color.FromArgb(value);
        if (color.A == 0)
        {
            color = Color.FromArgb(255, color);
        }

        return color;
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        int Mix(byte start, byte end)
        {
            return start + (int)((end - start) * amount);
        }

        return Color.FromArgb(Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
    }

    private void Reveal()
    {
        if (_window == null || _window.IsDisposed)
        {
            _window = new Form
            {
                Text = "Ladder",
                StartPosition = FormStartPosition.Manual,
                MinimumSize = new Size(760, 520),
                ShowInTaskbar = false,
            };
            _window.Controls.Add(CreateView());
        }

        FitWindowToPlayer();
        if (!_window.Visible)
        {
            IntPtr owner = _api.MB_GetWindowHandle();
            if (owner == IntPtr.Zero)
            {
                _window.Show();
            }
            else
            {
                _window.Show(new WindowOwner(owner));
            }
        }
        else
        {
            _window.Activate();
        }
    }

    private void FitWindowToPlayer()
    {
        Rectangle area = _api.MB_GetPanelBounds(PluginPanelDock.ApplicationWindow);
        if (area.Width < 640 || area.Height < 420)
        {
            _window.WindowState = FormWindowState.Maximized;
            return;
        }

        _window.WindowState = FormWindowState.Normal;
        _window.Bounds = area;
    }

    private void Save()
    {
        if (_session == null)
        {
            return;
        }

        try
        {
            _session.Save();
        }
        catch (Exception exception)
        {
            try
            {
                _api.MB_Trace("Ladder could not save: " + exception.Message);
            }
            catch (Exception)
            {
            }
        }
    }

    private static Assembly ResolveSiblingAssembly(object sender, ResolveEventArgs args)
    {
        string simpleName = new AssemblyName(args.Name).Name;
        if (!string.Equals(simpleName, "Ladder.Engine", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string name = simpleName + ".dll";
        string directory = PluginDirectory();
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        string path = Path.Combine(directory, name);
        if (!File.Exists(path))
        {
            return null;
        }

        return Assembly.LoadFrom(path);
    }

    private static string PluginDirectory()
    {
        string location = typeof(Plugin).Assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            location = typeof(Plugin).Assembly.CodeBase;
            if (!string.IsNullOrEmpty(location))
            {
                location = new Uri(location).LocalPath;
            }
        }

        return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
    }

    private sealed class WindowOwner : IWin32Window
    {
        public WindowOwner(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }
}
