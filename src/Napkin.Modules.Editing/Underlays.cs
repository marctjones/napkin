using Napkin.Core.Geometry;
using Napkin.Core.Project;

namespace Napkin.Modules.Editing;

/// <summary>An image's size in pixels, read from its header without decoding it: PNG's IHDR, or a JPEG's start-of-frame.</summary>
public static class ImageSize
{
    /// <summary>The width and height, or null when the bytes are not a PNG or JPEG napkin can read the size of.</summary>
    public static (long Width, long Height)? Of(ReadOnlySpan<byte> bytes)
    {
        string? extension = ProjectAssets.Extension(bytes);
        if (extension == ".png")
        {
            // The IHDR chunk follows the 8-byte signature: length (4), "IHDR" (4), width (4), height (4), big-endian.
            return bytes.Length >= 24 ? (BigEndian(bytes.Slice(16, 4)), BigEndian(bytes.Slice(20, 4))) : null;
        }

        if (extension != ".jpg")
        {
            return null;
        }

        // Walk the markers to a start-of-frame (SOF0–SOF15 but DHT, JPG and DAC): height and width follow its precision byte.
        int at = 2;
        while (at + 9 < bytes.Length && bytes[at] == 0xFF)
        {
            byte marker = bytes[at + 1];
            int length = (bytes[at + 2] << 8) | bytes[at + 3];
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                return (BigEndian(bytes.Slice(at + 7, 2)), BigEndian(bytes.Slice(at + 5, 2)));
            }

            at += 2 + length;
        }

        return null;
    }

    static long BigEndian(ReadOnlySpan<byte> bytes)
    {
        long value = 0;
        foreach (byte b in bytes)
        {
            value = (value << 8) | b;
        }

        return value;
    }
}

/// <summary>
/// Where an underlay's image lies on the drawing (docs/design/permit-set.md §5.4), for drawing only, in
/// double. Pixels run x right and y down; the drawing runs x east and y north; so a pixel offset is
/// flipped in y, then scaled and turned. A two-point calibration fixes scale and rotation only, and the
/// flip is part of the convention, never a guess: the survey never draws mirrored.
/// </summary>
/// <param name="Underlay">The underlay.</param>
public sealed record UnderlayPlacement(SurveyUnderlay Underlay)
{
    /// <summary>Drawing units (1/1024″) per pixel: the typed distance over the pixel distance between the two points.</summary>
    public double Scale
    {
        get
        {
            double px = Underlay.ImageB.X - Underlay.ImageA.X, py = Underlay.ImageB.Y - Underlay.ImageA.Y;
            return Underlay.Distance.Units / Math.Sqrt((px * px) + (py * py));
        }
    }

    /// <summary>The turn, radians anticlockwise, from the flipped image to the drawing.</summary>
    public double Rotation
    {
        get
        {
            double image = Math.Atan2(-(Underlay.ImageB.Y - Underlay.ImageA.Y), Underlay.ImageB.X - Underlay.ImageA.X);
            double world = Math.Atan2(Underlay.WorldB.Y.Units - Underlay.WorldA.Y.Units, Underlay.WorldB.X.Units - Underlay.WorldA.X.Units);
            return world - image;
        }
    }

    /// <summary>Where a point on the image is on the drawing, in drawing units.</summary>
    public (double X, double Y) ToWorld(double pixelX, double pixelY)
    {
        double dx = pixelX - Underlay.ImageA.X, dy = -(pixelY - Underlay.ImageA.Y);
        double cos = Math.Cos(Rotation), sin = Math.Sin(Rotation), s = Scale;
        return (Underlay.WorldA.X.Units + (s * ((dx * cos) - (dy * sin))), Underlay.WorldA.Y.Units + (s * ((dx * sin) + (dy * cos))));
    }

    /// <summary>The whole pixel a point on the drawing falls on.</summary>
    public Pixel ToPixel(Point2 world)
    {
        double dx = world.X.Units - Underlay.WorldA.X.Units, dy = world.Y.Units - Underlay.WorldA.Y.Units;
        double cos = Math.Cos(-Rotation), sin = Math.Sin(-Rotation), s = Scale;
        double ix = ((dx * cos) - (dy * sin)) / s, iy = ((dx * sin) + (dy * cos)) / s;
        return new Pixel((long)Math.Round(Underlay.ImageA.X + ix, MidpointRounding.ToEven), (long)Math.Round(Underlay.ImageA.Y - iy, MidpointRounding.ToEven));
    }
}

/// <summary>Bringing a survey image in and calibrating it (§5.4): the arithmetic the Site plan window runs.</summary>
public static class Underlays
{
    /// <summary>
    /// The calibration an image starts with: its top-left pixel on the origin and its top-right pixel a
    /// width of inches east, one inch a pixel, until the person calibrates it.
    /// </summary>
    public static SurveyUnderlay? Default(byte[] bytes, string name)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (ImageSize.Of(bytes) is not { Width: > 0 } size)
        {
            return null;
        }

        Length width = Length.Inches(size.Width);
        return new SurveyUnderlay(ProjectAssets.Hash(bytes), new Pixel(0, 0), new Pixel(size.Width, 0), Point2.Origin, new Point2(width, Length.Zero), width, name);
    }

    /// <summary>
    /// A calibration from two clicks on the drawing and a typed distance: each click is read back to its
    /// pixel through the placement the image has now; the second world point is the typed distance from
    /// the first, along the direction the person clicked, rounded once.
    /// </summary>
    public static SurveyUnderlay? Calibrate(SurveyUnderlay underlay, Point2 first, Point2 second, Length distance)
    {
        ArgumentNullException.ThrowIfNull(underlay);
        UnderlayPlacement placement = new(underlay);
        double dx = second.X.Units - first.X.Units, dy = second.Y.Units - first.Y.Units, length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length == 0 || distance <= Length.Zero)
        {
            return null;
        }

        Point2 along = new(
            new Length((long)Math.Round(first.X.Units + (dx / length * distance.Units), MidpointRounding.ToEven)),
            new Length((long)Math.Round(first.Y.Units + (dy / length * distance.Units), MidpointRounding.ToEven)));
        SurveyUnderlay calibrated = underlay with { ImageA = placement.ToPixel(first), ImageB = placement.ToPixel(second), WorldA = first, WorldB = along, Distance = distance };
        return SurveyUnderlayRules.Refusal(calibrated) is null ? calibrated : null;
    }
}
