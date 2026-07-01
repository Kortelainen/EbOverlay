using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using EbOverlay.Hooks;
using EbOverlay.Zones;
using EbOverlay.Services;

namespace EbOverlay;

public partial class OverlayWindow : Window
{
    private readonly FullscreenDetector _fullscreenDetector;
    private readonly DispatcherTimer _fullscreenTimer;
    private readonly WindowHook _windowHook;
    private AppNameZone? _appNameZone;
    private ClockZone? _clockZone;
    private MetricsZone? _metricsZone;
    private SpriteZone?      _spriteZone;
    public  StatusIconZone?  StatusIconZone { get; private set; }
    private ForegroundProcessMetrics? _processMetrics;

    // Safe inset margins derived from WorkArea — respects taskbar on any edge
    private Thickness _safeArea;

    /// <summary>
    /// When true the overlay is user-paused. OnFullscreenCheck will not override this.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// When false the fullscreen detector is bypassed — overlay stays visible over fullscreen apps.
    /// </summary>
    public bool FullscreenHideEnabled { get; set; } = true;

    /// <summary>
    /// Show or hide the sprite character without affecting other overlay elements.
    /// </summary>
    public bool SpriteVisible
    {
        get => SpriteContainer.Visibility == Visibility.Visible;
        set => SpriteContainer.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetPaused(bool paused)
    {
        IsPaused   = paused;
        Visibility = paused ? Visibility.Hidden : Visibility.Visible;
    }

    public OverlayWindow()
    {
        InitializeComponent();

        _fullscreenDetector = new FullscreenDetector();
        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _fullscreenTimer.Tick += OnFullscreenCheck;

        _windowHook = new WindowHook();
        _windowHook.ForegroundWindowChanged += hwnd =>
        {
            Dispatcher.Invoke(() => _appNameZone?.OnForegroundWindowChanged(hwnd));
            _processMetrics?.OnForegroundWindowChanged(hwnd);
            _spriteZone?.OnForegroundWindowChanged();
        };
        _windowHook.WindowRestored  += () => _spriteZone?.OnWindowOpened();
        _windowHook.WindowMinimized += () => _spriteZone?.OnWindowClosed();

        Loaded += OnLoaded;

        // Reposition content if taskbar moves or resizes (e.g. user changes taskbar size)
        SystemParameters.StaticPropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SystemParameters.WorkArea))
                Dispatcher.Invoke(PositionAllElements);
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StretchToFullScreen();
        SetClickThrough();
        PositionAllElements();

        var hwnd = new WindowInteropHelper(this).Handle;
        _fullscreenDetector.OwnHwnd = hwnd;

        _appNameZone    = new AppNameZone(AppNameText);
        _clockZone      = new ClockZone(ClockText);
        _metricsZone = new MetricsZone(
            CpuText, GpuText, VramText, RamText, DiskText,
            NetUpText, NetDownText, NetPanel,
            CpuBar, GpuBar, VramBar, RamBar,
            CpuSparkline, GpuSparkline, VramSparkline, RamSparkline,
            DiskReadSparkline, DiskWriteSparkline,
            UpSparkline, DownSparkline,
            Dispatcher);
        _processMetrics = new ForegroundProcessMetrics();
        _processMetrics.Updated += snap =>
        {
            _metricsZone.OnProcessUpdated(snap);
            _spriteZone?.OnProcessUpdated(snap);
            StatusIconZone?.OnProcessUpdated(snap);
        };

        _spriteZone = new SpriteZone(SpriteImage, Dispatcher);

        StatusIconZone = new StatusIconZone(StatusLayer, Dispatcher);

        _metricsZone.SystemUpdated += snap =>
        {
            _spriteZone.OnSystemUpdated(snap);
            StatusIconZone.OnSystemUpdated(snap);
        };
        _metricsZone.HardwareUpdated += snap =>
        {
            _spriteZone.OnHardwareUpdated(snap);
            StatusIconZone.OnHardwareUpdated(snap);
        };

        _fullscreenTimer.Start();
    }

    private void StretchToFullScreen()
    {
        Left   = 0;
        Top    = 0;
        Width  = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    // Compute safe inset from WorkArea so content never overlaps the taskbar,
    // regardless of which edge it is docked to.
    private void ComputeSafeArea()
    {
        var work   = SystemParameters.WorkArea;
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        _safeArea = new Thickness(
            left:   work.Left,                    // taskbar on left
            top:    work.Top,                     // taskbar on top
            right:  screenW - work.Right,         // taskbar on right
            bottom: screenH - work.Bottom         // taskbar on bottom
        );
    }

    private void PositionAllElements()
    {
        ComputeSafeArea();

        const double pad = 24;
        double contentLeft   = _safeArea.Left   + pad;
        double contentTop    = _safeArea.Top    + pad;
        double contentRight  = Width  - _safeArea.Right  - pad;
        double contentBottom = Height - _safeArea.Bottom - pad;

        // Top-left: app name
        Canvas.SetLeft(AppNameText, contentLeft);
        Canvas.SetTop(AppNameText,  contentTop);

        // Top-right: clock
        Canvas.SetLeft(ClockText, contentRight - 60);
        Canvas.SetTop(ClockText,  contentTop);

        // Bottom-right: system metrics — bottom-anchored so it always hugs the taskbar
        Canvas.SetLeft(MetricsPanel,   contentRight - 220);
        Canvas.SetTop(MetricsPanel,    double.NaN);
        Canvas.SetBottom(MetricsPanel, _safeArea.Bottom + pad);

        // Bottom-left: network — bottom-anchored
        Canvas.SetLeft(NetPanel,   contentLeft);
        Canvas.SetTop(NetPanel,    double.NaN);
        Canvas.SetBottom(NetPanel, _safeArea.Bottom + pad);

        // Mid-right: sprite + status overlay container
        Canvas.SetLeft(SpriteContainer, contentRight - 96);
        Canvas.SetTop(SpriteContainer,  Height / 2 - 48);
    }

    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            style | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE);
    }

    private void OnFullscreenCheck(object? sender, EventArgs e)
    {
        if (IsPaused) return;  // tray pause overrides everything

        if (!FullscreenHideEnabled)
        {
            Visibility = Visibility.Visible;
            return;
        }
        bool fullscreen = _fullscreenDetector.IsForegroundFullscreen();
        Visibility = fullscreen ? Visibility.Hidden : Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _fullscreenTimer.Stop();
        _windowHook.Dispose();
        _metricsZone?.Dispose();
        _spriteZone?.Dispose();
        _processMetrics?.Dispose();
        base.OnClosed(e);
    }
}

internal static class NativeMethods
{
    public const int GWL_EXSTYLE      = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED    = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}
