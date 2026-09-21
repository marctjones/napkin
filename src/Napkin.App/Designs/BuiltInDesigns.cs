using Napkin.Core.Geometry;

namespace Napkin.App.Designs;

/// <summary>
/// The two hand-computed sample drawings M1 ships with, built through the
/// <c>Core.Geometry</c> API.
/// </summary>
/// <remarks>
/// <para>
/// Every number below is a real shop or framing number written the way a person would say it —
/// a 2&#xBD;&#x2033; leg, a &#xBE;&#x2033; apron, a 3&#xBD;&#x2033; wall, a 3&#x2032;-0&#x2033;
/// window — and every one of them lands exactly on the 1/1024&#x2033; grid, which is the point of
/// the length type (docs/design/geometry-model.md &#xA7;1.1). Nothing here rounds.
/// </para>
/// <para>
/// These are built in code rather than read from <c>samples/</c> because the sample files and the
/// reader are being written in parallel (#37, #6). When the reader lands, a file source implements
/// <see cref="IDesignSource"/> beside <see cref="BuiltInDesignSource"/> and the canvas does not
/// change — which is the point of the seam.
/// </para>
/// </remarks>
public static class BuiltInDesigns
{
    /// <summary>Every sample the Samples menu offers, in menu order.</summary>
    public static IReadOnlyList<IDesignSource> All { get; } =
    [
        new BuiltInDesignSource(
            "Coffee table",
            "A 4'-0\" × 1'-8\" top on four legs, with aprons — the plan view.",
            CoffeeTable),
        new BuiltInDesignSource(
            "Wall with window",
            "A 12'-0\" 2×4 wall with a 3'-0\" opening, framed — the plan view.",
            WallWithWindow),
    ];

    /// <summary>
    /// A coffee table in plan: a 48&#x2033; &#xD7; 20&#x2033; top, four 2&#xBD;&#x2033; legs inset
    /// 1&#x2033; from the edges, and four &#xBE;&#x2033; aprons set back from the legs' outer
    /// faces.
    /// </summary>
    public static Design CoffeeTable()
    {
        DesignBuilder design = new("Coffee table");
        LayerId parts = design.AddLayer(DesignLayers.Parts);
        LayerId annotations = design.AddLayer(DesignLayers.Dimensions);

        Length width = Length.Inches(48);
        Length depth = Length.Inches(20);
        Length leg = Length.Inches(2, 1, 2);
        Length inset = Length.Inches(1);
        Length apron = Length.Inches(0, 3, 4);

        EntityId top = design.AddBox("Top", "Top", parts, Point2.Origin, width, depth);

        // The legs, inset from each corner of the top.
        Length legFar = width - inset - leg;      // 44 1/2"
        Length legBack = depth - inset - leg;     // 16 1/2"
        EntityId frontLeft = design.AddBox(
            "Leg, front left", "Leg", parts, new Point2(inset, inset), leg, leg);
        EntityId frontRight = design.AddBox(
            "Leg, front right", "Leg", parts, new Point2(legFar, inset), leg, leg);
        design.AddBox("Leg, back left", "Leg", parts, new Point2(inset, legBack), leg, leg);
        design.AddBox("Leg, back right", "Leg", parts, new Point2(legFar, legBack), leg, leg);

        // The aprons: set back 3/4" from each leg's outer face, running leg to leg.
        Length apronInset = inset + apron;                       // 1 3/4"
        Length apronRun = legFar - (inset + leg);                // 41" between the legs
        Length apronDepth = legBack - (inset + leg);             // 13" between the legs
        EntityId frontApron = design.AddBox(
            "Apron, front", "Apron", parts, new Point2(inset + leg, apronInset), apronRun, apron);
        design.AddBox(
            "Apron, back", "Apron", parts,
            new Point2(inset + leg, depth - apronInset - apron), apronRun, apron);
        design.AddBox(
            "Apron, left", "Apron", parts, new Point2(apronInset, inset + leg), apron, apronDepth);
        design.AddBox(
            "Apron, right", "Apron", parts,
            new Point2(width - apronInset - apron, inset + leg), apron, apronDepth);

        // The two overall dimensions drive the top's size; the rest are reference dimensions.
        RelationshipId drivesWidth = design.AddDrivingValue("Top width", new BoxWidthRef(top), width);
        RelationshipId drivesDepth = design.AddDrivingValue("Top depth", new BoxHeightRef(top), depth);

        design.AddSizeDimension(
            "Overall width", annotations, new BoxWidthRef(top),
            DimensionSide.South, Length.Inches(8), drivesWidth);
        design.AddSizeDimension(
            "Overall depth", annotations, new BoxHeightRef(top),
            DimensionSide.West, Length.Inches(8), drivesDepth);
        design.AddDistanceDimension(
            "Leg inset", annotations,
            new CornerRef(top, BoxCorner.SouthWest),
            new CornerRef(frontLeft, BoxCorner.SouthWest),
            Axis.X, DimensionSide.South, Length.Inches(3));
        design.AddSizeDimension(
            "Leg size", annotations, new BoxWidthRef(frontRight),
            DimensionSide.South, Length.Inches(3));
        design.AddSizeDimension(
            "Apron length", annotations, new BoxWidthRef(frontApron),
            DimensionSide.North, Length.Inches(3));

        return design.Build();
    }

    /// <summary>
    /// A wall in plan: 12&#x2032;-0&#x2033; of 2&#xD7;4 wall, 3&#xBD;&#x2033; thick, with a
    /// 3&#x2032;-0&#x2033; window opening framed by jack and king studs.
    /// </summary>
    public static Design WallWithWindow()
    {
        DesignBuilder design = new("Wall with window");
        LayerId wallLayer = design.AddLayer(DesignLayers.Wall);
        LayerId openingLayer = design.AddLayer(DesignLayers.Opening);
        LayerId framing = design.AddLayer(DesignLayers.Framing);
        LayerId annotations = design.AddLayer(DesignLayers.Dimensions);

        Length length = Length.Feet(12);              // 12'-0"
        Length thickness = Length.Inches(3, 1, 2);    // a 2x4 wall
        Length opening = Length.Feet(3);              // 3'-0" window
        Length fromEnd = Length.FeetInches(4, 2, 1, 2); // 4'-2 1/2" to the opening
        Length stud = Length.Inches(1, 1, 2);         // a 2x4 flat-wise in plan

        EntityId wall = design.AddBox("Wall", "Wall", wallLayer, Point2.Origin, length, thickness);
        EntityId window = design.AddBox(
            "Window", "Window", openingLayer, new Point2(fromEnd, Length.Zero), opening, thickness);

        // Jack studs carry the header at each side of the opening; king studs stand outside them.
        // They are drawn but not labelled: at any zoom that shows the whole wall, four names in
        // three inches of drawing is noise.
        design.AddBox("Jack, left", null, framing, new Point2(fromEnd - stud, Length.Zero), stud, thickness);
        design.AddBox("Jack, right", null, framing, new Point2(fromEnd + opening, Length.Zero), stud, thickness);
        design.AddBox("King, left", null, framing, new Point2(fromEnd - (stud * 2), Length.Zero), stud, thickness);
        design.AddBox("King, right", null, framing, new Point2(fromEnd + opening + stud, Length.Zero), stud, thickness);

        RelationshipId drivesLength = design.AddDrivingValue("Wall length", new BoxWidthRef(wall), length);
        RelationshipId drivesOpening = design.AddDrivingValue("Opening width", new BoxWidthRef(window), opening);

        design.AddSizeDimension(
            "Wall length", annotations, new BoxWidthRef(wall),
            DimensionSide.South, Length.Inches(14), drivesLength);
        design.AddSizeDimension(
            "Opening width", annotations, new BoxWidthRef(window),
            DimensionSide.North, Length.Inches(8), drivesOpening);
        design.AddDistanceDimension(
            "To opening", annotations,
            new CornerRef(wall, BoxCorner.SouthWest),
            new CornerRef(window, BoxCorner.SouthWest),
            Axis.X, DimensionSide.South, Length.Inches(6));
        design.AddDistanceDimension(
            "Past opening", annotations,
            new CornerRef(window, BoxCorner.SouthEast),
            new CornerRef(wall, BoxCorner.SouthEast),
            Axis.X, DimensionSide.South, Length.Inches(6));
        design.AddSizeDimension(
            "Wall thickness", annotations, new BoxHeightRef(wall),
            DimensionSide.West, Length.Inches(8));

        return design.Build();
    }
}

/// <summary>A sample shipped inside the application, built through the geometry API.</summary>
/// <param name="Name">What the menu calls it.</param>
/// <param name="Description">One line for the status bar.</param>
/// <param name="Factory">Builds the design; called each time it is opened.</param>
public sealed record BuiltInDesignSource(
    string Name,
    string Description,
    Func<Design> Factory) : IDesignSource
{
    /// <inheritdoc/>
    public Design Load() => Factory();
}
