using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project;

/// <summary>
/// Writes a <see cref="Sketch"/> as a napkin scene document: the mirror image of
/// <see cref="SceneReader"/>, through the same <see cref="SceneNames"/> table.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The output is deterministic.</strong> The same sketch produces the same bytes, on every
/// machine and in every process: fields are written in a fixed order, entities in
/// <see cref="EntityId"/> order and relationships in <see cref="RelationshipId"/> order, lengths
/// and angles as integers in the file's stored units, and nothing that is not in the sketch — no
/// timestamp, no machine name, no counter — is written at all. Lines end in <c>\n</c> on every
/// platform. That is what makes a saved project diff cleanly in git and makes "save twice, compare
/// the bytes" a test rather than a hope.
/// </para>
/// <para>
/// <strong>The scene body is unchanged from M1.</strong> The document still carries its own
/// <see cref="FormatStamp"/> at the top; the M2 container adds a second stamp of its own in
/// <c>manifest.json</c> for the container layout, and does not move this one (see
/// <c>docs/file-format.md</c>).
/// </para>
/// <para>
/// The writer does not check the sketch. <see cref="ProjectFile"/> runs
/// <see cref="Sketch.Validate"/> before saving; this type turns whatever it is given into the
/// document that says it, so that a test can write a scene the reader will refuse and see that it
/// is refused for the right reason.
/// </para>
/// </remarks>
public static class SceneWriter
{
    /// <summary>The format version this build writes, and the only one it reads.</summary>
    public static int FormatVersion => FormatStamp.CurrentVersion;

    /// <summary>The scene document for a sketch, as UTF-8 bytes.</summary>
    /// <param name="sketch">The sketch to write.</param>
    public static byte[] WriteToBytes(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        using MemoryStream stream = new();
        Write(stream, sketch);
        return stream.ToArray();
    }

    /// <summary>The scene document for a sketch, as text, for a test or a diff.</summary>
    /// <param name="sketch">The sketch to write.</param>
    public static string WriteToText(Sketch sketch)
        => Encoding.UTF8.GetString(WriteToBytes(sketch));

    /// <summary>Writes the scene document for a sketch to a stream, in UTF-8.</summary>
    /// <param name="stream">Where the bytes go. Not closed by this call.</param>
    /// <param name="sketch">The sketch to write.</param>
    public static void Write(Stream stream, Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(sketch);

        using (Utf8JsonWriter writer = new(stream, JsonLayout.Options))
        {
            WriteDocument(writer, sketch);
            writer.Flush();
        }

        // A file that ends in a newline is one a text tool, a diff and a person all expect. The
        // reader passes over it: trailing whitespace is not part of the document.
        stream.WriteByte((byte)'\n');
    }

    private static void WriteDocument(Utf8JsonWriter writer, Sketch sketch)
    {
        writer.WriteStartObject();

        writer.WriteNumber(SceneNames.FormatVersion, FormatStamp.CurrentVersion);

        writer.WriteStartObject(SceneNames.Units);
        writer.WriteString(SceneNames.UnitsLength, FormatStamp.InchGrid);
        writer.WriteString(SceneNames.UnitsAngle, FormatStamp.Arcsecond);
        writer.WriteEndObject();

        writer.WriteStartArray(SceneNames.Layers);
        foreach (Layer layer in sketch.Layers)
        {
            writer.WriteStartObject();
            WriteId(writer, SceneNames.Id, layer.Id.Value);
            writer.WriteString(SceneNames.Name, layer.Name);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        // Entities in id order and relationships in id order. The sketch holds both in a
        // dictionary, whose enumeration order is an implementation detail; sorting is what makes
        // the file a function of the sketch's value (geometry design §4.4 step 3).
        writer.WriteStartArray(SceneNames.Entities);
        foreach (Entity entity in sketch.Entities.Values.OrderBy(entity => entity.Id))
        {
            WriteEntity(writer, entity);
        }

        writer.WriteEndArray();

        writer.WriteStartArray(SceneNames.Relationships);
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            WriteRelationship(writer, relationship);
        }

        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    // ---------------------------------------------------------------------------------------
    // Entities
    // ---------------------------------------------------------------------------------------

    private static void WriteEntity(Utf8JsonWriter writer, Entity entity)
    {
        writer.WriteStartObject();
        WriteId(writer, SceneNames.Id, entity.Id.Value);
        writer.WriteString(SceneNames.Type, TypeOf(entity));
        WriteId(writer, SceneNames.Layer, entity.Layer.Value);
        writer.WriteString(SceneNames.Name, entity.Name);

        switch (entity)
        {
            case Node node:
                WritePoint(writer, SceneNames.Position, node.Position);
                break;

            case Segment segment:
                WriteId(writer, SceneNames.Start, segment.Start.Value);
                WriteId(writer, SceneNames.End, segment.End.Value);
                break;

            case Box box:
                if (Unspellable(box) is { } why)
                {
                    throw new NotSupportedException(why);
                }

                WritePoint(writer, SceneNames.Anchor, box.Anchor.XY);
                writer.WriteNumber(SceneNames.Width, box.Width.Units);
                writer.WriteNumber(SceneNames.Height, box.Height.Units);
                writer.WriteNumber(SceneNames.Rotation, box.Rotation.Arcseconds);
                WritePart(writer, box.Part, box.Depth);
                WriteCuts(writer, box.Cuts);
                break;

            case Dimension dimension:
                WriteMeasurand(writer, dimension.Measures);

                // Written even when there is nothing to say, because the format has no optional
                // fields: a reference dimension says "drives": null (docs/file-format.md).
                if (dimension.Drives is { } drives)
                {
                    WriteId(writer, SceneNames.Drives, drives.Value);
                }
                else
                {
                    writer.WriteNull(SceneNames.Drives);
                }

                writer.WriteStartObject(SceneNames.Placement);
                writer.WriteNumber(SceneNames.Offset, dimension.Placement.Offset.Units);
                writer.WriteString(SceneNames.Side, SceneNames.Of(dimension.Placement.Side));
                writer.WriteEndObject();
                break;

            default:
                throw Unwritable(entity);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Why this box cannot be written in format version 3, or <see langword="null"/> when it can.
    /// </summary>
    /// <remarks>
    /// Version 3 is a plan format: a box in it lies as drawn at the plan datum, and its depth is
    /// its part's out-of-plane dimension or, for a box that is not a part, the rectangle tool's
    /// default. A box in space that is anything else would be written as a different box and read
    /// back as that, so it is refused rather than quietly flattened. docs/design/assembly-model.md
    /// §10 step 5 gives the file the fields to say it.
    /// </remarks>
    internal static string? Unspellable(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (box.FaceUp != BoxFace.Top)
        {
            return $"Box {box.Id} is turned {box.FaceUp} up, and this file format can only store a box lying as drawn.";
        }

        if (box.Anchor.Z != Length.Zero)
        {
            return $"Box {box.Id} is {box.Anchor.Z} above the plan, and this file format can only store a box on the plan.";
        }

        if (box.Part is null && box.Depth != Box.DefaultDepth)
        {
            return $"Box {box.Id} is {box.Depth} deep and is not a part, and this file format can only store "
                   + $"the depth of a part (a plain box is read back {Box.DefaultDepth} deep).";
        }

        return null;
    }

    /// <summary>
    /// A box's part, or <c>"part": null</c> for a box that is not a piece anybody cuts. Written
    /// even when there is nothing to say, because the format has no optional fields. The part's
    /// out-of-plane dimension is the box's depth.
    /// </summary>
    private static void WritePart(Utf8JsonWriter writer, Part? part, Length depth)
    {
        if (part is null)
        {
            writer.WriteNull(SceneNames.Part);
            return;
        }

        writer.WriteStartObject(SceneNames.Part);

        if (part.Stock is { } stock)
        {
            writer.WriteString(SceneNames.Stock, stock);
        }
        else
        {
            writer.WriteNull(SceneNames.Stock);
        }

        if (part.Species is { } species)
        {
            writer.WriteString(SceneNames.Species, species);
        }
        else
        {
            writer.WriteNull(SceneNames.Species);
        }

        writer.WriteNumber(SceneNames.Quantity, part.Quantity);
        writer.WriteNumber(SceneNames.OutOfPlane, depth.Units);

        writer.WriteStartObject(SceneNames.PlanAxes);
        writer.WriteString(SceneNames.X, SceneNames.Of(part.PlanAxes.X));
        writer.WriteString(SceneNames.Y, SceneNames.Of(part.PlanAxes.Y));
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    /// <summary>
    /// What has been cut off a box's blank, in site order — <c>"cuts": []</c> for a plain
    /// rectangle, written for the same reason <c>"part": null</c> is
    /// (<c>docs/design/shaped-parts-model.md</c> §5).
    /// </summary>
    /// <remarks>
    /// <see cref="Box.Cuts"/> is already in site order by construction, so the array is written in
    /// the order the list holds and the reader refuses a file whose order differs. That is what
    /// keeps a file one spelling of one shape.
    /// </remarks>
    private static void WriteCuts(Utf8JsonWriter writer, ImmutableList<Cut> cuts)
    {
        writer.WriteStartArray(SceneNames.Cuts);

        foreach (Cut cut in cuts)
        {
            writer.WriteStartObject();

            switch (cut)
            {
                case CornerCut corner:
                    writer.WriteString(SceneNames.Kind, SceneNames.CornerCut);
                    writer.WriteString(SceneNames.Corner, SceneNames.Of(corner.Corner));
                    writer.WriteNumber(SceneNames.AlongX, corner.AlongX.Units);
                    writer.WriteNumber(SceneNames.AlongY, corner.AlongY.Units);
                    break;

                case RoundedCorner rounded:
                    writer.WriteString(SceneNames.Kind, SceneNames.RoundedCorner);
                    writer.WriteString(SceneNames.Corner, SceneNames.Of(rounded.Corner));
                    writer.WriteNumber(SceneNames.CutRadius, rounded.Radius.Units);
                    break;

                case CurvedEdge curve:
                    writer.WriteString(SceneNames.Kind, SceneNames.CurvedEdge);
                    writer.WriteString(SceneNames.Edge, SceneNames.Of(curve.Edge));
                    writer.WriteString(SceneNames.Bow, SceneNames.Of(curve.Bow));
                    writer.WriteNumber(SceneNames.Depth, curve.Depth.Units);
                    break;

                default:
                    throw Unwritable(cut);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string TypeOf(Entity entity) => entity switch
    {
        Node => SceneNames.Node,
        Segment => SceneNames.Segment,
        Box => SceneNames.Box,
        Dimension => SceneNames.Dimension,
        _ => throw Unwritable(entity),
    };

    // ---------------------------------------------------------------------------------------
    // Relationships
    // ---------------------------------------------------------------------------------------

    private static void WriteRelationship(Utf8JsonWriter writer, Relationship relationship)
    {
        writer.WriteStartObject();
        WriteId(writer, SceneNames.Id, relationship.Id.Value);

        switch (relationship)
        {
            case Anchored anchored:
                writer.WriteString(SceneNames.Kind, SceneNames.Anchored);
                WriteId(writer, SceneNames.Entity, anchored.Entity.Value);
                break;

            case Coincident coincident:
                writer.WriteString(SceneNames.Kind, SceneNames.Coincident);
                WritePointRef(writer, SceneNames.A, coincident.A);
                WritePointRef(writer, SceneNames.B, coincident.B);
                break;

            case Horizontal horizontal:
                writer.WriteString(SceneNames.Kind, SceneNames.Horizontal);
                WriteEdgeRef(writer, SceneNames.Edge, horizontal.Edge);
                break;

            case Vertical vertical:
                writer.WriteString(SceneNames.Kind, SceneNames.Vertical);
                WriteEdgeRef(writer, SceneNames.Edge, vertical.Edge);
                break;

            case Flush flush:
                writer.WriteString(SceneNames.Kind, SceneNames.Flush);
                WriteEdgeRef(writer, SceneNames.A, flush.A);
                WriteEdgeRef(writer, SceneNames.B, flush.B);
                break;

            case AxisDistance axisDistance:
                writer.WriteString(SceneNames.Kind, SceneNames.AxisDistance);
                WritePointRef(writer, SceneNames.From, axisDistance.From);
                WritePointRef(writer, SceneNames.To, axisDistance.To);
                writer.WriteString(SceneNames.Axis, SceneNames.Of(axisDistance.Axis));
                writer.WriteNumber(SceneNames.Distance, axisDistance.Distance.Units);
                break;

            case ParamValue paramValue:
                writer.WriteString(SceneNames.Kind, SceneNames.ParamValue);
                WriteParamRef(writer, SceneNames.Param, paramValue.Param);
                writer.WriteNumber(SceneNames.Value, paramValue.Value.Units);
                break;

            case EqualParam equalParam:
                writer.WriteString(SceneNames.Kind, SceneNames.EqualParam);
                WriteParamRef(writer, SceneNames.A, equalParam.A);
                WriteParamRef(writer, SceneNames.B, equalParam.B);
                break;

            case Centered centered:
                writer.WriteString(SceneNames.Kind, SceneNames.Centered);
                WritePointRef(writer, SceneNames.Middle, centered.Middle);
                WritePointRef(writer, SceneNames.A, centered.A);
                WritePointRef(writer, SceneNames.B, centered.B);
                writer.WriteString(SceneNames.Axis, SceneNames.Of(centered.Axis));
                break;

            case Geometry.Parallel parallel:
                writer.WriteString(SceneNames.Kind, SceneNames.Parallel);
                WriteEdgeRef(writer, SceneNames.A, parallel.A);
                WriteEdgeRef(writer, SceneNames.B, parallel.B);
                break;

            case Perpendicular perpendicular:
                writer.WriteString(SceneNames.Kind, SceneNames.Perpendicular);
                WriteEdgeRef(writer, SceneNames.A, perpendicular.A);
                WriteEdgeRef(writer, SceneNames.B, perpendicular.B);
                break;

            case AngleBetween angleBetween:
                writer.WriteString(SceneNames.Kind, SceneNames.AngleBetween);
                WriteEdgeRef(writer, SceneNames.A, angleBetween.A);
                WriteEdgeRef(writer, SceneNames.B, angleBetween.B);
                writer.WriteNumber(SceneNames.Angle, angleBetween.Angle.Arcseconds);
                break;

            case Distance distance:
                writer.WriteString(SceneNames.Kind, SceneNames.Distance);
                WritePointRef(writer, SceneNames.A, distance.A);
                WritePointRef(writer, SceneNames.B, distance.B);
                writer.WriteNumber(SceneNames.Value, distance.Value.Units);
                break;

            case PointOnEdge pointOnEdge:
                writer.WriteString(SceneNames.Kind, SceneNames.PointOnEdge);
                WritePointRef(writer, SceneNames.Point, pointOnEdge.Point);
                WriteEdgeRef(writer, SceneNames.Edge, pointOnEdge.Edge);
                break;

            case Symmetric symmetric:
                writer.WriteString(SceneNames.Kind, SceneNames.Symmetric);
                WritePointRef(writer, SceneNames.A, symmetric.A);
                WritePointRef(writer, SceneNames.B, symmetric.B);
                WriteEdgeRef(writer, SceneNames.Mirror, symmetric.Mirror);
                break;

            case Tangent tangent:
                writer.WriteString(SceneNames.Kind, SceneNames.Tangent);
                WriteEdgeRef(writer, SceneNames.A, tangent.A);
                WriteEdgeRef(writer, SceneNames.B, tangent.B);
                break;

            case Radius radius:
                writer.WriteString(SceneNames.Kind, SceneNames.Radius);
                WriteId(writer, SceneNames.Arc, radius.Arc.Value);
                writer.WriteNumber(SceneNames.Value, radius.Value.Units);
                break;

            default:
                throw Unwritable(relationship);
        }

        writer.WriteEndObject();
    }

    // ---------------------------------------------------------------------------------------
    // References
    // ---------------------------------------------------------------------------------------

    // Version 3 has point slots and edge slots, and names a box's place by a plan corner or a plan
    // edge of a box lying as drawn: a local upright and a side face. Any other feature — a vertex, a
    // top or bottom face — has no version-3 spelling until docs/design/assembly-model.md §10 step 5
    // gives the file the feature reference, and is refused rather than saved as something else.
    private static void WritePointRef(Utf8JsonWriter writer, string name, PlaceRef reference)
    {
        writer.WriteStartObject(name);

        switch (reference)
        {
            case NodeRef node:
                writer.WriteString(SceneNames.Kind, SceneNames.Node);
                WriteId(writer, SceneNames.Node, node.Node.Value);
                break;

            case FeatureRef feature when SceneNames.TryCornerOf(feature.Feature, out BoxCorner corner):
                writer.WriteString(SceneNames.Kind, SceneNames.Corner);
                WriteId(writer, SceneNames.Box, feature.Box.Value);
                writer.WriteString(SceneNames.Corner, SceneNames.Of(corner));
                break;

            case CenterRef center:
                writer.WriteString(SceneNames.Kind, SceneNames.Center);
                WriteId(writer, SceneNames.Box, center.Box.Value);
                break;

            default:
                throw Unwritable(reference);
        }

        writer.WriteEndObject();
    }

    private static void WriteEdgeRef(Utf8JsonWriter writer, string name, PlaceRef reference)
    {
        writer.WriteStartObject(name);

        switch (reference)
        {
            case SegmentRef segment:
                writer.WriteString(SceneNames.Kind, SceneNames.Segment);
                WriteId(writer, SceneNames.Segment, segment.Segment.Value);
                break;

            case FeatureRef feature when SceneNames.TryEdgeOf(feature.Feature, out BoxEdge edge):
                writer.WriteString(SceneNames.Kind, SceneNames.BoxEdge);
                WriteId(writer, SceneNames.Box, feature.Box.Value);
                writer.WriteString(SceneNames.Edge, SceneNames.Of(edge));
                break;

            default:
                throw Unwritable(reference);
        }

        writer.WriteEndObject();
    }

    private static void WriteParamRef(Utf8JsonWriter writer, string name, ParamRef reference)
    {
        writer.WriteStartObject(name);
        WriteParamRefBody(writer, reference);
        writer.WriteEndObject();
    }

    private static void WriteParamRefBody(Utf8JsonWriter writer, ParamRef reference)
    {
        switch (reference)
        {
            case BoxWidthRef width:
                writer.WriteString(SceneNames.Kind, SceneNames.BoxWidth);
                WriteId(writer, SceneNames.Box, width.Box.Value);
                break;

            case BoxHeightRef height:
                writer.WriteString(SceneNames.Kind, SceneNames.BoxHeight);
                WriteId(writer, SceneNames.Box, height.Box.Value);
                break;

            case BoxDepthRef depth:
                writer.WriteString(SceneNames.Kind, SceneNames.BoxDepth);
                WriteId(writer, SceneNames.Box, depth.Box.Value);
                break;

            case SegmentLengthRef length:
                writer.WriteString(SceneNames.Kind, SceneNames.SegmentLength);
                WriteId(writer, SceneNames.Segment, length.Segment.Value);
                break;

            default:
                throw Unwritable(reference);
        }
    }

    private static void WriteMeasurand(Utf8JsonWriter writer, Measurand measurand)
    {
        writer.WriteStartObject(SceneNames.Measures);

        switch (measurand)
        {
            case ParamMeasurand param:
                // A size measured directly is the size reference itself, with no wrapper, as the
                // geometry design's §6 example writes it and the reader's ReadMeasurand expects.
                WriteParamRefBody(writer, param.Param);
                break;

            case AxisMeasurand span:
                writer.WriteString(SceneNames.Kind, SceneNames.AxisMeasurand);
                WritePointRef(writer, SceneNames.From, span.From);
                WritePointRef(writer, SceneNames.To, span.To);
                writer.WriteString(SceneNames.Axis, SceneNames.Of(span.Axis));
                break;

            default:
                throw Unwritable(measurand);
        }

        writer.WriteEndObject();
    }

    // ---------------------------------------------------------------------------------------
    // Primitives
    // ---------------------------------------------------------------------------------------

    private static void WritePoint(Utf8JsonWriter writer, string name, Point2 point)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber(SceneNames.X, point.X.Units);
        writer.WriteNumber(SceneNames.Y, point.Y.Units);
        writer.WriteEndObject();
    }

    /// <summary>An id, in the canonical 8-4-4-4-12 form the reader parses with <c>Guid.TryParseExact</c>.</summary>
    private static void WriteId(Utf8JsonWriter writer, string name, Guid id)
        => writer.WriteString(name, id.ToString("D", CultureInfo.InvariantCulture));

    /// <summary>
    /// A kind of entity, relationship, reference or measurand the model holds and the format has
    /// no spelling for. That is a gap between the two, not a bad file, so it throws rather than
    /// producing a document the reader would refuse.
    /// </summary>
    private static NotSupportedException Unwritable(object value)
        => new($"The scene format has no spelling for {value.GetType().Name}, so it cannot be written.");
}
