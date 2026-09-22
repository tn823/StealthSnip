using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StealthSnip;

public class StealthSnipForm : Form
{
    private readonly Bitmap _backgroundSnapshot;
    private readonly AppSettings _settings;
    private readonly Action<string, string>? _notifyAction;

    private bool _isDragging = false;
    private Point _startPoint;
    private Point _currentPoint;
    private Point _mouseHoverPoint;

    private readonly Pen _screenBorderPen;
    private readonly Pen _selectionBorderPen;
    private readonly Pen _selectionGlowPen;
    private readonly Font _uiFont;
    private readonly Font _dimFont;
    private readonly Brush _textBrush;
    private readonly Brush _pillBgBrush;
    private readonly Pen _pillBorderPen;

    public StealthSnipForm(Bitmap snapshot, AppSettings settings, Action<string, string>? notifyAction)
    {
        _backgroundSnapshot = snapshot;
        _settings = settings;
        _notifyAction = notifyAction;

        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        Cursor = Cursors.Cross;

        Shown += (s, e) =>
        {
            Activate();
            BringToFront();
            Focus();
        };

        // Visual styling
        _screenBorderPen = new Pen(Color.FromArgb(220, 0, 122, 255), 2.5f); // 2.5px accent blue around screen edge
        _selectionBorderPen = new Pen(Color.FromArgb(255, 0, 122, 255), 1.5f);
        _selectionGlowPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1.0f);

        _uiFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _dimFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _textBrush = new SolidBrush(Color.White);
        _pillBgBrush = new SolidBrush(Color.FromArgb(240, 28, 28, 32)); // Sleek modern dark glass pill
        _pillBorderPen = new Pen(Color.FromArgb(100, 255, 255, 255), 1f);

        _mouseHoverPoint = PointToClient(Cursor.Position);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Force bounds to match VirtualScreen after DPI layout initialization
        Bounds = SystemInformation.VirtualScreen;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 1. Draw snapshot with 100% natural screen brightness (HOÀN TOÀN KHÔNG LÀM TỐI MÀN HÌNH!)
        g.DrawImage(
            _backgroundSnapshot,
            new Rectangle(0, 0, _backgroundSnapshot.Width, _backgroundSnapshot.Height),
            0,
            0,
            _backgroundSnapshot.Width,
            _backgroundSnapshot.Height,
            GraphicsUnit.Pixel
        );

        // 2. Draw 2.5px crisp screen border (tín hiệu trực quan báo hiệu đang ở chế độ chụp mà không làm tối màn)
        g.DrawRectangle(_screenBorderPen, new Rectangle(1, 1, ClientRectangle.Width - 2, ClientRectangle.Height - 2));

        // 3. Floating Guidance Pill at top center
        DrawTopGuidancePill(g);

        // 4. Selection Box or Hover Tooltip
        if (_isDragging)
        {
            DrawSelectionBox(g);
        }
        else
        {
            DrawCursorHint(g);
        }
    }

    private void DrawTopGuidancePill(Graphics g)
    {
        string hint = "📸 Chế độ chụp vùng: Nhấp giữ chuột trái & Kéo để chọn  •  Esc: Hủy";
        SizeF textSize = g.MeasureString(hint, _uiFont);

        int pillWidth = (int)textSize.Width + 28;
        int pillHeight = (int)textSize.Height + 14;
        int pillX = (ClientRectangle.Width - pillWidth) / 2;
        int pillY = 16;

        Rectangle pillRect = new Rectangle(pillX, pillY, pillWidth, pillHeight);

        using GraphicsPath path = CreateRoundedRectanglePath(pillRect, 8);
        g.FillPath(_pillBgBrush, path);
        g.DrawPath(_pillBorderPen, path);
        g.DrawString(hint, _uiFont, _textBrush, pillX + 14, pillY + 7);
    }

    private void DrawCursorHint(Graphics g)
    {
        // Draw a tiny subtle tag near the cursor so user knows mouse is ready
        if (_mouseHoverPoint.X > 0 && _mouseHoverPoint.Y > 0)
        {
            string tag = "Kéo để chụp";
            SizeF size = g.MeasureString(tag, _dimFont);
            int tx = _mouseHoverPoint.X + 15;
            int ty = _mouseHoverPoint.Y + 15;

            // Keep within screen
            if (tx + size.Width + 12 > ClientRectangle.Right) tx = _mouseHoverPoint.X - (int)size.Width - 15;
            if (ty + size.Height + 8 > ClientRectangle.Bottom) ty = _mouseHoverPoint.Y - (int)size.Height - 15;

            Rectangle tagRect = new Rectangle(tx, ty, (int)size.Width + 12, (int)size.Height + 6);
            using GraphicsPath path = CreateRoundedRectanglePath(tagRect, 4);
            g.FillPath(_pillBgBrush, path);
            g.DrawPath(_pillBorderPen, path);
            g.DrawString(tag, _dimFont, _textBrush, tx + 6, ty + 3);
        }
    }

    private void DrawSelectionBox(Graphics g)
    {
        Rectangle rect = GetNormalizedRect(_startPoint, _currentPoint);
        if (rect.Width > 0 && rect.Height > 0)
        {
            // Outer glow + crisp inner border
            g.DrawRectangle(_selectionGlowPen, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2));
            g.DrawRectangle(_selectionBorderPen, rect);

            // Small dimension badge (e.g. "640 × 480 px")
            string dimText = $"{rect.Width} × {rect.Height} px";
            SizeF textSize = g.MeasureString(dimText, _dimFont);
            int badgeWidth = (int)textSize.Width + 14;
            int badgeHeight = (int)textSize.Height + 6;

            int badgeX = rect.Right - badgeWidth;
            int badgeY = rect.Bottom + 6;

            if (badgeY + badgeHeight > ClientRectangle.Bottom)
            {
                badgeY = rect.Top - badgeHeight - 6;
            }
            if (badgeX < rect.Left)
            {
                badgeX = rect.Left;
            }

            Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeWidth, badgeHeight);
            using GraphicsPath path = CreateRoundedRectanglePath(badgeRect, 4);
            g.FillPath(_pillBgBrush, path);
            g.DrawPath(_pillBorderPen, path);
            g.DrawString(dimText, _dimFont, _textBrush, badgeX + 7, badgeY + 3);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _startPoint = e.Location;
            _currentPoint = e.Location;
            Invalidate();
        }
        else if (e.Button == MouseButtons.Right)
        {
            // Right-click cancels selection silently
            Close();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        _mouseHoverPoint = e.Location;

        if (_isDragging)
        {
            _currentPoint = e.Location;
        }

        Invalidate();
    }

    public Bitmap? ResultImage { get; private set; }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left)
        {
            if (_isDragging)
            {
                _isDragging = false;
                Rectangle rect = GetNormalizedRect(_startPoint, _currentPoint);

                // Minimum 4x4 px to avoid accidental 1-pixel clicks
                if (rect.Width >= 4 && rect.Height >= 4)
                {
                    // Hide immediately so user feels instant response
                    Visible = false;

                    // Crop pristine snapshot (without any guidance UI pills!)
                    Bitmap? cropped = CaptureHelper.CropBitmap(_backgroundSnapshot, rect);
                    if (cropped != null)
                    {
                        CaptureHelper.ProcessCapturedImage(cropped, _settings, _notifyAction);
                        ResultImage = cropped;
                    }
                }
            }

            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Escape cancels snip silently
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle r, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Rectangle GetNormalizedRect(Point p1, Point p2)
    {
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int width = Math.Abs(p1.X - p2.X);
        int height = Math.Abs(p1.Y - p2.Y);
        return new Rectangle(x, y, width, height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backgroundSnapshot?.Dispose();
            _screenBorderPen?.Dispose();
            _selectionBorderPen?.Dispose();
            _selectionGlowPen?.Dispose();
            _uiFont?.Dispose();
            _dimFont?.Dispose();
            _textBrush?.Dispose();
            _pillBgBrush?.Dispose();
            _pillBorderPen?.Dispose();
        }
        base.Dispose(disposing);
    }
}
