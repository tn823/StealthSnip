using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StealthSnip;

public class SnipPreviewPopup : Form
{
    private readonly Bitmap _capturedImage;
    private readonly Action<Bitmap> _onEditRequested;
    private bool _actionHandled = false;

    private readonly System.Windows.Forms.Timer _animationTimer;
    private const int TotalDurationMs = 4000;
    private int _remainingMs = TotalDurationMs;

    private enum PopupState { FadeIn, Active, FadeOut, Closed }
    private PopupState _state = PopupState.FadeIn;

    // Layout
    private const int BarHeight = 3;
    private readonly Rectangle _imageRect;
    private readonly Rectangle _closeBtnRect;

    private bool _isCloseHovered = false;
    private bool _isImageHovered = false;

    public SnipPreviewPopup(Bitmap capturedImage, Action<Bitmap> onEditRequested)
    {
        _capturedImage = capturedImage;
        _onEditRequested = onEditRequested;

        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Opacity = 0.0;

        // Calculate dynamic dimensions matching image aspect ratio
        CalculateSize(out int w, out int h);
        Size = new Size(w, h + BarHeight);

        _imageRect = new Rectangle(0, 0, w, h);
        _closeBtnRect = new Rectangle(w - 28, 6, 22, 22);

        // Position at bottom-right of current screen
        PositionAtBottomRight();

        // Timer for fade & countdown
        _animationTimer = new System.Windows.Forms.Timer { Interval = 25 };
        _animationTimer.Tick += OnAnimationTick;

        Cursor = Cursors.Hand;
    }

    private void CalculateSize(out int w, out int h)
    {
        const int maxW = 320;
        const int maxH = 200;
        const int minW = 140;
        const int minH = 80;

        float ratio = (float)_capturedImage.Width / Math.Max(1, _capturedImage.Height);

        if (ratio >= 1.0f)
        {
            w = maxW;
            h = (int)(w / ratio);
            if (h > maxH)
            {
                h = maxH;
                w = (int)(h * ratio);
            }
            if (h < minH) h = minH;
        }
        else
        {
            h = maxH;
            w = (int)(h * ratio);
            if (w > maxW)
            {
                w = maxW;
                h = (int)(w / ratio);
            }
            if (w < minW) w = minW;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    private void PositionAtBottomRight()
    {
        Screen screen = Screen.FromPoint(Cursor.Position);
        Rectangle workArea = screen.WorkingArea;
        const int margin = 16;
        Location = new Point(workArea.Right - Width - margin, workArea.Bottom - Height - margin);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateFormRegion();
        _animationTimer.Start();
    }

    private void UpdateFormRegion()
    {
        using GraphicsPath path = CreateRoundedRectanglePath(new Rectangle(0, 0, Width, Height), 8);
        Region = new Region(path);
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        Point mousePos = PointToClient(Cursor.Position);
        bool isMouseInside = ClientRectangle.Contains(mousePos);

        switch (_state)
        {
            case PopupState.FadeIn:
                Opacity += 0.15;
                if (Opacity >= 0.98)
                {
                    Opacity = 0.98;
                    _state = PopupState.Active;
                }
                Invalidate();
                break;

            case PopupState.Active:
                if (!isMouseInside)
                {
                    _remainingMs -= _animationTimer.Interval;
                    if (_remainingMs <= 0)
                    {
                        _state = PopupState.FadeOut;
                    }
                }
                Invalidate();
                break;

            case PopupState.FadeOut:
                Opacity -= 0.15;
                if (Opacity <= 0.05)
                {
                    _animationTimer.Stop();
                    _state = PopupState.Closed;
                    Close();
                }
                Invalidate();
                break;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        bool closeHover = _closeBtnRect.Contains(e.Location);
        bool imgHover = !closeHover && _imageRect.Contains(e.Location);

        if (closeHover != _isCloseHovered || imgHover != _isImageHovered)
        {
            _isCloseHovered = closeHover;
            _isImageHovered = imgHover;
            Cursor = Cursors.Hand;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isCloseHovered = false;
        _isImageHovered = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
        {
            if (_closeBtnRect.Contains(e.Location))
            {
                DismissPopup();
            }
            else
            {
                // Click anywhere on image opens editor
                TriggerEdit();
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            DismissPopup();
        }
    }

    private void TriggerEdit()
    {
        if (_actionHandled) return;
        _actionHandled = true;

        _animationTimer.Stop();
        Close();

        _onEditRequested?.Invoke(_capturedImage);
    }

    private void DismissPopup()
    {
        if (_actionHandled) return;
        _actionHandled = true;

        _animationTimer.Stop();
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // 1. Draw the captured image directly filling the image rectangle
        g.DrawImage(_capturedImage, _imageRect);

        // 2. Hover overlay on image
        if (_isImageHovered)
        {
            using var hoverOverlay = new SolidBrush(Color.FromArgb(25, 255, 255, 255));
            g.FillRectangle(hoverOverlay, _imageRect);
        }

        // 3. Close Button in top-right corner of image (circular dark badge)
        DrawCloseButton(g);

        // 4. Subtle Outer Border around the popup
        Color borderColor = _isImageHovered ? Color.FromArgb(0, 122, 255) : Color.FromArgb(80, 255, 255, 255);
        using (var borderPen = new Pen(borderColor, _isImageHovered ? 1.5f : 1f))
        {
            using GraphicsPath borderPath = CreateRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), 8);
            g.DrawPath(borderPen, borderPath);
        }

        // 5. Countdown Progress Bar right under the image
        DrawCountdownBar(g);
    }

    private void DrawCloseButton(Graphics g)
    {
        // Dark translucent circle/pill background so close button is clear over any background
        Color bg = _isCloseHovered ? Color.FromArgb(220, 230, 40, 40) : Color.FromArgb(160, 20, 20, 24);
        using (var bgBrush = new SolidBrush(bg))
        {
            g.FillEllipse(bgBrush, _closeBtnRect);
        }

        using (var borderPen = new Pen(_isCloseHovered ? Color.White : Color.FromArgb(80, 255, 255, 255), 1f))
        {
            g.DrawEllipse(borderPen, _closeBtnRect);
        }

        // White 'X' symbol
        using var xPen = new Pen(Color.White, 1.4f);
        int pad = 6;
        g.DrawLine(xPen, _closeBtnRect.Left + pad, _closeBtnRect.Top + pad, _closeBtnRect.Right - pad, _closeBtnRect.Bottom - pad);
        g.DrawLine(xPen, _closeBtnRect.Right - pad, _closeBtnRect.Top + pad, _closeBtnRect.Left + pad, _closeBtnRect.Bottom - pad);
    }

    private void DrawCountdownBar(Graphics g)
    {
        int barY = Height - BarHeight;
        Rectangle fullBar = new Rectangle(0, barY, Width, BarHeight);

        // Dark track background
        using (var trackBrush = new SolidBrush(Color.FromArgb(180, 20, 20, 24)))
        {
            g.FillRectangle(trackBrush, fullBar);
        }

        float progress = Math.Clamp((float)_remainingMs / TotalDurationMs, 0f, 1f);
        int fillW = (int)(Width * progress);

        if (fillW > 0)
        {
            using var progressBrush = new SolidBrush(Color.FromArgb(0, 122, 255));
            g.FillRectangle(progressBrush, new Rectangle(0, barY, fillW, BarHeight));
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        return path;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        _animationTimer.Stop();
        _animationTimer.Dispose();

        if (!_actionHandled)
        {
            _capturedImage?.Dispose();
        }

        NativeMethods.MinimizeMemory();
    }
}
