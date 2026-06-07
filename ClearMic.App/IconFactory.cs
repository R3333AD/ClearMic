using System.Drawing;
using System.Runtime.InteropServices;

namespace ClearMic.App;

internal static class IconFactory
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateRed()
    {
        return CreateColoredIcon(Color.Red);
    }

    public static Icon CreateGreen()
    {
        return CreateColoredIcon(Color.LimeGreen);
    }

    public static Icon CreateAttenuationIcon(float reductionDb)
    {
        // reductionDb: how many dB of noise reduction (0 = none, 40+ = heavy)
        float intensity = Math.Clamp(MathF.Abs(reductionDb) / 40f, 0f, 1f);

        // Color from yellow (light reduction) → green (moderate) → blue (heavy)
        int r = (int)(255 * (1 - intensity));
        int g = 200;
        int b = (int)(200 * intensity);
        var color = Color.FromArgb(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));

        var icon = CreateColoredIcon(color);
        return icon;
    }

    private static Icon CreateColoredIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(color);
        var hicon = bmp.GetHicon();
        var icon = Icon.FromHandle(hicon);
        return icon;
    }

    public static void FreeIcon(Icon icon)
    {
        if (icon is not null && icon.Handle != IntPtr.Zero)
            DestroyIcon(icon.Handle);
    }
}
