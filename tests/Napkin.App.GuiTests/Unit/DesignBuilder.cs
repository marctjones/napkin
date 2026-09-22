using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Napkin.App.Designs;
using Napkin.Core.Geometry;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Assembles a <see cref="Design"/> from named parts, giving every entity an id derived from its
/// name.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A test helper, and only that.</strong> It used to live in the application, because the
/// viewer built its samples in code while the scene reader (#6) and the sample files (#37) were
/// being written in parallel. The samples are files now, so the application has no reason to
/// assemble a design from parts; what is left is the tests' need for a small synthetic drawing —
/// a part 3 1/32&#x2033; wide to prove the &#x2248; marker, a wall to widen and watch a label
/// follow — that no fixture on disk should have to carry.
/// </para>
/// <para>
/// <strong>Ids are derived, not generated.</strong> <see cref="EntityId.New"/> would make a
/// different sketch every time, so "nothing moved" could not be asserted by comparing two builds
/// and a test could not name the part it wants. Hashing the design's name and the part's name gives
/// ids that are stable across runs, processes and platforms, and that are still globally unique the
/// way a GUID is meant to be.
/// </para>
/// <para>
/// This is a construction helper, not a format. Nothing in it is written to disk and nothing reads
/// it back.
/// </para>
/// </remarks>
public sealed class DesignBuilder
{
    readonly string _designName;
    readonly ImmutableDictionary<EntityId, string>.Builder _labels =
        ImmutableDictionary.CreateBuilder<EntityId, string>();

    Sketch _sketch = new(
        ImmutableDictionary<EntityId, Entity>.Empty,
        ImmutableDictionary<RelationshipId, Relationship>.Empty,
        ImmutableList<Layer>.Empty);

    /// <param name="designName">
    /// The design's name. It salts every id, so two designs never share one.
    /// </param>
    public DesignBuilder(string designName) => _designName = designName;

    /// <summary>Adds a layer and returns its id.</summary>
    public LayerId AddLayer(string name)
    {
        LayerId id = new(StableGuid($"{_designName}/layer/{name}"));
        _sketch = _sketch.WithLayer(new Layer(id, name));
        return id;
    }

    /// <summary>Adds a box.</summary>
    /// <param name="key">
    /// What this part is called in the design, which is what its id is derived from. It must be
    /// unique within the design — four legs are "Leg, front left" and so on — because two parts
    /// sharing a key would share an id, and the second would silently replace the first.
    /// </param>
    /// <param name="label">What is drawn on the part, or null to draw nothing on it.</param>
    /// <param name="layer">The layer it is drawn on.</param>
    /// <param name="anchor">The south-west corner.</param>
    /// <param name="width">Along X.</param>
    /// <param name="height">Along Y.</param>
    public EntityId AddBox(
        string key,
        string? label,
        LayerId layer,
        Point2 anchor,
        Length width,
        Length height)
    {
        EntityId id = Id(key);
        if (_sketch.Entities.ContainsKey(id))
        {
            throw new ArgumentException(
                $"{_designName} already has an entity called \"{key}\". Keys are what ids are " +
                "derived from, so they have to be unique within a design.",
                nameof(key));
        }

        _sketch = _sketch.WithEntity(new Box(id, layer, anchor, width, height, Angle.Zero));
        if (label is not null)
        {
            _labels[id] = label;
        }

        return id;
    }

    /// <summary>
    /// Adds a relationship that owns a size, so the dimension over it is a driving dimension and
    /// not a reference one (docs/design/geometry-model.md &#xA7;3.3).
    /// </summary>
    public RelationshipId AddDrivingValue(string name, ParamRef param, Length value)
    {
        RelationshipId id = new(StableGuid($"{_designName}/relationship/{name}"));
        _sketch = _sketch.WithRelationship(new ParamValue(id, param, value));
        return id;
    }

    /// <summary>Adds a dimension over a size — a part's width or height.</summary>
    public EntityId AddSizeDimension(
        string name,
        LayerId layer,
        ParamRef param,
        DimensionSide side,
        Length offset,
        RelationshipId? drives = null)
    {
        EntityId id = Id(name);
        _sketch = _sketch.WithEntity(new Dimension(
            id,
            layer,
            new ParamMeasurand(param),
            drives,
            new DimensionPlacement(offset, side)));
        return id;
    }

    /// <summary>Adds a dimension over the distance between two points along one axis.</summary>
    public EntityId AddDistanceDimension(
        string name,
        LayerId layer,
        PointRef from,
        PointRef to,
        Axis axis,
        DimensionSide side,
        Length offset)
    {
        EntityId id = Id(name);
        _sketch = _sketch.WithEntity(new Dimension(
            id,
            layer,
            new AxisMeasurand(from, to, axis),
            null,
            new DimensionPlacement(offset, side)));
        return id;
    }

    /// <summary>The id this design gives the entity with a key.</summary>
    public EntityId Id(string key) => IdFor(_designName, key);

    /// <summary>
    /// The id a design gives one of its entities, without building the design: how a test names
    /// the part it means.
    /// </summary>
    public static EntityId IdFor(string designName, string key) =>
        new(StableGuid($"{designName}/entity/{key}"));

    /// <summary>The finished design.</summary>
    public Design Build() => new(_designName, _sketch, _labels.ToImmutable());

    /// <summary>
    /// A GUID derived from a name: the first sixteen bytes of its SHA-256, which is stable
    /// everywhere and collides no more often than a random GUID does.
    /// </summary>
    static Guid StableGuid(string seed) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(seed)).AsSpan(0, 16));
}
