using System.IO;
using System.Text;
using LibreHardwareMonitor.Hardware;
using Timer = System.Threading.Timer;

namespace EbOverlay.Services;

/// <summary>
/// Polls CPU temperature, GPU load/temperature, and VRAM via LibreHardwareMonitor.
/// Sensors unavailable without admin or on unsupported hardware report -1.
/// License: LibreHardwareMonitorLib is MPL-2.0.
/// </summary>
public sealed class HardwareMetrics : IDisposable
{
    public event Action<HardwareSnapshot>? Updated;

    private readonly Computer _computer;
    private readonly Timer _timer;

    public HardwareMetrics()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
        };

        try { _computer.Open(); }
        catch { /* silently continue — sensors will report -1 */ }

#if DEBUG
        DumpSensors();
#endif
        _timer = new Timer(_ => Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void Poll()
    {
        float cpuTemp       = -1;
        float gpuPercent    = -1;
        float gpuTemp       = -1;
        float gpuVramUsed   = -1;
        float gpuVramTotal  = -1;

        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();

                if (hw.HardwareType == HardwareType.Cpu)
                {
                    // Prefer "Package" sensors, fall back to first available temperature
                    cpuTemp = ReadSensor(hw, SensorType.Temperature, "Package", "Tdie", "CPU")
                           ?? FirstSensor(hw, SensorType.Temperature)
                           ?? -1;
                }
                else if (hw.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                {
                    gpuPercent = ReadSensor(hw, SensorType.Load,        "Core", "GPU")
                              ?? FirstSensor(hw, SensorType.Load)
                              ?? -1;
                    gpuTemp    = ReadSensor(hw, SensorType.Temperature, "Core", "GPU")
                              ?? FirstSensor(hw, SensorType.Temperature)
                              ?? -1;

                    float vramUsedMb  = ReadSensor(hw, SensorType.SmallData, "Memory Used",  "Used")  ?? -1;
                    float vramTotalMb = ReadSensor(hw, SensorType.SmallData, "Memory Total", "Total") ?? -1;

                    if (vramUsedMb  >= 0) gpuVramUsed  = vramUsedMb  / 1024f;
                    if (vramTotalMb >= 0) gpuVramTotal = vramTotalMb / 1024f;
                }
            }
        }
        catch { /* sensor read failure — report what we have */ }

        Updated?.Invoke(new HardwareSnapshot(cpuTemp, gpuPercent, gpuTemp, gpuVramUsed, gpuVramTotal));
    }

    private void DumpSensors()
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"=== LHM Sensor Dump ===");
        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            lines.AppendLine($"[{hw.HardwareType}] {hw.Name}");
            foreach (var sensor in hw.Sensors)
                lines.AppendLine($"  {sensor.SensorType,-16} | {sensor.Name,-30} | {sensor.Value?.ToString("F2") ?? "null"}");
            foreach (var sub in hw.SubHardware)
            {
                sub.Update();
                lines.AppendLine($"  [{sub.HardwareType}] {sub.Name} (sub)");
                foreach (var sensor in sub.Sensors)
                    lines.AppendLine($"    {sensor.SensorType,-16} | {sensor.Name,-30} | {sensor.Value?.ToString("F2") ?? "null"}");
            }
        }
        File.WriteAllText(@"C:\Kortelainen\EbOverlay\sensor_dump.txt", lines.ToString());
    }

    // Returns first sensor value whose name contains any of the hints, or null.
    // Values <= 0 are treated as unavailable (e.g. AMD Ryzen AI SMU returns 0 when unreadable).
    private static float? ReadSensor(IHardware hw, SensorType type, params string[] nameHints)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType != type || !sensor.Value.HasValue || sensor.Value.Value <= 0) continue;
            foreach (var hint in nameHints)
                if (sensor.Name.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return sensor.Value.Value;
        }
        return null;
    }

    // Returns the first available sensor of this type with a positive value, regardless of name
    private static float? FirstSensor(IHardware hw, SensorType type)
    {
        foreach (var sensor in hw.Sensors)
            if (sensor.SensorType == type && sensor.Value.HasValue && sensor.Value.Value > 0)
                return sensor.Value.Value;
        return null;
    }

    public void Dispose()
    {
        _timer.Dispose();
        try { _computer.Close(); } catch { }
    }
}
