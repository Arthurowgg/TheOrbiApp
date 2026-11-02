using System.Drawing;
using System.Drawing.Drawing2D;

namespace Orbi;

/// <summary>Desenha o ícone da bandeja em código (nada de arquivo solto no build).</summary>
public static class TrayIcon
{
    public static Icon Load()
    {
        try
        {
            using var bitmap = Draw(32);
            var handle = bitmap.GetHicon();
            return Icon.FromHandle(handle);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public static Bitmap Draw(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var ink = Color.FromArgb(0x14, 0x10, 0x0C);
        var signal = Color.FromArgb(0xC7, 0x45, 0x2A);
        var bone = Color.FromArgb(0xF2, 0xED, 0xE3);

        var scale = size / 32f;
        using (var background = new SolidBrush(ink))
        {
            graphics.FillRectangle(background, 0, 0, size, size);
        }

        // órbita
        using (var pen = new Pen(bone, Math.Max(1.4f, 1.6f * scale)))
        {
            var rect = new RectangleF(6 * scale, 6 * scale, 20 * scale, 20 * scale);
            graphics.DrawEllipse(pen, rect);
        }

        // núcleo
        using (var core = new SolidBrush(signal))
        {
            var d = 9 * scale;
            graphics.FillEllipse(core, (size - d) / 2, (size - d) / 2, d, d);
        }

        // satélite
        using (var satellite = new SolidBrush(bone))
        {
            var d = 4.2f * scale;
            graphics.FillEllipse(satellite, 21.5f * scale, 6.4f * scale, d, d);
        }

        return bitmap;
    }
}
