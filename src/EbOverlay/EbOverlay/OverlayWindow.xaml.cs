using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using EbOverlay.Hooks;
using EbOverlay.Zones;

namespace EbOverlay;

public partial class OverlayWindow : Window
{
    private readonly FullscreenDetector _fullscreenDetector;
    private readonly DispatcherTimer _fullscreenTimer;
    private readonly WindowHook _windowHook;
    private AppNameZone? _appNameZone;
    private ClockZone? _clockZone;

    // Safe inset margins derived from WorkArea — respects taskbar on any edge
    private Thickness _safeArea;

    public OverlayWindow()
    {
        InitializeComponent();

        _fullscreenDetector = new FullscreenDetector();
        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _fullscreenTimer.Tick += OnFullscreenCheck;

        _windowHook = new WindowHook();
        _windowHook.ForegroundWindowChanged += hwnd =>
            Dispatcher.Invoke(() => _appNameZone?.OnForegroundWindowChanged(hwnd));

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

        _appNameZone = new AppNameZone(AppNameText);
        _clockZone   = new ClockZone(ClockText);

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

        // Bottom-right: system metrics
        Canvas.SetLeft(MetricsPanel, contentRight - 120);
        Canvas.SetTop(MetricsPanel,  contentBottom - 40);

        // Bottom-left: network
        Canvas.SetLeft(NetPanel, contentLeft);
        Canvas.SetTop(NetPanel,  contentBottom - 40);

        // Mid-right: sprite
        Canvas.SetLeft(SpriteImage, contentRight - 96);
        Canvas.SetTop(SpriteImage,  Height / 2 - 48);
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
        bool fullscreen = _fullscreenDetector.IsForegroundFullscreen();
        Visibility = fullscreen ? Visibility.Hidden : Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _fullscreenTimer.Stop();
        _windowHook.Dispose();
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
