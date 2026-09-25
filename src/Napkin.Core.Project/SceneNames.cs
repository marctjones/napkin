using Napkin.Core.Geometry;

namespace Napkin.Core.Project;

/// <summary>
/// Every name the scene format uses, in one place, and the read direction of every spelled-out
/// value.
/// </summary>
/// <remarks>
/// The M2 writer is the mirror image of the reader: it writes these same constants and the
/// inverse of these same mappings, which is why they live here rather than in the binder. A name
/// changed here changes both directions at once, and the format version is bumped because the
/// file means something new (DESIGN.md &#xA7;6.4).
/// </remarks>
internal static class SceneNames
{
    // The document.
    internal const string FormatVersion = "formatVersion";
    internal const string Units = "units";
    internal const string UnitsLength = "length";
    internal const string UnitsAngle = "angle";
    internal const string Layers = "layers";
    internal const string Entities = "entities";
    internal const string Relationships = "relationships";
    internal const string FastenerChoices = "fastenerChoices";
    internal const string Supplies = "supplies";

    // The project's building inputs (format version 6).
    internal const string Code = "code";
    internal const string CodePack = "pack";
    internal const string CodeRevision = "revision";
    internal const string CodeMode = "mode";
    internal const string CodeLockedOn = "lockedOn";
    internal const string CodeLocked = "locked";
    internal const string CodeFollowing = "following";
    internal const string Site = "site";
    internal const string SiteGroundSnowLoad = "groundSnowLoad";
    internal const string SiteUltimateWindSpeed = "ultimateWindSpeed";
    internal const string SiteSeismicDesignCategory = "seismicDesignCategory";
    internal const string SiteFrostDepth = "frostDepth";
    internal const string SiteBuildingWidth = "buildingWidth";
    internal const string SiteSource = "source";
    internal const string SiteSourceText = "text";
    internal const string SiteSourceOn = "on";

    // A wall's inputs, on a box (format version 6).
    internal const string Wall = "wall";
    internal const string WallSupports = "supports";
    internal const string WallStudSpacing = "studSpacing";

    // Shared.
    internal const string Id = "id";
    internal const string Name = "name";
    internal const string Type = "type";
    internal const string Kind = "kind";
    internal const string Layer = "layer";
    internal const string X = "x";
    internal const string Y = "y";
    internal const string Z = "z";
    internal const string Axis = "axis";
    internal const string Value = "value";

    // Entities.
    internal const string Node = "node";
    internal const string Segment = "segment";
    internal const string Box = "box";
    internal const string Dimension = "dimension";
    internal const string Position = "position";
    internal const string Start = "start";
    internal const string End = "end";
    internal const string Anchor = "anchor";
    internal const string Width = "width";
    internal const string Height = "height";
    internal const string FaceUp = "faceUp";
    internal const string Rotation = "rotation";
    internal const string Measures = "measures";
    internal const string Drives = "drives";
    internal const string Placement = "placement";
    internal const string Offset = "offset";
    internal const string Side = "side";

    // A part, on a box (format version 2).
    internal const string Part = "part";
    internal const string Stock = "stock";
    internal const string Species = "species";
    internal const string Quantity = "quantity";
    internal const string PlanAxes = "planAxes";

    // The three finished dimensions a part has. "length" is also the units object's length field
    // and "width" also a box's stored width, which is the point: a plan axis names one of these.
    internal const string PartLength = "length";
    internal const string PartWidth = "width";
    internal const string PartThickness = "thickness";

    // The cuts on a box's blank (format version 3). "corner" and "edge" name a corner and an edge
    // of the blank in its own local frame, and "radius" is the relationship kind's spelling reused
    // for the value a rounded corner stores — one spelling per word, as "length" is both a unit
    // and a part's dimension. "depth" is both a curved edge's depth and a box's own (format
    // version 4): each is how far something reaches along the axis it is measured on.
    internal const string Cuts = "cuts";
    internal const string Corner = "corner";
    internal const string Edge = "edge";
    internal const string CornerCut = "cornerCut";
    internal const string RoundedCorner = "roundedCorner";
    internal const string CurvedEdge = "curvedEdge";
    internal const string AlongX = "alongX";
    internal const string AlongY = "alongY";
    internal const string CutRadius = "radius";
    internal const string Depth = "depth";
    internal const string Bow = "bow";
    internal const string Outward = "outward";
    internal const string Inward = "inward";

    // Corners, edges and sides, in the box's own local frame.
    internal const string SouthWest = "southWest";
    internal const string SouthEast = "southEast";
    internal const string NorthEast = "northEast";
    internal const string NorthWest = "northWest";
    internal const string South = "south";
    internal const string East = "east";
    internal const string North = "north";
    internal const string West = "west";

    // The two faces a plan cannot see, which with the four sides above are the six faces of a box
    // in its own local frame (format version 4).
    internal const string Bottom = "bottom";
    internal const string Top = "top";

    // References: to a place (format version 4) and to a size.
    internal const string Center = "center";
    internal const string Feature = "feature";
    internal const string Faces = "faces";
    internal const string BoxWidth = "boxWidth";
    internal const string BoxHeight = "boxHeight";
    internal const string BoxDepth = "boxDepth";
    internal const string SegmentLength = "segmentLength";
    internal const string AxisMeasurand = "axis";

    // The two reference kinds format version 4 removed in favour of "feature", spelled here only so
    // that a file still using one is told what replaced it.
    internal const string RemovedCorner = "corner";
    internal const string RemovedBoxEdge = "boxEdge";

    // Relationship kinds.
    internal const string Anchored = "anchored";
    internal const string Coincident = "coincident";
    internal const string Horizontal = "horizontal";
    internal const string Vertical = "vertical";
    internal const string Flush = "flush";
    internal const string AxisDistance = "axisDistance";
    internal const string ParamValue = "paramValue";
    internal const string EqualParam = "equalParam";
    internal const string Centered = "centered";
    internal const string Parallel = "parallel";
    internal const string Perpendicular = "perpendicular";
    internal const string AngleBetween = "angleBetween";
    internal const string Distance = "distance";
    internal const string PointOnEdge = "pointOnEdge";
    internal const string Symmetric = "symmetric";
    internal const string Tangent = "tangent";
    internal const string Radius = "radius";
    internal const string Joint = "joint";

    // A joint (format version 5), and the joinery lists.
    internal const string Receiving = "receiving";
    internal const string Inserted = "inserted";
    internal const string Fastening = "fastening";
    internal const string Count = "count";
    internal const string PocketFace = "pocketFace";
    internal const string Glue = "glue";
    internal const string Hardware = "hardware";
    internal const string Thickness = "thickness";
    internal const string Size = "size";
    internal const string PackSize = "packSize";
    internal const string Item = "item";
    internal const string Note = "note";

    // Relationship fields.
    internal const string A = "a";
    internal const string B = "b";
    internal const string Entity = "entity";
    internal const string From = "from";
    internal const string To = "to";
    internal const string Middle = "middle";
    internal const string Mirror = "mirror";
    internal const string Param = "param";
    internal const string Point = "point";
    internal const string Arc = "arc";
    internal const string Angle = "angle";

    /// <summary>Every entity type the format spells out, for a message that lists them.</summary>
    internal static readonly string[] EntityTypes = [Box, Dimension, Node, Segment];

    /// <summary>The three names a part's plan axis can carry, for a message that lists them.</summary>
    internal static readonly string[] PartDimensions = [PartLength, PartWidth, PartThickness];

    /// <summary>Every kind of cut the format spells out, for a message that lists them.</summary>
    internal static readonly string[] CutKinds = [CornerCut, CurvedEdge, RoundedCorner];

    /// <summary>
    /// The six faces of a box in <see cref="BoxFace"/> order, which is the order a feature's
    /// <c>faces</c> are written in, for a message that says what the order is.
    /// </summary>
    internal static readonly string[] BoxFaces = [South, East, North, West, Bottom, Top];

    /// <summary>The four ways of referring to a place, for a message that lists them.</summary>
    internal static readonly string[] PlaceKinds = [Center, Feature, Node, Segment];

    /// <summary>The two ways of referring to a line, for a message that lists them.</summary>
    internal static readonly string[] LineKinds = [Feature, Segment];

    /// <summary>The two ways a curved edge bows, for a message that lists them.</summary>
    internal static readonly string[] Bows = [Outward, Inward];

    /// <summary>
    /// The eight sites a cut can be at, in the fixed order <see cref="CutSite"/> sorts them and a
    /// box's <c>cuts</c> array is written in, for a message that says what the order is.
    /// </summary>
    internal static readonly string[] Sites = [SouthWest, SouthEast, NorthEast, NorthWest, South, East, North, West];

    /// <summary>Every relationship kind the format spells out, for a message that lists them.</summary>
    internal static readonly string[] RelationshipKinds =
    [
        AngleBetween, Anchored, AxisDistance, Centered, Coincident, Distance, EqualParam, Flush,
        Horizontal, Joint, ParamValue, Parallel, Perpendicular, PointOnEdge, Radius, Symmetric, Tangent,
        Vertical,
    ];

    /// <summary>The joint types the format spells out, for a message that lists them.</summary>
    internal static readonly string[] JointTypes = ["butt", "groove", "rabbet", "halfLap", "tabletop"];

    /// <summary>The fastenings the format spells out, for a message that lists them.</summary>
    internal static readonly string[] FasteningKinds =
        ["none", "pocketScrews", "screws", "brads", "nails", "dowels", "biscuits", "clips"];

    /// <summary>The fastener kinds a choice can be for, for a message that lists them.</summary>
    internal static readonly string[] FastenerKinds =
        ["pocketScrew", "woodScrew", "brad", "nail", "dowel", "biscuit", "tabletopClip"];

    internal static bool TryJointType(string text, out JointType type)
    {
        (bool ok, JointType found) = text switch
        {
            "butt" => (true, JointType.Butt),
            "groove" => (true, JointType.Groove),
            "rabbet" => (true, JointType.Rabbet),
            "halfLap" => (true, JointType.HalfLap),
            "tabletop" => (true, JointType.Tabletop),
            _ => (false, default),
        };
        type = found;
        return ok;
    }

    internal static bool TryFasteningKind(string text, out FasteningKind kind)
    {
        (bool ok, FasteningKind found) = text switch
        {
            "none" => (true, FasteningKind.None),
            "pocketScrews" => (true, FasteningKind.PocketScrews),
            "screws" => (true, FasteningKind.Screws),
            "brads" => (true, FasteningKind.Brads),
            "nails" => (true, FasteningKind.Nails),
            "dowels" => (true, FasteningKind.Dowels),
            "biscuits" => (true, FasteningKind.Biscuits),
            "clips" => (true, FasteningKind.Clips),
            _ => (false, default),
        };
        kind = found;
        return ok;
    }

    internal static bool TryFastenerKind(string text, out FastenerKind kind)
    {
        (bool ok, FastenerKind found) = text switch
        {
            "pocketScrew" => (true, FastenerKind.PocketScrew),
            "woodScrew" => (true, FastenerKind.WoodScrew),
            "brad" => (true, FastenerKind.Brad),
            "nail" => (true, FastenerKind.Nail),
            "dowel" => (true, FastenerKind.Dowel),
            "biscuit" => (true, FastenerKind.Biscuit),
            "tabletopClip" => (true, FastenerKind.TabletopClip),
            _ => (false, default),
        };
        kind = found;
        return ok;
    }

    internal static string Of(JointType type) => type switch
    {
        JointType.Butt => "butt",
        JointType.Groove => "groove",
        JointType.Rabbet => "rabbet",
        JointType.HalfLap => "halfLap",
        JointType.Tabletop => "tabletop",
        _ => throw Unknown(nameof(type), type),
    };

    internal static string Of(FasteningKind kind) => kind switch
    {
        FasteningKind.None => "none",
        FasteningKind.PocketScrews => "pocketScrews",
        FasteningKind.Screws => "screws",
        FasteningKind.Brads => "brads",
        FasteningKind.Nails => "nails",
        FasteningKind.Dowels => "dowels",
        FasteningKind.Biscuits => "biscuits",
        FasteningKind.Clips => "clips",
        _ => throw Unknown(nameof(kind), kind),
    };

    internal static string Of(FastenerKind kind) => kind switch
    {
        FastenerKind.PocketScrew => "pocketScrew",
        FastenerKind.WoodScrew => "woodScrew",
        FastenerKind.Brad => "brad",
        FastenerKind.Nail => "nail",
        FastenerKind.Dowel => "dowel",
        FastenerKind.Biscuit => "biscuit",
        FastenerKind.TabletopClip => "tabletopClip",
        _ => throw Unknown(nameof(kind), kind),
    };

    internal static bool TryAxis(string text, out Axis axis)
    {
        switch (text)
        {
            case X: axis = Geometry.Axis.X; return true;
            case Y: axis = Geometry.Axis.Y; return true;
            case Z: axis = Geometry.Axis.Z; return true;
            default: axis = default; return false;
        }
    }

    internal static bool TryCorner(string text, out BoxCorner corner)
    {
        switch (text)
        {
            case SouthWest: corner = BoxCorner.SouthWest; return true;
            case SouthEast: corner = BoxCorner.SouthEast; return true;
            case NorthEast: corner = BoxCorner.NorthEast; return true;
            case NorthWest: corner = BoxCorner.NorthWest; return true;
            default: corner = default; return false;
        }
    }

    internal static bool TryEdge(string text, out Geometry.BoxEdge edge)
    {
        switch (text)
        {
            case South: edge = Geometry.BoxEdge.South; return true;
            case East: edge = Geometry.BoxEdge.East; return true;
            case North: edge = Geometry.BoxEdge.North; return true;
            case West: edge = Geometry.BoxEdge.West; return true;
            default: edge = default; return false;
        }
    }

    internal static bool TryFace(string text, out BoxFace face)
    {
        switch (text)
        {
            case South: face = BoxFace.South; return true;
            case East: face = BoxFace.East; return true;
            case North: face = BoxFace.North; return true;
            case West: face = BoxFace.West; return true;
            case Bottom: face = BoxFace.Bottom; return true;
            case Top: face = BoxFace.Top; return true;
            default: face = default; return false;
        }
    }

    internal static bool TryBow(string text, out Geometry.Bow bow)
    {
        switch (text)
        {
            case Outward: bow = Geometry.Bow.Outward; return true;
            case Inward: bow = Geometry.Bow.Inward; return true;
            default: bow = default; return false;
        }
    }

    internal static bool TryPartDimension(string text, out PartDimension dimension)
    {
        switch (text)
        {
            case PartLength: dimension = PartDimension.Length; return true;
            case PartWidth: dimension = PartDimension.Width; return true;
            case PartThickness: dimension = PartDimension.Thickness; return true;
            default: dimension = default; return false;
        }
    }

    internal static bool TrySide(string text, out DimensionSide side)
    {
        switch (text)
        {
            case North: side = DimensionSide.North; return true;
            case South: side = DimensionSide.South; return true;
            case East: side = DimensionSide.East; return true;
            case West: side = DimensionSide.West; return true;
            default: side = default; return false;
        }
    }

    // The write direction of the spelled-out value sets. Each is the exact inverse of the
    // Try… above it: a spelling added on one side without the other stops compiling here, which is
    // the point of keeping both directions in one file.

    internal static string Of(Axis axis) => axis switch
    {
        Geometry.Axis.X => X,
        Geometry.Axis.Y => Y,
        Geometry.Axis.Z => Z,
        _ => throw Unknown(nameof(axis), axis),
    };

    internal static string Of(BoxCorner corner) => corner switch
    {
        BoxCorner.SouthWest => SouthWest,
        BoxCorner.SouthEast => SouthEast,
        BoxCorner.NorthEast => NorthEast,
        BoxCorner.NorthWest => NorthWest,
        _ => throw Unknown(nameof(corner), corner),
    };

    internal static string Of(Geometry.BoxEdge edge) => edge switch
    {
        Geometry.BoxEdge.South => South,
        Geometry.BoxEdge.East => East,
        Geometry.BoxEdge.North => North,
        Geometry.BoxEdge.West => West,
        _ => throw Unknown(nameof(edge), edge),
    };

    internal static string Of(BoxFace face) => face switch
    {
        BoxFace.South => South,
        BoxFace.East => East,
        BoxFace.North => North,
        BoxFace.West => West,
        BoxFace.Bottom => Bottom,
        BoxFace.Top => Top,
        _ => throw Unknown(nameof(face), face),
    };

    internal static string Of(Geometry.Bow bow) => bow switch
    {
        Geometry.Bow.Outward => Outward,
        Geometry.Bow.Inward => Inward,
        _ => throw Unknown(nameof(bow), bow),
    };

    internal static string Of(PartDimension dimension) => dimension switch
    {
        PartDimension.Length => PartLength,
        PartDimension.Width => PartWidth,
        PartDimension.Thickness => PartThickness,
        _ => throw Unknown(nameof(dimension), dimension),
    };

    internal static string Of(DimensionSide side) => side switch
    {
        DimensionSide.North => North,
        DimensionSide.South => South,
        DimensionSide.East => East,
        DimensionSide.West => West,
        _ => throw Unknown(nameof(side), side),
    };

    /// <summary>The spellings a message offers when one was not recognised.</summary>
    internal static string List(params string[] values) => string.Join(", ", values);

    private static ArgumentOutOfRangeException Unknown<T>(string name, T value)
        => new(name, value, "The scene format has no spelling for this value.");
}
