using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace EbOverlay;

/// <summary>
/// System tray icon with right-click menu.
/// Pause state lives in OverlayWindow so the fullscreen timer can respect it.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly OverlayWindow _overlay;

    public TrayIcon(OverlayWindow overlay)
    {
        _overlay = overlay;

        _notify = new NotifyIcon
        {
            Text    = "EbOverlay",
            Icon    = CreateFallbackIcon(),
            Visible = true,
        };

        _notify.ContextMenuStrip = BuildMenu();
        _notify.DoubleClick += (_, _) => TogglePause();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var header = new ToolStripLabel("EbOverlay")
        {
            Font      = new Font("Courier New", 9f, System.Drawing.FontStyle.Bold),
            ForeColor = Color.FromArgb(0x66, 0xFF, 0x99),
        };

        var pauseItem = new ToolStripMenuItem("Pause overlay");
        pauseItem.Click += (_, _) => TogglePause();

        var fullscreenItem = new ToolStripMenuItem("Hide on fullscreen")
        {
            CheckOnClick = true,
            Checked      = _overlay.FullscreenHideEnabled,
        };
        fullscreenItem.CheckedChanged += (_, _) =>
            _overlay.FullscreenHideEnabled = fullscreenItem.Checked;

        var spriteItem = new ToolStripMenuItem("Show sprite")
        {
            CheckOnClick = true,
            Checked      = _overlay.SpriteVisible,
        };
        spriteItem.CheckedChanged += (_, _) =>
            _overlay.Dispatcher.Invoke(() => _overlay.SpriteVisible = spriteItem.Checked);

        // Keep menu labels in sync with actual state when opening
        menu.Opening += (_, _) =>
        {
            pauseItem.Text         = _overlay.IsPaused ? "Resume overlay" : "Pause overlay";
            fullscreenItem.Checked = _overlay.FullscreenHideEnabled;
            spriteItem.Checked     = _overlay.SpriteVisible;
        };

#if DEBUG
        var testIconsItem = new ToolStripMenuItem("Test status icons")
        {
            CheckOnClick = true,
            Checked      = false,
        };
        testIconsItem.CheckedChanged += (_, _) =>
            _overlay.StatusIconZone?.SetTestMode(testIconsItem.Checked);
#endif

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _notify.Visible = false;
            System.Windows.Application.Current.Shutdown();
        };

        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(fullscreenItem);
        menu.Items.Add(spriteItem);
#if DEBUG
        menu.Items.Add(testIconsItem);
#endif
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void TogglePause()
    {
        _overlay.Dispatcher.Invoke(() => _overlay.SetPaused(!_overlay.IsPaused));
        _notify.Text = _overlay.IsPaused ? "EbOverlay — paused" : "EbOverlay";
    }

    private static Icon CreateFallbackIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.FillEllipse(new SolidBrush(Color.FromArgb(0x66, 0xFF, 0x99)), 2, 2, 12, 12);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
    }
}
