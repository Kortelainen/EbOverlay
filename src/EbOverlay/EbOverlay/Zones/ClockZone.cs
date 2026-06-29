using System.Windows.Controls;
using System.Windows.Threading;

namespace EbOverlay.Zones;

/// <summary>
/// Updates the clock text block every second.
/// Triggers a brief glitch effect on minute rollover.
/// </summary>
public class ClockZone
{
    private readonly TextBlock _label;
    private readonly DispatcherTimer _timer;
    private int _lastMinute = -1;

    public ClockZone(TextBlock label)
    {
        _label = label;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();

        // Render immediately rather than waiting for first tick
        OnTick(null, EventArgs.Empty);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        _label.Text = now.ToString("HH:mm");

        if (now.Minute != _lastMinute)
        {
            _lastMinute = now.Minute;
            if (_lastMinute >= 0)
                PlayGlitch();
        }
    }

    private async void PlayGlitch()
    {
        // Flicker the label a couple of times on minute rollover
        for (int i = 0; i < 3; i++)
        {
            _label.Opacity = 0.1;
            await Task.Delay(40);
            _label.Opacity = 0.6;
            await Task.Delay(30);
        }
        _label.Opacity = 0.6;
    }
}
