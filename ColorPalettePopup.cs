using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StealthSnip;

public class ColorPalettePopup : ToolStripDropDown
{
    private Color _selectedColor;
    private float _strokeSize;
    private readonly bool _isHighlighter;

    public event Action<Color, float>? ValuesChanged;

    private readonly Panel _containerPanel;
    private readonly PictureBox _previewBox;
    private readonly TrackBar _sizeSlider;

    private static readonly Color[] PaletteColors = new[]
    {
        // Row 1: Grayscale & Neutrals
        Color.FromArgb(20, 20, 20),
        Color.FromArgb(255, 255, 255),
        Color.FromArgb(170, 170, 170),
        Color.FromArgb(110, 110, 110),
        Color.FromArgb(70, 75, 80),
        Color.FromArgb(45, 50, 55),

        // Row 2: Reds, Oranges, Yellows
        Color.FromArgb(218, 30, 85),
        Color.FromArgb(232, 17, 35),
        Color.FromArgb(255, 106, 0),
        Color.FromArgb(255, 154, 0),
        Color.FromArgb(255, 204, 0),
        Color.FromArgb(255, 241, 0),

        // Row 3: Greens, Cyans, Blues
        Color.FromArgb(142, 212, 0),
        Color.FromArgb(16, 180, 50),
        Color.FromArgb(0, 153, 117),
        Color.FromArgb(0, 164, 228),
        Color.FromArgb(0, 120, 215),
        Color.FromArgb(75, 40, 220),

        // Row 4: Purples, Pastels, Browns
        Color.FromArgb(136, 23, 152),
        Color.FromArgb(177, 70, 194),
        Color.FromArgb(243, 137, 182),
        Color.FromArgb(247, 198, 173),
        Color.FromArgb(152, 107, 76),
        Color.FromArgb(98, 65, 42),
    };

    public ColorPalettePopup(Color initialColor, float initialSize, bool isHighlighter)
    {
        _selectedColor = initialColor;
        _strokeSize = initialSize;
        _isHighlighter = isHighlighter;

        AutoClose = true;
        DropShadowEnabled = true;
        Padding = new Padding(0);
        Margin = new Padding(0);

        _containerPanel = new Panel
        {
            BackColor = Color.FromArgb(36, 36, 38),
            Width = 270,
            Height = isHighlighter ? 270 : 290,
            Padding = new Padding(12)
        };

        // Title: Colors
        var lblColors = new Label
        {
            Text = "Colors",
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(12, 10),
            AutoSize = true
        };
        _containerPanel.Controls.Add(lblColors);

        // Preview Box with curved sine wave
        int sizeY = 166;
        _previewBox = new PictureBox
        {
            Location = new Point(12, sizeY + 22),
            Size = new Size(244, 40),
            BackColor = Color.FromArgb(28, 28, 30)
        };
        _previewBox.Paint += (s, pe) =>
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color drawCol = _isHighlighter 
                ? Color.FromArgb(120, _selectedColor.R, _selectedColor.G, _selectedColor.B)
                : _selectedColor;

            using Pen p = new Pen(drawCol, _strokeSize)
            {
                StartCap = _isHighlighter ? LineCap.Flat : LineCap.Round,
                EndCap = _isHighlighter ? LineCap.Flat : LineCap.Round,
                LineJoin = LineJoin.Round
            };

            PointF[] curvePoints = new PointF[]
            {
                new PointF(15, 25),
                new PointF(70, 10),
                new PointF(130, 28),
                new PointF(185, 12),
                new PointF(230, 20)
            };
            pe.Graphics.DrawCurve(p, curvePoints, 0.5f);
        };

        // Color grid panel
        var gridPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 32),
            Size = new Size(250, 130),
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        foreach (var col in PaletteColors)
        {
            var btn = new Button
            {
                Size = new Size(34, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Tag = col,
                Margin = new Padding(3, 2, 3, 2)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;

            btn.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color c = (Color)((Button)s!).Tag!;
                Rectangle rect = new Rectangle(5, 3, 24, 24);

                using Brush b = new SolidBrush(c);
                pe.Graphics.FillEllipse(b, rect);

                if (c == Color.FromArgb(255, 255, 255))
                {
                    using Pen bp = new Pen(Color.FromArgb(120, 120, 120), 1f);
                    pe.Graphics.DrawEllipse(bp, rect);
                }

                if (c.R == _selectedColor.R && c.G == _selectedColor.G && c.B == _selectedColor.B)
                {
                    using Pen selPen = new Pen(Color.White, 2.5f);
                    pe.Graphics.DrawEllipse(selPen, rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);
                }
            };

            btn.Click += (s, e) =>
            {
                Color clickedCol = (Color)((Button)s!).Tag!;
                _selectedColor = _isHighlighter 
                    ? Color.FromArgb(110, clickedCol.R, clickedCol.G, clickedCol.B)
                    : Color.FromArgb(255, clickedCol.R, clickedCol.G, clickedCol.B);

                gridPanel.Invalidate(true);
                _previewBox.Invalidate();
                ValuesChanged?.Invoke(_selectedColor, _strokeSize);
            };

            gridPanel.Controls.Add(btn);
        }
        _containerPanel.Controls.Add(gridPanel);

        // Title: Size
        var lblSize = new Label
        {
            Text = "Size",
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(12, sizeY),
            AutoSize = true
        };
        _containerPanel.Controls.Add(lblSize);
        _containerPanel.Controls.Add(_previewBox);

        // Slider
        _sizeSlider = new TrackBar
        {
            Location = new Point(10, sizeY + 66),
            Width = 248,
            Height = 30,
            Minimum = _isHighlighter ? 8 : 1,
            Maximum = _isHighlighter ? 48 : 30,
            Value = Math.Max(_isHighlighter ? 8 : 1, Math.Min(_isHighlighter ? 48 : 30, (int)_strokeSize)),
            TickStyle = TickStyle.None,
            BackColor = Color.FromArgb(36, 36, 38)
        };
        _sizeSlider.ValueChanged += (s, e) =>
        {
            _strokeSize = _sizeSlider.Value;
            _previewBox.Invalidate();
            ValuesChanged?.Invoke(_selectedColor, _strokeSize);
        };
        _containerPanel.Controls.Add(_sizeSlider);

        var host = new ToolStripControlHost(_containerPanel)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false,
            Size = _containerPanel.Size
        };
        Items.Add(host);
    }

    public void UpdateValues(Color color, float size)
    {
        _selectedColor = color;
        _strokeSize = size;
        _sizeSlider.Value = Math.Max(_sizeSlider.Minimum, Math.Min(_sizeSlider.Maximum, (int)size));
        _containerPanel.Invalidate(true);
    }
}
