using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Builds sketches from a seed, for the round-trip property: <c>Load(Save(sketch)) == sketch</c>
/// over more shapes than anyone would write by hand (PRJ-007, geometry design property P9).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Constructive, not rejection-sampled.</strong> The reader re-runs
/// <see cref="Sketch.Validate"/> and <see cref="RelationshipChecker"/> on everything it loads, so
/// a generated sketch whose geometry does not satisfy its own relationships would be refused and
/// the round trip would fail for a reason that has nothing to do with the writer. Every
/// relationship here is therefore built <em>from</em> the geometry that has already been placed —
/// a distance is measured, never guessed — and the geometry that a relationship cannot be measured
/// from is placed to suit it.
/// </para>
/// <para>
/// <strong>What the seeds are aimed at:</strong> negative coordinates, sizes that are not round
/// numbers of inches, boxes at each of the four right-angle rotations, several layers, driving and
/// reference dimensions, and every relationship kind the model can hold. The two kinds it cannot —
/// <see cref="Tangent"/> and <see cref="Radius"/> — are about arcs, which do not exist, so the
/// checker reports them as violations and no file containing one can be loaded by any build
/// (geometry design §10). <c>SceneWriterTests</c> covers those two directly.
/// </para>
/// </remarks>
internal static class SketchGenerator
{
    /// <summary>The four rotations whose corners are exact sums (geometry design §1.6).</summary>
    private static readonly Angle[] RightAngles =
    [
        Angle.Zero,
        new Angle(Angle.RightAngleArcseconds),
        new Angle(2 * Angle.RightAngleArcseconds),
        new Angle(3 * Angle.RightAngleArcseconds),
    ];

    /// <summary>Layer names, including ones that have to survive JSON escaping.</summary>
    private static readonly string[] LayerNames =
    [
        "Walls", "Openings", "Dimensions \"as drawn\"", "Étage — 1er", "Back\\slash", "日本語",
    ];

    /// <summary>
    /// Entity names, chosen for the same reason the layer names are: an entity name is written
    /// into the same JSON and has to survive the same escaping. The empty string is in the list
    /// because "unnamed" is legal and round-trips like any other name.
    /// </summary>
    private static readonly string[] EntityNames =
    [
        string.Empty, "Leg, south-west", "Apron \"long\"", "Tablette — chêne", "Back\\slash", "脚",
    ];

    /// <summary>Stock names, including none at all.</summary>
    private static readonly string?[] StockNames = [null, "2x4", "1x6", "23/32", "a size nobody stocks"];

    /// <summary>Species, including none at all.</summary>
    private static readonly string?[] SpeciesNames = [null, "Douglas fir", "white oak", string.Empty];

    /// <summary>One sketch, the same one every time for the same seed.</summary>
    /// <param name="seed">The seed, which every failure message prints.</param>
    internal static Sketch Generate(int seed) => new Builder(seed).Build();

    /// <summary>
    /// One generated sketch under construction. Entities are placed first and relationships are
    /// measured off them afterwards, which is why this is a class holding the sketch so far.
    /// </summary>
    private sealed class Builder(int seed)
    {
        private readonly Random random = new(seed);
        private readonly List<Layer> layers = [];
        private readonly List<Box> boxes = [];
        private readonly List<Box> spaced = [];
        private readonly List<Node> nodes = [];
        private readonly List<Segment> segments = [];
        private readonly List<Relationship> relationships = [];

        private Sketch sketch = new(
            ImmutableDictionary<EntityId, Entity>.Empty,
            ImmutableDictionary<RelationshipId, Relationship>.Empty,
            ImmutableList<Layer>.Empty);

        internal Sketch Build()
        {
            PlaceLayers();
            PlaceBoxes();
            PlaceNodesAndSegments();
            Relate();
            Annotate();
            return sketch;
        }

        // -----------------------------------------------------------------------------------
        // Geometry
        // -----------------------------------------------------------------------------------

        private void PlaceLayers()
        {
            layers.Add(Layer.Default);
            int extra = random.Next(0, 3);
            for (int i = 0; i < extra; i++)
            {
                layers.Add(new Layer(new LayerId(NextGuid()), LayerNames[random.Next(LayerNames.Length)]));
            }

            sketch = sketch with { Layers = [.. layers] };
        }

        private void PlaceBoxes()
        {
            int count = random.Next(2, 5);
            for (int i = 0; i < count; i++)
            {
                Length width = NextSize();
                Length height = NextSize();

                // Two boxes made the same width on purpose, so that equalParam has something
                // true to say.
                if (i == 1 && random.Next(2) == 0)
                {
                    width = boxes[0].Width;
                }

                // Lying as drawn and on the plan datum: the relationships below are measured off
                // these boxes' plan corners and edges, which are their local uprights and side faces
                // only for a box that is top up. The depth is anything, part or not (format
                // version 4 stores it on every box).
                Box box = Box.AsDrawn(
                    new EntityId(NextGuid()),
                    NextLayer(),
                    new Point2(NextCoordinate(), NextCoordinate()),
                    width,
                    height,
                    NextSize(),
                    RightAngles[random.Next(RightAngles.Length)]) with
                {
                    Name = NextName(),
                    Part = NextPart(),
                };

                boxes.Add(box);
                Add(box);
            }

            PlaceBoxesInSpace();
        }

        /// <summary>
        /// Boxes off the plan datum and turned every way (<c>docs/design/assembly-model.md</c>
        /// &#xA7;1.3): one for each of the six faces that can be up, at a Z that is as likely to be
        /// below the datum as above it, at any of the four spins. Relationships reach them through
        /// features and measured Z spans (<see cref="RelateInSpace"/>).
        /// </summary>
        private void PlaceBoxesInSpace()
        {
            foreach (BoxFace up in Enum.GetValues<BoxFace>())
            {
                Box box = new(
                    new EntityId(NextGuid()),
                    NextLayer(),
                    new Point3(NextCoordinate(), NextCoordinate(), NextCoordinate()),
                    NextSize(),
                    NextSize(),
                    NextSize(),
                    up,
                    RightAngles[random.Next(RightAngles.Length)])
                {
                    Name = NextName(),
                    Part = NextPart(),
                };

                spaced.Add(box);
                Add(box);
            }
        }

        private void PlaceNodesAndSegments()
        {
            // A node exactly on a box corner: what makes coincident hold exactly.
            foreach (Box box in boxes)
            {
                BoxCorner corner = (BoxCorner)random.Next(4);
                Node node = NodeAt(box.Corner(corner));
                CornerNodes.Add((box, corner, node));
            }

            // Two horizontal segments at the same height, so that flush, horizontal, pointOnEdge
            // and a segment length all have something true to say about them.
            Length y = NextCoordinate();
            Length left = NextCoordinate();
            Node a0 = NodeAt(new Point2(left, y));
            Node a1 = NodeAt(new Point2(left + NextSize(), y));
            Node b0 = NodeAt(new Point2(left + NextSize(), y));
            Node b1 = NodeAt(new Point2(left + NextSize() + NextSize(), y));
            FirstRail = SegmentBetween(a0, a1);
            SecondRail = SegmentBetween(b0, b1);

            // A vertical segment, and two nodes mirrored across it.
            Length mirrorX = NextCoordinate();
            Length bottom = NextCoordinate();
            Node m0 = NodeAt(new Point2(mirrorX, bottom));
            Node m1 = NodeAt(new Point2(mirrorX, bottom + NextSize()));
            Mirror = SegmentBetween(m0, m1);

            Length reach = NextSize();
            Length height = NextCoordinate();
            LeftOfMirror = NodeAt(new Point2(mirrorX - reach, height));
            RightOfMirror = NodeAt(new Point2(mirrorX + reach, height));

            // A node on the first rail's line, between its ends.
            OnFirstRail = NodeAt(new Point2(left, y));
        }

        private List<(Box Box, BoxCorner Corner, Node Node)> CornerNodes { get; } = [];

        private Segment FirstRail { get; set; } = null!;

        private Segment SecondRail { get; set; } = null!;

        private Segment Mirror { get; set; } = null!;

        private Node LeftOfMirror { get; set; } = null!;

        private Node RightOfMirror { get; set; } = null!;

        private Node OnFirstRail { get; set; } = null!;

        // -----------------------------------------------------------------------------------
        // Relationships, every one measured off the geometry above
        // -----------------------------------------------------------------------------------

        private void Relate()
        {
            Relate(new Anchored(NextRelationshipId(), boxes[0].Id));

            foreach (Box box in boxes)
            {
                if (random.Next(3) != 0)
                {
                    Relate(new ParamValue(NextRelationshipId(), new BoxWidthRef(box.Id), box.Width));
                }

                if (random.Next(3) != 0)
                {
                    Relate(new ParamValue(NextRelationshipId(), new BoxHeightRef(box.Id), box.Height));
                }
            }

            if (boxes[0].Width == boxes[1].Width)
            {
                Relate(new EqualParam(
                    NextRelationshipId(), new BoxWidthRef(boxes[0].Id), new BoxWidthRef(boxes[1].Id)));
            }

            foreach ((Box box, BoxCorner corner, Node node) in CornerNodes)
            {
                Relate(new Coincident(
                    NextRelationshipId(), TestRefs.Corner(box.Id, corner), new NodeRef(node.Id)));
            }

            // The segment rails: exact by construction, and their length is axis-aligned so it is
            // an exact integer too.
            Relate(new Horizontal(NextRelationshipId(), new SegmentRef(FirstRail.Id)));
            Relate(new Horizontal(NextRelationshipId(), new SegmentRef(SecondRail.Id)));
            Relate(new Flush(
                NextRelationshipId(), new SegmentRef(FirstRail.Id), new SegmentRef(SecondRail.Id)));
            Relate(new ParamValue(
                NextRelationshipId(),
                new SegmentLengthRef(FirstRail.Id),
                sketch.ValueOf(new SegmentLengthRef(FirstRail.Id))));
            Relate(new Vertical(NextRelationshipId(), new SegmentRef(Mirror.Id)));
            Relate(new PointOnEdge(
                NextRelationshipId(), new NodeRef(OnFirstRail.Id), new SegmentRef(FirstRail.Id)));
            Relate(new Symmetric(
                NextRelationshipId(),
                new NodeRef(LeftOfMirror.Id),
                new NodeRef(RightOfMirror.Id),
                new SegmentRef(Mirror.Id)));

            // A distance measured between two points that differ along one axis only, so that the
            // Euclidean magnitude is the exact integer separation.
            Relate(new Distance(
                NextRelationshipId(),
                new NodeRef(LeftOfMirror.Id),
                new NodeRef(RightOfMirror.Id),
                (sketch.PlanPoint(new NodeRef(RightOfMirror.Id)) - sketch.PlanPoint(new NodeRef(LeftOfMirror.Id)))
                    .Magnitude()));

            // A signed axis distance, measured rather than stated.
            Axis axis = random.Next(2) == 0 ? Axis.X : Axis.Y;
            PlaceRef from = TestRefs.Corner(boxes[0].Id, BoxCorner.SouthWest);
            PlaceRef to = new CenterRef(boxes[1].Id);
            AxisDistance span = new(
                NextRelationshipId(),
                from,
                to,
                axis,
                sketch.PlanPoint(to).Component(axis) - sketch.PlanPoint(from).Component(axis));
            DrivingSpan = (AxisDistance)Relate(span);

            // A node placed at the midpoint of that span, so that centered holds.
            Length middle = (sketch.PlanPoint(from).Component(axis) + sketch.PlanPoint(to).Component(axis))
                .Divide(2, Rounding.HalfToEven);
            Node midpoint = NodeAt(
                new Point2(
                    axis == Axis.X ? middle : NextCoordinate(),
                    axis == Axis.Y ? middle : NextCoordinate()));
            Relate(new Centered(NextRelationshipId(), new NodeRef(midpoint.Id), from, to, axis));

            RelateBoxEdges();
            RelateInSpace();
        }

        /// <summary>
        /// The boxes in space, reached the way &#xA7;2.3 allows: a depth held at its value, and
        /// signed Z spans measured between a face, an edge, a vertex and a centre that all fix Z —
        /// so that every shape of feature reference, one face to three, is in the round trip, and
        /// the "z" axis with them.
        /// </summary>
        private void RelateInSpace()
        {
            foreach (Box box in spaced)
            {
                if (random.Next(2) == 0)
                {
                    Relate(new ParamValue(NextRelationshipId(), new BoxDepthRef(box.Id), box.Depth));
                }
            }

            // The face that is up fixes Z, and so do an edge along it and a vertex on it.
            Box first = spaced[random.Next(spaced.Count)];
            Box second = spaced[random.Next(spaced.Count)];
            Box third = spaced[random.Next(spaced.Count)];

            PlaceRef face = new FeatureRef(first.Id, BoxFeature.Face(first.FaceUp));
            PlaceRef edge = new FeatureRef(second.Id, BoxFeature.Edge(second.FaceUp, SideOf(second.FaceUp)));
            PlaceRef vertex = new FeatureRef(
                third.Id,
                BoxFeature.Vertex(third.FaceUp, SideOf(third.FaceUp), OtherSideOf(third.FaceUp)));

            RelateAlongZ(face, vertex);
            RelateAlongZ(edge, new CenterRef(first.Id));
        }

        private void RelateAlongZ(PlaceRef from, PlaceRef to)
            => Relate(new AxisDistance(
                NextRelationshipId(),
                from,
                to,
                Axis.Z,
                sketch.PlaceOf(to)[Axis.Z] - sketch.PlaceOf(from)[Axis.Z]));

        /// <summary>A face that meets <paramref name="face"/> at an edge.</summary>
        private static BoxFace SideOf(BoxFace face)
            => face is BoxFace.Bottom or BoxFace.Top ? BoxFace.South : BoxFace.Bottom;

        /// <summary>A face that meets both <paramref name="face"/> and <see cref="SideOf"/> it.</summary>
        private static BoxFace OtherSideOf(BoxFace face)
            => face is BoxFace.East or BoxFace.West ? BoxFace.South : BoxFace.West;

        /// <summary>
        /// The angular kinds, between box edges. Both directions are stored angles on right-angle
        /// rotations, so the checker judges them exactly (geometry design §5.3) — which means the
        /// kind has to be chosen from the angle that is actually there rather than hoped for.
        /// </summary>
        private void RelateBoxEdges()
        {
            FeatureRef a = TestRefs.Edge(boxes[0].Id, (BoxEdge)random.Next(4));
            FeatureRef b = TestRefs.Edge(boxes[1].Id, (BoxEdge)random.Next(4));

            Angle between = new(DirectionOf(b).Arcseconds - DirectionOf(a).Arcseconds);
            long quarterTurns = between.Arcseconds / Angle.RightAngleArcseconds;

            if (quarterTurns % 2 == 0)
            {
                Relate(new Geometry.Parallel(NextRelationshipId(), a, b));
            }
            else
            {
                Relate(new Perpendicular(NextRelationshipId(), a, b));
            }

            // angleBetween states the angle that is there, whatever it is.
            Relate(new AngleBetween(NextRelationshipId(), a, b, between));

            // A box edge is axis-aligned by its rotation, so whether it is horizontal or vertical
            // is read off the geometry, not chosen.
            (Point2 start, Point2 end) = sketch.PlanLine(a);
            Relate(start.Y == end.Y
                ? new Horizontal(NextRelationshipId(), a)
                : new Vertical(NextRelationshipId(), a));
        }

        /// <summary>The direction a right-angle-rotated box's edge runs, exactly.</summary>
        private Angle DirectionOf(FeatureRef edge)
        {
            Box box = sketch.Find<Box>(edge.Box)!;
            Angle local = edge.Feature.Faces[0] is BoxFace.South or BoxFace.North ? Angle.Zero : Angle.Right;
            return box.Rotation + local;
        }

        private AxisDistance DrivingSpan { get; set; } = null!;

        // -----------------------------------------------------------------------------------
        // Dimensions
        // -----------------------------------------------------------------------------------

        private void Annotate()
        {
            // A driving dimension on a size: the paramValue that already holds owns the number.
            if (relationships.OfType<ParamValue>().FirstOrDefault(value => value.Param is BoxWidthRef) is { } driven)
            {
                Add(new Dimension(
                    new EntityId(NextGuid()),
                    NextLayer(),
                    new ParamMeasurand(driven.Param),
                    driven.Id,
                    NextPlacement())
                {
                    Name = NextName(),
                });
            }

            // A driving linear dimension: the axisDistance owns the number.
            Add(new Dimension(
                new EntityId(NextGuid()),
                NextLayer(),
                new AxisMeasurand(DrivingSpan.From, DrivingSpan.To, DrivingSpan.Axis),
                DrivingSpan.Id,
                NextPlacement())
            {
                Name = NextName(),
            });

            // A reference dimension: it measures and owns nothing.
            Add(new Dimension(
                new EntityId(NextGuid()),
                NextLayer(),
                new ParamMeasurand(new SegmentLengthRef(FirstRail.Id)),
                null,
                NextPlacement())
            {
                Name = NextName(),
            });
        }

        private DimensionPlacement NextPlacement()
            => new(NextCoordinate(), (DimensionSide)random.Next(4));

        /// <summary>A name for an entity, sometimes empty, which is a legal "unnamed".</summary>
        private string NextName() => EntityNames[random.Next(EntityNames.Length)];

        /// <summary>
        /// A part, or none: a third of the boxes are walls and openings rather than pieces
        /// somebody cuts, so that <c>"part": null</c> is in the round trip as often as a part is.
        /// </summary>
        private Part? NextPart()
        {
            if (random.Next(3) == 0)
            {
                return null;
            }

            PartDimension x = (PartDimension)random.Next(3);
            PartDimension y = (PartDimension)((int)(x + 1 + random.Next(2)) % 3);

            return new Part(
                StockNames[random.Next(StockNames.Length)],
                SpeciesNames[random.Next(SpeciesNames.Length)],
                random.Next(1, 13),
                new PlanAxes(x, y));
        }

        // -----------------------------------------------------------------------------------
        // Plumbing
        // -----------------------------------------------------------------------------------

        private void Add(Entity entity) => sketch = sketch.WithEntity(entity);

        /// <summary>
        /// Adds a relationship unless the sketch already says the same thing about the same
        /// references: invariant 4 forbids a structural duplicate, and the reader refuses one.
        /// Returns the relationship that is in the sketch afterwards — the new one, or the one
        /// that was already saying it — so that a dimension can name it and not dangle.
        /// </summary>
        private Relationship Relate(Relationship relationship)
        {
            if (relationships.FirstOrDefault(
                    existing => Relationship.AreStructurallyIdentical(existing, relationship)) is { } already)
            {
                return already;
            }

            relationships.Add(relationship);
            sketch = sketch.WithRelationship(relationship);
            return relationship;
        }

        private Node NodeAt(Point2 position)
        {
            Node node = new(new EntityId(NextGuid()), NextLayer(), position) { Name = NextName() };
            nodes.Add(node);
            Add(node);
            return node;
        }

        private Segment SegmentBetween(Node start, Node end)
        {
            Segment segment = new(new EntityId(NextGuid()), NextLayer(), start.Id, end.Id)
            {
                Name = NextName(),
            };

            segments.Add(segment);
            Add(segment);
            return segment;
        }

        private LayerId NextLayer() => layers[random.Next(layers.Count)].Id;

        /// <summary>A coordinate that is as likely to be negative as positive, and rarely round.</summary>
        private Length NextCoordinate() => new(random.NextInt64(-400_000, 400_001));

        /// <summary>A size greater than zero, and rarely a whole number of inches.</summary>
        private Length NextSize() => new(random.NextInt64(1, 120_001));

        private RelationshipId NextRelationshipId() => new(NextGuid());

        /// <summary>An id drawn from the seeded generator, so the whole sketch is a function of the seed.</summary>
        private Guid NextGuid()
        {
            byte[] bytes = new byte[16];
            random.NextBytes(bytes);
            return new Guid(bytes);
        }
    }

    /// <summary>The seed, spelled for a failure message.</summary>
    internal static string Describe(int seed) => seed.ToString(CultureInfo.InvariantCulture);
}
