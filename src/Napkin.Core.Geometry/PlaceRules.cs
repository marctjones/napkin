using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// What a relationship may pair and what a dimension may measure, judged on the world axes each
/// place fixes (<c>docs/design/assembly-model.md</c> &#xA7;2.3, invariant 13). One rule for every
/// door: <see cref="Sketch.Validate"/> — so the loader and every post-write check — and
/// <see cref="DirectUpdater"/>'s <see cref="AddRelationship"/> and <see cref="AddEntity"/> all read
/// it from here, the way <see cref="CutRules"/> is the one reading of a cut's fit.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The rule.</strong> A feature fixes a set of world axes; <see cref="Coincident"/> and
/// <see cref="Flush"/> mean "equal on every axis both fix" (&#xA7;2.1). So a
/// <see cref="Coincident"/> is legal when the two places have two or three axes in common — a face
/// never qualifies, two edges must be parallel, a node meets an upright edge or a centre on X and
/// Y — and a <see cref="Flush"/> when both fix exactly one axis, the same one.
/// <see cref="AxisDistance"/> needs both places to fix its axis, and <see cref="Centered"/> all
/// three. A pairing that fails is refused as <see cref="ValidationErrorKind.PlacesNotComparable"/>
/// rather than stored and quietly violated.
/// </para>
/// <para>
/// The rule is about what the direct updater holds, which is boxes on the 24 orientations. A box
/// spun off the quarter turns comes only from a solver-written file, and its side faces fix no
/// world axis at all; this has nothing to say about a relationship on one, and the checker judges
/// it geometrically in the tolerance class, as geometry-model &#xA7;10.2 already does.
/// </para>
/// </remarks>
public static class PlaceRules
{
    /// <summary>
    /// Why this relationship cannot hold on the places it names, or <see langword="null"/> when it
    /// can — or when a place cannot be read (a dangling reference, reported on its own) or belongs to
    /// a box off the quarter turns.
    /// </summary>
    public static ValidationError? Refusal(Sketch sketch, Relationship relationship)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(relationship);

        if (StrutPlaceRefusal(sketch, relationship) is { } strutPlace)
        {
            return strutPlace;
        }

        switch (relationship)
        {
            case Coincident coincident when Places(sketch, coincident.A, coincident.B) is [var a, var b]:
            {
                ImmutableArray<Axis> common = Place.Common(a, b);
                return common.Length >= 2
                    ? null
                    : NotComparable(
                        relationship,
                        $"{Fixes(sketch, coincident.A, a)}; {Fixes(sketch, coincident.B, b)}. "
                        + $"A coincident needs two or three axes both places fix, and these share {InWords(common)}.");
            }

            case Flush flush when Places(sketch, flush.A, flush.B) is [var a, var b]:
                return a.Count == 1 && b.Count == 1 && a.Axes[0] == b.Axes[0]
                    ? null
                    : NotComparable(
                        relationship,
                        $"{Fixes(sketch, flush.A, a)}; {Fixes(sketch, flush.B, b)}. "
                        + "A flush needs two planes: both places fixing exactly one axis, the same one.");

            case AxisDistance distance when Places(sketch, distance.From, distance.To) is [var from, var to]:
                return from.Fixes(distance.Axis) && to.Fixes(distance.Axis)
                    ? null
                    : NotComparable(
                        relationship,
                        $"{Fixes(sketch, distance.From, from)}; {Fixes(sketch, distance.To, to)}. "
                        + $"A distance along {distance.Axis} needs both places to fix {distance.Axis}.");

            case Centered centered
                when Places(sketch, centered.Middle, centered.A, centered.B) is [var middle, var a, var b]:
                return middle.Fixes(centered.Axis) && a.Fixes(centered.Axis) && b.Fixes(centered.Axis)
                    ? null
                    : NotComparable(
                        relationship,
                        $"{Fixes(sketch, centered.Middle, middle)}; {Fixes(sketch, centered.A, a)}; "
                        + $"{Fixes(sketch, centered.B, b)}. Centring along {centered.Axis} needs all three to fix {centered.Axis}.");

            case Joint joint when Places(sketch, joint.Receiving, joint.Inserted) is [var receiving, var inserted]:
                return receiving.Count == 1 && inserted.Count == 1 && receiving.Axes[0] == inserted.Axes[0]
                    ? null
                    : NotComparable(
                        relationship,
                        $"{Fixes(sketch, joint.Receiving, receiving)}; {Fixes(sketch, joint.Inserted, inserted)}. "
                        + "A joint needs two faces that can touch: both fixing exactly one axis, the same one.");

            default:
                return null;
        }
    }

    /// <summary>
    /// Why this dimension cannot be drawn in the plan, or <see langword="null"/> when it can
    /// (<c>docs/design/assembly-model.md</c> invariant 13, &#xA7;7.3). Judged in world terms through
    /// the owning box's orientation <em>as the sketch has it</em>: a request that would turn a box —
    /// <c>SetOrientation</c>, &#xA7;2.4 — asks this of the sketch it would write, and a dimension that
    /// would leave the plan after the turn is a refusal there.
    /// </summary>
    /// <remarks>
    /// A size lies in the plan when its local axis, tipped by the box's face-up, lands on world X or
    /// Y; the spin about Z never changes that, so this holds off the quarter turns as well. An axis
    /// span lies in the plan when its axis is X or Y and both places fix it. No dimension measures
    /// along world Z in this design: a depth on a box lying as drawn is refused, and so is a width on
    /// a box standing on its east face.
    /// </remarks>
    public static ValidationError? MeasurandRefusal(Sketch sketch, Dimension dimension)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(dimension);

        switch (dimension.Measures)
        {
            case ParamMeasurand { Param: var size } when LocalAxisOf(size) is { } local
                                                        && sketch.Find<Box>(size.Owner) is { } box:
            {
                Axis world = new Orientation(box.FaceUp, Angle.Zero).Image(local).Axis;
                return world == Axis.Z
                    ? LeavesThePlan(
                        dimension,
                        $"measures the {SizeName(size)} of {NameOf(sketch, box.Id)}, which stands along world Z with "
                        + $"its {box.FaceUp.ToString().ToLowerInvariant()} face up. A dimension lies in the plan; "
                        + "hold that size with a typed value instead.")
                    : null;
            }

            // A strut's cross-section is square to its centreline, which is not along any axis, so
            // it lies in the plan only by accident of the lean; it is held by a typed value instead.
            case ParamMeasurand { Param: StrutHeightRef or StrutDepthRef }:
                return LeavesThePlan(
                    dimension,
                    $"measures the cross-section of {NameOf(sketch, ((ParamMeasurand)dimension.Measures).Param.Owner)}, "
                    + "an angled part, which does not lie in the plan. Hold that size with a typed value instead.");

            case AxisMeasurand span:
            {
                if (span.Axis is not (Axis.X or Axis.Y))
                {
                    return LeavesThePlan(
                        dimension,
                        $"measures along {span.Axis}. A dimension lies in the plan and measures along X or Y.");
                }

                if (Readable(sketch, span.From) is not { } from || Readable(sketch, span.To) is not { } to)
                {
                    return null;
                }

                return from.Fixes(span.Axis) && to.Fixes(span.Axis)
                    ? null
                    : LeavesThePlan(
                        dimension,
                        $"measures along {span.Axis}, but {Fixes(sketch, span.From, from)} and {Fixes(sketch, span.To, to)}.");
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// A place in words, for a message: "Apron's north face", "Leg's top south-west corner",
    /// "node N1". Names the entity by its name when it has one and by its id otherwise.
    /// </summary>
    public static string Describe(Sketch sketch, PlaceRef reference)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(reference);

        string name = NameOf(sketch, reference.Owner);
        return reference switch
        {
            NodeRef => $"node {name}",
            SegmentRef => $"segment {name}",
            CenterRef => $"{name}'s centre",
            FeatureRef feature => $"{name}'s {InWords(feature.Feature)}",
            StrutEndRef end => $"{name}'s {(end.End == StrutEnd.From ? "from" : "to")} end",
            StrutFaceRef face => $"{name}'s {face.Face.ToString().ToLowerInvariant()} face",
            StrutEndFaceRef endFace => $"{name}'s {(endFace.End == StrutEnd.From ? "from" : "to")} end face",
            _ => name,
        };
    }

    /// <summary>
    /// A feature in words, in the box's local frame: "north face", "south-west edge", "top east
    /// edge", "bottom north-west corner".
    /// </summary>
    public static string InWords(BoxFeature feature)
    {
        ImmutableArray<BoxFace> faces = feature.Faces;
        string? northSouth = Word(faces, BoxFace.South, BoxFace.North);
        string? eastWest = Word(faces, BoxFace.East, BoxFace.West);
        string? cap = Word(faces, BoxFace.Bottom, BoxFace.Top);
        string sides = string.Join("-", new[] { northSouth, eastWest }.OfType<string>());

        return faces.Length switch
        {
            1 => $"{sides}{cap} face",
            2 when cap is null => $"{sides} edge",
            2 => $"{cap} {sides} edge",
            3 => $"{cap} {sides} corner",
            _ => "feature naming no faces",
        };
    }

    private static string? Word(ImmutableArray<BoxFace> faces, BoxFace one, BoxFace other)
        => faces.Contains(one) ? one.ToString().ToLowerInvariant()
            : faces.Contains(other) ? other.ToString().ToLowerInvariant()
            : null;

    // Every place readable, and every box among them on one of the 24 orientations; otherwise
    // nothing, and the rule has no opinion.
    private static Place[]? Places(Sketch sketch, params PlaceRef[] references)
    {
        Place[] places = new Place[references.Length];
        for (int i = 0; i < references.Length; i++)
        {
            if (references[i] is CenterRef or FeatureRef
                && sketch.Find<Box>(references[i].Owner) is { Orientation.IsExact: false })
            {
                return null;
            }

            if (Readable(sketch, references[i]) is not { } place)
            {
                return null;
            }

            places[i] = place;
        }

        return places;
    }

    // What a place fixes, or nothing when it cannot be read — including a place whose coordinates
    // overflow, which is the checker's to report as a file out of range, not this rule's to throw
    // from inside Sketch.Validate.
    private static Place? Readable(Sketch sketch, PlaceRef reference)
    {
        try
        {
            return sketch.TryPlaceOf(reference);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static string Fixes(Sketch sketch, PlaceRef reference, Place place)
        => $"{Describe(sketch, reference)} fixes {place.AxesInWords()}";

    private static string InWords(ImmutableArray<Axis> axes) => axes switch
    {
        [] => "none",
        [var only] => $"only {only}",
        _ => string.Join(" and ", axes),
    };

    /// <summary>
    /// A strut's face that cannot be held, said in words (<c>docs/design/angled-parts.md</c> &#xA7;3.2):
    /// an end's cut face, which is joinery's (#193); a long face of a strut that leans two ways, which
    /// is square to nothing; and a long face across an odd size, which would sit half a unit off the
    /// grid. The solver (#28) is where a flush to a two-way lean belongs; this names it instead of
    /// approximating it.
    /// </summary>
    private static ValidationError? StrutPlaceRefusal(Sketch sketch, Relationship relationship)
    {
        foreach (PlaceRef place in PlacesNamed(relationship))
        {
            switch (place)
            {
                case StrutEndFaceRef:
                    return NotComparable(
                        relationship,
                        $"{Describe(sketch, place)} is where a joint will meet a strut's end, which napkin cannot hold yet; "
                        + "a strut's two ends and the faces of a strut that leans one way are places it can.");

                case StrutFaceRef when relationship is not Flush:
                    return NotComparable(
                        relationship,
                        $"{Describe(sketch, place)} is a face of an angled part, which only a flush can hold.");

                case StrutFaceRef face when sketch.Find<Strut>(face.Strut) is { } strut:
                    if (Strut.FaceNormal(strut.Direction, strut.Reference, face.Face) is null)
                    {
                        bool leansTwoWays = strut.Direction is { Dx.Units: not 0, Dy.Units: not 0, Dz.Units: not 0 };
                        return NotComparable(
                            relationship,
                            leansTwoWays
                                ? $"{Describe(sketch, place)} is not square to anything: the strut leans two ways, so none of "
                                  + "its faces is. napkin can't hold this."
                                : $"{Describe(sketch, place)} is not square to any axis: it tilts with the lean. The two faces "
                                  + "square to the axis the strut does not lean along are the ones a flush can hold.");
                    }

                    if (strut.FacePlane(face.Face) is null)
                    {
                        return NotComparable(
                            relationship,
                            $"{Describe(sketch, place)} sits half of {strut.SizeAcross(face.Face)} from the strut's centreline, "
                            + "which is an odd number of 1/1024″ units and so half a unit off the grid.");
                    }

                    break;
            }
        }

        return null;
    }

    /// <summary>Every place a relationship names, in field order.</summary>
    internal static IEnumerable<PlaceRef> PlacesNamed(Relationship relationship) => relationship switch
    {
        Coincident r => [r.A, r.B],
        Horizontal r => [r.Edge],
        Vertical r => [r.Edge],
        Flush r => [r.A, r.B],
        AxisDistance r => [r.From, r.To],
        Centered r => [r.Middle, r.A, r.B],
        Parallel r => [r.A, r.B],
        Perpendicular r => [r.A, r.B],
        AngleBetween r => [r.A, r.B],
        Distance r => [r.A, r.B],
        PointOnEdge r => [r.Point, r.Edge],
        Symmetric r => [r.A, r.B, r.Mirror],
        Tangent r => [r.A, r.B],
        Joint r => [r.Receiving, r.Inserted],
        _ => [],
    };

    private static ValidationError NotComparable(Relationship relationship, string why)
        => new(ValidationErrorKind.PlacesNotComparable, $"Relationship {relationship.Id} cannot hold: {why}");

    private static ValidationError LeavesThePlan(Dimension dimension, string why)
        => new(ValidationErrorKind.MeasurandLeavesThePlan, $"Dimension {dimension.Id} {why}");

    private static Axis? LocalAxisOf(ParamRef size) => size switch
    {
        BoxWidthRef => Axis.X,
        BoxHeightRef => Axis.Y,
        BoxDepthRef => Axis.Z,
        _ => null,
    };

    private static string SizeName(ParamRef size) => size switch
    {
        BoxWidthRef => "width",
        BoxHeightRef => "height",
        _ => "depth",
    };

    private static string NameOf(Sketch sketch, EntityId id)
        => sketch.Find(id) is { Name.Length: > 0 } entity ? entity.Name : id.ToString();
}
