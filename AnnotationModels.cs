using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace StealthSnip;

public interface IAnnotation
{
    void Draw(Graphics g);
    RectangleF GetBounds();
    bool HitTest(PointF p);
}

public class PenStrokeAnnotation : IAnnotation
{
    public List<PointF> Points { get; } = new();
    public Color Color { get; set; }
    public float Width { get; set; }
    public bool IsHighlighter { get; set; }

    public PenStrokeAnnotation(Color color, float width, bool isHighlighter)
    {
        Color = color;
        Width = width;
        IsHighlighter = isHighlighter;
    }

    public void Draw(Graphics g)
    {
        if (Points.Count < 2)
        {
            if (Points.Count == 1)
            {
                using Brush brush = new SolidBrush(Color);
                float r = Width / 2f;
                g.FillEllipse(brush, Points[0].X - r, Points[0].Y - r, Width, Width);
            }
            return;
        }

        using Pen pen = new Pen(Color, Width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        if (IsHighlighter)
        {
            pen.StartCap = LineCap.Flat;
            pen.EndCap = LineCap.Flat;
            pen.LineJoin = LineJoin.Bevel;
        }

        g.DrawLines(pen, Points.ToArray());
    }

    public RectangleF GetBounds()
    {
        if (Points.Count == 0) return RectangleF.Empty;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var pt in Points)
        {
            if (pt.X < minX) minX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y > maxY) maxY = pt.Y;
        }

        float pad = Width + 4;
        return new RectangleF(minX - pad, minY - pad, (maxX - minX) + pad * 2, (maxY - minY) + pad * 2);
    }

    public bool HitTest(PointF p)
    {
        RectangleF bounds = GetBounds();
        if (!bounds.Contains(p)) return false;

        float threshold = Math.Max(Width / 2f + 5f, 8f);
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (DistanceToLineSegment(p, Points[i], Points[i + 1]) <= threshold)
                return true;
        }
        return false;
    }

    private static float DistanceToLineSegment(PointF p, PointF a, PointF b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float l2 = dx * dx + dy * dy;
        if (l2 == 0)
        {
            float ddx = p.X - a.X;
            float ddy = p.Y - a.Y;
            return MathF.Sqrt(ddx * ddx + ddy * ddy);
        }

        float t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2));
        PointF projection = new PointF(a.X + t * dx, a.Y + t * dy);
        float px = p.X - projection.X;
        float py = p.Y - projection.Y;
        return MathF.Sqrt(px * px + py * py);
    }
}

public class ArrowAnnotation : IAnnotation
{
    public PointF Start { get; set; }
    public PointF End { get; set; }
    public Color Color { get; set; }
    public float Width { get; set; }

    public ArrowAnnotation(PointF start, PointF end, Color color, float width)
    {
        Start = start;
        End = end;
        Color = color;
        Width = width;
    }

    public void Draw(Graphics g)
    {
        float dx = End.X - Start.X;
        float dy = End.Y - Start.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 2) return;

        using Pen pen = new Pen(Color, Width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        // Arrow head dimensions proportional to width
        float headLength = Math.Max(Width * 3.5f, 14f);
        float headWidth = Math.Max(Width * 2.5f, 10f);

        // Unit vectors
        float ux = dx / length;
        float uy = dy / length;

        // Normal vectors
        float nx = -uy;
        float ny = ux;

        // Base point of arrow head
        float baseX = End.X - ux * headLength;
        float baseY = End.Y - uy * headLength;

        // Arrow head wing corners
        PointF left = new PointF(baseX + nx * headWidth * 0.5f, baseY + ny * headWidth * 0.5f);
        PointF right = new PointF(baseX - nx * headWidth * 0.5f, baseY - ny * headWidth * 0.5f);

        // Draw main line up to base of arrow head
        g.DrawLine(pen, Start.X, Start.Y, baseX, baseY);

        // Draw filled arrow head
        using Brush brush = new SolidBrush(Color);
        PointF[] headPoints = { End, left, right };
        g.FillPolygon(brush, headPoints);
    }

    public RectangleF GetBounds()
    {
        float minX = Math.Min(Start.X, End.X);
        float minY = Math.Min(Start.Y, End.Y);
        float maxX = Math.Max(Start.X, End.X);
        float maxY = Math.Max(Start.Y, End.Y);
        float pad = Width * 4 + 8;
        return new RectangleF(minX - pad, minY - pad, (maxX - minX) + pad * 2, (maxY - minY) + pad * 2);
    }

    public bool HitTest(PointF p)
    {
        return GetBounds().Contains(p);
    }
}

public class RectangleAnnotation : IAnnotation
{
    public RectangleF Rect { get; set; }
    public Color Color { get; set; }
    public float Width { get; set; }

    public RectangleAnnotation(RectangleF rect, Color color, float width)
    {
        Rect = rect;
        Color = color;
        Width = width;
    }

    public void Draw(Graphics g)
    {
        if (Rect.Width <= 0 || Rect.Height <= 0) return;

        using Pen pen = new Pen(Color, Width)
        {
            Alignment = PenAlignment.Center
        };
        g.DrawRectangle(pen, Rect.X, Rect.Y, Rect.Width, Rect.Height);
    }

    public RectangleF GetBounds()
    {
        float pad = Width + 4;
        return new RectangleF(Rect.X - pad, Rect.Y - pad, Rect.Width + pad * 2, Rect.Height + pad * 2);
    }

    public bool HitTest(PointF p)
    {
        return GetBounds().Contains(p);
    }
}

public class EllipseAnnotation : IAnnotation
{
    public RectangleF Rect { get; set; }
    public Color Color { get; set; }
    public float Width { get; set; }

    public EllipseAnnotation(RectangleF rect, Color color, float width)
    {
        Rect = rect;
        Color = color;
        Width = width;
    }

    public void Draw(Graphics g)
    {
        if (Rect.Width <= 0 || Rect.Height <= 0) return;

        using Pen pen = new Pen(Color, Width);
        g.DrawEllipse(pen, Rect.X, Rect.Y, Rect.Width, Rect.Height);
    }

    public RectangleF GetBounds()
    {
        float pad = Width + 4;
        return new RectangleF(Rect.X - pad, Rect.Y - pad, Rect.Width + pad * 2, Rect.Height + pad * 2);
    }

    public bool HitTest(PointF p)
    {
        return GetBounds().Contains(p);
    }
}

public class TextAnnotation : IAnnotation
{
    public PointF Location { get; set; }
    public string Text { get; set; }
    public Font Font { get; set; }
    public Color Color { get; set; }

    public TextAnnotation(PointF location, string text, Font font, Color color)
    {
        Location = location;
        Text = text;
        Font = font;
        Color = color;
    }

    public void Draw(Graphics g)
    {
        if (string.IsNullOrEmpty(Text)) return;

        SizeF size = g.MeasureString(Text, Font);
        RectangleF bgRect = new RectangleF(Location.X - 4, Location.Y - 2, size.Width + 8, size.Height + 4);

        // Semi-transparent dark background for crisp readability
        using Brush bgBrush = new SolidBrush(Color.FromArgb(190, 20, 20, 24));
        g.FillRectangle(bgBrush, bgRect);

        using Brush textBrush = new SolidBrush(Color);
        g.DrawString(Text, Font, textBrush, Location);
    }

    public RectangleF GetBounds()
    {
        return new RectangleF(Location.X - 4, Location.Y - 2, 200, 40);
    }

    public bool HitTest(PointF p)
    {
        return GetBounds().Contains(p);
    }
}

public class StepBadgeAnnotation : IAnnotation
{
    public PointF Center { get; set; }
    public int Number { get; set; }
    public Color Color { get; set; }
    public float Radius { get; set; } = 14f;

    public StepBadgeAnnotation(PointF center, int number, Color color)
    {
        Center = center;
        Number = number;
        Color = color;
    }

    public void Draw(Graphics g)
    {
        float d = Radius * 2;
        RectangleF circleRect = new RectangleF(Center.X - Radius, Center.Y - Radius, d, d);

        // Outer white ring
        using (Pen whitePen = new Pen(Color.White, 2.2f))
        {
            g.DrawEllipse(whitePen, circleRect);
        }

        // Fill badge
        using (Brush fillBrush = new SolidBrush(Color))
        {
            g.FillEllipse(fillBrush, circleRect);
        }

        // Draw centered number
        string numStr = Number.ToString();
        using Font font = new Font("Segoe UI", Radius > 12 ? 10f : 8.5f, FontStyle.Bold);
        SizeF size = g.MeasureString(numStr, font);

        using Brush textBrush = new SolidBrush(Color.White);
        g.DrawString(numStr, font, textBrush, Center.X - size.Width / 2f, Center.Y - size.Height / 2f);
    }

    public RectangleF GetBounds()
    {
        float pad = Radius + 4;
        return new RectangleF(Center.X - pad, Center.Y - pad, pad * 2, pad * 2);
    }

    public bool HitTest(PointF p)
    {
        float dx = p.X - Center.X;
        float dy = p.Y - Center.Y;
        return (dx * dx + dy * dy) <= Radius * Radius * 1.5f;
    }
}

public class BlurAnnotation : IAnnotation
{
    public Rectangle Rect { get; set; }
    private Bitmap? _cachedMosaic;
    private readonly Bitmap _sourceBitmap;

    public BlurAnnotation(Rectangle rect, Bitmap sourceBitmap)
    {
        Rect = rect;
        _sourceBitmap = sourceBitmap;
        GenerateMosaic();
    }

    public void UpdateRect(Rectangle rect)
    {
        Rect = rect;
        GenerateMosaic();
    }

    private void GenerateMosaic()
    {
        _cachedMosaic?.Dispose();
        _cachedMosaic = null;

        if (Rect.Width <= 0 || Rect.Height <= 0) return;

        Rectangle validRect = Rectangle.Intersect(new Rectangle(0, 0, _sourceBitmap.Width, _sourceBitmap.Height), Rect);
        if (validRect.Width <= 0 || validRect.Height <= 0) return;

        int blockSize = Math.Max(8, Math.Min(validRect.Width, validRect.Height) / 10);
        _cachedMosaic = new Bitmap(validRect.Width, validRect.Height);

        using Graphics mg = Graphics.FromImage(_cachedMosaic);
        for (int y = 0; y < validRect.Height; y += blockSize)
        {
            for (int x = 0; x < validRect.Width; x += blockSize)
            {
                int sampleX = Math.Min(validRect.X + x + blockSize / 2, _sourceBitmap.Width - 1);
                int sampleY = Math.Min(validRect.Y + y + blockSize / 2, _sourceBitmap.Height - 1);
                Color pixelColor = _sourceBitmap.GetPixel(sampleX, sampleY);

                using Brush b = new SolidBrush(pixelColor);
                int bw = Math.Min(blockSize, validRect.Width - x);
                int bh = Math.Min(blockSize, validRect.Height - y);
                mg.FillRectangle(b, x, y, bw, bh);
            }
        }
    }

    public void Draw(Graphics g)
    {
        if (_cachedMosaic != null)
        {
            Rectangle validRect = Rectangle.Intersect(new Rectangle(0, 0, _sourceBitmap.Width, _sourceBitmap.Height), Rect);
            g.DrawImage(_cachedMosaic, validRect.Location);
        }
    }

    public RectangleF GetBounds()
    {
        return Rect;
    }

    public bool HitTest(PointF p)
    {
        return Rect.Contains((int)p.X, (int)p.Y);
    }
}
