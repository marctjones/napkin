using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using Napkin.App.GuiTests.Harness;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>Reads pixels back off the rendered frame, for workflows that assert what is actually drawn.</summary>
internal static class FrameSampling
{
    /// <summary>The colours of a rectangle of the window's last rendered frame, row by row.</summary>
    internal static List<Color> Patch(AppDriver app, int x, int y, int width, int height)
    {
        Bitmap frame = app.CaptureFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
        byte[] pixels = new byte[width * height * 4];
        GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(new PixelRect(x, y, width, height), pin.AddrOfPinnedObject(), pixels.Length, width * 4);
        }
        finally
        {
            pin.Free();
        }

        // Four bytes a pixel, in whichever order this platform's bitmaps keep them.
        bool bgra = frame.Format == Avalonia.Platform.PixelFormat.Bgra8888;
        List<Color> colors = [];
        for (int i = 0; i < width * height; i++)
        {
            byte c0 = pixels[i * 4], g = pixels[(i * 4) + 1], c2 = pixels[(i * 4) + 2], a = pixels[(i * 4) + 3];
            colors.Add(bgra ? Color.FromArgb(a, c2, g, c0) : Color.FromArgb(a, c0, g, c2));
        }

        return colors;
    }
}
