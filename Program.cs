using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace StealthSnip;

static class Program
{
    private static Mutex? _mutex = null;
    private const string AppMutexName = "StealthSnip_SingleInstance_Mutex_987654";

    public static readonly int WM_TRIGGER_SNIP = NativeMethods.RegisterWindowMessage("StealthSnip_Trigger_Snip_Msg");

    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.ExceptionObject?.ToString());
            }
            catch { }
        };

        Application.ThreadException += (s, e) =>
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception?.ToString());
            }
            catch { }
        };

        if (args.Length > 0 && args[0] == "--test-snip")
        {
            RunSnipDiagnostic();
            return;
        }

        // Ensure single instance only
        _mutex = new Mutex(true, AppMutexName, out bool createdNew);
        if (!createdNew)
        {
            // If already running, double-clicking StealthSnip.exe triggers snip immediately!
            NativeMethods.PostMessage((IntPtr)0xFFFF, WM_TRIGGER_SNIP, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), ex.ToString());
        }
        finally
        {
            // Keep mutex alive until application exits
            GC.KeepAlive(_mutex);
        }
    }

    private static void RunSnipDiagnostic()
    {
        try
        {
            Console.WriteLine("[Diagnostic] Step 1: Initializing configuration...");
            ApplicationConfiguration.Initialize();

            Console.WriteLine($"[Diagnostic] Step 2: VirtualScreen = {SystemInformation.VirtualScreen}");

            Console.WriteLine("[Diagnostic] Step 3: Capturing screen via CaptureHelper.CaptureVirtualScreen()...");
            using var snapshot = CaptureHelper.CaptureVirtualScreen();
            Console.WriteLine($"[Diagnostic] Snapshot captured successfully: {snapshot.Width}x{snapshot.Height}");

            Console.WriteLine("[Diagnostic] Step 4: Creating StealthSnipForm...");
            var settings = AppSettings.Load();
            using var form = new StealthSnipForm(snapshot, settings, (t, m) => Console.WriteLine($"[Notify] {t}: {m}"));
            Console.WriteLine($"[Diagnostic] Form created with Bounds = {form.Bounds}");

            Console.WriteLine("[Diagnostic] Step 5: Showing form for 3 seconds so user can see it...");
            var timer = new System.Windows.Forms.Timer { Interval = 3000 };
            timer.Tick += (s, e) =>
            {
                Console.WriteLine("[Diagnostic] Timer elapsed, test completed successfully!");
                timer.Stop();
                form.Close();
                Application.Exit();
            };
            timer.Start();

            Application.Run(form);
            Console.WriteLine("[Diagnostic] Form test finished cleanly!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Diagnostic] ERROR: {ex}");
        }
    }
}