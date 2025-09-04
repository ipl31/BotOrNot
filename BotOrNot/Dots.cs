using System.Drawing.Drawing2D;


namespace BotOrNot;

public class Dots
{
    private static Bitmap Get(Color fill, Color border, int size = 16)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var rect = new Rectangle(1, 1, size - 3, size - 3);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, 1.5f);

        g.FillEllipse(brush, rect);
        g.DrawEllipse(pen, rect);
        return bmp;
    }

    public static Bitmap Green(int size = 16)
    {
        return Get(Color.Green, Color.DarkGreen, size);
    }

    public static Bitmap Red(int size = 16)
    {
        return Get(Color.Red, Color.DarkRed, size);
    }
}