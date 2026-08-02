using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace idc_lite.Services;

/// <summary>
/// Сервис для общения с USB HID LCD-дисплеем (VID 0x1A86, PID 0xE317).
/// Протокол: 64-байтные отчёты.
/// Формат: [0x55, 0xBB, 0x02, cmd, valHi, valLo, cksum, 0x00×57]
/// На Windows: prepend 0x00 (Report ID) → 65 байт.
/// </summary>
public sealed class HidService : IDisposable
{
    private const ushort VID = 0x1A86;
    private const ushort PID = 0xE317;
    private const int ReportLength = 64;

    // Команды
    public const byte CMD_TEMPERATURE = 1;
    public const byte CMD_FREQUENCY   = 2;
    public const byte CMD_USAGE       = 3;
    public const byte CMD_SHOW        = 4;

    private SafeFileHandle? _deviceHandle;
    private bool _disposed;

    // ===== P/Invoke =====

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetupDiGetClassDevsW(
        ref Guid ClassGuid,
        string? Enumerator,
        IntPtr hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr DeviceInfoSet,
        IntPtr DeviceInfoData,
        ref Guid InterfaceClassGuid,
        uint MemberIndex,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        uint DeviceInterfaceDetailDataSize,
        out uint RequiredSize,
        IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(
        SafeFileHandle HidDeviceObject,
        ref HIDD_ATTRIBUTES Attributes);

    // ===== Constants =====

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint DIGCF_PRESENT = 0x02;
    private const uint DIGCF_DEVICEINTERFACE = 0x10;

    // ===== Structs =====

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    private static readonly Guid HidGuid = new("4D1E55B2-F16F-11CF-88CB-001111000030");

    // ===== Public API =====

    /// <summary>
    /// Поиск HID-устройства и открытие для чтения/записи.
    /// Использует синхронный I/O (НЕ overlapped — иначе WriteFile молча падает).
    /// </summary>
    public bool OpenDevice()
    {
        CloseDevice();

        var devicePath = FindDevicePath(VID, PID);
        if (devicePath == null)
            return false;

        _deviceHandle = CreateFileW(
            devicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,               // ← Синхронный I/O, НЕ overlapped!
            IntPtr.Zero);

        if (_deviceHandle.IsInvalid)
        {
            _deviceHandle.Dispose();
            _deviceHandle = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Переподключение устройства.
    /// </summary>
    public bool Reconnect()
    {
        return OpenDevice();
    }

    public bool IsConnected => _deviceHandle != null && !_deviceHandle.IsInvalid;

    /// <summary>
    /// Отправка кадра на дисплей.
    /// </summary>
    public bool SendFrame(byte command, ushort value)
    {
        if (!IsConnected)
            return false;

        var frame = BuildFrame(command, value);

        // Windows: prepend 0x00 (Report ID)
        var buffer = new byte[ReportLength + 1];
        buffer[0] = 0x00;
        Array.Copy(frame, 0, buffer, 1, ReportLength);

        bool result = WriteFile(
            _deviceHandle!,
            buffer,
            (uint)buffer.Length,
            out uint written,
            IntPtr.Zero);  // Синхронный режим

        return result && written == buffer.Length;
    }

    // ===== Convenience methods =====

    public bool SendTemperature(int tempC) => SendFrame(CMD_TEMPERATURE, (ushort)tempC);
    public bool SendFrequency(int freqMHz) => SendFrame(CMD_FREQUENCY, (ushort)freqMHz);
    public bool SendUsage(int percent) => SendFrame(CMD_USAGE, (ushort)percent);
    public bool SendShow(bool on) => SendFrame(CMD_SHOW, (ushort)(on ? 1 : 0));

    // ===== Static frame builder =====

    /// <summary>
    /// Построение 64-байтного кадра для LCD-дисплея.
    /// Формат: [0x55, 0xBB, 0x02, cmd, valHi, valLo, cksum, 0x00×57]
    /// </summary>
    public static byte[] BuildFrame(byte command, ushort value)
    {
        var frame = new byte[ReportLength];

        frame[0] = 0x55;  // Header
        frame[1] = 0xBB;  // Header
        frame[2] = 0x02;  // Data length
        frame[3] = command;
        frame[4] = (byte)(value >> 8);   // Value high byte
        frame[5] = (byte)(value & 0xFF); // Value low byte

        // Checksum: sum of bytes 0..5 mod 256
        byte cksum = 0;
        for (int i = 0; i < 6; i++)
            cksum += frame[i];
        frame[6] = cksum;

        return frame;
    }

    // ===== Cleanup =====

    public void CloseDevice()
    {
        _deviceHandle?.Close();
        _deviceHandle?.Dispose();
        _deviceHandle = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CloseDevice();
            _disposed = true;
        }
    }

    // ===== Device enumeration =====

    private static string? FindDevicePath(ushort vid, ushort pid)
    {
        var hidGuid = HidGuid;
        var deviceInfoSet = SetupDiGetClassDevsW(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            return null;

        try
        {
            var interfaceData = new SP_DEVICE_INTERFACE_DATA();
            interfaceData.cbSize = Marshal.SizeOf(interfaceData);

            for (uint i = 0; SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, i, ref interfaceData); i++)
            {
                SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);

                var detailData = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);

                    if (SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, detailData, requiredSize, out requiredSize, IntPtr.Zero))
                    {
                        var pathPtr = IntPtr.Add(detailData, 4);
                        var devicePath = Marshal.PtrToStringUni(pathPtr);

                        if (devicePath != null)
                        {
                            using var testHandle = CreateFileW(
                                devicePath, 0,
                                FILE_SHARE_READ | FILE_SHARE_WRITE,
                                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

                            if (!testHandle.IsInvalid)
                            {
                                var attr = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
                                if (HidD_GetAttributes(testHandle, ref attr))
                                {
                                    if (attr.VendorID == vid && attr.ProductID == pid)
                                        return devicePath;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailData);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return null;
    }
}