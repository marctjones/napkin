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

        // The builder's typed lists (format version 5): written even when empty, because the
        // format has no optional fields.
        writer.WriteStartArray(SceneNames.FastenerChoices);
        foreach (FastenerChoice choice in sketch.FastenerChoices)
        {
            writer.WriteStartObject();
            writer.WriteString(SceneNames.Kind, SceneNames.Of(choice.Kind));
            WriteOptionalNumber(writer, SceneNames.Thickness, choice.Thickness?.Units);
            writer.WriteString(SceneNames.Size, choice.Size);
            WriteOptionalNumber(writer, SceneNames.PackSize, choice.PackSize);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray(SceneNames.Supplies);
        foreach (SupplyLine line in sketch.Supplies)
        {
            writer.WriteStartObject();
            writer.WriteString(SceneNames.Item, line.Item);
            writer.WriteString(SceneNames.Note, line.Note);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        WriteCode(writer, sketch.Code);
        WriteSite(writer, sketch.Site);

        writer.WriteEndObject();
    }

    /// <summary>The adopted code (format version 6), or <c>"code": null</c> before one is chosen.</summary>
    private static void WriteCode(Utf8JsonWriter writer, CodeChoice? code)
    {
        if (code is null)
        {
            writer.WriteNull(SceneNames.Code);
            return;
        }

        writer.WriteStartObject(SceneNames.Code);
        writer.WriteString(SceneNames.CodePack, code.PackId);
        writer.WriteNumber(SceneNames.CodeRevision, code.Revision);
        writer.WriteString(SceneNames.CodeMode, code.Mode == CodeMode.Locked ? SceneNames.CodeLocked : SceneNames.CodeFollowing);
        WriteOptionalDate(writer, SceneNames.CodeLockedOn, code.LockedOn);
        writer.WriteEndObject();
    }

    /// <summary>The site values (format version 7): every field written, null when not entered.</summary>
    private static void WriteSite(Utf8JsonWriter writer, SiteValues site)
    {
        writer.WriteStartObject(SceneNames.Site);
        WriteOptionalNumber(writer, SceneNames.SiteGroundSnowLoad, site.GroundSnowLoadPsf);
        WriteOptionalNumber(writer, SceneNames.SiteUltimateWindSpeed, site.UltimateWindSpeedMph);
        WriteOptionalText(writer, SceneNames.SiteSeismicDesignCategory, site.SeismicDesignCategory);
        WriteOptionalNumber(writer, SceneNames.SiteFrostDepth, site.FrostDepth?.Units);
        WriteOptionalNumber(writer, SceneNames.SiteBuildingWidth, site.BuildingWidth?.Units);
        WriteOptionalNumber(writer, SceneNames.SiteRoofLiveLoad, site.RoofLiveLoadPsf);
        if (site.Source is { } source)
        {
            writer.WriteStartObject(SceneNames.SiteSource);
            writer.WriteString(SceneNames.SiteSourceText, source.Text);
            WriteOptionalDate(writer, SceneNames.SiteSourceOn, source.On);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull(SceneNames.SiteSource);
        }

        writer.WriteEndObject();
    }

    /// <summary>A wall's inputs (format version 6; bracing, version 8), or <c>"wall": null</c> for a box with none.</summary>
    private static void WriteWallInputs(Utf8JsonWriter writer, WallInputs? inputs)
    {
        if (inputs is null)
        {
            writer.WriteNull(SceneNames.Wall);
            return;
        }

        writer.WriteStartObject(SceneNames.Wall);
        WriteOptionalText(writer, SceneNames.WallSupports, inputs.Supports);
        WriteOptionalNumber(writer, SceneNames.WallStudSpacing, inputs.StudSpacing?.Units);
        if (inputs.Bracing.IsEmpty)
        {
            writer.WriteNull(SceneNames.WallBracing);
        }
        else
        {
            // In the order held: the building module writes them start to end.
            writer.WriteStartArray(SceneNames.WallBracing);
            foreach (BracingAssignment assignment in inputs.Bracing)
            {
                writer.WriteStartObject();
                WriteOptionalId(writer, SceneNames.BracingFrom, assignment.From);
                WriteOptionalId(writer, SceneNames.BracingTo, assignment.To);
                writer.WriteString(SceneNames.BracingMethod, assignment.Method);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (inputs.Side is { } side)
        {
            writer.WriteString(SceneNames.WallSide, SceneNames.Spell(SceneNames.WallSides, side));
        }
        else
        {
            writer.WriteNull(SceneNames.WallSide);
        }

        if (inputs.Bearing is { } bearing)
        {
            writer.WriteBoolean(SceneNames.WallBearing, bearing);
        }
        else
        {
            writer.WriteNull(SceneNames.WallBearing);
        }

        if (inputs.Header is { } header)
        {
            writer.WriteStartObject(SceneNames.WallHeader);
            writer.WriteNumber(SceneNames.HeaderPlies, header.Plies);
            writer.WriteString(SceneNames.HeaderLumber, header.Lumber);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull(SceneNames.WallHeader);
        }

        writer.WriteEndObject();
    }

    /// <summary>A room's finishes and measurements (format version 10), or <c>"room": null</c>.</summary>
    private static void WriteRoom(Utf8JsonWriter writer, RoomInputs? room)
    {
        if (room is null)
        {
            writer.WriteNull(SceneNames.Room);
            return;
        }

        writer.WriteStartObject(SceneNames.Room);
        writer.WriteString(SceneNames.RoomDrywall, SceneNames.Spell(SceneNames.Surfaces, room.Drywall));
        if (room.Sheet is { } sheet)
        {
            writer.WriteStartObject(SceneNames.RoomSheet);
            writer.WriteNumber(SceneNames.SheetWidth, sheet.Width.Units);
            writer.WriteNumber(SceneNames.SheetLength, sheet.Length.Units);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull(SceneNames.RoomSheet);
        }

        writer.WriteString(SceneNames.RoomInsulation, SceneNames.Spell(SceneNames.Insulated, room.Insulation));
        writer.WriteString(SceneNames.RoomInsulationBy, SceneNames.Spell(SceneNames.InsulationWays, room.InsulationBy));
        WriteOptionalNumber(writer, SceneNames.RoomInsulationCoverage, room.InsulationCoverage);
        writer.WriteString(SceneNames.RoomPaint, SceneNames.Spell(SceneNames.Surfaces, room.Paint));
        WriteOptionalNumber(writer, SceneNames.RoomPaintCoats, room.PaintCoats);
        WriteOptionalNumber(writer, SceneNames.RoomPaintCoverage, room.PaintCoverage);
        writer.WriteBoolean(SceneNames.RoomFlooring, room.Flooring);
        writer.WriteNumber(SceneNames.RoomFlooringWaste, room.FlooringWaste);
        WriteOptionalNumber(writer, SceneNames.RoomFlooringBox, room.FlooringBox);
        writer.WriteBoolean(SceneNames.RoomBaseboard, room.Baseboard);
        WriteOptionalNumber(writer, SceneNames.RoomBaseboardStick, room.BaseboardStick?.Units);
        writer.WriteStartObject(SceneNames.RoomMeasured);
        WriteOptionalNumber(writer, SceneNames.South, room.Measured.South?.Units);
        WriteOptionalNumber(writer, SceneNames.North, room.Measured.North?.Units);
        WriteOptionalNumber(writer, SceneNames.East, room.Measured.East?.Units);
        WriteOptionalNumber(writer, SceneNames.West, room.Measured.West?.Units);
        WriteOptionalNumber(writer, SceneNames.MeasuredDiagonal1, room.Measured.Diagonal1?.Units);
        WriteOptionalNumber(writer, SceneNames.MeasuredDiagonal2, room.Measured.Diagonal2?.Units);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteOptionalText(Utf8JsonWriter writer, string name, string? text)
    {
        if (text is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, text);
        }
    }

    private static void WriteOptionalDate(Utf8JsonWriter writer, string name, DateOnly? date)
        => WriteOptionalText(writer, name, date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private static void WriteOptionalNumber(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
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
        writer.WriteString(SceneNames.Phase, SceneNames.Spell(SceneNames.Phases, entity.Phase));

        switch (entity)
        {
            case Note note:
                WritePoint(writer, SceneNames.Position, note.Position);
                writer.WriteString(SceneNames.NoteText, note.Text);
                writer.WriteString(SceneNames.NoteSymbol, SceneNames.Spell(SceneNames.NoteSymbols, note.Symbol));
                break;

            case Node node:
                WritePoint(writer, SceneNames.Position, node.Position);
                break;

            case Segment segment:
                WriteId(writer, SceneNames.Start, segment.Start.Value);
                WriteId(writer, SceneNames.End, segment.End.Value);
                break;

            case Box box:
                WritePoint(writer, SceneNames.Anchor, box.Anchor);
                writer.WriteNumber(SceneNames.Width, box.Width.Units);
                writer.WriteNumber(SceneNames.Height, box.Height.Units);
                writer.WriteNumber(SceneNames.Depth, box.Depth.Units);
                writer.WriteString(SceneNames.FaceUp, SceneNames.Of(box.FaceUp));
                writer.WriteNumber(SceneNames.Rotation, box.Rotation.Arcseconds);
                WritePart(writer, box.Part);
                WriteWallInputs(writer, box.WallInputs);
                WriteRoom(writer, box.Room);
                WriteCuts(writer, box.Cuts);
                break;

            case Strut strut:
                // The two ends, the cuts, the reference and the cross-section; never the blank,
                // which is derived (angled-parts §7).
                WritePoint(writer, SceneNames.From, strut.From);
                WritePoint(writer, SceneNames.To, strut.To);
                writer.WriteString(SceneNames.FromCut, SceneNames.Spell(SceneNames.EndCuts, strut.FromCut));
                writer.WriteString(SceneNames.ToCut, SceneNames.Spell(SceneNames.EndCuts, strut.ToCut));
                writer.WriteString(SceneNames.Reference, SceneNames.Spell(SceneNames.ReferenceAxes, strut.Reference));
                writer.WriteNumber(SceneNames.Height, strut.Height.Units);
                writer.WriteNumber(SceneNames.Depth, strut.Depth.Units);
                WritePart(writer, strut.Part);
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
    /// A box's part, or <c>"part": null</c> for a box that is not a piece anybody cuts. Written
    /// even when there is nothing to say, because the format has no optional fields. The part's
    /// third dimension is not written here: it is the box's <c>depth</c>, stored once.
    /// </summary>
    private static void WritePart(Utf8JsonWriter writer, Part? part)
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

        writer.WriteStartObject(SceneNames.PlanAxes);
        writer.WriteString(SceneNames.X, SceneNames.Of(part.PlanAxes.X));
        writer.WriteString(SceneNames.Y, SceneNames.Of(part.PlanAxes.Y));
        writer.WriteEndObject();

        writer.WriteStartArray(SceneNames.Hardware);
        foreach (HardwareItem item in part.Hardware)
        {
            writer.WriteStartObject();
            writer.WriteString(SceneNames.Name, item.Name);
            writer.WriteNumber(SceneNames.Quantity, item.Quantity);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteBoolean(SceneNames.Rough, part.Rough);
        WriteOptionalText(writer, SceneNames.Grain, part.Grain is { } grain ? SceneNames.Of(grain) : null);
        WriteOptionalText(writer, SceneNames.ShowFace, part.ShowFace is { } face ? SceneNames.Of(face) : null);

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
        Note => SceneNames.NoteType,
        Strut => SceneNames.StrutType,
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
                WritePlaceRef(writer, SceneNames.A, coincident.A);
                WritePlaceRef(writer, SceneNames.B, coincident.B);
                break;

            case Horizontal horizontal:
                writer.WriteString(SceneNames.Kind, SceneNames.Horizontal);
                WritePlaceRef(writer, SceneNames.Edge, horizontal.Edge);
                break;

            case Vertical vertical:
                writer.WriteString(SceneNames.Kind, SceneNames.Vertical);
                WritePlaceRef(writer, SceneNames.Edge, vertical.Edge);
                break;

            case Flush flush:
                writer.WriteString(SceneNames.Kind, SceneNames.Flush);
                WritePlaceRef(writer, SceneNames.A, flush.A);
                WritePlaceRef(writer, SceneNames.B, flush.B);
                break;

            case AxisDistance axisDistance:
                writer.WriteString(SceneNames.Kind, SceneNames.AxisDistance);
                WritePlaceRef(writer, SceneNames.From, axisDistance.From);
                WritePlaceRef(writer, SceneNames.To, axisDistance.To);
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
                WritePlaceRef(writer, SceneNames.Middle, centered.Middle);
                WritePlaceRef(writer, SceneNames.A, centered.A);
                WritePlaceRef(writer, SceneNames.B, centered.B);
                writer.WriteString(SceneNames.Axis, SceneNames.Of(centered.Axis));
                break;

            case Joint joint:
                writer.WriteString(SceneNames.Kind, SceneNames.Joint);
                writer.WriteString(SceneNames.Type, SceneNames.Of(joint.Type));
                WritePlaceRef(writer, SceneNames.Receiving, joint.Receiving);
                WritePlaceRef(writer, SceneNames.Inserted, joint.Inserted);
                WriteOptionalNumber(writer, SceneNames.Depth, joint.Depth?.Units);
                writer.WriteStartObject(SceneNames.Fastening);
                writer.WriteString(SceneNames.Kind, SceneNames.Of(joint.Fastening.Kind));
                WriteOptionalNumber(writer, SceneNames.Count, joint.Fastening.Count);
                if (joint.Fastening.PocketFace is { } pocket)
                {
                    writer.WriteString(SceneNames.PocketFace, SceneNames.Of(pocket));
                }
                else
                {
                    writer.WriteNull(SceneNames.PocketFace);
                }

                writer.WriteEndObject();
                writer.WriteBoolean(SceneNames.Glue, joint.Glue);
                break;

            case StrutJoint onStrut:
                // A butt on a strut's end (angled-parts §7): its pocket face is one of the strut's four
                // long faces, spelled as a box's face of the same name.
                writer.WriteString(SceneNames.Kind, SceneNames.Joint);
                writer.WriteString(SceneNames.Type, SceneNames.Of(JointType.Butt));
                WritePlaceRef(writer, SceneNames.Receiving, onStrut.Receiving);
                WritePlaceRef(writer, SceneNames.Inserted, onStrut.Inserted);
                writer.WriteNull(SceneNames.Depth);
                writer.WriteStartObject(SceneNames.Fastening);
                writer.WriteString(SceneNames.Kind, SceneNames.Of(onStrut.Fastening.Kind));
                WriteOptionalNumber(writer, SceneNames.Count, onStrut.Fastening.Count);
                if (onStrut.PocketFrom is { } from)
                {
                    writer.WriteString(SceneNames.PocketFace, SceneNames.Spell(SceneNames.StrutFaces, from));
                }
                else
                {
                    writer.WriteNull(SceneNames.PocketFace);
                }

                writer.WriteEndObject();
                writer.WriteBoolean(SceneNames.Glue, onStrut.Glue);
                break;

            case Geometry.Parallel parallel:
                writer.WriteString(SceneNames.Kind, SceneNames.Parallel);
                WritePlaceRef(writer, SceneNames.A, parallel.A);
                WritePlaceRef(writer, SceneNames.B, parallel.B);
                break;

            case Perpendicular perpendicular:
                writer.WriteString(SceneNames.Kind, SceneNames.Perpendicular);
                WritePlaceRef(writer, SceneNames.A, perpendicular.A);
                WritePlaceRef(writer, SceneNames.B, perpendicular.B);
                break;

            case AngleBetween angleBetween:
                writer.WriteString(SceneNames.Kind, SceneNames.AngleBetween);
                WritePlaceRef(writer, SceneNames.A, angleBetween.A);
                WritePlaceRef(writer, SceneNames.B, angleBetween.B);
                writer.WriteNumber(SceneNames.Angle, angleBetween.Angle.Arcseconds);
                break;

            case Distance distance:
                writer.WriteString(SceneNames.Kind, SceneNames.Distance);
                WritePlaceRef(writer, SceneNames.A, distance.A);
                WritePlaceRef(writer, SceneNames.B, distance.B);
                writer.WriteNumber(SceneNames.Value, distance.Value.Units);
                break;

            case PointOnEdge pointOnEdge:
                writer.WriteString(SceneNames.Kind, SceneNames.PointOnEdge);
                WritePlaceRef(writer, SceneNames.Point, pointOnEdge.Point);
                WritePlaceRef(writer, SceneNames.Edge, pointOnEdge.Edge);
                break;

            case Symmetric symmetric:
                writer.WriteString(SceneNames.Kind, SceneNames.Symmetric);
                WritePlaceRef(writer, SceneNames.A, symmetric.A);
                WritePlaceRef(writer, SceneNames.B, symmetric.B);
                WritePlaceRef(writer, SceneNames.Mirror, symmetric.Mirror);
                break;

            case Tangent tangent:
                writer.WriteString(SceneNames.Kind, SceneNames.Tangent);
                WritePlaceRef(writer, SceneNames.A, tangent.A);
                WritePlaceRef(writer, SceneNames.B, tangent.B);
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

    // One writer for every place, whichever slot it is in: what a slot may hold is the reader's
    // and the kernel's business (docs/design/assembly-model.md §2.2), and this type writes whatever
    // it is given so that a test can see the reader refuse it for the right reason.
    private static void WritePlaceRef(Utf8JsonWriter writer, string name, PlaceRef reference)
    {
        writer.WriteStartObject(name);

        switch (reference)
        {
            case NodeRef node:
                writer.WriteString(SceneNames.Kind, SceneNames.Node);
                WriteId(writer, SceneNames.Node, node.Node.Value);
                break;

            case SegmentRef segment:
                writer.WriteString(SceneNames.Kind, SceneNames.Segment);
                WriteId(writer, SceneNames.Segment, segment.Segment.Value);
                break;

            case CenterRef center:
                writer.WriteString(SceneNames.Kind, SceneNames.Center);
                WriteId(writer, SceneNames.Box, center.Box.Value);
                break;

            case FeatureRef feature:
                // The faces in BoxFace order, which is the order BoxFeature lists them in: a
                // feature has one spelling in memory and in the file.
                writer.WriteString(SceneNames.Kind, SceneNames.Feature);
                WriteId(writer, SceneNames.Box, feature.Box.Value);
                writer.WriteStartArray(SceneNames.Faces);
                foreach (BoxFace face in feature.Feature.Faces)
                {
                    writer.WriteStringValue(SceneNames.Of(face));
                }

                writer.WriteEndArray();
                break;

            case StrutEndRef end:
                writer.WriteString(SceneNames.Kind, SceneNames.StrutEndKind);
                WriteId(writer, SceneNames.StrutType, end.Strut.Value);
                writer.WriteString(SceneNames.End, SceneNames.Spell(SceneNames.StrutEnds, end.End));
                break;

            case StrutFaceRef face:
                writer.WriteString(SceneNames.Kind, SceneNames.StrutFaceKind);
                WriteId(writer, SceneNames.StrutType, face.Strut.Value);
                writer.WriteString(SceneNames.Face, SceneNames.Spell(SceneNames.StrutFaces, face.Face));
                break;

            case StrutEndFaceRef endFace:
                writer.WriteString(SceneNames.Kind, SceneNames.StrutEndFaceKind);
                WriteId(writer, SceneNames.StrutType, endFace.Strut.Value);
                writer.WriteString(SceneNames.End, SceneNames.Spell(SceneNames.StrutEnds, endFace.End));
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

            case StrutHeightRef strutHeight:
                writer.WriteString(SceneNames.Kind, SceneNames.StrutHeight);
                WriteId(writer, SceneNames.StrutType, strutHeight.Strut.Value);
                break;

            case StrutDepthRef strutDepth:
                writer.WriteString(SceneNames.Kind, SceneNames.StrutDepth);
                WriteId(writer, SceneNames.StrutType, strutDepth.Strut.Value);
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
                WritePlaceRef(writer, SceneNames.From, span.From);
                WritePlaceRef(writer, SceneNames.To, span.To);
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

    private static void WritePoint(Utf8JsonWriter writer, string name, Point3 point)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber(SceneNames.X, point.X.Units);
        writer.WriteNumber(SceneNames.Y, point.Y.Units);
        writer.WriteNumber(SceneNames.Z, point.Z.Units);
        writer.WriteEndObject();
    }

    /// <summary>An id, in the canonical 8-4-4-4-12 form the reader parses with <c>Guid.TryParseExact</c>.</summary>
    private static void WriteId(Utf8JsonWriter writer, string name, Guid id)
        => writer.WriteString(name, id.ToString("D", CultureInfo.InvariantCulture));

    private static void WriteOptionalId(Utf8JsonWriter writer, string name, EntityId? id)
    {
        if (id is { } value)
        {
            WriteId(writer, name, value.Value);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    /// <summary>
    /// A kind of entity, relationship, reference or measurand the model holds and the format has
    /// no spelling for. That is a gap between the two, not a bad file, so it throws rather than
    /// producing a document the reader would refuse.
    /// </summary>
    private static NotSupportedException Unwritable(object value)
        => new($"The scene format has no spelling for {value.GetType().Name}, so it cannot be written.");
}
