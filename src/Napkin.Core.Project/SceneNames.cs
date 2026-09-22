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

    // Shared.
    internal const string Id = "id";
    internal const string Name = "name";
    internal const string Type = "type";
    internal const string Kind = "kind";
    internal const string Layer = "layer";
    internal const string X = "x";
    internal const string Y = "y";
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
    internal const string OutOfPlane = "outOfPlane";
    internal const string PlanAxes = "planAxes";

    // The three finished dimensions a part has. "length" is also the units object's length field
    // and "width" also a box's stored width, which is the point: a plan axis names one of these.
    internal const string PartLength = "length";
    internal const string PartWidth = "width";
    internal const string PartThickness = "thickness";

    // Corners, edges and sides, in the box's own local frame.
    internal const string SouthWest = "southWest";
    internal const string SouthEast = "southEast";
    internal const string NorthEast = "northEast";
    internal const string NorthWest = "northWest";
    internal const string South = "south";
    internal const string East = "east";
    internal const string North = "north";
    internal const string West = "west";

    // References.
    internal const string Corner = "corner";
    internal const string Center = "center";
    internal const string Edge = "edge";
    internal const string BoxEdge = "boxEdge";
    internal const string BoxWidth = "boxWidth";
    internal const string BoxHeight = "boxHeight";
    internal const string SegmentLength = "segmentLength";
    internal const string AxisMeasurand = "axis";

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

    /// <summary>Every relationship kind the format spells out, for a message that lists them.</summary>
    internal static readonly string[] RelationshipKinds =
    [
        AngleBetween, Anchored, AxisDistance, Centered, Coincident, Distance, EqualParam, Flush,
        Horizontal, ParamValue, Parallel, Perpendicular, PointOnEdge, Radius, Symmetric, Tangent,
        Vertical,
    ];

    internal static bool TryAxis(string text, out Axis axis)
    {
        switch (text)
        {
            case X: axis = Geometry.Axis.X; return true;
            case Y: axis = Geometry.Axis.Y; return true;
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

    // The write direction of the four spelled-out value sets. Each is the exact inverse of the
    // Try… above it: a spelling added on one side without the other stops compiling here, which is
    // the point of keeping both directions in one file.

    internal static string Of(Axis axis) => axis switch
    {
        Geometry.Axis.X => X,
        Geometry.Axis.Y => Y,
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
