using System.Drawing.Drawing2D;

namespace Librewin;

internal static class TrayArtwork
{
    public static Color RangeColour(Snapshot s) => s.Current.Low || s.Current.Mgdl < s.Low
        ? Color.FromArgb(255, 100, 116) : s.Current.High || s.Current.Mgdl > s.High
        ? Color.FromArgb(255, 190, 65) : Color.FromArgb(88, 230, 160);

    public static Bitmap Draw(int trend, Color colour, string state)
    {
        var bitmap = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var background = new SolidBrush(Color.FromArgb(22, 34, 45));
        g.FillEllipse(background, 1, 1, 62, 62);
        using var brush = new SolidBrush(state == "live" ? colour : Color.Silver);
        using var pen = new Pen(Color.Silver, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (state == "error")
        {
            g.DrawLine(pen, 32, 14, 32, 35); g.FillEllipse(brush, 28, 44, 8, 8);
        }
        else if (state == "stale")
        {
            g.DrawEllipse(pen, 13, 13, 38, 38);
            g.DrawLines(pen, [new Point(32, 21), new Point(32, 32), new Point(41, 37)]);
        }
        else if (state == "unknown" || trend is < 1 or > 7)
        {
            using var font = new Font("Segoe UI", 46, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("?", font, brush, new RectangleF(0, -1, 64, 64), format);
        }
        else
        {
            g.TranslateTransform(32, 32);
            g.RotateTransform(trend switch { 1 or 6 => 90, 2 => 45, 4 => -45, 5 or 7 => -90, _ => 0 });
            g.TranslateTransform(-32, -32);
            // Filled geometry remains bold when Windows scales this to 16–32 pixels.
            if (trend is 6 or 7)
            {
                foreach (int y in new[] { 12, 36 })
                    g.FillPolygon(brush, [new Point(12, y + 6), new Point(36, y + 6), new Point(36, y),
                        new Point(53, y + 10), new Point(36, y + 20), new Point(36, y + 14), new Point(12, y + 14)]);
            }
            else g.FillPolygon(brush, [new Point(10, 26), new Point(34, 26), new Point(34, 12),
                new Point(55, 32), new Point(34, 52), new Point(34, 38), new Point(10, 38)]);
        }
        return bitmap;
    }

    public static void Preview(string path)
    {
        using var sheet = new Bitmap(760, 220);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(240, 242, 245));
        using var font = new Font("Segoe UI", 10);
        string[] labels = ["Low / falling", "In range / steady", "High / rising", "Stale", "Fetch failed", "No trend"];
        for (int i = 0; i < labels.Length; i++)
        {
            using var icon = Draw(i == 0 ? 1 : i == 1 ? 3 : i == 2 ? 4 : 0,
                i == 0 ? Color.FromArgb(255, 100, 116) : i == 2 ? Color.FromArgb(255, 190, 65) : Color.FromArgb(88, 230, 160),
                i == 3 ? "stale" : i == 4 ? "error" : "live");
            int x = 12 + i * 125;
            g.DrawImage(icon, x + 20, 15, 64, 64);
            g.DrawString(labels[i], font, Brushes.Black, x, 88);
            g.DrawImage(icon, x + 40, 117, 16, 16);
            g.FillRectangle(Brushes.Black, x + 28, 151, 40, 40);
            g.DrawImage(icon, x + 40, 163, 16, 16);
        }
        sheet.Save(path);
    }
}
