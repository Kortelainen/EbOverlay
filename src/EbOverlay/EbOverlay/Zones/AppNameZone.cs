using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EbOverlay.Zones;

/// <summary>
/// Displays the foreground window's title in the top-left zone.
/// Fades in on change, holds for a few seconds, then fades out.
/// </summary>
public class AppNameZone
{
    private readonly TextBlock _label;
    private readonly DispatcherTimer _holdTimer;

    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(300));
    private const double HoldSeconds = 4;

    public AppNameZone(TextBlock label)
    {
        _label = label;

        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HoldSeconds) };
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            FadeTo(0);
        };
    }

    public void OnForegroundWindowChanged(IntPtr hwnd)
    {
        string title = GetWindowTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title))
            return;

        _label.Text = title;

        _holdTimer.Stop();
        FadeTo(0.85);
        _holdTimer.Start();
    }

    private void FadeTo(double opacity)
    {
        var anim = new DoubleAnimation(opacity, FadeDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        _label.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int len = GetWindowTextLength(hwnd);
        if (len == 0) return string.Empty;

        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);
}
