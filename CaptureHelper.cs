using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace StealthSnip;

public static class CaptureHelper
{
    public static Bitmap CaptureRectangle(Rectangle rect)
    {
        IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        IntPtr hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, rect.Width, rect.Height);
        IntPtr hOld = NativeMethods.SelectObject(hdcMem, hBitmap);

        NativeMethods.BitBlt(
            hdcMem, 0, 0, rect.Width, rect.Height,
            hdcScreen, rect.X, rect.Y,
            NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT
        );

        using Bitmap tempBmp = Image.FromHbitmap(hBitmap);
        // Create fully managed GDI+ copy so we can free GDI HBITMAP immediately
        Bitmap result = new Bitmap(tempBmp);

        NativeMethods.SelectObject(hdcMem, hOld);
        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(hdcMem);
        NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);

        return result;
    }

    public static Bitmap CaptureVirtualScreen()
    {
        return CaptureRectangle(SystemInformation.VirtualScreen);
    }

    public static Bitmap? CropBitmap(Bitmap source, Rectangle cropArea)
    {
        if (cropArea.Width <= 0 || cropArea.Height <= 0)
            return null;

        int x = Math.Max(0, Math.Min(cropArea.X, source.Width - 1));
        int y = Math.Max(0, Math.Min(cropArea.Y, source.Height - 1));
        int w = Math.Min(cropArea.Width, source.Width - x);
        int h = Math.Min(cropArea.Height, source.Height - y);

        if (w <= 0 || h <= 0)
            return null;

        Rectangle validRect = new Rectangle(x, y, w, h);
        return source.Clone(validRect, PixelFormat.Format32bppArgb);
    }

    public static void ProcessCapturedImage(Image image, AppSettings settings, Action<string, string>? notifyAction = null)
    {
        try
        {
            // 1. Copy to Clipboard with retry mechanism
            bool copied = false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetImage(image);
                    copied = true;
                    break;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(20);
                }
            }

            if (!copied)
            {
                notifyAction?.Invoke("Lỗi Clipboard", "Không thể ghi ảnh vào Clipboard vì một ứng dụng khác đang chiếm giữ.");
                return;
            }

            string? savedFilePath = null;

            // 2. Auto-save if enabled
            if (settings.AutoSave)
            {
                if (!Directory.Exists(settings.SaveDirectory))
                {
                    Directory.CreateDirectory(settings.SaveDirectory);
                }

                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                savedFilePath = Path.Combine(settings.SaveDirectory, fileName);
                image.Save(savedFilePath, ImageFormat.Png);
            }

            // 3. Play sound if enabled
            if (settings.PlaySound)
            {
                SystemSounds.Asterisk.Play();
            }

            // 4. Notification if enabled
            if (settings.ShowNotification && notifyAction != null)
            {
                string message = settings.AutoSave && savedFilePath != null
                    ? $"Đã copy vào Clipboard & lưu vào:\n{Path.GetFileName(savedFilePath)}"
                    : "Đã copy ảnh vào Clipboard! (Ctrl + V để dán)";
                notifyAction("Chụp màn hình thành công", message);
            }
        }
        catch (Exception ex)
        {
            notifyAction?.Invoke("Lỗi xử lý ảnh", ex.Message);
        }
        finally
        {
            NativeMethods.MinimizeMemory();
        }
    }

    public static Bitmap CaptureFullscreen(AppSettings settings, Action<string, string>? notifyAction = null)
    {
        Bitmap bmp = CaptureVirtualScreen();
        ProcessCapturedImage(bmp, settings, notifyAction);
        return bmp;
    }

    public static Bitmap CaptureActiveWindow(AppSettings settings, Action<string, string>? notifyAction = null)
    {
        IntPtr hWnd = NativeMethods.GetForegroundWindow();
        Rectangle bounds = NativeMethods.GetAccurateWindowBounds(hWnd);
        Rectangle virtualScreen = SystemInformation.VirtualScreen;

        bounds.Intersect(virtualScreen);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return CaptureFullscreen(settings, notifyAction);
        }

        Bitmap bmp = CaptureRectangle(bounds);
        ProcessCapturedImage(bmp, settings, notifyAction);
        return bmp;
    }
}
