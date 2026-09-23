using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project;

/// <summary>
/// Turns one parsed scene document into a <see cref="Sketch"/>, or into the list of reasons it was
/// refused. Strict throughout: an unknown field, a repeated field, a decimal where an integer
/// belongs, an id that names nothing or names the wrong kind of thing are each a refusal.
/// </summary>
/// <remarks>
/// One binder reads one document; it holds the problems found so far, so it is not reusable and
/// not thread-safe. <see cref="SceneReader"/> makes one per call.
/// </remarks>
internal sealed class SceneBinder
{
    private readonly List<LoadProblem> problems = [];
    private readonly Dictionary<EntityId, Entity> entities = [];
    private readonly Dictionary<RelationshipId, Relationship> relationships = [];
    private readonly List<KindCheck> kindChecks = [];
    private readonly List<DriveCheck> driveChecks = [];

    /// <summary>Reads a document that has already been parsed as JSON.</summary>
    internal LoadResult Read(JsonElement root, IGeometryUpdater updater)
    {
        JsonFields? document = ReadFields(root, string.Empty, "the document");
        if (document is null)
        {
            return Refuse();
        }

        // The stamp is read and judged before any of the scene is parsed: a file from another
        // format version is refused for that reason alone, whatever else is wrong with it.
        FormatStamp? stamp = ReadStamp(document);
        if (stamp is null)
        {
            return Refuse();
        }

        ImmutableList<Layer> layers = ReadLayers(document);
        ReadEntities(document);
        ReadRelationships(document);
        RejectUnknownFields(document);

        if (problems.Count > 0)
        {
            return Refuse();
        }

        // Every id a reference names exists and names the right kind of entity. This runs before
        // anything evaluates geometry, because the checker reads a corner off whatever entity an
        // id names and cannot be asked about a corner of a node.
        ResolveReferences();
        if (problems.Count > 0)
        {
            return Refuse();
        }

        Sketch sketch = new(
            entities.ToImmutableDictionary(),
            relationships.ToImmutableDictionary(),
            layers);

        ValidationResult validation = sketch.Validate();
        if (!validation.IsValid)
        {
            foreach (ValidationError error in validation.Errors)
            {
                problems.Add(new LoadProblem(KindOf(error.Kind), string.Empty, error.Message));
            }

            return Refuse();
        }

        RefuseUnsupportedRelationships(sketch, updater);
        if (problems.Count > 0)
        {
            return Refuse();
        }

        CheckReport report;
        try
        {
            report = RelationshipChecker.Check(sketch);
        }
        catch (OverflowException exception)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.OutOfRange,
                string.Empty,
                $"The file's geometry is too large to evaluate: {exception.Message}"));
            return Refuse();
        }

        if (!report.AllHold)
        {
            foreach (Violation violation in report.Violations)
            {
                Relationship relationship = sketch.Relationships[violation.Relationship];
                problems.Add(new LoadProblem(
                    LoadProblemKind.RelationshipViolated,
                    string.Empty,
                    $"The file's geometry does not satisfy its own {NameOf(relationship)} relationship "
                    + $"{violation.Relationship}: it is out by {violation.Residual}."));
            }

            return Refuse();
        }

        return new Loaded(sketch, stamp);
    }

    private static LoadProblemKind KindOf(ValidationErrorKind kind) => kind switch
    {
        ValidationErrorKind.DanglingReference => LoadProblemKind.DanglingReference,
        ValidationErrorKind.WrongEntityKind => LoadProblemKind.WrongReferenceKind,
        ValidationErrorKind.NonPositiveSize => LoadProblemKind.InvalidValue,
        ValidationErrorKind.DuplicateRelationship => LoadProblemKind.DuplicateRelationship,
        ValidationErrorKind.UnknownLayer => LoadProblemKind.DanglingReference,
        _ => LoadProblemKind.InvalidValue,
    };

    private static string NameOf(Relationship relationship) => relationship switch
    {
        Anchored => SceneNames.Anchored,
        Coincident => SceneNames.Coincident,
        Horizontal => SceneNames.Horizontal,
        Vertical => SceneNames.Vertical,
        Flush => SceneNames.Flush,
        AxisDistance => SceneNames.AxisDistance,
        ParamValue => SceneNames.ParamValue,
        EqualParam => SceneNames.EqualParam,
        Centered => SceneNames.Centered,
        Geometry.Parallel => SceneNames.Parallel,
        Perpendicular => SceneNames.Perpendicular,
        AngleBetween => SceneNames.AngleBetween,
        Distance => SceneNames.Distance,
        PointOnEdge => SceneNames.PointOnEdge,
        Symmetric => SceneNames.Symmetric,
        Tangent => SceneNames.Tangent,
        Radius => SceneNames.Radius,
        _ => relationship.GetType().Name,
    };

    private static string Describe(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "text",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        _ => "nothing",
    };

    private LoadResult Refuse()
    {
        if (problems.Count == 0)
        {
            throw new InvalidOperationException("A refusal with no problem in it is a bug in the reader.");
        }

        return new Refused([.. problems]);
    }

    private void Add(LoadProblemKind kind, string location, string message)
        => problems.Add(new LoadProblem(kind, location, message));

    // -----------------------------------------------------------------------------------------
    // The stamp
    // -----------------------------------------------------------------------------------------

    private FormatStamp? ReadStamp(JsonFields document)
    {
        int before = problems.Count;
        long? version = ReadInteger(document, SceneNames.FormatVersion);
        if (version is not { } found)
        {
            return null;
        }

        if (found != FormatStamp.CurrentVersion)
        {
            Add(
                LoadProblemKind.UnsupportedFormatVersion,
                $"/{SceneNames.FormatVersion}",
                $"The file is format version {found.ToString(CultureInfo.InvariantCulture)}; this build of napkin reads "
                + $"format version {FormatStamp.CurrentVersion.ToString(CultureInfo.InvariantCulture)} and no other. "
                + "napkin is a beta and writes no migration code: a file from another version cannot be opened.");
            return null;
        }

        JsonFields? units = ReadObject(document, SceneNames.Units);
        if (units is null)
        {
            return null;
        }

        string? length = ReadText(units, SceneNames.UnitsLength);
        string? angle = ReadText(units, SceneNames.UnitsAngle);
        RejectUnknownFields(units);

        if (length is not null && length != FormatStamp.InchGrid)
        {
            Add(
                LoadProblemKind.UnsupportedUnits,
                $"{units.Path}/{SceneNames.UnitsLength}",
                $"The file stores lengths in \"{length}\"; napkin stores them in \"{FormatStamp.InchGrid}\".");
        }

        if (angle is not null && angle != FormatStamp.Arcsecond)
        {
            Add(
                LoadProblemKind.UnsupportedUnits,
                $"{units.Path}/{SceneNames.UnitsAngle}",
                $"The file stores angles in \"{angle}\"; napkin stores them in \"{FormatStamp.Arcsecond}\".");
        }

        return problems.Count > before ? null : new FormatStamp((int)found, length!, angle!);
    }

    // -----------------------------------------------------------------------------------------
    // Layers, entities, relationships
    // -----------------------------------------------------------------------------------------

    private ImmutableList<Layer> ReadLayers(JsonFields document)
    {
        ImmutableList<Layer>.Builder layers = ImmutableList.CreateBuilder<Layer>();
        HashSet<LayerId> seen = [];

        foreach ((JsonElement element, string path) in ReadArray(document, SceneNames.Layers))
        {
            JsonFields? fields = ReadFields(element, path, "a layer");
            if (fields is null)
            {
                continue;
            }

            Guid? id = ReadId(fields, SceneNames.Id);
            string? name = ReadText(fields, SceneNames.Name);
            RejectUnknownFields(fields);

            if (id is not { } value || name is null)
            {
                continue;
            }

            LayerId layerId = new(value);
            if (!seen.Add(layerId))
            {
                Add(LoadProblemKind.DuplicateId, path, $"Two layers share the id {value}.");
                continue;
            }

            layers.Add(new Layer(layerId, name));
        }

        return layers.ToImmutable();
    }

    private void ReadEntities(JsonFields document)
    {
        foreach ((JsonElement element, string path) in ReadArray(document, SceneNames.Entities))
        {
            JsonFields? fields = ReadFields(element, path, "an entity");
            if (fields is null)
            {
                continue;
            }

            Guid? id = ReadId(fields, SceneNames.Id);
            string? type = ReadText(fields, SceneNames.Type);
            Guid? layer = ReadId(fields, SceneNames.Layer);

            // Every entity carries a name, whatever its type: a named dimension reads better in a
            // conflict message, and an empty string is a legal "unnamed" (format version 2).
            string? name = ReadText(fields, SceneNames.Name);

            if (id is not { } entityId || type is null || layer is not { } layerId)
            {
                RejectUnknownFields(fields);
                continue;
            }

            Entity? entity = type switch
            {
                SceneNames.Node => ReadNode(fields, new EntityId(entityId), new LayerId(layerId)),
                SceneNames.Segment => ReadSegment(fields, new EntityId(entityId), new LayerId(layerId)),
                SceneNames.Box => ReadBox(fields, new EntityId(entityId), new LayerId(layerId)),
                SceneNames.Dimension => ReadDimension(fields, new EntityId(entityId), new LayerId(layerId)),
                _ => UnknownType(fields, type),
            };

            RejectUnknownFields(fields);

            if (entity is null || name is null)
            {
                continue;
            }

            entity = entity with { Name = name };

            if (!entities.TryAdd(entity.Id, entity))
            {
                Add(LoadProblemKind.DuplicateId, path, $"Two entities share the id {entityId}.");
            }
        }
    }

    private Entity? UnknownType(JsonFields fields, string type)
    {
        Add(
            LoadProblemKind.UnknownValue,
            $"{fields.Path}/{SceneNames.Type}",
            $"\"{type}\" is not an entity type this build knows. The types are: {SceneNames.List(SceneNames.EntityTypes)}.");
        return null;
    }

    private Entity? ReadNode(JsonFields fields, EntityId id, LayerId layer)
    {
        Point2? position = ReadPoint(fields, SceneNames.Position);
        return position is { } value ? new Node(id, layer, value) : null;
    }

    private Entity? ReadSegment(JsonFields fields, EntityId id, LayerId layer)
    {
        EntityId? start = ReadEntityReference(fields, SceneNames.Start, typeof(Node));
        EntityId? end = ReadEntityReference(fields, SceneNames.End, typeof(Node));
        return start is { } from && end is { } to ? new Segment(id, layer, from, to) : null;
    }

    private Entity? ReadBox(JsonFields fields, EntityId id, LayerId layer)
    {
        Point2? anchor = ReadPoint(fields, SceneNames.Anchor);
        long? width = ReadInteger(fields, SceneNames.Width);
        long? height = ReadInteger(fields, SceneNames.Height);
        long? rotation = ReadInteger(fields, SceneNames.Rotation);
        (bool partRead, Part? part, Length? outOfPlane) = ReadPart(fields);
        (bool cutsRead, ImmutableList<Cut> cuts) = ReadCuts(fields);

        if (width is { } w && w <= 0)
        {
            Add(
                LoadProblemKind.InvalidValue,
                $"{fields.Path}/{SceneNames.Width}",
                $"A box's width must be greater than zero; this one is {w.ToString(CultureInfo.InvariantCulture)} units.");
            width = null;
        }

        if (height is { } h && h <= 0)
        {
            Add(
                LoadProblemKind.InvalidValue,
                $"{fields.Path}/{SceneNames.Height}",
                $"A box's height must be greater than zero; this one is {h.ToString(CultureInfo.InvariantCulture)} units.");
            height = null;
        }

        if (rotation is { } r && (r < 0 || r >= Angle.FullTurn))
        {
            Add(
                LoadProblemKind.InvalidValue,
                $"{fields.Path}/{SceneNames.Rotation}",
                $"A rotation is stored normalised, in arcseconds from 0 up to but not including "
                + $"{Angle.FullTurn.ToString(CultureInfo.InvariantCulture)} (360 degrees); this one is "
                + $"{r.ToString(CultureInfo.InvariantCulture)}.");
            rotation = null;
        }

        // Format version 3 is a plan format: every box in it lies as drawn at the plan datum, and
        // its third size is its part's out-of-plane dimension, or the rectangle tool's default for a
        // box that is not a part. docs/design/assembly-model.md §10 step 5 gives the file a depth,
        // a face-up and a Z of its own; until then this is what a version-3 box means.
        return anchor is { } corner && width is { } wide && height is { } tall && rotation is { } turn
            && partRead && cutsRead
            ? new Box(
                id,
                layer,
                new Point3(corner.X, corner.Y, Length.Zero),
                new Length(wide),
                new Length(tall),
                outOfPlane ?? Box.DefaultDepth,
                BoxFace.Top,
                new Angle(turn))
            {
                Part = part,
                Cuts = cuts,
            }
            : null;
    }

    /// <summary>
    /// Reads a box's <c>cuts</c>, which is required and is empty for a plain rectangle
    /// (<c>docs/design/shaped-parts-model.md</c> §5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only what the file alone can be wrong about is judged here: an unknown <c>kind</c>, a
    /// corner, edge or <c>bow</c> the format does not spell, a value that is not a positive
    /// integer, and the array's order. Invariants 5 to 9 — one cut per site, a curve's claim on
    /// its corners, whether a cut fits the blank it is on — belong to the geometry kernel's cut
    /// rules and are checked where every other sketch invariant is, by the
    /// <see cref="Sketch.Validate"/> this reader already runs; there is no second copy of them
    /// here to drift.
    /// </para>
    /// <para>
    /// <strong>The order is judged, not fixed.</strong> <see cref="Box.Cuts"/>'s initialiser sorts,
    /// so a file out of site order would load as a sketch that no longer equals it; it is refused
    /// instead, the same stance the format takes on an un-normalised rotation.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Whether the field was read without a problem, and the cuts it held — which are empty both
    /// for a well-formed <c>"cuts": []</c> and for a refusal, so the flag is what tells them apart.
    /// </returns>
    private (bool Read, ImmutableList<Cut> Cuts) ReadCuts(JsonFields fields)
    {
        int before = problems.Count;
        ImmutableList<Cut>.Builder cuts = ImmutableList.CreateBuilder<Cut>();
        CutSite? previous = null;
        bool saidSo = false;

        foreach ((JsonElement element, string path) in ReadArray(fields, SceneNames.Cuts))
        {
            JsonFields? cut = ReadFields(element, path, "a cut");
            if (cut is null)
            {
                continue;
            }

            string? kind = ReadText(cut, SceneNames.Kind);
            Cut? read = kind switch
            {
                null => null,
                SceneNames.CornerCut => ReadCornerCut(cut),
                SceneNames.RoundedCorner => ReadRoundedCorner(cut),
                SceneNames.CurvedEdge => ReadCurvedEdge(cut),
                _ => UnknownCutKind(cut, kind),
            };

            RejectUnknownFields(cut);

            if (read is null)
            {
                continue;
            }

            // Strictly out of order is a refusal; a site repeated is not reported here, so that
            // it falls through to CutRules as the duplicate site it is (invariant 5).
            if (!saidSo && previous is { } last && last > read.Site)
            {
                Add(
                    LoadProblemKind.InvalidValue,
                    path,
                    $"A box's cuts are stored in site order — {SceneNames.List(SceneNames.Sites)} — so that a "
                    + "file has one spelling of one shape. This one's cut at the "
                    + $"{read.Site} follows its cut at the {last}. A file out of order is refused rather than "
                    + "quietly sorted, the way an un-normalised rotation is.");
                saidSo = true;
            }

            previous = read.Site;
            cuts.Add(read);
        }

        return problems.Count > before ? (false, ImmutableList<Cut>.Empty) : (true, cuts.ToImmutable());
    }

    private Cut? UnknownCutKind(JsonFields fields, string kind)
    {
        Add(
            LoadProblemKind.UnknownValue,
            $"{fields.Path}/{SceneNames.Kind}",
            $"\"{kind}\" is not a kind of cut this build knows. The kinds are: {SceneNames.List(SceneNames.CutKinds)}.");
        return null;
    }

    private Cut? ReadCornerCut(JsonFields fields)
    {
        BoxCorner? corner = ReadCutCorner(fields);
        Length? alongX = ReadCutValue(fields, SceneNames.AlongX);
        Length? alongY = ReadCutValue(fields, SceneNames.AlongY);

        return corner is { } which && alongX is { } across && alongY is { } up
            ? new CornerCut(which, across, up)
            : null;
    }

    private Cut? ReadRoundedCorner(JsonFields fields)
    {
        BoxCorner? corner = ReadCutCorner(fields);
        Length? radius = ReadCutValue(fields, SceneNames.CutRadius);

        return corner is { } which && radius is { } round ? new RoundedCorner(which, round) : null;
    }

    private Cut? ReadCurvedEdge(JsonFields fields)
    {
        BoxEdge? edge = ReadCutEdge(fields);
        Bow? bow = ReadBow(fields);
        Length? depth = ReadCutValue(fields, SceneNames.Depth);

        return edge is { } which && bow is { } way && depth is { } deep ? new CurvedEdge(which, way, deep) : null;
    }

    private BoxCorner? ReadCutCorner(JsonFields fields)
    {
        string? text = ReadText(fields, SceneNames.Corner);
        if (text is null)
        {
            return null;
        }

        if (!SceneNames.TryCorner(text, out BoxCorner corner))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.Corner}",
                $"\"{text}\" is not a corner. The corners are: southWest, southEast, northEast, northWest.");
            return null;
        }

        return corner;
    }

    private BoxEdge? ReadCutEdge(JsonFields fields)
    {
        string? text = ReadText(fields, SceneNames.Edge);
        if (text is null)
        {
            return null;
        }

        if (!SceneNames.TryEdge(text, out BoxEdge edge))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.Edge}",
                $"\"{text}\" is not an edge. The edges are: south, east, north, west.");
            return null;
        }

        return edge;
    }

    private Bow? ReadBow(JsonFields fields)
    {
        string? text = ReadText(fields, SceneNames.Bow);
        if (text is null)
        {
            return null;
        }

        if (!SceneNames.TryBow(text, out Bow bow))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.Bow}",
                $"\"{text}\" is not a way for an edge to bow. The ways are: {SceneNames.List(SceneNames.Bows)}.");
            return null;
        }

        return bow;
    }

    /// <summary>
    /// One of a cut's stored lengths. Every one of them is how far the cut reaches into the blank,
    /// so zero and negative are refused here; whether it reaches too far is invariant 7's business.
    /// </summary>
    private Length? ReadCutValue(JsonFields fields, string name)
    {
        long? value = ReadInteger(fields, name);
        if (value is not { } units)
        {
            return null;
        }

        if (units <= 0)
        {
            Add(
                LoadProblemKind.InvalidValue,
                $"{fields.Path}/{name}",
                $"A cut's \"{name}\" is how far it reaches into the blank and must be greater than zero; "
                + $"this one is {units.ToString(CultureInfo.InvariantCulture)} units.");
            return null;
        }

        return new Length(units);
    }

    /// <summary>
    /// Reads a box's <c>part</c>, which is required and may be <see langword="null"/>: a wall and
    /// an opening are boxes that are not pieces anybody cuts.
    /// </summary>
    /// <returns>
    /// Whether the field was read without a problem, and the part it held, which is
    /// <see langword="null"/> both for a well-formed <c>"part": null</c> and for a refusal — the
    /// flag is what tells them apart.
    /// </returns>
    private (bool Read, Part? Part, Length? OutOfPlane) ReadPart(JsonFields fields)
    {
        JsonElement? element = Take(fields, SceneNames.Part);
        if (element is not { } value)
        {
            return (false, null, null);
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return (true, null, null);
        }

        JsonFields? part = ReadFields(value, $"{fields.Path}/{SceneNames.Part}", $"\"{SceneNames.Part}\"");
        if (part is null)
        {
            return (false, null, null);
        }

        // The stock name is not checked against this build's materials library, deliberately: a
        // file is refused for being malformed, never for naming something this build has not heard
        // of (docs/design/parts-and-cut-list.md §2.2).
        (bool stockRead, string? stock) = ReadTextOrNull(part, SceneNames.Stock);
        (bool speciesRead, string? species) = ReadTextOrNull(part, SceneNames.Species);
        long? quantity = ReadInteger(part, SceneNames.Quantity);
        long? outOfPlane = ReadInteger(part, SceneNames.OutOfPlane);
        PlanAxes? planAxes = ReadPlanAxes(part);
        RejectUnknownFields(part);

        if (quantity is { } count && count < 1)
        {
            Add(
                LoadProblemKind.InvalidValue,
                $"{part.Path}/{SceneNames.Quantity}",
                $"A part stands for at least one piece; this one says {count.ToString(CultureInfo.InvariantCulture)}.");
            quantity = null;
        }

        if (outOfPlane is { } third && third <= 0)
        {
            Add(
                LoadProblemKind.InvalidValue,
                $"{part.Path}/{SceneNames.OutOfPlane}",
                "A part's out-of-plane dimension must be greater than zero; this one is "
                + $"{third.ToString(CultureInfo.InvariantCulture)} units.");
            outOfPlane = null;
        }

        // The part's out-of-plane dimension is the box's depth now (assembly-model §1.2); the file
        // keeps it on the part until §10 step 5 moves it onto the box.
        return stockRead && speciesRead && quantity is { } pieces && outOfPlane is { } units && planAxes is { } axes
            ? (true, new Part(stock, species, (int)pieces, axes), new Length(units))
            : (false, null, null);
    }

    private PlanAxes? ReadPlanAxes(JsonFields part)
    {
        JsonFields? fields = ReadObject(part, SceneNames.PlanAxes);
        if (fields is null)
        {
            return null;
        }

        PartDimension? x = ReadPartDimension(fields, SceneNames.X);
        PartDimension? y = ReadPartDimension(fields, SceneNames.Y);
        RejectUnknownFields(fields);

        if (x is not { } across || y is not { } up)
        {
            return null;
        }

        if (across == up)
        {
            Add(
                LoadProblemKind.InvalidValue,
                fields.Path,
                $"A part's two plan axes must name different dimensions; both name \"{SceneNames.Of(across)}\". "
                + "The third dimension is the one neither axis claims, and there would be none.");
            return null;
        }

        return new PlanAxes(across, up);
    }

    private PartDimension? ReadPartDimension(JsonFields fields, string name)
    {
        string? text = ReadText(fields, name);
        if (text is null)
        {
            return null;
        }

        if (!SceneNames.TryPartDimension(text, out PartDimension dimension))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{name}",
                $"\"{text}\" is not one of a part's three dimensions. They are: "
                + $"{SceneNames.List(SceneNames.PartDimensions)}.");
            return null;
        }

        return dimension;
    }

    private Entity? ReadDimension(JsonFields fields, EntityId id, LayerId layer)
    {
        Measurand? measures = ReadMeasurand(fields, SceneNames.Measures);
        RelationshipId? drives = ReadDrives(fields);
        DimensionPlacement? placement = ReadPlacement(fields);

        return measures is not null && placement is { } where
            ? new Dimension(id, layer, measures, drives, where)
            : null;
    }

    private RelationshipId? ReadDrives(JsonFields fields)
    {
        JsonElement? element = Take(fields, SceneNames.Drives);
        if (element is not { } value)
        {
            return null;
        }

        string path = $"{fields.Path}/{SceneNames.Drives}";
        if (value.ValueKind == JsonValueKind.Null)
        {
            // A reference dimension: it measures, and no relationship owns its number (design §3.3).
            return null;
        }

        Guid? id = AsId(value, path, SceneNames.Drives);
        if (id is not { } driving)
        {
            return null;
        }

        RelationshipId relationshipId = new(driving);
        driveChecks.Add(new DriveCheck(path, relationshipId));
        return relationshipId;
    }

    private DimensionPlacement? ReadPlacement(JsonFields fields)
    {
        JsonFields? placement = ReadObject(fields, SceneNames.Placement);
        if (placement is null)
        {
            return null;
        }

        long? offset = ReadInteger(placement, SceneNames.Offset);
        DimensionSide? side = ReadSide(placement);
        RejectUnknownFields(placement);

        return offset is { } distance && side is { } which
            ? new DimensionPlacement(new Length(distance), which)
            : null;
    }

    private DimensionSide? ReadSide(JsonFields placement)
    {
        string? text = ReadText(placement, SceneNames.Side);
        if (text is null)
        {
            return null;
        }

        if (!SceneNames.TrySide(text, out DimensionSide side))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{placement.Path}/{SceneNames.Side}",
                $"\"{text}\" is not a side. The sides are: north, south, east, west.");
            return null;
        }

        return side;
    }

    private void ReadRelationships(JsonFields document)
    {
        foreach ((JsonElement element, string path) in ReadArray(document, SceneNames.Relationships))
        {
            JsonFields? fields = ReadFields(element, path, "a relationship");
            if (fields is null)
            {
                continue;
            }

            Guid? id = ReadId(fields, SceneNames.Id);
            string? kind = ReadText(fields, SceneNames.Kind);
            if (id is not { } value || kind is null)
            {
                RejectUnknownFields(fields);
                continue;
            }

            RelationshipId relationshipId = new(value);
            Relationship? relationship = ReadRelationship(fields, relationshipId, kind);
            RejectUnknownFields(fields);

            if (relationship is null)
            {
                continue;
            }

            if (!relationships.TryAdd(relationshipId, relationship))
            {
                Add(LoadProblemKind.DuplicateId, path, $"Two relationships share the id {value}.");
            }
        }
    }

    private Relationship? ReadRelationship(JsonFields fields, RelationshipId id, string kind)
    {
        switch (kind)
        {
            case SceneNames.Anchored:
            {
                EntityId? entity = ReadEntityReference(fields, SceneNames.Entity, expected: null);
                return entity is { } target ? new Anchored(id, target) : null;
            }

            case SceneNames.Coincident:
            {
                PointRef? a = ReadPointRef(fields, SceneNames.A);
                PointRef? b = ReadPointRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Coincident(id, a, b) : null;
            }

            case SceneNames.Horizontal:
            {
                EdgeRef? edge = ReadEdgeRef(fields, SceneNames.Edge);
                return edge is not null ? new Horizontal(id, edge) : null;
            }

            case SceneNames.Vertical:
            {
                EdgeRef? edge = ReadEdgeRef(fields, SceneNames.Edge);
                return edge is not null ? new Vertical(id, edge) : null;
            }

            case SceneNames.Flush:
            {
                EdgeRef? a = ReadEdgeRef(fields, SceneNames.A);
                EdgeRef? b = ReadEdgeRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Flush(id, a, b) : null;
            }

            case SceneNames.AxisDistance:
            {
                PointRef? from = ReadPointRef(fields, SceneNames.From);
                PointRef? to = ReadPointRef(fields, SceneNames.To);
                Axis? axis = ReadAxis(fields);
                long? distance = ReadInteger(fields, SceneNames.Distance);
                return from is not null && to is not null && axis is { } along && distance is { } units
                    ? new AxisDistance(id, from, to, along, new Length(units))
                    : null;
            }

            case SceneNames.ParamValue:
            {
                ParamRef? param = ReadParamRef(fields, SceneNames.Param);
                long? value = ReadInteger(fields, SceneNames.Value);
                return param is not null && value is { } units ? new ParamValue(id, param, new Length(units)) : null;
            }

            case SceneNames.EqualParam:
            {
                ParamRef? a = ReadParamRef(fields, SceneNames.A);
                ParamRef? b = ReadParamRef(fields, SceneNames.B);
                return a is not null && b is not null ? new EqualParam(id, a, b) : null;
            }

            case SceneNames.Centered:
            {
                PointRef? middle = ReadPointRef(fields, SceneNames.Middle);
                PointRef? a = ReadPointRef(fields, SceneNames.A);
                PointRef? b = ReadPointRef(fields, SceneNames.B);
                Axis? axis = ReadAxis(fields);
                return middle is not null && a is not null && b is not null && axis is { } along
                    ? new Centered(id, middle, a, b, along)
                    : null;
            }

            case SceneNames.Parallel:
            {
                EdgeRef? a = ReadEdgeRef(fields, SceneNames.A);
                EdgeRef? b = ReadEdgeRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Geometry.Parallel(id, a, b) : null;
            }

            case SceneNames.Perpendicular:
            {
                EdgeRef? a = ReadEdgeRef(fields, SceneNames.A);
                EdgeRef? b = ReadEdgeRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Perpendicular(id, a, b) : null;
            }

            case SceneNames.AngleBetween:
            {
                EdgeRef? a = ReadEdgeRef(fields, SceneNames.A);
                EdgeRef? b = ReadEdgeRef(fields, SceneNames.B);
                long? angle = ReadInteger(fields, SceneNames.Angle);
                return a is not null && b is not null && angle is { } arcseconds
                    ? new AngleBetween(id, a, b, new Angle(arcseconds))
                    : null;
            }

            case SceneNames.Distance:
            {
                PointRef? a = ReadPointRef(fields, SceneNames.A);
                PointRef? b = ReadPointRef(fields, SceneNames.B);
                long? value = ReadInteger(fields, SceneNames.Value);
                return a is not null && b is not null && value is { } units
                    ? new Distance(id, a, b, new Length(units))
                    : null;
            }

            case SceneNames.PointOnEdge:
            {
                PointRef? point = ReadPointRef(fields, SceneNames.Point);
                EdgeRef? edge = ReadEdgeRef(fields, SceneNames.Edge);
                return point is not null && edge is not null ? new PointOnEdge(id, point, edge) : null;
            }

            case SceneNames.Symmetric:
            {
                PointRef? a = ReadPointRef(fields, SceneNames.A);
                PointRef? b = ReadPointRef(fields, SceneNames.B);
                EdgeRef? mirror = ReadEdgeRef(fields, SceneNames.Mirror);
                return a is not null && b is not null && mirror is not null ? new Symmetric(id, a, b, mirror) : null;
            }

            case SceneNames.Tangent:
            {
                EdgeRef? a = ReadEdgeRef(fields, SceneNames.A);
                EdgeRef? b = ReadEdgeRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Tangent(id, a, b) : null;
            }

            case SceneNames.Radius:
            {
                EntityId? arc = ReadEntityReference(fields, SceneNames.Arc, expected: null);
                long? value = ReadInteger(fields, SceneNames.Value);
                return arc is { } target && value is { } units ? new Radius(id, target, new Length(units)) : null;
            }

            default:
                Add(
                    LoadProblemKind.UnknownValue,
                    $"{fields.Path}/{SceneNames.Kind}",
                    $"\"{kind}\" is not a relationship kind this build knows. The kinds are: "
                    + $"{SceneNames.List(SceneNames.RelationshipKinds)}.");
                return null;
        }
    }

    private void RefuseUnsupportedRelationships(Sketch sketch, IGeometryUpdater updater)
    {
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            if (!updater.SupportedRelationships.Contains(relationship.GetType()))
            {
                Add(
                    LoadProblemKind.UnsupportedRelationship,
                    string.Empty,
                    $"The file holds a \"{NameOf(relationship)}\" relationship ({relationship.Id}), which this "
                    + "build of napkin cannot hold. It is reserved for the constraint solver, which is not in "
                    + "this build.");
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // References
    // -----------------------------------------------------------------------------------------

    private PointRef? ReadPointRef(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        string? kind = ReadText(fields, SceneNames.Kind);
        PointRef? reference = kind switch
        {
            null => null,
            SceneNames.Node => ReadEntityReference(fields, SceneNames.Node, typeof(Node)) is { } node
                ? new NodeRef(node)
                : null,
            SceneNames.Corner => ReadCornerRef(fields),
            SceneNames.Center => ReadEntityReference(fields, SceneNames.Box, typeof(Box)) is { } box
                ? new CenterRef(box)
                : null,
            _ => UnknownRefKind<PointRef>(fields, kind, "a point", SceneNames.Node, SceneNames.Corner, SceneNames.Center),
        };

        RejectUnknownFields(fields);
        return reference;
    }

    private PointRef? ReadCornerRef(JsonFields fields)
    {
        EntityId? box = ReadEntityReference(fields, SceneNames.Box, typeof(Box));
        string? corner = ReadText(fields, SceneNames.Corner);
        if (box is not { } target || corner is null)
        {
            return null;
        }

        if (!SceneNames.TryCorner(corner, out BoxCorner which))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.Corner}",
                $"\"{corner}\" is not a corner. The corners are: southWest, southEast, northEast, northWest.");
            return null;
        }

        return new CornerRef(target, which);
    }

    private EdgeRef? ReadEdgeRef(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        string? kind = ReadText(fields, SceneNames.Kind);
        EdgeRef? reference = kind switch
        {
            null => null,
            SceneNames.Segment => ReadEntityReference(fields, SceneNames.Segment, typeof(Segment)) is { } segment
                ? new SegmentRef(segment)
                : null,
            SceneNames.BoxEdge => ReadBoxEdgeRef(fields),
            _ => UnknownRefKind<EdgeRef>(fields, kind, "an edge", SceneNames.Segment, SceneNames.BoxEdge),
        };

        RejectUnknownFields(fields);
        return reference;
    }

    private EdgeRef? ReadBoxEdgeRef(JsonFields fields)
    {
        EntityId? box = ReadEntityReference(fields, SceneNames.Box, typeof(Box));
        string? edge = ReadText(fields, SceneNames.Edge);
        if (box is not { } target || edge is null)
        {
            return null;
        }

        if (!SceneNames.TryEdge(edge, out BoxEdge which))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.Edge}",
                $"\"{edge}\" is not an edge. The edges are: south, east, north, west.");
            return null;
        }

        return new BoxEdgeRef(target, which);
    }

    private ParamRef? ReadParamRef(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        ParamRef? reference = ReadParamRefBody(fields);
        RejectUnknownFields(fields);
        return reference;
    }

    private ParamRef? ReadParamRefBody(JsonFields fields)
    {
        string? kind = ReadText(fields, SceneNames.Kind);
        return kind switch
        {
            null => null,
            SceneNames.BoxWidth => ReadEntityReference(fields, SceneNames.Box, typeof(Box)) is { } width
                ? new BoxWidthRef(width)
                : null,
            SceneNames.BoxHeight => ReadEntityReference(fields, SceneNames.Box, typeof(Box)) is { } height
                ? new BoxHeightRef(height)
                : null,
            SceneNames.BoxDepth => ReadEntityReference(fields, SceneNames.Box, typeof(Box)) is { } depth
                ? new BoxDepthRef(depth)
                : null,
            SceneNames.SegmentLength => ReadEntityReference(fields, SceneNames.Segment, typeof(Segment)) is { } segment
                ? new SegmentLengthRef(segment)
                : null,
            _ => UnknownRefKind<ParamRef>(
                fields, kind, "a size", SceneNames.BoxWidth, SceneNames.BoxHeight, SceneNames.BoxDepth, SceneNames.SegmentLength),
        };
    }

    private Measurand? ReadMeasurand(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        Measurand? measurand;
        if (fields.Peek(SceneNames.Kind) == SceneNames.AxisMeasurand)
        {
            ReadText(fields, SceneNames.Kind);
            PointRef? from = ReadPointRef(fields, SceneNames.From);
            PointRef? to = ReadPointRef(fields, SceneNames.To);
            Axis? axis = ReadAxis(fields);
            measurand = from is not null && to is not null && axis is { } along
                ? new AxisMeasurand(from, to, along)
                : null;
        }
        else
        {
            // A size measured directly: the measurand is the size reference itself, as the design's
            // §6 example writes it.
            ParamRef? param = ReadParamRefBody(fields);
            measurand = param is not null ? new ParamMeasurand(param) : null;
        }

        RejectUnknownFields(fields);
        return measurand;
    }

    private T? UnknownRefKind<T>(JsonFields fields, string kind, string what, params string[] kinds)
        where T : class
    {
        Add(
            LoadProblemKind.UnknownValue,
            $"{fields.Path}/{SceneNames.Kind}",
            $"\"{kind}\" is not a way of referring to {what}. The ways are: {SceneNames.List(kinds)}.");
        return null;
    }

    private EntityId? ReadEntityReference(JsonFields fields, string name, Type? expected)
    {
        Guid? id = ReadId(fields, name);
        if (id is not { } value)
        {
            return null;
        }

        EntityId entityId = new(value);
        kindChecks.Add(new KindCheck($"{fields.Path}/{name}", entityId, expected));
        return entityId;
    }

    private void ResolveReferences()
    {
        foreach (KindCheck check in kindChecks)
        {
            if (!entities.TryGetValue(check.Id, out Entity? entity))
            {
                Add(
                    LoadProblemKind.DanglingReference,
                    check.Path,
                    $"Entity {check.Id.Value} is not in this file.");
                continue;
            }

            if (check.Expected is not null && !check.Expected.IsInstanceOfType(entity))
            {
                Add(
                    LoadProblemKind.WrongReferenceKind,
                    check.Path,
                    $"Entity {check.Id.Value} is a {entity.GetType().Name.ToLowerInvariant()}, and this reference "
                    + $"needs a {check.Expected.Name.ToLowerInvariant()}.");
            }
        }

        foreach (DriveCheck check in driveChecks)
        {
            if (!relationships.ContainsKey(check.Id))
            {
                Add(
                    LoadProblemKind.DanglingReference,
                    check.Path,
                    $"Relationship {check.Id.Value} is not in this file, so nothing owns this dimension's number.");
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // JSON primitives
    // -----------------------------------------------------------------------------------------

    private JsonFields? ReadFields(JsonElement element, string path, string what)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Add(LoadProblemKind.Malformed, path, $"Expected {what} to be an object, and found {Describe(element)}.");
            return null;
        }

        JsonFields fields = new(path);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!fields.TryAdd(property))
            {
                Add(
                    LoadProblemKind.DuplicateField,
                    $"{path}/{property.Name}",
                    $"The field \"{property.Name}\" appears more than once.");
            }
        }

        return fields;
    }

    private void RejectUnknownFields(JsonFields fields)
    {
        foreach (string name in fields.Unused)
        {
            Add(
                LoadProblemKind.UnknownField,
                $"{fields.Path}/{name}",
                $"\"{name}\" is not a field this format defines. napkin reads its own format strictly: "
                + "with the format version pinned there is no legitimate reason for an unknown field.");
        }
    }

    private JsonElement? Take(JsonFields fields, string name)
    {
        if (fields.Take(name) is { } element)
        {
            return element;
        }

        Add(
            LoadProblemKind.MissingField,
            $"{fields.Path}/{name}",
            $"The field \"{name}\" is required and is not there.");
        return null;
    }

    private IEnumerable<(JsonElement Element, string Path)> ReadArray(JsonFields fields, string name)
    {
        JsonElement? element = Take(fields, name);
        if (element is not { } value)
        {
            yield break;
        }

        string path = $"{fields.Path}/{name}";
        if (value.ValueKind != JsonValueKind.Array)
        {
            Add(LoadProblemKind.Malformed, path, $"Expected \"{name}\" to be an array, and found {Describe(value)}.");
            yield break;
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            yield return (item, $"{path}/{index.ToString(CultureInfo.InvariantCulture)}");
            index++;
        }
    }

    private JsonFields? ReadObject(JsonFields fields, string name)
    {
        JsonElement? element = Take(fields, name);
        return element is { } value ? ReadFields(value, $"{fields.Path}/{name}", $"\"{name}\"") : null;
    }

    private Point2? ReadPoint(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        long? x = ReadInteger(fields, SceneNames.X);
        long? y = ReadInteger(fields, SceneNames.Y);
        RejectUnknownFields(fields);

        return x is { } across && y is { } up ? new Point2(new Length(across), new Length(up)) : null;
    }

    private Axis? ReadAxis(JsonFields fields)
    {
        string? text = ReadText(fields, SceneNames.Axis);
        if (text is null)
        {
            return null;
        }

        if (!SceneNames.TryAxis(text, out Axis axis))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.Axis}",
                $"\"{text}\" is not an axis. The axes are: x, y.");
            return null;
        }

        return axis;
    }

    private long? ReadInteger(JsonFields fields, string name)
    {
        JsonElement? element = Take(fields, name);
        if (element is not { } value)
        {
            return null;
        }

        string path = $"{fields.Path}/{name}";
        if (value.ValueKind != JsonValueKind.Number)
        {
            Add(
                LoadProblemKind.Malformed,
                path,
                $"Expected \"{name}\" to be a whole number, and found {Describe(value)}.");
            return null;
        }

        string raw = value.GetRawText();
        if (raw.Contains('.', StringComparison.Ordinal)
            || raw.Contains('e', StringComparison.OrdinalIgnoreCase))
        {
            Add(
                LoadProblemKind.NotAnInteger,
                path,
                $"\"{name}\" is {raw}. Lengths and angles are whole numbers of the file's stored units "
                + $"({FormatStamp.InchGrid} and {FormatStamp.Arcsecond}); a decimal is never a length.");
            return null;
        }

        if (!value.TryGetInt64(out long number))
        {
            Add(
                LoadProblemKind.OutOfRange,
                path,
                $"\"{name}\" is {raw}, which does not fit in a 64-bit integer.");
            return null;
        }

        return number;
    }

    private string? ReadText(JsonFields fields, string name)
    {
        JsonElement? element = Take(fields, name);
        if (element is not { } value)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Add(
                LoadProblemKind.Malformed,
                $"{fields.Path}/{name}",
                $"Expected \"{name}\" to be text, and found {Describe(value)}.");
            return null;
        }

        return value.GetString();
    }

    /// <summary>
    /// Reads a field the format defines as text <em>or</em> <see langword="null"/> — a part's
    /// stock name and its species, neither of which every part has.
    /// </summary>
    /// <returns>Whether the field was read without a problem, and the text it held.</returns>
    private (bool Read, string? Text) ReadTextOrNull(JsonFields fields, string name)
    {
        JsonElement? element = Take(fields, name);
        if (element is not { } value)
        {
            return (false, null);
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return (true, null);
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Add(
                LoadProblemKind.Malformed,
                $"{fields.Path}/{name}",
                $"Expected \"{name}\" to be text or null, and found {Describe(value)}.");
            return (false, null);
        }

        return (true, value.GetString());
    }

    private Guid? ReadId(JsonFields fields, string name)
    {
        JsonElement? element = Take(fields, name);
        return element is { } value ? AsId(value, $"{fields.Path}/{name}", name) : null;
    }

    private Guid? AsId(JsonElement element, string path, string name)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            Add(
                LoadProblemKind.Malformed,
                path,
                $"Expected \"{name}\" to be an id in text, and found {Describe(element)}.");
            return null;
        }

        string text = element.GetString() ?? string.Empty;
        if (!Guid.TryParseExact(text, "D", out Guid id))
        {
            Add(
                LoadProblemKind.NotAnId,
                path,
                $"\"{text}\" is not an id. Ids are GUIDs written as 8-4-4-4-12 hexadecimal digits, "
                + "for example 0192f1a0-0000-4000-8000-000000000001.");
            return null;
        }

        return id;
    }

    private readonly record struct KindCheck(string Path, EntityId Id, Type? Expected);

    private readonly record struct DriveCheck(string Path, RelationshipId Id);

    /// <summary>
    /// One JSON object's fields, and which of them have been read. What is left over when the
    /// reader has taken everything it knows about is, by definition, an unknown field.
    /// </summary>
    private sealed class JsonFields(string path)
    {
        private readonly Dictionary<string, JsonElement> values = new(StringComparer.Ordinal);
        private readonly List<string> unused = [];

        internal string Path { get; } = path;

        internal IReadOnlyList<string> Unused => unused;

        internal bool TryAdd(JsonProperty property)
        {
            if (!values.TryAdd(property.Name, property.Value))
            {
                return false;
            }

            unused.Add(property.Name);
            return true;
        }

        internal JsonElement? Take(string name)
        {
            if (!values.TryGetValue(name, out JsonElement element))
            {
                return null;
            }

            unused.Remove(name);
            return element;
        }

        /// <summary>The text of a field without taking it, for a shape that dispatches on one.</summary>
        internal string? Peek(string name)
            => values.TryGetValue(name, out JsonElement element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
    }
}
