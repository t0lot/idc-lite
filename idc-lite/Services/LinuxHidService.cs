using System.IO;
using System.Runtime.InteropServices;

namespace idc_lite.Services;

/// <summary>
/// Кроссплатформенный доступ к дисплею ID-COOLING через Linux /dev/hidraw.
/// </summary>
public sealed class LinuxHidService : IDisposable
{
    public const byte CMD_TEMPERATURE = 0x01;
    public const byte CMD_FREQUENCY   = 0x02;
    public const byte CMD_USAGE       = 0x03;
    public const byte CMD_SHOW        = 0x04;

    private const ushort TARGET_VID = 0x1A86;
    private const ushort TARGET_PID = 0xE317;

    private int _fd = -1;
    private readonly object _lock = new();

    public bool IsConnected => _fd >= 0;

    public bool OpenDevice()
    {
        lock (_lock)
        {
            CloseDevice();

            if (!OperatingSystem.IsLinux())
                return false;

            var devPath = FindHidrawPath(TARGET_VID, TARGET_PID);
            if (string.IsNullOrEmpty(devPath))
                return false;

            try
            {
                _fd = open(devPath, O_RDWR | O_NONBLOCK);
                return _fd >= 0;
            }
            catch
            {
                _fd = -1;
                return false;
            }
        }
    }

    public bool SendFrame(byte command, ushort value)
    {
        lock (_lock)
        {
            if (_fd < 0) return false;

            byte[] frame = BuildFrame(command, value);
            try
            {
                IntPtr written = write(_fd, frame, (UIntPtr)frame.Length);
                if (written.ToInt64() == frame.Length)
                {
                    return true;
                }
            }
            catch { }

            CloseDevice();
            return false;
        }
    }

    public bool SendTemperature(int tempC) => SendFrame(CMD_TEMPERATURE, (ushort)tempC);
    public bool SendFrequency(int freqMHz) => SendFrame(CMD_FREQUENCY, (ushort)freqMHz);
    public bool SendUsage(int percent) => SendFrame(CMD_USAGE, (ushort)percent);
    public bool SendShow(bool on) => SendFrame(CMD_SHOW, (ushort)(on ? 1 : 0));

    public static byte[] BuildFrame(byte command, ushort value)
    {
        byte[] frame = new byte[64];
        frame[0] = 0x55;
        frame[1] = 0xBB;
        frame[2] = command;
        frame[3] = (byte)(value & 0xFF);
        frame[4] = (byte)((value >> 8) & 0xFF);
        return frame;
    }

    public void CloseDevice()
    {
        lock (_lock)
        {
            if (_fd >= 0)
            {
                try { close(_fd); } catch { }
                _fd = -1;
            }
        }
    }

    public void Dispose()
    {
        CloseDevice();
    }

    private static string? FindHidrawPath(ushort vid, ushort pid)
    {
        try
        {
            var hidraws = Directory.GetFiles("/dev", "hidraw*");
            foreach (var hidraw in hidraws)
            {
                var baseName = Path.GetFileName(hidraw);
                var ueventPath = $"/sys/class/hidraw/{baseName}/device/uevent";
                if (File.Exists(ueventPath))
                {
                    var text = File.ReadAllText(ueventPath);
                    // HID_ID=0003:00001A86:0000E317
                    if (text.Contains($"0000{vid:X4}:0000{pid:X4}", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains($"{vid:X4}:{pid:X4}", StringComparison.OrdinalIgnoreCase))
                    {
                        return hidraw;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private const int O_RDWR = 0x0002;
    private const int O_NONBLOCK = 0x0800;

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr write(int fd, byte[] buf, UIntPtr count);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);
}
