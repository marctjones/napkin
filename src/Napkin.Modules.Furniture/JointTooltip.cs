using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// One joint in a sentence, for its marker's tooltip and its line in the panels (joinery note &#xA7;5.3):
/// "Rabbet &#x2014; Drawer side, left, A &#x2190; Drawer box front, A. 1/4" deep, 1/2" wide. 3 brads
/// (18 ga x 1), glue." A fastener's size is the person's typed choice or "size not chosen".
/// </summary>
public static class JointTooltip
{
    /// <summary>The words for a joint's type.</summary>
    /// <param name="type">The type.</param>
    public static string TypeName(JointType type) => type switch
    {
        JointType.Butt => "Butt",
        JointType.Groove => "Groove",
        JointType.Rabbet => "Rabbet",
        JointType.HalfLap => "Half-lap",
        _ => "Tabletop",
    };

    /// <summary>What the sentence adds after the type when the joint's parts have moved apart.</summary>
    public const string PartsNoLongerTouch = " (parts no longer touch)";

    /// <summary>Why a joint that needs a depth is refused without one: "A rabbet needs a depth greater than zero, like 1/4"."</summary>
    /// <param name="type">The joint's type.</param>
    public static string DepthRefusal(JointType type) => $"A {TypeName(type).ToLowerInvariant()} needs a depth greater than zero, like 1/4\".";

    /// <summary>Why joining two parts did nothing when they do not touch: "Part 1 and Part 3 don't touch."</summary>
    /// <param name="first">The first part's name.</param>
    /// <param name="second">The second part's name.</param>
    public static string DontTouch(string first, string second) => $"{first} and {second} don't touch.";

    /// <summary>The one letter its marker carries: B butt, G groove, R rabbet, L half-lap, T tabletop (&#xA7;5.2).</summary>
    /// <param name="type">The type.</param>
    public static char Letter(JointType type) => type switch
    {
        JointType.Butt => 'B',
        JointType.Groove => 'G',
        JointType.Rabbet => 'R',
        JointType.HalfLap => 'L',
        _ => 'T',
    };

    /// <summary>The joint as a sentence.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="joint">The joint.</param>
    /// <param name="nameOf">What to call a part, or null for its own name.</param>
    public static string Of(Sketch sketch, Joint joint, Func<EntityId, string>? nameOf = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(joint);

        JointShape? shape = JointGeometry.Of(sketch, joint);
        bool satisfied = JointGeometry.IsSatisfied(sketch, joint);
        nameOf ??= id => NameOf(sketch, id);
        string receiving = nameOf(joint.Receiving.Box);
        string inserted = nameOf(joint.Inserted.Box);

        StringBuilder text = new();
        text.Append(TypeName(joint.Type)).Append(satisfied ? string.Empty : PartsNoLongerTouch)
            .Append(" — ").Append(receiving).Append(" ← ").Append(inserted).Append('.');

        if (joint.Depth is { } depth)
        {
            text.Append(' ').Append(CutListCsv.Text(depth)).Append(" deep");
            Length? width = shape switch
            {
                { Groove: { } groove } => groove.Width,
                { Rabbet: { } rabbet } => rabbet.Width,
                _ => null,
            };
            text.Append(width is { } w ? $", {CutListCsv.Text(w)} wide." : ".");
        }

        List<string> how = [];
        if (Recipes.FastenerOf(joint.Fastening.Kind) is { } kind && sketch.Find<Box>(joint.Inserted.Box) is { } box)
        {
            int? count = joint.Fastening.Count ?? (shape is { } s ? Recipes.Count(joint, s.JointLength) : null);
            Length? thickness = Recipes.DependsOnThickness(kind) && box.Part is { } part ? part.SizeOn(box).Thickness : null;
            string size = FastenerList.ChoiceFor(sketch, kind, thickness)?.Size.Trim() is { Length: > 0 } typed ? typed : SuppliesList.SizeNotChosen;
            string noun = SuppliesList.KindName(kind).ToLowerInvariant();
            how.Add(count is { } n ? $"{n} {noun}{(n == 1 ? string.Empty : "s")} ({size})" : $"{noun}s ({size})");
        }

        if (joint.Glue)
        {
            how.Add("glue");
        }

        if (how.Count > 0)
        {
            string sentence = string.Join(", ", how);
            text.Append(' ').Append(char.ToUpperInvariant(sentence[0])).Append(sentence, 1, sentence.Length - 1).Append('.');
        }

        return text.ToString();
    }

    private static string NameOf(Sketch sketch, EntityId id)
        => sketch.Find(id) is { Name.Length: > 0 } entity ? entity.Name : "a part";
}
