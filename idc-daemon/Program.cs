using System;
using System.Threading;
using System.Threading.Tasks;
using idc_lite.Services;

namespace idc_daemon;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine(" IDC-Lite Daemon v2.0.0 (Linux)");
        Console.WriteLine(" ID-COOLING FX LCD Display Controller");
        Console.WriteLine("==================================================");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[Daemon] Shutting down...");
        };

        using var hid = new LinuxHidService();
        Console.WriteLine("[Daemon] Starting hardware telemetry & HID monitor loop...");

        bool wasConnected = false;

        while (!cts.Token.IsCancellationRequested)
        {
            if (!hid.IsConnected)
            {
                if (hid.OpenDevice())
                {
                    Console.WriteLine("[Daemon] ID-COOLING LCD Display connected successfully.");
                    wasConnected = true;
                }
                else
                {
                    if (wasConnected)
                    {
                        Console.WriteLine("[Daemon] Device disconnected. Retrying in 2 seconds...");
                        wasConnected = false;
                    }
                    try
                    {
                        await Task.Delay(2000, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }
            }

            try
            {
                float? temp = LinuxHardwareService.GetCpuTemperature();
                float? load = LinuxHardwareService.GetCpuLoad();
                float? freq = LinuxHardwareService.GetCpuFrequency();

                if (temp.HasValue && temp.Value > 0)
                {
                    int tempInt = (int)Math.Round(temp.Value);
                    bool ok = hid.SendTemperature(tempInt);
                    if (!ok)
                    {
                        Console.WriteLine("[Daemon] Failed to send telemetry frame. Reconnecting...");
                        hid.CloseDevice();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Daemon] Loop error: {ex.Message}");
            }

            try
            {
                await Task.Delay(1000, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        hid.CloseDevice();
        Console.WriteLine("[Daemon] Service stopped cleanly.");
    }
}
