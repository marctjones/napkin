using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// What a part's joints do to it: the extra length a groove or rabbet it is inserted into costs, and
/// what to do to the blank at the bench, in fixed sentences
/// (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;6.1, &#xA7;6.2).
/// </summary>
/// <remarks>
/// <para>
/// Two steps, like <see cref="CutDescription"/>: <see cref="FactsOf"/> reads the sketch and gives
/// each part its joinery in structural form (<see cref="JointFact"/>), and <see cref="Describe"/>
/// turns structure into sentences. The row holds the structure, so it stays a value with no
/// formatting decisions in it, and the sentences are derived from it.
/// </para>
/// <para>
/// Only operations done to the blank at the bench are sentences: a butt joint, screws, brads, nails,
/// dowels, biscuits and glue are assembly and live on the marker's tooltip and the fastener list. That
/// is what keeps four legs one row while the aprons say where their pocket holes go.
/// </para>
/// <para>
/// <strong>Faces are named in the part's own frame as drawn</strong>, the way <see cref="CutDescription"/>
/// names them, and lengths are rendered by <see cref="CutListCsv.Text"/>, so a size that is not exact
/// at 1/16&#x2033; carries the &#x2248; marker. Length text uses the straight quote mark of that renderer
/// (1/4"), not the typographic one the design note's prose uses.
/// </para>
/// </remarks>
public static class JointDescription
{
    /// <summary>What the row says when one of its parts' joints no longer holds (&#xA7;6.4).</summary>
    public const string NotSatisfied = "joint not satisfied";

    /// <summary>What pressing J says when fewer than two parts are selected to join (#179).</summary>
    public const string SelectTwoPartsPrompt = "Select the two parts to join first, then press J.";

    /// <summary>
    /// The centre of everything drawn: the middle of the extent of all the sketch's boxes and this
    /// one. Which of an apron's two long faces is its inside is the one nearer it (&#xA7;6.2).
    /// </summary>
    private static Point3 CentreOf(Sketch sketch, Box box) => JointGeometry.CentreOfEverything(sketch, box);

    /// <summary>
    /// The joinery of one part: what its joints do to it, each in structural form. Empty for a part
    /// nothing is done to — four legs — and whose joints are all assembly.
    /// </summary>
    /// <remarks>
    /// An allowance is applied while the joint's depth is less than the receiving part's thickness,
    /// even when the parts have since moved apart (&#xA7;4.3: the cut list "still applies its
    /// allowance"); a depth that is not less is a slot, not a groove, and adds nothing (&#xA7;6.1). A
    /// sentence needs a contact to measure, so a joint whose parts do not touch says nothing
    /// (<see cref="Unsatisfied"/> is how the row hears of it).
    /// </remarks>
    /// <param name="sketch">The design.</param>
    /// <param name="box">The part to describe. Its <see cref="Box.Part"/> must not be null.</param>
    public static ImmutableArray<JointFact> FactsOf(Sketch sketch, Box box)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(box);

        if (box.Part is not { } part || !box.Orientation.IsExact)
        {
            return [];
        }

        Point3 centre = CentreOf(sketch, box);
        List<JointFact> facts = [];
        foreach (Joint joint in sketch.RelationshipsInOrder.OfType<Joint>())
        {
            if (joint.Inserted.Box == box.Id)
            {
                AddInserted(sketch, box, part, joint, centre, facts);
            }
            else if (joint.Receiving.Box == box.Id)
            {
                AddReceiving(sketch, joint, facts);
            }
        }

        return JointSequence.Sorted(facts);
    }

    /// <summary>Whether any joint on this part is not satisfied: its parts have moved apart, or its depth is not less than the thickness.</summary>
    public static bool Unsatisfied(Sketch sketch, Box box)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(box);

        return sketch.RelationshipsInOrder.OfType<Joint>()
            .Any(joint => (joint.Receiving.Box == box.Id || joint.Inserted.Box == box.Id)
                          && !JointGeometry.IsSatisfied(sketch, joint));
    }

    /// <summary>
    /// Whether two parts have the same joinery: the same entries, or the same entries under a
    /// half-turn of the part about any of its own axes (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;6.3).
    /// </summary>
    public static bool SameJoinery(ImmutableArray<JointFact> a, ImmutableArray<JointFact> b)
        => JointSequence.AreEqual(a, b);

    /// <summary>What a part's joinery adds to one of its three dimensions: the sum of its allowances.</summary>
    public static Length AllowanceOn(ImmutableArray<JointFact> facts, PartDimension dimension)
        => facts.Where(fact => fact.Kind == JointFactKind.Allowance && fact.Dimension == dimension)
            .Aggregate(Length.Zero, (total, fact) => total + fact.Depth);

    private static void AddInserted(Sketch sketch, Box box, Part part, Joint joint, Point3 centre, List<JointFact> facts)
    {
        BoxFace face = joint.Inserted.Feature.Faces[0];
        Box? receiving = sketch.Find<Box>(joint.Receiving.Box);
        BoxFace receivingFace = joint.Receiving.Feature.Faces[0];

        // The allowance: the part goes that far into the other part, along the axis its contact face is on.
        if (joint.Type is JointType.Groove or JointType.Rabbet
            && joint.Depth is { } depth
            && receiving is not null
            && depth < JointGeometry.ThicknessAcross(receiving, receivingFace))
        {
            facts.Add(new JointFact(
                JointFactKind.Allowance,
                Face: face,
                Dimension: DimensionAlong(part, face),
                Type: joint.Type,
                Depth: depth));
        }

        if (!JointGeometry.IsSatisfied(sketch, joint) || JointGeometry.Of(sketch, joint) is not { } shape)
        {
            return;
        }

        switch (joint.Type)
        {
            case JointType.Butt when joint.Fastening is { Kind: FasteningKind.PocketScrews, PocketFace: { } from }
                                     && shape.PocketEnd is { } end:
                facts.Add(new JointFact(JointFactKind.PocketHoles, Face: from, End: end, Count: Recipes.Count(joint, shape.JointLength)));
                break;

            case JointType.Tabletop when InsideFace(box, face, centre) is { } inside:
                facts.Add(new JointFact(JointFactKind.TabletopClips, Face: inside, Count: Recipes.Count(joint, shape.JointLength)));
                break;

            case JointType.HalfLap when shape.Lap is { } lap:
                facts.Add(LapFact(face, lap.Inserted, lap.Depth));
                break;
        }
    }

    private static void AddReceiving(Sketch sketch, Joint joint, List<JointFact> facts)
    {
        if (!JointGeometry.IsSatisfied(sketch, joint) || JointGeometry.Of(sketch, joint) is not { } shape)
        {
            return;
        }

        BoxFace face = joint.Receiving.Feature.Faces[0];
        switch (joint.Type)
        {
            case JointType.Groove when shape.Groove is { } groove:
                facts.Add(new JointFact(
                    JointFactKind.Groove,
                    Face: face,
                    End: groove.OffsetFrom,
                    Direction: groove.Direction,
                    Width: groove.Width,
                    Depth: groove.Depth,
                    Offset: groove.Offset));
                break;

            case JointType.Rabbet when shape.Rabbet is { } rabbet:
                facts.Add(new JointFact(JointFactKind.Rabbet, Face: face, End: rabbet.End, Width: rabbet.Width, Depth: rabbet.Depth));
                break;

            case JointType.HalfLap when shape.Lap is { } lap:
                facts.Add(LapFact(face, lap.Receiving, lap.Depth));
                break;
        }
    }

    private static JointFact LapFact(BoxFace face, LapPart part, Length depth)
        => new(JointFactKind.HalfLap, Face: face, End: part.Nearest, Depth: depth, Long: part.Long, Offset: part.Offset);

    // Which of the part's three dimensions lies along the local axis a face is perpendicular to.
    private static PartDimension DimensionAlong(Part part, BoxFace face) => JointGeometry.LocalAxisOf(face) switch
    {
        Axis.X => part.PlanAxes.X,
        Axis.Y => part.PlanAxes.Y,
        _ => part.PlanAxes.OutOfPlane,
    };

    // The face tabletop clips are on: of the two long faces flanking the part's top edge, the one nearer the middle
    // of everything drawn (§6.2) — the inside of an apron. A tie takes the lower face.
    private static BoxFace? InsideFace(Box box, BoxFace contact, Point3 middle)
    {
        Axis contactAxis = JointGeometry.LocalAxisOf(contact);
        Axis lengthAxis = JointGeometry.LengthAxis(box);
        Axis[] remaining = [.. new[] { Axis.X, Axis.Y, Axis.Z }.Where(axis => axis != contactAxis && axis != lengthAxis)];
        if (remaining.Length != 1)
        {
            return null;
        }

        BoxFace[] pair = remaining[0] switch
        {
            Axis.X => [BoxFace.West, BoxFace.East],
            Axis.Y => [BoxFace.South, BoxFace.North],
            _ => [BoxFace.Bottom, BoxFace.Top],
        };

        Length Distance(BoxFace face) => JointGeometry.DistanceFromPoint(box, face, middle);

        return Distance(pair[1]) < Distance(pair[0]) ? pair[1] : pair[0];
    }

    /// <summary>
    /// The sentences for a part's joinery, in the order the bench wants them: allowances first, then
    /// everything else by the face it is on (south, east, north, west, bottom, top), then by how far
    /// it is from its edge, then rabbet, groove, lap, pocket holes, clips.
    /// </summary>
    /// <param name="facts">The joinery, from <see cref="FactsOf"/>.</param>
    public static ImmutableArray<string> Describe(ImmutableArray<JointFact> facts)
    {
        if (facts.IsDefaultOrEmpty)
        {
            return [];
        }

        List<string> sentences = [];
        if (AllowanceSentence(facts) is { } allowance)
        {
            sentences.Add(allowance);
        }

        JointFact[] others =
        [
            .. facts.Where(fact => fact.Kind != JointFactKind.Allowance)
                .OrderBy(fact => (int)fact.Face!.Value)
                .ThenBy(fact => fact.Offset)
                .ThenBy(fact => (int)fact.Kind)
                .ThenBy(fact => (int)(fact.End ?? 0))
                .ThenBy(fact => fact.Width)
                .ThenBy(fact => fact.Depth),
        ];

        HashSet<BoxFace> pocketFacesSaid = [];
        foreach (JointFact fact in others)
        {
            if (fact.Kind == JointFactKind.PocketHoles)
            {
                // Holes drilled from one face are one sentence, wherever the first of them sorts.
                if (pocketFacesSaid.Add(fact.Face!.Value))
                {
                    sentences.Add(PocketSentence(others.Where(other => other.Kind == JointFactKind.PocketHoles && other.Face == fact.Face)));
                }

                continue;
            }

            sentences.Add(Sentence(fact));
        }

        return [.. sentences];
    }

    private static string Sentence(JointFact fact)
    {
        string face = Word(fact.Face!.Value);
        return fact.Kind switch
        {
            JointFactKind.Groove when fact.Direction == GrooveDirection.AlongLength =>
                $"Groove the {face} face: {Len(fact.Width)} wide, {Len(fact.Depth)} deep, {Len(fact.Offset)} from the {Word(fact.End!.Value)} edge, full length.",
            JointFactKind.Groove =>
                $"Dado the {face} face: {Len(fact.Width)} wide, {Len(fact.Depth)} deep, {Len(fact.Offset)} from the {Word(fact.End!.Value)} end, across the width.",
            JointFactKind.Rabbet =>
                $"Rabbet the {Word(fact.End!.Value)} end on the {face} face: {Len(fact.Width)} wide, {Len(fact.Depth)} deep.",
            JointFactKind.HalfLap when fact.Offset == Length.Zero =>
                $"Half-lap the {face} face at the {Word(fact.End!.Value)} end: {Len(fact.Long)} long, {Len(fact.Depth)} deep, across the width.",
            JointFactKind.HalfLap =>
                $"Half-lap the {face} face {Len(fact.Offset)} from the {Word(fact.End!.Value)} end: {Len(fact.Long)} long, {Len(fact.Depth)} deep, across the width.",
            _ =>
                $"Fit {Count(fact.Count, "tabletop clip")} along the top edge on the {face} face (slot or recess per the clip's instructions).",
        };
    }

    // Consecutive holes from one face read as one sentence: the biggest run first, then the lower end.
    private static string PocketSentence(IEnumerable<JointFact> holes)
    {
        JointFact[] ordered = [.. holes.OrderByDescending(fact => fact.Count).ThenBy(fact => LowFirst(fact.End!.Value))];
        string from = Word(ordered[0].Face!.Value);
        if (ordered.Length == 1)
        {
            return $"Drill {Count(ordered[0].Count, "pocket hole")} in the {Word(ordered[0].End!.Value)} end from the {from} face.";
        }

        List<string> clauses = [$"{Count(ordered[0].Count, "pocket hole")} in the {Word(ordered[0].End!.Value)} end"];
        clauses.AddRange(ordered.Skip(1).Select(fact => $"{fact.Count.ToString(CultureInfo.InvariantCulture)} in the {Word(fact.End!.Value)} end"));
        string joined = clauses.Count == 2
            ? $"{clauses[0]} and {clauses[1]}"
            : $"{string.Join(", ", clauses.Take(clauses.Count - 1))} and {clauses[^1]}";
        return $"Drill {joined}, from the {from} face.";
    }

    // The order of two ends of one part when nothing else separates them: the low side first (west, south, bottom, then east, north, top).
    private static int LowFirst(BoxFace face) => face switch
    {
        BoxFace.West => 0,
        BoxFace.South => 1,
        BoxFace.Bottom => 2,
        BoxFace.East => 3,
        BoxFace.North => 4,
        _ => 5,
    };

    // "Length includes 1/4" into a rabbet at each end; width includes 1/4" into a groove at each edge."
    private static string? AllowanceSentence(ImmutableArray<JointFact> facts)
    {
        JointFact[] allowances = [.. facts.Where(fact => fact.Kind == JointFactKind.Allowance)];
        if (allowances.Length == 0)
        {
            return null;
        }

        List<string> clauses = [];
        foreach (PartDimension dimension in new[] { PartDimension.Length, PartDimension.Width, PartDimension.Thickness })
        {
            foreach (IGrouping<(JointType? Type, Length Depth), JointFact> group in allowances
                         .Where(fact => fact.Dimension == dimension)
                         .GroupBy(fact => (fact.Type, fact.Depth))
                         .OrderBy(group => (int)group.Key.Type!.Value)
                         .ThenBy(group => group.Key.Depth))
            {
                string kind = group.Key.Type == JointType.Groove ? "groove" : "rabbet";
                string place = group.Count() > 1
                    ? $"each {Place(dimension)}"
                    : $"the {Word(group.First().Face!.Value)} {Place(dimension)}";
                string name = dimension.ToString();
                if (clauses.Count > 0)
                {
                    name = name.ToLowerInvariant();
                }

                clauses.Add($"{name} includes {Len(group.Key.Depth)} into a {kind} at {place}");
            }
        }

        return string.Join("; ", clauses) + ".";
    }

    // What the allowed-for side of a dimension is called: a length has ends, a width has edges, a thickness faces.
    private static string Place(PartDimension dimension) => dimension switch
    {
        PartDimension.Length => "end",
        PartDimension.Width => "edge",
        _ => "face",
    };

    private static string Word(BoxFace face) => face.ToString().ToLowerInvariant();

    private static string Len(Length length) => CutListCsv.Text(length);

    private static string Count(int count, string noun)
        => $"{count.ToString(CultureInfo.InvariantCulture)} {noun}{(count == 1 ? string.Empty : "s")}";
}
