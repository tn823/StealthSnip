using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace StealthSnip;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AppSettings _settings;
    private readonly HotkeyManager _hotkeyManager;
    private IntPtr _iconHandle = IntPtr.Zero;
    private bool _isSnipActive = false;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        _hotkeyManager = new HotkeyManager();

        // Build context menu
        var contextMenu = new ContextMenuStrip();

        var itemSnip = new ToolStripMenuItem("📸 Chụp vùng chọn (Stealth Snip)\tAlt + A", null, (s, e) => TriggerStealthSnip())
        {
            Font = new Font(contextMenu.Font, FontStyle.Bold)
        };
        var itemFullscreen = new ToolStripMenuItem("🖥️ Chụp toàn màn hình\tAlt + S", null, (s, e) => TriggerFullscreen());
        var itemActiveWindow = new ToolStripMenuItem("🪟 Chụp cửa sổ hiện tại\tAlt + W", null, (s, e) => TriggerActiveWindow());

        var itemOpenFolder = new ToolStripMenuItem("📂 Mở thư mục lưu ảnh", null, (s, e) => OpenScreenshotsFolder());

        var itemOpenEditor = new ToolStripMenuItem("✏️ Mở trình vẽ sau khi chụp (Markup Editor)", null, (s, e) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                _settings.OpenEditorAfterSnip = !mi.Checked;
                mi.Checked = _settings.OpenEditorAfterSnip;
                _settings.Save();
            }
        }) { Checked = _settings.OpenEditorAfterSnip };

        var itemAutoSave = new ToolStripMenuItem("💾 Tự động lưu ảnh vào máy", null, (s, e) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                _settings.AutoSave = !mi.Checked;
                mi.Checked = _settings.AutoSave;
                _settings.Save();
            }
        }) { Checked = _settings.AutoSave };

        var itemSound = new ToolStripMenuItem("🔔 Âm thanh khi chụp", null, (s, e) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                _settings.PlaySound = !mi.Checked;
                mi.Checked = _settings.PlaySound;
                _settings.Save();
            }
        }) { Checked = _settings.PlaySound };

        var itemNotify = new ToolStripMenuItem("💬 Hiện thông báo sau khi chụp", null, (s, e) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                _settings.ShowNotification = !mi.Checked;
                mi.Checked = _settings.ShowNotification;
                _settings.Save();
            }
        }) { Checked = _settings.ShowNotification };

        var itemStartup = new ToolStripMenuItem("🚀 Khởi động cùng Windows", null, (s, e) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                bool newState = !mi.Checked;
                _settings.SetStartWithWindows(newState);
                mi.Checked = _settings.StartWithWindows;
            }
        }) { Checked = _settings.StartWithWindows };

        var itemHelp = new ToolStripMenuItem("ℹ️ Hướng dẫn sử dụng", null, (s, e) => ShowHelpDialog());
        var itemExit = new ToolStripMenuItem("❌ Thoát (Exit)", null, (s, e) => ExitApp());

        contextMenu.Items.AddRange(new ToolStripItem[]
        {
            itemSnip,
            itemFullscreen,
            itemActiveWindow,
            new ToolStripSeparator(),
            itemOpenEditor,
            itemOpenFolder,
            itemAutoSave,
            itemSound,
            itemNotify,
            itemStartup,
            new ToolStripSeparator(),
            itemHelp,
            itemExit
        });

        // Generate and keep Icon handle valid for the entire app lifetime
        Icon appIcon = CreateAppIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "StealthSnip (Alt + A)",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        // Left-click on tray icon triggers snip immediately!
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                TriggerStealthSnip();
            }
        };

        _notifyIcon.DoubleClick += (s, e) => TriggerStealthSnip();

        // Register Global Hotkeys
        RegisterHotkeys();

        // Welcome balloon notification on first start
        _notifyIcon.ShowBalloonTip(
            3500,
            "StealthSnip đang hoạt động",
            "• Bấm Alt + A (hoặc nhấp chuột trái vào icon này) để chụp vùng kín đáo\n• Alt + S: Chụp toàn màn hình\n• Ảnh tự copy vào Clipboard, sẵn sàng Ctrl+V!",
            ToolTipIcon.Info
        );

        // Memory trim on start
        NativeMethods.MinimizeMemory();
    }

    private void RegisterHotkeys()
    {
        // 1. Alt + A : Primary Stealth Region Snip
        _hotkeyManager.Register(
            HotkeyManager.HOTKEY_SNIP,
            NativeMethods.MOD_ALT,
            Keys.A,
            TriggerStealthSnip
        );

        // 2. Ctrl + Shift + A : Backup Region Snip (trường hợp Alt+A bị app khác chặn)
        _hotkeyManager.Register(
            10,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT,
            Keys.A,
            TriggerStealthSnip
        );

        // 3. PrintScreen key (nếu trống)
        _hotkeyManager.Register(
            11,
            0,
            Keys.PrintScreen,
            TriggerStealthSnip
        );

        // 4. Alt + S : Fullscreen
        _hotkeyManager.Register(
            HotkeyManager.HOTKEY_FULLSCREEN,
            NativeMethods.MOD_ALT,
            Keys.S,
            TriggerFullscreen
        );

        // 5. Alt + W : Active Window
        _hotkeyManager.Register(
            HotkeyManager.HOTKEY_ACTIVE_WINDOW,
            NativeMethods.MOD_ALT,
            Keys.W,
            TriggerActiveWindow
        );
    }

    private void TriggerStealthSnip()
    {
        if (_isSnipActive) return;
        _isSnipActive = true;

        try
        {
            // Capture snapshot immediately BEFORE showing the form
            Bitmap snapshot = CaptureHelper.CaptureVirtualScreen();

            using var form = new StealthSnipForm(snapshot, _settings, ShowNotification);
            form.ShowDialog();

            if (_settings.OpenEditorAfterSnip && form.ResultImage != null)
            {
                using var editor = new SnipEditorForm(form.ResultImage, _settings, ShowNotification);
                form.ResultImage.Dispose();
                editor.ShowDialog();
            }
            else
            {
                form.ResultImage?.Dispose();
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Lỗi chụp vùng", ex.Message);
        }
        finally
        {
            _isSnipActive = false;
            NativeMethods.MinimizeMemory();
        }
    }

    private void TriggerFullscreen()
    {
        try
        {
            using Bitmap bmp = CaptureHelper.CaptureFullscreen(_settings, ShowNotification);
            if (_settings.OpenEditorAfterSnip)
            {
                using var editor = new SnipEditorForm(bmp, _settings, ShowNotification);
                editor.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Lỗi chụp toàn màn", ex.Message);
        }
        finally
        {
            NativeMethods.MinimizeMemory();
        }
    }

    private void TriggerActiveWindow()
    {
        try
        {
            using Bitmap bmp = CaptureHelper.CaptureActiveWindow(_settings, ShowNotification);
            if (_settings.OpenEditorAfterSnip)
            {
                using var editor = new SnipEditorForm(bmp, _settings, ShowNotification);
                editor.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Lỗi chụp cửa sổ", ex.Message);
        }
        finally
        {
            NativeMethods.MinimizeMemory();
        }
    }

    private void OpenScreenshotsFolder()
    {
        try
        {
            if (!Directory.Exists(_settings.SaveDirectory))
            {
                Directory.CreateDirectory(_settings.SaveDirectory);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = _settings.SaveDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở thư mục: {ex.Message}", "StealthSnip", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowNotification(string title, string text)
    {
        _notifyIcon.ShowBalloonTip(2000, title, text, ToolTipIcon.Info);
    }

    private void ShowHelpDialog()
    {
        string help = 
            "=== StealthSnip - Chụp Màn Hình Tàng Hình ===\n\n" +
            "Cách kích hoạt chụp vùng chọn:\n" +
            "  1. Nhấn phím Alt + A (hoặc Ctrl + Shift + A)\n" +
            "  2. HOẶC nhấp chuột trái trực tiếp vào biểu tượng camera ở góc dưới bên phải màn hình.\n\n" +
            "Các phím khác:\n" +
            "  • Alt + S: Chụp toàn màn hình\n" +
            "  • Alt + W: Chụp cửa sổ hiện tại\n" +
            "  • Phím Esc / Chuột phải: Hủy chụp\n\n" +
            "Ảnh chụp được copy tự động vào Clipboard (chỉ cần Ctrl + V để dán).";

        MessageBox.Show(help, "Hướng dẫn StealthSnip", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        _hotkeyManager.Dispose();
        _notifyIcon.Dispose();

        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        ExitThread();
    }

    private Icon CreateAppIcon()
    {
        using Bitmap bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Outer rounded box (vibrant blue background)
            using (GraphicsPath path = new GraphicsPath())
            {
                int radius = 6;
                Rectangle rect = new Rectangle(1, 1, 30, 30);
                path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
                path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();

                using Brush bgBrush = new SolidBrush(Color.FromArgb(0, 122, 255));
                g.FillPath(bgBrush, path);
            }

            // Camera lens ring (white)
            using Pen lensPen = new Pen(Color.White, 2.2f);
            g.DrawEllipse(lensPen, 8, 8, 16, 16);

            // Center target dot
            using Brush centerBrush = new SolidBrush(Color.White);
            g.FillEllipse(centerBrush, 13, 13, 6, 6);
        }

        _iconHandle = bmp.GetHicon();
        return Icon.FromHandle(_iconHandle);
    }
}
