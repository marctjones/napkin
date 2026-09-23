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
        // anything evaluates geometry, because the checker reads a feature off whatever entity an
        // id names and cannot be asked about a feature of a node.
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
        Point3? anchor = ReadPoint3(fields, SceneNames.Anchor);
        long? width = ReadInteger(fields, SceneNames.Width);
        long? height = ReadInteger(fields, SceneNames.Height);
        long? depth = ReadInteger(fields, SceneNames.Depth);
        BoxFace? faceUp = ReadFaceUp(fields);
        long? rotation = ReadInteger(fields, SceneNames.Rotation);
        (bool partRead, Part? part) = ReadPart(fields);
        (bool cutsRead, ImmutableList<Cut> cuts) = ReadCuts(fields);

        width = RefuseNonPositive(fields, SceneNames.Width, width);
        height = RefuseNonPositive(fields, SceneNames.Height, height);
        depth = RefuseNonPositive(fields, SceneNames.Depth, depth);

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

        return anchor is { } corner && width is { } wide && height is { } tall && depth is { } deep
            && faceUp is { } up && rotation is { } turn && partRead && cutsRead
            ? new Box(id, layer, corner, new Length(wide), new Length(tall), new Length(deep), up, new Angle(turn))
            {
                Part = part,
                Cuts = cuts,
            }
            : null;
    }

    /// <summary>
    /// One of a box's three sizes, refused when it is zero or negative: a box has an extent along
    /// every one of its local axes (invariants 2 and 10).
    /// </summary>
    private long? RefuseNonPositive(JsonFields fields, string name, long? size)
    {
        if (size is not { } units || units > 0)
        {
            return size;
        }

        Add(
            LoadProblemKind.InvalidValue,
            $"{fields.Path}/{name}",
            $"A box's {name} must be greater than zero; this one is {units.ToString(CultureInfo.InvariantCulture)} units.");
        return null;
    }

    /// <summary>
    /// Which of a box's six local faces points up (<c>docs/design/assembly-model.md</c> &#xA7;1.3):
    /// <c>top</c> for a box lying as drawn.
    /// </summary>
    private BoxFace? ReadFaceUp(JsonFields fields)
    {
        string? text = ReadText(fields, SceneNames.FaceUp);
        if (text is null)
        {
            return null;
        }

        if (!SceneNames.TryFace(text, out BoxFace face))
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.FaceUp}",
                $"\"{text}\" is not a face of a box. The faces are: {SceneNames.List(SceneNames.BoxFaces)}.");
            return null;
        }

        return face;
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
    private (bool Read, Part? Part) ReadPart(JsonFields fields)
    {
        JsonElement? element = Take(fields, SceneNames.Part);
        if (element is not { } value)
        {
            return (false, null);
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return (true, null);
        }

        JsonFields? part = ReadFields(value, $"{fields.Path}/{SceneNames.Part}", $"\"{SceneNames.Part}\"");
        if (part is null)
        {
            return (false, null);
        }

        // The stock name is not checked against this build's materials library, deliberately: a
        // file is refused for being malformed, never for naming something this build has not heard
        // of (docs/design/parts-and-cut-list.md §2.2).
        (bool stockRead, string? stock) = ReadTextOrNull(part, SceneNames.Stock);
        (bool speciesRead, string? species) = ReadTextOrNull(part, SceneNames.Species);
        long? quantity = ReadInteger(part, SceneNames.Quantity);
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

        // A part's third dimension is its box's depth, which the box stores (format version 4,
        // assembly-model §1.2). A version-3 part's "outOfPlane" is therefore an unknown field here,
        // refused like any other, rather than a second copy of a number the box already holds.
        return stockRead && speciesRead && quantity is { } pieces && planAxes is { } axes
            ? (true, new Part(stock, species, (int)pieces, axes))
            : (false, null);
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
                PlaceRef? a = ReadPlaceRef(fields, SceneNames.A);
                PlaceRef? b = ReadPlaceRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Coincident(id, a, b) : null;
            }

            case SceneNames.Horizontal:
            {
                PlaceRef? edge = ReadLineRef(fields, SceneNames.Edge);
                return edge is not null ? new Horizontal(id, edge) : null;
            }

            case SceneNames.Vertical:
            {
                PlaceRef? edge = ReadLineRef(fields, SceneNames.Edge);
                return edge is not null ? new Vertical(id, edge) : null;
            }

            case SceneNames.Flush:
            {
                PlaceRef? a = ReadPlaceRef(fields, SceneNames.A);
                PlaceRef? b = ReadPlaceRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Flush(id, a, b) : null;
            }

            case SceneNames.AxisDistance:
            {
                PlaceRef? from = ReadPlaceRef(fields, SceneNames.From);
                PlaceRef? to = ReadPlaceRef(fields, SceneNames.To);
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
                PlaceRef? middle = ReadPlaceRef(fields, SceneNames.Middle);
                PlaceRef? a = ReadPlaceRef(fields, SceneNames.A);
                PlaceRef? b = ReadPlaceRef(fields, SceneNames.B);
                Axis? axis = ReadAxis(fields);
                return middle is not null && a is not null && b is not null && axis is { } along
                    ? new Centered(id, middle, a, b, along)
                    : null;
            }

            case SceneNames.Parallel:
            {
                PlaceRef? a = ReadLineRef(fields, SceneNames.A);
                PlaceRef? b = ReadLineRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Geometry.Parallel(id, a, b) : null;
            }

            case SceneNames.Perpendicular:
            {
                PlaceRef? a = ReadLineRef(fields, SceneNames.A);
                PlaceRef? b = ReadLineRef(fields, SceneNames.B);
                return a is not null && b is not null ? new Perpendicular(id, a, b) : null;
            }

            case SceneNames.AngleBetween:
            {
                PlaceRef? a = ReadLineRef(fields, SceneNames.A);
                PlaceRef? b = ReadLineRef(fields, SceneNames.B);
                long? angle = ReadInteger(fields, SceneNames.Angle);
                return a is not null && b is not null && angle is { } arcseconds
                    ? new AngleBetween(id, a, b, new Angle(arcseconds))
                    : null;
            }

            case SceneNames.Distance:
            {
                PlaceRef? a = ReadPlaceRef(fields, SceneNames.A);
                PlaceRef? b = ReadPlaceRef(fields, SceneNames.B);
                long? value = ReadInteger(fields, SceneNames.Value);
                return a is not null && b is not null && value is { } units
                    ? new Distance(id, a, b, new Length(units))
                    : null;
            }

            case SceneNames.PointOnEdge:
            {
                PlaceRef? point = ReadPlaceRef(fields, SceneNames.Point);
                PlaceRef? edge = ReadLineRef(fields, SceneNames.Edge);
                return point is not null && edge is not null ? new PointOnEdge(id, point, edge) : null;
            }

            case SceneNames.Symmetric:
            {
                PlaceRef? a = ReadPlaceRef(fields, SceneNames.A);
                PlaceRef? b = ReadPlaceRef(fields, SceneNames.B);
                PlaceRef? mirror = ReadLineRef(fields, SceneNames.Mirror);
                return a is not null && b is not null && mirror is not null ? new Symmetric(id, a, b, mirror) : null;
            }

            case SceneNames.Tangent:
            {
                PlaceRef? a = ReadLineRef(fields, SceneNames.A);
                PlaceRef? b = ReadLineRef(fields, SceneNames.B);
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

    /// <summary>
    /// A place — a node, a segment, a box's centre or a feature of a box — in a slot that takes
    /// any of them (<c>docs/design/assembly-model.md</c> &#xA7;2.2). Whether the places a
    /// relationship pairs can be compared at all is judged afterwards, by what each fixes, in
    /// <see cref="Sketch.Validate"/> (&#xA7;2.3, <see cref="PlaceRules"/>): the file's shape is this
    /// reader's business and the pairing is the kernel's.
    /// </summary>
    private PlaceRef? ReadPlaceRef(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        string? kind = ReadText(fields, SceneNames.Kind);
        PlaceRef? reference = kind switch
        {
            null => null,
            SceneNames.Node => ReadEntityReference(fields, SceneNames.Node, typeof(Node)) is { } node
                ? new NodeRef(node)
                : null,
            SceneNames.Segment => ReadSegmentRef(fields),
            SceneNames.Center => ReadEntityReference(fields, SceneNames.Box, typeof(Box)) is { } box
                ? new CenterRef(box)
                : null,
            SceneNames.Feature => ReadFeatureRef(fields),
            _ => UnknownPlaceKind(fields, kind, "a place", SceneNames.PlaceKinds),
        };

        RejectUnknownFields(fields);
        return reference;
    }

    /// <summary>
    /// A line — a segment, or a feature of a box — in the slots of the kinds that are about lines:
    /// <c>horizontal</c>, <c>vertical</c> and the solver's angular kinds. A node or a centre has no
    /// direction for them to be about.
    /// </summary>
    private PlaceRef? ReadLineRef(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        string? kind = ReadText(fields, SceneNames.Kind);
        PlaceRef? reference = kind switch
        {
            null => null,
            SceneNames.Segment => ReadSegmentRef(fields),
            SceneNames.Feature => ReadFeatureRef(fields),
            _ => UnknownPlaceKind(fields, kind, "a line", SceneNames.LineKinds),
        };

        RejectUnknownFields(fields);
        return reference;
    }

    private PlaceRef? ReadSegmentRef(JsonFields fields)
        => ReadEntityReference(fields, SceneNames.Segment, typeof(Segment)) is { } segment ? new SegmentRef(segment) : null;

    /// <summary>
    /// A feature of a box: <c>{ "kind": "feature", "box": …, "faces": ["south", "west"] }</c> — one
    /// face, the edge where two meet, or the vertex where three meet (&#xA7;1.5).
    /// </summary>
    /// <remarks>
    /// The faces are held to invariant 12 here, where the file can be told exactly what is wrong
    /// with them: one to three faces the format spells, none repeated, no two opposite, and in
    /// <see cref="BoxFace"/> order. <strong>The order is judged, not fixed</strong>, as a box's cuts
    /// are: a feature is the set of its faces and has one spelling, so a file that spells it
    /// another way is refused rather than quietly re-ordered.
    /// </remarks>
    private PlaceRef? ReadFeatureRef(JsonFields fields)
    {
        EntityId? box = ReadEntityReference(fields, SceneNames.Box, typeof(Box));
        BoxFeature? feature = ReadFaces(fields);
        return box is { } target && feature is { } which ? new FeatureRef(target, which) : null;
    }

    private BoxFeature? ReadFaces(JsonFields fields)
    {
        string path = $"{fields.Path}/{SceneNames.Faces}";
        int before = problems.Count;
        List<BoxFace> faces = [];

        foreach ((JsonElement item, string itemPath) in ReadArray(fields, SceneNames.Faces))
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                Add(LoadProblemKind.Malformed, itemPath, $"Expected a face, as text, and found {Describe(item)}.");
                continue;
            }

            string text = item.GetString() ?? string.Empty;
            if (!SceneNames.TryFace(text, out BoxFace face))
            {
                Add(
                    LoadProblemKind.UnknownValue,
                    itemPath,
                    $"\"{text}\" is not a face of a box. The faces are: {SceneNames.List(SceneNames.BoxFaces)}.");
                continue;
            }

            faces.Add(face);
        }

        // A missing field, a field that is not an array, or a face that is not one of the six has
        // already been reported, and there is no set of faces to judge.
        if (problems.Count > before)
        {
            return null;
        }

        if (faces.Count is < 1 or > 3)
        {
            Add(
                LoadProblemKind.InvalidValue,
                path,
                $"A feature is one face, the edge where two faces meet, or the vertex where three meet; this one names "
                + $"{faces.Count.ToString(CultureInfo.InvariantCulture)}.");
            return null;
        }

        for (int i = 0; i < faces.Count; i++)
        {
            for (int j = i + 1; j < faces.Count; j++)
            {
                if (faces[i] == faces[j])
                {
                    Add(
                        LoadProblemKind.InvalidValue,
                        path,
                        $"A feature names each of its faces once; \"{SceneNames.Of(faces[i])}\" is named twice.");
                    return null;
                }

                if (Opposite(faces[i], faces[j]))
                {
                    Add(
                        LoadProblemKind.InvalidValue,
                        path,
                        $"\"{SceneNames.Of(faces[i])}\" and \"{SceneNames.Of(faces[j])}\" are opposite faces of a box and "
                        + "never meet, so no feature has both.");
                    return null;
                }
            }
        }

        for (int i = 1; i < faces.Count; i++)
        {
            if (faces[i - 1] > faces[i])
            {
                Add(
                    LoadProblemKind.InvalidValue,
                    path,
                    $"A feature's faces are written in the order {SceneNames.List(SceneNames.BoxFaces)}, so that a "
                    + $"file has one spelling of one feature; this one has \"{SceneNames.Of(faces[i])}\" after "
                    + $"\"{SceneNames.Of(faces[i - 1])}\". A file out of order is refused rather than quietly sorted.");
                return null;
            }
        }

        return faces.Count switch
        {
            1 => BoxFeature.Face(faces[0]),
            2 => BoxFeature.Edge(faces[0], faces[1]),
            _ => BoxFeature.Vertex(faces[0], faces[1], faces[2]),
        };
    }

    // Two faces on the same local axis: south and north, east and west, bottom and top. BoxFace
    // declares them in that pairing, south-east-north-west then bottom-top, so a side's opposite is
    // two further round and a cap's is the other cap.
    private static bool Opposite(BoxFace a, BoxFace b)
        => a != b && (a, b) switch
        {
            (<= BoxFace.West, <= BoxFace.West) => Math.Abs((int)a - (int)b) == 2,
            (>= BoxFace.Bottom, >= BoxFace.Bottom) => true,
            _ => false,
        };

    /// <summary>
    /// A reference kind this build does not read — and for the two format version 4 removed, what
    /// replaced them.
    /// </summary>
    private PlaceRef? UnknownPlaceKind(JsonFields fields, string kind, string what, string[] kinds)
    {
        if (kind is SceneNames.RemovedCorner or SceneNames.RemovedBoxEdge)
        {
            Add(
                LoadProblemKind.UnknownValue,
                $"{fields.Path}/{SceneNames.Kind}",
                $"\"{kind}\" is a reference kind of format version 3, which version 4 replaced: a box's corner or edge "
                + $"is now a \"{SceneNames.Feature}\" naming the faces that meet there — a corner of the blank as "
                + "[\"south\", \"west\"], and a plan edge as the side face [\"north\"].");
            return null;
        }

        return UnknownRefKind<PlaceRef>(fields, kind, what, kinds);
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
            PlaceRef? from = ReadPlaceRef(fields, SceneNames.From);
            PlaceRef? to = ReadPlaceRef(fields, SceneNames.To);
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

    /// <summary>
    /// A point in space: a box's anchor (format version 4). A node stays a plan point, at the plan
    /// datum, so it keeps <see cref="ReadPoint"/> (<c>docs/design/assembly-model.md</c> &#xA7;1.4).
    /// </summary>
    private Point3? ReadPoint3(JsonFields parent, string name)
    {
        JsonFields? fields = ReadObject(parent, name);
        if (fields is null)
        {
            return null;
        }

        long? x = ReadInteger(fields, SceneNames.X);
        long? y = ReadInteger(fields, SceneNames.Y);
        long? z = ReadInteger(fields, SceneNames.Z);
        RejectUnknownFields(fields);

        return x is { } across && y is { } up && z is { } high
            ? new Point3(new Length(across), new Length(up), new Length(high))
            : null;
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
                $"\"{text}\" is not an axis. The axes are: x, y, z.");
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
