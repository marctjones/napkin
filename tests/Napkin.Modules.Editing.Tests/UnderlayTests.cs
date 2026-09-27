using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>A survey image under the site plan (permit-set §5.4, #224): its size, its placement and its calibration.</summary>
public class UnderlayTests
{
    /// <summary>A PNG's signature and IHDR saying the given size; no pixels, since nothing here decodes.</summary>
    static byte[] Png(int width, int height) =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
        (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
        (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height, 8, 2, 0, 0, 0,
    ];

    /// <summary>A JPEG's start, an APP0 segment, then a baseline start-of-frame saying the given size.</summary>
    static byte[] Jpeg(int width, int height) =>
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xC0, 0, 17, 8,
        (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3, 1, 0x11, 0,
    ];

    [Fact]
    public void An_images_size_is_read_from_its_header()
    {
        Assert.Equal((400L, 300L), ImageSize.Of(Png(400, 300)));
        Assert.Equal((1024L, 768L), ImageSize.Of(Jpeg(1024, 768)));
        Assert.Null(ImageSize.Of(Png(400, 300)[..20]));
        Assert.Null(ImageSize.Of("GIF89a"u8));
        Assert.Null(ImageSize.Of([0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0]));
    }

    [Fact]
    [Trait("Feature", "SITE-001")]
    public void An_image_starts_an_inch_a_pixel_from_the_origin_with_pixels_running_down_and_north_up()
    {
        byte[] bytes = Png(400, 300);
        SurveyUnderlay underlay = Underlays.Default(bytes, "survey.png")!;
        Assert.Equal((ProjectAssets.Hash(bytes), new Pixel(0, 0), new Pixel(400, 0), Point2.Origin, Point2.Inches(400, 0), Length.Inches(400)), (underlay.Asset, underlay.ImageA, underlay.ImageB, underlay.WorldA, underlay.WorldB, underlay.Distance));

        UnderlayPlacement placement = new(underlay);
        Assert.Equal((4096d, -3072d), placement.ToWorld(4, 3));
        Assert.Equal(new Pixel(4, 3), placement.ToPixel(Point2.Inches(4, -3)));
        Assert.Null(Underlays.Default("not an image"u8.ToArray(), "notes.txt"));
    }

    [Fact]
    [Trait("Feature", "SITE-001")]
    public void Two_clicks_and_a_typed_distance_calibrate_it_and_measure_back_that_distance()
    {
        SurveyUnderlay start = Underlays.Default(Png(400, 300), "survey.png")!;

        // Clicked 400" apart along x; typed 100'-0": each pixel is now 3" (1200" over 400 pixels).
        SurveyUnderlay calibrated = Underlays.Calibrate(start, Point2.Origin, Point2.Inches(400, 0), Length.Feet(100))!;
        Assert.Equal((new Pixel(0, 0), new Pixel(400, 0), Point2.Origin, new Point2(Length.Feet(100), Length.Zero)), (calibrated.ImageA, calibrated.ImageB, calibrated.WorldA, calibrated.WorldB));
        UnderlayPlacement placement = new(calibrated);
        Assert.Equal(3072d, placement.Scale, 9);
        (double ax, double ay) = placement.ToWorld(0, 0);
        (double bx, double by) = placement.ToWorld(400, 0);
        Assert.Equal((0d, 0d), (ax, ay));
        Assert.InRange(Math.Sqrt(((bx - ax) * (bx - ax)) + ((by - ay) * (by - ay))) - Length.Feet(100).Units, -1, 1);

        // Clicked along a diagonal: the second world point is the typed distance along it, rounded once.
        SurveyUnderlay turned = Underlays.Calibrate(start, Point2.Origin, Point2.Inches(300, 300), Length.Feet(50))!;
        double expected = Length.Feet(50).Units / Math.Sqrt(2);
        Assert.Equal(new Point2(new Length((long)Math.Round(expected, MidpointRounding.ToEven)), new Length((long)Math.Round(expected, MidpointRounding.ToEven))), turned.WorldB);
        Assert.Equal(new Pixel(300, -300), turned.ImageB);

        Assert.Null(Underlays.Calibrate(start, Point2.Origin, Point2.Origin, Length.Feet(100)));
        Assert.Null(Underlays.Calibrate(start, Point2.Origin, Point2.Inches(400, 0), Length.Zero));
    }

    [Fact]
    public void The_editor_keeps_the_images_bytes_and_undoes_the_underlay_in_one_step()
    {
        DesignEditor editor = new();
        byte[] bytes = Png(400, 300);
        SurveyUnderlay underlay = editor.AddUnderlay(bytes, "survey.png")!;
        Assert.Equal(underlay, editor.Sketch.Site.Underlay);
        Assert.Equal(bytes, editor.Design!.Assets[underlay.Asset]);

        Assert.True(editor.Undo());
        Assert.Null(editor.Sketch.Site.Underlay);
        Assert.Null(editor.AddUnderlay("not an image"u8.ToArray(), "notes.txt"));
        Assert.Null(editor.Sketch.Site.Underlay);
    }
}
