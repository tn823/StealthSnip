using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StealthSnip;

public enum EditorTool
{
    Pen,
    Highlighter,
    Eraser,
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    StepBadge,
    Blur
}

public class SnipEditorForm : Form
{
    private readonly Bitmap _originalImage;
    private readonly AppSettings _settings;
    private readonly Action<string, string>? _notifyAction;

    // Annotation history for Undo/Redo
    private readonly List<IAnnotation> _annotations = new();
    private readonly Stack<List<IAnnotation>> _undoStack = new();
    private readonly Stack<List<IAnnotation>> _redoStack = new();

    // Active tool settings
    private EditorTool _currentTool = EditorTool.Pen;
    private Color _penColor = Color.FromArgb(232, 17, 35); // Default Snipping red
    private float _penSize = 4f;

    private Color _highlighterColor = Color.FromArgb(110, 255, 241, 0); // Default fluorescent yellow
    private float _highlighterSize = 20f;

    private Color _shapeAndTextColor = Color.FromArgb(232, 17, 35);
    private float _shapeSize = 3f;

    private int _currentStepNumber = 1;

    // Drawing states
    private bool _isDrawing = false;
    private PointF _dragStartPoint;
    private PointF _dragCurrentPoint;
    private PenStrokeAnnotation? _activeStroke;
    private BlurAnnotation? _activeBlur;

    // In-place text input
    private TextBox? _activeTextBox;
    private PointF _textLocation;

    // UI Controls
    private readonly Panel _toolbarPanel;
    private readonly Panel _canvasContainer;
    private readonly PictureBox _canvasBox;
    private readonly Dictionary<EditorTool, Button> _toolButtons = new();
    private readonly Button _btnUndo;
    private readonly Button _btnRedo;
    private readonly Button _btnCopy;
    private readonly Button _btnSave;

    public SnipEditorForm(Bitmap image, AppSettings settings, Action<string, string>? notifyAction = null)
    {
        _originalImage = (Bitmap)image.Clone();
        _settings = settings;
        _notifyAction = notifyAction;

        Text = "StealthSnip - Markup & Annotate";
        DoubleBuffered = true;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        // Size appropriately
        int screenW = Screen.PrimaryScreen?.WorkingArea.Width ?? 1280;
        int screenH = Screen.PrimaryScreen?.WorkingArea.Height ?? 800;
        int initW = Math.Min(screenW - 100, Math.Max(950, _originalImage.Width + 80));
        int initH = Math.Min(screenH - 100, Math.Max(650, _originalImage.Height + 130));
        Size = new Size(initW, initH);

        // 1. Modern Top Toolbar
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.FromArgb(40, 40, 42),
            Padding = new Padding(12, 6, 12, 6)
        };
        _toolbarPanel.Paint += (s, pe) =>
        {
            using Pen borderPen = new Pen(Color.FromArgb(60, 60, 65), 1f);
            pe.Graphics.DrawLine(borderPen, 0, _toolbarPanel.Height - 1, _toolbarPanel.Width, _toolbarPanel.Height - 1);
        };

        var flowLeft = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            BackColor = Color.Transparent,
            WrapContents = false
        };

        // Tool buttons
        AddToolButton(flowLeft, EditorTool.Pen, "✏️ Bút", "Bút vẽ tự do (nhấp lại để chọn màu & cỡ nét)");
        AddToolButton(flowLeft, EditorTool.Highlighter, "🖌️ Dạ quang", "Bút dạ quang trong suốt (nhấp lại để chọn màu & cỡ)");
        AddToolButton(flowLeft, EditorTool.Eraser, "🧹 Tẩy", "Xóa các nét vẽ và đối tượng");

        AddSeparator(flowLeft);

        AddToolButton(flowLeft, EditorTool.Arrow, "↗️ Mũi tên", "Vẽ mũi tên chỉ dẫn");
        AddToolButton(flowLeft, EditorTool.Rectangle, "🔲 Khung chữ nhật", "Vẽ khung chữ nhật");
        AddToolButton(flowLeft, EditorTool.Ellipse, "⭕ Khung tròn", "Vẽ khung elip/tròn");
        AddToolButton(flowLeft, EditorTool.Text, "🔤 Chữ", "Thêm ghi chú chữ");
        AddToolButton(flowLeft, EditorTool.StepBadge, "🔢 Đánh số", "Đánh số thứ tự 1, 2, 3... (Chuột phải để reset số)");
        AddToolButton(flowLeft, EditorTool.Blur, "🌫️ Làm mờ", "Kéo che mờ vùng thông tin nhạy cảm");

        var flowRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            BackColor = Color.Transparent,
            WrapContents = false
        };

        _btnUndo = CreateActionButton("↩️", "Hoàn tác (Ctrl + Z)", (s, e) => Undo());
        _btnRedo = CreateActionButton("↪️", "Làm lại (Ctrl + Y)", (s, e) => Redo());
        var btnClear = CreateActionButton("🗑️", "Xóa tất cả chú thích", (s, e) => ClearAll());

        AddSeparator(flowRight);

        _btnCopy = CreateActionButton("📋 Sao chép", "Sao chép ảnh đã đánh dấu vào Clipboard (Ctrl + C)", (s, e) => CopyToClipboard());
        _btnCopy.BackColor = Color.FromArgb(0, 120, 215);
        _btnCopy.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        _btnSave = CreateActionButton("💾 Lưu ảnh", "Lưu ảnh đã đánh dấu ra file (Ctrl + S)", (s, e) => SaveToFile());

        flowRight.Controls.Add(_btnUndo);
        flowRight.Controls.Add(_btnRedo);
        flowRight.Controls.Add(btnClear);
        flowRight.Controls.Add(_btnCopy);
        flowRight.Controls.Add(_btnSave);

        _toolbarPanel.Controls.Add(flowLeft);
        _toolbarPanel.Controls.Add(flowRight);
        Controls.Add(_toolbarPanel);

        // 2. Canvas Container with smooth centering
        _canvasContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(26, 26, 28)
        };

        _canvasBox = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Normal,
            Size = _originalImage.Size,
            Cursor = Cursors.Cross,
            BackColor = Color.Transparent
        };

        _canvasBox.Paint += CanvasBox_Paint;
        _canvasBox.MouseDown += CanvasBox_MouseDown;
        _canvasBox.MouseMove += CanvasBox_MouseMove;
        _canvasBox.MouseUp += CanvasBox_MouseUp;

        _canvasContainer.Controls.Add(_canvasBox);
        _canvasContainer.Resize += (s, e) => CenterCanvas();
        Controls.Add(_canvasContainer);

        UpdateToolButtonStyles();
        UpdateUndoRedoState();
        CenterCanvas();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Y)
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.C)
        {
            CopyToClipboard();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            SaveToFile();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            if (_activeTextBox != null)
            {
                CancelTextInput();
            }
            else
            {
                Close();
            }
            e.Handled = true;
        }
    }

    private void CenterCanvas()
    {
        int x = Math.Max(20, (_canvasContainer.ClientSize.Width - _canvasBox.Width) / 2);
        int y = Math.Max(20, (_canvasContainer.ClientSize.Height - _canvasBox.Height) / 2);
        _canvasBox.Location = new Point(x, y);
    }

    private void AddToolButton(Control parent, EditorTool tool, string text, string tooltip)
    {
        var btn = new Button
        {
            Text = text,
            Tag = tool,
            Height = 36,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 3, 1),
            Padding = new Padding(8, 0, 8, 0)
        };
        btn.FlatAppearance.BorderSize = 0;

        var tip = new ToolTip();
        tip.SetToolTip(btn, tooltip);

        btn.Click += (s, e) =>
        {
            if (_currentTool == tool)
            {
                // Click active tool again -> Show Color/Size Popup
                ShowToolOptionsPopup(btn, tool);
            }
            else
            {
                _currentTool = tool;
                UpdateToolButtonStyles();
                UpdateCursor();
            }
        };

        _toolButtons[tool] = btn;
        parent.Controls.Add(btn);
    }

    private Button CreateActionButton(string text, string tooltip, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            Height = 36,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 3, 1),
            Padding = new Padding(8, 0, 8, 0),
            BackColor = Color.FromArgb(50, 50, 54)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;

        var tip = new ToolTip();
        tip.SetToolTip(btn, tooltip);

        return btn;
    }

    private void AddSeparator(Control parent)
    {
        var sep = new Panel
        {
            Width = 1,
            Height = 24,
            BackColor = Color.FromArgb(70, 70, 76),
            Margin = new Padding(6, 6, 6, 6)
        };
        parent.Controls.Add(sep);
    }

    private void UpdateToolButtonStyles()
    {
        foreach (var kvp in _toolButtons)
        {
            bool isActive = kvp.Key == _currentTool;
            kvp.Value.BackColor = isActive ? Color.FromArgb(0, 120, 215) : Color.Transparent;
            kvp.Value.Font = new Font("Segoe UI", 9f, isActive ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private void UpdateCursor()
    {
        _canvasBox.Cursor = _currentTool switch
        {
            EditorTool.Eraser => Cursors.Hand,
            EditorTool.Text => Cursors.IBeam,
            _ => Cursors.Cross
        };
    }

    private void ShowToolOptionsPopup(Button anchor, EditorTool tool)
    {
        Color curCol = tool == EditorTool.Highlighter ? _highlighterColor : _penColor;
        float curSize = tool == EditorTool.Highlighter ? _highlighterSize : _penSize;
        bool isHighlighter = tool == EditorTool.Highlighter;

        if (tool == EditorTool.Arrow || tool == EditorTool.Rectangle || tool == EditorTool.Ellipse || tool == EditorTool.StepBadge)
        {
            curCol = _shapeAndTextColor;
            curSize = _shapeSize;
            isHighlighter = false;
        }

        var popup = new ColorPalettePopup(curCol, curSize, isHighlighter);
        popup.ValuesChanged += (newCol, newSize) =>
        {
            if (tool == EditorTool.Highlighter)
            {
                _highlighterColor = newCol;
                _highlighterSize = newSize;
            }
            else if (tool == EditorTool.Pen)
            {
                _penColor = newCol;
                _penSize = newSize;
            }
            else
            {
                _shapeAndTextColor = newCol;
                _shapeSize = newSize;
            }
        };

        popup.Show(anchor, new Point(0, anchor.Height + 4));
    }

    #region Canvas Painting & Mouse Events

    private void CanvasBox_Paint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // 1. Draw base screenshot
        g.DrawImage(_originalImage, 0, 0, _originalImage.Width, _originalImage.Height);

        // 2. Draw committed annotations
        foreach (var annotation in _annotations)
        {
            annotation.Draw(g);
        }

        // 3. Draw live/in-progress annotation
        if (_isDrawing)
        {
            DrawLivePreview(g);
        }
    }

    private void DrawLivePreview(Graphics g)
    {
        switch (_currentTool)
        {
            case EditorTool.Pen:
            case EditorTool.Highlighter:
                _activeStroke?.Draw(g);
                break;

            case EditorTool.Arrow:
                new ArrowAnnotation(_dragStartPoint, _dragCurrentPoint, _shapeAndTextColor, _shapeSize).Draw(g);
                break;

            case EditorTool.Rectangle:
                RectangleF r = GetNormalizedRect(_dragStartPoint, _dragCurrentPoint);
                new RectangleAnnotation(r, _shapeAndTextColor, _shapeSize).Draw(g);
                break;

            case EditorTool.Ellipse:
                RectangleF er = GetNormalizedRect(_dragStartPoint, _dragCurrentPoint);
                new EllipseAnnotation(er, _shapeAndTextColor, _shapeSize).Draw(g);
                break;

            case EditorTool.Blur:
                _activeBlur?.Draw(g);
                break;
        }
    }

    private void CanvasBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_activeTextBox != null)
        {
            CommitTextInput();
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            if (_currentTool == EditorTool.StepBadge)
            {
                _currentStepNumber = 1;
                _notifyAction?.Invoke("Đánh số", "Đã reset số thứ tự về 1.");
            }
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        PointF pt = e.Location;

        if (_currentTool == EditorTool.Eraser)
        {
            EraseAtPoint(pt);
            return;
        }

        if (_currentTool == EditorTool.StepBadge)
        {
            PushUndoState();
            _annotations.Add(new StepBadgeAnnotation(pt, _currentStepNumber++, _shapeAndTextColor));
            UpdateUndoRedoState();
            _canvasBox.Invalidate();
            return;
        }

        if (_currentTool == EditorTool.Text)
        {
            StartTextInput(pt);
            return;
        }

        _isDrawing = true;
        _dragStartPoint = pt;
        _dragCurrentPoint = pt;

        if (_currentTool == EditorTool.Pen)
        {
            _activeStroke = new PenStrokeAnnotation(_penColor, _penSize, false);
            _activeStroke.Points.Add(pt);
        }
        else if (_currentTool == EditorTool.Highlighter)
        {
            _activeStroke = new PenStrokeAnnotation(_highlighterColor, _highlighterSize, true);
            _activeStroke.Points.Add(pt);
        }
        else if (_currentTool == EditorTool.Blur)
        {
            _activeBlur = new BlurAnnotation(Rectangle.Round(GetNormalizedRect(pt, pt)), _originalImage);
        }

        _canvasBox.Invalidate();
    }

    private void CanvasBox_MouseMove(object? sender, MouseEventArgs e)
    {
        PointF pt = e.Location;

        if (_currentTool == EditorTool.Eraser && e.Button == MouseButtons.Left)
        {
            EraseAtPoint(pt);
            return;
        }

        if (!_isDrawing) return;

        _dragCurrentPoint = pt;

        if (_currentTool == EditorTool.Pen || _currentTool == EditorTool.Highlighter)
        {
            _activeStroke?.Points.Add(pt);
        }
        else if (_currentTool == EditorTool.Blur && _activeBlur != null)
        {
            _activeBlur.UpdateRect(Rectangle.Round(GetNormalizedRect(_dragStartPoint, pt)));
        }

        _canvasBox.Invalidate();
    }

    private void CanvasBox_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        PushUndoState();

        switch (_currentTool)
        {
            case EditorTool.Pen:
            case EditorTool.Highlighter:
                if (_activeStroke != null && _activeStroke.Points.Count > 0)
                {
                    _annotations.Add(_activeStroke);
                }
                _activeStroke = null;
                break;

            case EditorTool.Arrow:
                float dx = _dragCurrentPoint.X - _dragStartPoint.X;
                float dy = _dragCurrentPoint.Y - _dragStartPoint.Y;
                if (MathF.Sqrt(dx * dx + dy * dy) > 4)
                {
                    _annotations.Add(new ArrowAnnotation(_dragStartPoint, _dragCurrentPoint, _shapeAndTextColor, _shapeSize));
                }
                break;

            case EditorTool.Rectangle:
                RectangleF r = GetNormalizedRect(_dragStartPoint, _dragCurrentPoint);
                if (r.Width > 3 && r.Height > 3)
                {
                    _annotations.Add(new RectangleAnnotation(r, _shapeAndTextColor, _shapeSize));
                }
                break;

            case EditorTool.Ellipse:
                RectangleF er = GetNormalizedRect(_dragStartPoint, _dragCurrentPoint);
                if (er.Width > 3 && er.Height > 3)
                {
                    _annotations.Add(new EllipseAnnotation(er, _shapeAndTextColor, _shapeSize));
                }
                break;

            case EditorTool.Blur:
                if (_activeBlur != null && _activeBlur.Rect.Width > 4 && _activeBlur.Rect.Height > 4)
                {
                    _annotations.Add(_activeBlur);
                }
                _activeBlur = null;
                break;
        }

        UpdateUndoRedoState();
        _canvasBox.Invalidate();
    }

    private void EraseAtPoint(PointF pt)
    {
        for (int i = _annotations.Count - 1; i >= 0; i--)
        {
            if (_annotations[i].HitTest(pt))
            {
                PushUndoState();
                _annotations.RemoveAt(i);
                UpdateUndoRedoState();
                _canvasBox.Invalidate();
                break;
            }
        }
    }

    #endregion

    #region Text Input Handling

    private void StartTextInput(PointF pt)
    {
        _textLocation = pt;
        _activeTextBox = new TextBox
        {
            Location = new Point((int)pt.X, (int)pt.Y),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = _shapeAndTextColor,
            BackColor = Color.FromArgb(28, 28, 30),
            BorderStyle = BorderStyle.FixedSingle,
            Width = 220,
            Multiline = true,
            Height = 32
        };

        _activeTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                CommitTextInput();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelTextInput();
                e.Handled = true;
            }
        };

        _activeTextBox.LostFocus += (s, e) => CommitTextInput();

        _canvasBox.Controls.Add(_activeTextBox);
        _activeTextBox.Focus();
    }

    private void CommitTextInput()
    {
        if (_activeTextBox == null) return;

        string text = _activeTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            PushUndoState();
            _annotations.Add(new TextAnnotation(_textLocation, text, _activeTextBox.Font, _shapeAndTextColor));
            UpdateUndoRedoState();
        }

        _canvasBox.Controls.Remove(_activeTextBox);
        _activeTextBox.Dispose();
        _activeTextBox = null;
        _canvasBox.Invalidate();
    }

    private void CancelTextInput()
    {
        if (_activeTextBox == null) return;
        _canvasBox.Controls.Remove(_activeTextBox);
        _activeTextBox.Dispose();
        _activeTextBox = null;
        _canvasBox.Invalidate();
    }

    #endregion

    #region Undo / Redo / Actions

    private void PushUndoState()
    {
        _undoStack.Push(new List<IAnnotation>(_annotations));
        _redoStack.Clear();
    }

    private void Undo()
    {
        if (_undoStack.Count > 0)
        {
            _redoStack.Push(new List<IAnnotation>(_annotations));
            _annotations.Clear();
            _annotations.AddRange(_undoStack.Pop());
            UpdateUndoRedoState();
            _canvasBox.Invalidate();
        }
    }

    private void Redo()
    {
        if (_redoStack.Count > 0)
        {
            _undoStack.Push(new List<IAnnotation>(_annotations));
            _annotations.Clear();
            _annotations.AddRange(_redoStack.Pop());
            UpdateUndoRedoState();
            _canvasBox.Invalidate();
        }
    }

    private void ClearAll()
    {
        if (_annotations.Count == 0) return;
        PushUndoState();
        _annotations.Clear();
        UpdateUndoRedoState();
        _canvasBox.Invalidate();
    }

    private void UpdateUndoRedoState()
    {
        _btnUndo.Enabled = _undoStack.Count > 0;
        _btnRedo.Enabled = _redoStack.Count > 0;
        _btnUndo.ForeColor = _btnUndo.Enabled ? Color.White : Color.Gray;
        _btnRedo.ForeColor = _btnRedo.Enabled ? Color.White : Color.Gray;
    }

    public Bitmap RenderFinalImage()
    {
        Bitmap result = new Bitmap(_originalImage.Width, _originalImage.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(result))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.DrawImage(_originalImage, 0, 0, _originalImage.Width, _originalImage.Height);

            foreach (var annotation in _annotations)
            {
                annotation.Draw(g);
            }
        }
        return result;
    }

    private void CopyToClipboard()
    {
        try
        {
            using Bitmap finalImage = RenderFinalImage();
            Clipboard.SetImage(finalImage);

            string origText = _btnCopy.Text;
            _btnCopy.Text = "✅ Đã chép!";
            _btnCopy.BackColor = Color.FromArgb(16, 180, 50);

            var timer = new System.Windows.Forms.Timer { Interval = 1400 };
            timer.Tick += (s, e) =>
            {
                _btnCopy.Text = origText;
                _btnCopy.BackColor = Color.FromArgb(0, 120, 215);
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();

            _notifyAction?.Invoke("Đã sao chép", "Ảnh đã được sao chép vào Clipboard (Ctrl + V để dán).");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi sao chép: {ex.Message}", "StealthSnip", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveToFile()
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Lưu ảnh chụp",
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp",
            FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                using Bitmap finalImage = RenderFinalImage();
                ImageFormat fmt = Path.GetExtension(sfd.FileName).ToLower() switch
                {
                    ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                    ".bmp" => ImageFormat.Bmp,
                    _ => ImageFormat.Png
                };

                finalImage.Save(sfd.FileName, fmt);
                _notifyAction?.Invoke("Đã lưu ảnh", $"Đã lưu vào:\n{Path.GetFileName(sfd.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu file: {ex.Message}", "StealthSnip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    #endregion

    private static RectangleF GetNormalizedRect(PointF p1, PointF p2)
    {
        float x = Math.Min(p1.X, p2.X);
        float y = Math.Min(p1.Y, p2.Y);
        float w = Math.Abs(p1.X - p2.X);
        float h = Math.Abs(p1.Y - p2.Y);
        return new RectangleF(x, y, w, h);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _originalImage.Dispose();
        }
        base.Dispose(disposing);
    }
}
