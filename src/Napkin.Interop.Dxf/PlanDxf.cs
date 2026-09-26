using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;

using CSMath;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

using CadEntity = ACadSharp.Entities.Entity;
using Entity = Napkin.Core.Geometry.Entity;
using Layer = ACadSharp.Tables.Layer;

namespace Napkin.Interop.Dxf;

/// <summary>
/// The plan as a DXF file (#23, DESIGN.md §5.5), for FreeCAD, QCAD or LibreCAD: DXF 2000 (AC1015),
/// written by ACadSharp. Every entity is at napkin's own plan coordinates in inches, Y up (north),
/// on a DXF layer named after its napkin layer.
/// </summary>
/// <remarks>
/// <para>
/// <strong>What is written.</strong> Each box's plan outline as one closed polyline, with a curved
/// cut or a rounded corner as an arc (a polyline bulge); each angled part's plan outline; and each
/// dimension <em>as literal geometry</em> — its dimension line, two extension lines, two
/// arrowheads and its label as plain text, on a layer named <see cref="DimensionLayer"/>. There is
/// no associative <c>DIMENSION</c> entity: another program's idea of a dimension style would
/// change what the label says, and the label must read as napkin's does.
/// </para>
/// <para>
/// <strong>Sizes of the marks.</strong> Text height and arrow length are napkin's own choice, not a
/// standard: a fiftieth of the drawing's larger side, kept between 1/4″ and 12″, so a label reads
/// at a whole-drawing zoom whether the drawing is a stool or a house.
/// </para>
/// <para>
/// <strong>Not written.</strong> The R12 fallback DESIGN.md §5.5 asks for: ACadSharp reads R12
/// (AC1009) but does not write it, and a hand-written R12 writer is what the design says not to
/// build. Hidden lines, the 3D model and the standard views are not written either; this is the plan.
/// </para>
/// </remarks>
public static class PlanDxf
{
    /// <summary>The DXF layer the dimensions are drawn on.</summary>
    public const string DimensionLayer = "Dimensions";

    /// <summary>The DXF version written: DXF 2000.</summary>
    public const ACadVersion Version = ACadVersion.AC1015;

    /// <summary>Writes the plan of a sketch to a stream as ASCII DXF.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="format">How the dimension labels are written, as the canvas writes them.</param>
    /// <param name="stream">Where to write it; left open.</param>
    public static void Write(Sketch sketch, LengthFormat format, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        CadDocument document = Document(sketch, format);

        // The writer closes the stream it is given; a MemoryStream's bytes outlive that, and the
        // caller's stream stays open for them.
        MemoryStream buffer = new();
        using (DxfWriter writer = new(buffer, document, binary: false))
        {
            writer.Write();
        }

        stream.Write(buffer.ToArray());
    }

    /// <summary>The plan of a sketch as a DXF document, before it is written.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="format">How the dimension labels are written.</param>
    public static CadDocument Document(Sketch sketch, LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(format);

        CadDocument document = new(Version);
        document.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Inches;
        Dictionary<LayerId, Layer> layers = [];
        Layer LayerFor(LayerId id)
        {
            if (!layers.TryGetValue(id, out Layer? layer))
            {
                string name = sketch.Layers.FirstOrDefault(candidate => candidate.Id == id)?.Name ?? "0";
                layer = document.Layers.TryGetValue(name, out Layer? existing) ? existing : new Layer(name);
                if (!document.Layers.Contains(name))
                {
                    document.Layers.Add(layer);
                }

                layers[id] = layer;
            }

            return layer;
        }

        List<(double X, double Y)> extent = [];
        foreach (Entity entity in sketch.Entities.Values.OrderBy(entity => entity.Id))
        {
            switch (entity)
            {
                case Box box:
                {
                    LwPolyline outline = Polyline(PlanShape.Outline(box));
                    outline.Layer = LayerFor(box.Layer);
                    document.Entities.Add(outline);
                    extent.AddRange(outline.Vertices.Select(vertex => (vertex.Location.X, vertex.Location.Y)));
                    break;
                }

                case Strut strut:
                {
                    var corners = StrutSolid.PlanOutline(strut);
                    LwPolyline outline = new(corners.Select(corner => (IVector)new XY(corner.X, corner.Y))) { IsClosed = true };
                    outline.Layer = LayerFor(strut.Layer);
                    document.Entities.Add(outline);
                    extent.AddRange(corners);
                    break;
                }
            }
        }

        List<DimensionMeasurement> dimensions = [.. PlanDimensions.Measure(sketch)];
        if (dimensions.Count > 0)
        {
            // A napkin layer may already be called Dimensions; its outlines and the dimensions share it.
            if (!document.Layers.TryGetValue(DimensionLayer, out Layer? layer))
            {
                layer = new Layer(DimensionLayer);
                document.Layers.Add(layer);
            }

            foreach (DimensionMeasurement dimension in dimensions)
            {
                extent.AddRange([Xy(dimension.LineFrom), Xy(dimension.LineTo)]);
            }

            double size = MarkSize(extent);
            foreach (DimensionMeasurement dimension in dimensions)
            {
                foreach (CadEntity mark in DimensionMarks(dimension, format, size))
                {
                    mark.Layer = layer;
                    document.Entities.Add(mark);
                }
            }
        }

        return document;
    }

    /// <summary>A fiftieth of the drawing's larger side, between 1/4″ and 12″: napkin's choice (see remarks).</summary>
    internal static double MarkSize(IReadOnlyCollection<(double X, double Y)> points)
    {
        if (points.Count == 0)
        {
            return 0.25;
        }

        double width = points.Max(point => point.X) - points.Min(point => point.X);
        double height = points.Max(point => point.Y) - points.Min(point => point.Y);
        return Math.Clamp(Math.Max(width, height) / 50, 0.25, 12);
    }

    /// <summary>
    /// A dimension's marks: the dimension line, the two extension lines from what is measured to it,
    /// an arrowhead at each end (two strokes each), and the label centred above the line.
    /// </summary>
    static IEnumerable<CadEntity> DimensionMarks(DimensionMeasurement dimension, LengthFormat format, double size)
    {
        (double X, double Y) from = Xy(dimension.From), to = Xy(dimension.To);
        (double X, double Y) a = Xy(dimension.LineFrom), b = Xy(dimension.LineTo);
        yield return Line(a, b);
        yield return Line(from, a);
        yield return Line(to, b);

        double length = Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));
        if (length > 0)
        {
            (double X, double Y) along = ((b.X - a.X) / length, (b.Y - a.Y) / length);
            (double X, double Y) across = (-along.Y, along.X);
            double back = size * 0.8, spread = size * 0.25;
            foreach (((double X, double Y) tip, double sign) in new[] { (a, 1.0), (b, -1.0) })
            {
                (double X, double Y) root = (tip.X + (sign * along.X * back), tip.Y + (sign * along.Y * back));
                yield return Line(tip, (root.X + (across.X * spread), root.Y + (across.Y * spread)));
                yield return Line(tip, (root.X - (across.X * spread), root.Y - (across.Y * spread)));
            }
        }

        // The label sits just off the line on its outer side, reading along it, as on the canvas.
        (double X, double Y) middle = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        bool vertical = dimension.Axis == Axis.Y;
        yield return new TextEntity(dimension.Label(format))
        {
            Height = size,
            InsertPoint = new XYZ(middle.X, middle.Y, 0),
            AlignmentPoint = new XYZ(middle.X + (vertical ? -size * 0.4 : 0), middle.Y + (vertical ? 0 : size * 0.4), 0),
            HorizontalAlignment = TextHorizontalAlignment.Center,
            VerticalAlignment = TextVerticalAlignmentType.Bottom,
            Rotation = vertical ? Math.PI / 2 : 0,
        };
    }

    static Line Line((double X, double Y) from, (double X, double Y) to) => new(new XYZ(from.X, from.Y, 0), new XYZ(to.X, to.Y, 0));

    static (double X, double Y) Xy(Point2 point) => (point.X.ToInches(), point.Y.ToInches());

    /// <summary>
    /// A closed outline as one polyline: a vertex at each segment's start, and on an arc's start the
    /// bulge DXF uses for an arc (the tangent of a quarter of its signed sweep).
    /// </summary>
    internal static LwPolyline Polyline(Outline outline)
    {
        List<LwPolyline.Vertex> vertices = [];
        foreach (OutlineSegment segment in outline.Segments)
        {
            (double X, double Y) start = Xy(segment.From);
            vertices.Add(new LwPolyline.Vertex(new XY(start.X, start.Y)) { Bulge = Math.Tan(Sweep(segment) / 4) });
        }

        return new LwPolyline(vertices) { IsClosed = true };
    }

    /// <summary>The signed angle a segment turns through, counter-clockwise positive; zero for a straight one.</summary>
    internal static double Sweep(OutlineSegment segment)
    {
        switch (segment)
        {
            case ArcByCenter arc:
            {
                // A rounded corner: the shorter way round its centre.
                (double X, double Y) c = Xy(arc.Center), f = Xy(arc.From), t = Xy(arc.To);
                (double X, double Y) u = (f.X - c.X, f.Y - c.Y), v = (t.X - c.X, t.Y - c.Y);
                return Math.Atan2((u.X * v.Y) - (u.Y * v.X), (u.X * v.X) + (u.Y * v.Y));
            }

            case ArcThrough arc:
            {
                // Through three points: the way round that passes the middle one.
                (double X, double Y) p = Xy(arc.From), m = Xy(arc.Through), q = Xy(arc.To);
                double d = 2 * ((p.X * (m.Y - q.Y)) + (m.X * (q.Y - p.Y)) + (q.X * (p.Y - m.Y)));
                double pp = (p.X * p.X) + (p.Y * p.Y), mm = (m.X * m.X) + (m.Y * m.Y), qq = (q.X * q.X) + (q.Y * q.Y);
                (double X, double Y) c = (
                    ((pp * (m.Y - q.Y)) + (mm * (q.Y - p.Y)) + (qq * (p.Y - m.Y))) / d,
                    ((pp * (q.X - m.X)) + (mm * (p.X - q.X)) + (qq * (m.X - p.X))) / d);
                double Angle((double X, double Y) point) => Math.Atan2(point.Y - c.Y, point.X - c.X);
                double Ccw(double from, double to) => ((to - from) % (2 * Math.PI) + (2 * Math.PI)) % (2 * Math.PI);
                double toEnd = Ccw(Angle(p), Angle(q)), toMiddle = Ccw(Angle(p), Angle(m));
                return toMiddle <= toEnd ? toEnd : toEnd - (2 * Math.PI);
            }

            default:
                return 0;
        }
    }
}
