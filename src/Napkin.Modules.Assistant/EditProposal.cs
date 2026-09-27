using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Assistant;

/// <summary>Which of a box's three sizes a resize names, as the edit schema spells it.</summary>
public enum EditedSize
{
    /// <summary>"width": the box's size along its own X, in the plan.</summary>
    Width,

    /// <summary>"height": the box's size along its own Y, in the plan.</summary>
    Height,

    /// <summary>"depth": the box's third size, along its own Z.</summary>
    Depth,
}

/// <summary>
/// One edit in words exactly as the model wrote it (docs/design/llm-assistant.md &#xA7;4.5): which
/// part, as the context pack names it, and what to change — nothing read yet. Closed: exactly six
/// (a reflection test holds it to six, the <see cref="ModelReply"/> pattern), and the parser has no
/// case for anything else — no wall input, site value, code choice, phase, joint, cut or
/// relationship but the <see cref="ParamValue"/> a typed size already is.
/// </summary>
public abstract record PartEdit
{
    private PartEdit(string part) => Part = part;

    /// <summary>The part, as the model named it: <c>[n]</c> or the name the pack gives it.</summary>
    public string Part { get; }

    /// <summary><c>resize</c>: one of the part's three sizes to a length, as typing it does.</summary>
    /// <param name="Part">The part, as the model named it.</param>
    /// <param name="Size">Which size.</param>
    /// <param name="Length">The new size, as feet-inch text.</param>
    public sealed record Resize(string Part, EditedSize Size, string Length) : PartEdit(Part);

    /// <summary><c>move</c>: the part's south-west corner to a place in the plan, as the Part panel's position fields do.</summary>
    /// <param name="Part">The part, as the model named it.</param>
    /// <param name="X">Where the corner goes along X, as feet-inch text.</param>
    /// <param name="Y">Where the corner goes along Y, as feet-inch text.</param>
    public sealed record Move(string Part, string X, string Y) : PartEdit(Part);

    /// <summary><c>rename</c>: what the part is called.</summary>
    /// <param name="Part">The part, as the model named it.</param>
    /// <param name="Name">The new name.</param>
    public sealed record Rename(string Part, string Name) : PartEdit(Part);

    /// <summary><c>stock</c>: the stock the part is cut from, by the name the materials library prints.</summary>
    /// <param name="Part">The part, as the model named it.</param>
    /// <param name="Name">The stock's name.</param>
    public sealed record Stock(string Part, string Name) : PartEdit(Part);

    /// <summary><c>quantity</c>: how many identical pieces the part stands for.</summary>
    /// <param name="Part">The part, as the model named it.</param>
    /// <param name="Count">How many.</param>
    public sealed record Quantity(string Part, int Count) : PartEdit(Part);

    /// <summary><c>remove</c>: the part, gone.</summary>
    /// <param name="Part">The part, as the model named it.</param>
    public sealed record Remove(string Part) : PartEdit(Part);
}

/// <summary>
/// Edit in words (docs/design/llm-assistant.md &#xA7;4.3, &#xA7;4.5): the JSON the model is asked to
/// fill, napkin's strict parser for it, and the plan it becomes — one line per edit, each the very
/// request the panel makes for that change today, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// A part is named as the pack names it: exactly <c>[n]</c> for a design line of the pack, or
/// exactly a name the pack gives an entity (trimmed, letter case aside). No match, or two, refuses
/// the line — the refusal names both by their <c>[n]</c> — and nothing is guessed. A wall, an
/// opening, a note or any other entity that is not a furniture part refuses the line too: the
/// assistant edits parts (&#xA7;1).
/// </para>
/// <para>
/// Every length is read by napkin's own parser and a length that parser had to round onto the
/// 1/1024&#x2033; grid refuses the line (<c>wasRounded</c>): a person who says a size means that
/// size, and nothing here is rough — the opposite of Sketch from words' snap (&#xA7;4.3).
/// </para>
/// </remarks>
/// <param name="Edits">The edits, in the reply's order.</param>
public sealed record EditProposal(ImmutableArray<PartEdit> Edits)
{
    /// <summary>
    /// The JSON schema the model fills, byte for byte the one the MLX bridge compiles ahead of the
    /// first proposal (<c>native/NapkinMlx/Sources/NapkinMlxSchemas/Schemas.swift</c>): a list of
    /// edits, each exactly one of the six shapes, every field required, no other member. A test
    /// holds the two equal.
    /// </summary>
    public const string Schema = """{"type":"object","properties":{"edits":{"type":"array","items":{"anyOf":[{"type":"object","properties":{"edit":{"const":"resize"},"part":{"type":"string"},"dimension":{"enum":["width","height","depth"]},"length":{"type":"string"}},"required":["edit","part","dimension","length"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"move"},"part":{"type":"string"},"x":{"type":"string"},"y":{"type":"string"}},"required":["edit","part","x","y"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"rename"},"part":{"type":"string"},"name":{"type":"string"}},"required":["edit","part","name"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"stock"},"part":{"type":"string"},"stock":{"type":"string"}},"required":["edit","part","stock"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"quantity"},"part":{"type":"string"},"quantity":{"type":"integer"}},"required":["edit","part","quantity"],"additionalProperties":false},{"type":"object","properties":{"edit":{"const":"remove"},"part":{"type":"string"}},"required":["edit","part"],"additionalProperties":false}]}}},"required":["edits"],"additionalProperties":false}""";

    /// <summary>The undo step, and the gesture the accepted edits land in: "Assistant edit" (&#xA7;4.3).</summary>
    public const string What = "Assistant edit";

    /// <summary>What the note says when the reply is not a document this parser reads (&#xA7;4.3); nothing else happens.</summary>
    public const string Unreadable = SketchProposal.Unreadable;

    /// <summary>What the note says for a readable proposal with no edits in it: a question asked with parts selected gets this.</summary>
    public const string NoEdits = "The assistant proposed no edits. To ask a question instead, select nothing and ask again.";

    /// <summary>
    /// Every edit's members, by the name its <c>edit</c> member gives it: the six of &#xA7;4.5 and no
    /// other, each in the schema's order. The parser reads nothing that is not a key here.
    /// </summary>
    public static readonly ImmutableDictionary<string, ImmutableArray<string>> Members = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
    {
        ["resize"] = ["edit", "part", "dimension", "length"],
        ["move"] = ["edit", "part", "x", "y"],
        ["rename"] = ["edit", "part", "name"],
        ["stock"] = ["edit", "part", "stock"],
        ["quantity"] = ["edit", "part", "quantity"],
        ["remove"] = ["edit", "part"],
    }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>
    /// How a length is written on the sheet: feet, inches and a fraction at the 1/1024&#x2033; grid,
    /// so what the line says is exactly what accepting it states — 1'-6", 1'-4 1/4".
    /// </summary>
    static readonly LengthFormat Exactly = new FeetInchesFormat((int)Length.UnitsPerInch);

    /// <summary>Reads a reply, or null when it is not a proposal this parser reads.</summary>
    /// <param name="document">The reply's JSON text.</param>
    public static EditProposal? Parse(string document) => TryParse(document, out EditProposal? proposal, out _) ? proposal : null;

    /// <summary>
    /// Reads a reply strictly (&#xA7;4.3): a JSON object with exactly <c>edits</c>, a list; each edit
    /// an object whose <c>edit</c> is one of the six names and whose other members are exactly that
    /// edit's, every one a string but <c>quantity</c>, a whole number written as one, and
    /// <c>dimension</c> one of width, height or depth. An edit napkin does not make, an unknown
    /// member, a missing one, a member written twice, a null or a value of the wrong kind refuses
    /// the whole reply; nothing is defaulted or guessed.
    /// </summary>
    /// <param name="document">The reply's JSON text.</param>
    /// <param name="proposal">The proposal, when it was read.</param>
    /// <param name="why">Why it was not, in napkin's words, when it was not.</param>
    public static bool TryParse(string document, [NotNullWhen(true)] out EditProposal? proposal, [NotNullWhen(false)] out string? why)
    {
        ArgumentNullException.ThrowIfNull(document);
        proposal = null;

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(document);
        }
        catch (JsonException)
        {
            why = "it is not JSON";
            return false;
        }

        using (json)
        {
            if (!StrictJson.TryMembers(json.RootElement, ["edits"], "the reply", out Dictionary<string, JsonElement>? root, out why))
            {
                return false;
            }

            if (root["edits"].ValueKind != JsonValueKind.Array)
            {
                why = "\"edits\" is not a list";
                return false;
            }

            List<PartEdit> edits = [];
            int k = 0;
            foreach (JsonElement element in root["edits"].EnumerateArray())
            {
                k++;
                if (!TryEdit(element, $"edit {k.ToString(CultureInfo.InvariantCulture)}", out PartEdit? edit, out why))
                {
                    return false;
                }

                edits.Add(edit);
            }

            proposal = new EditProposal([.. edits]);
            why = null;
            return true;
        }
    }

    /// <summary>
    /// The sheet for this proposal (&#xA7;4.5): one line per edit, in order, each the request the panel
    /// makes for that change — typing a size (<see cref="DimensionEntry.RequestFor"/>, with the rough
    /// mark cleared as typing clears it, <see cref="RoughEntry.Typed"/>), the Part panel's position
    /// (<see cref="SetPosition"/> of its south-west corner), name (<see cref="SetName"/>), stock
    /// (<see cref="StockAssignment.RequestsFor(Sketch, Box, Part, StockItem?)"/>) and quantity
    /// (<see cref="SetPart"/>), and delete (<see cref="RemoveEntity"/>). A line napkin refuses says
    /// why and carries nothing to apply.
    /// </summary>
    /// <remarks>
    /// Every line is made against the design as it was when the question was asked; accepting it
    /// against any other is refused whole (the stale rule, <see cref="ProposalPlan.IsFor"/>). Two
    /// lines that would each set what the same part is — its stock, its quantity, or the rough mark
    /// a resize clears — to different things are not both offered: each is a whole
    /// <see cref="Part"/>, so the second would silently undo the first. The later is refused, to ask
    /// again once the first has landed.
    /// </remarks>
    /// <param name="design">The design the model was asked about.</param>
    /// <param name="pack">The context pack it was asked with, for what its <c>[n]</c> named.</param>
    /// <param name="library">The materials library a stock name is looked up in.</param>
    public ProposalPlan Plan(Design design, ContextPack pack, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(library);

        Dictionary<EntityId, Part> changed = [];
        return new ProposalPlan(What, design.Sketch, [.. Edits.Select(edit => Line(edit, design, pack, library, changed))]);
    }

    /// <summary>The note's closing line once the ticked edits have landed: "Made 2 edits."</summary>
    /// <param name="made">How many lines landed.</param>
    public static string ClosingLine(int made) => made == 0 ? "Changed nothing." : $"Made {EditCount(made)}.";

    /// <summary>The message bar's line after an acceptance (&#xA7;8): "Assistant edit: made 2 edits."</summary>
    /// <param name="made">How many lines landed.</param>
    public static string MessageLine(int made) => made == 0 ? $"{What}: changed nothing." : $"{What}: made {EditCount(made)}.";

    static string EditCount(int n) => $"{n.ToString(CultureInfo.InvariantCulture)} edit{(n == 1 ? string.Empty : "s")}";

    /// <summary>A length as the sheet writes it, exactly: 1'-6", 1'-4 1/4", -6".</summary>
    static string Written(Length length) => length.Format(Exactly).Text;

    /// <summary>
    /// What one edit comes to on a part napkin found: why napkin refuses it, or the sentence, the
    /// request, and — when the request sets the whole <see cref="Part"/> — what the part becomes.
    /// </summary>
    readonly record struct Change(string? Why, string Sentence, Request? Request, Part? Becomes)
    {
        public static Change Refused(string why) => new(why, string.Empty, null, null);

        public static Change To(string sentence, Request request, Part? becomes = null) => new(null, sentence, request, becomes);
    }

    /// <summary>One edit's line: the part it names, found; what the panel would put to the updater for it; or why not.</summary>
    static ProposalLine Line(PartEdit edit, Design design, ContextPack pack, MaterialsLibrary library, Dictionary<EntityId, Part> changed)
    {
        if (!TryFindPart(edit.Part, design, pack, out Box? box, out string label, out string? refusal))
        {
            return ProposalLine.Refusal($"{label}: refused, {refusal}");
        }

        Sketch sketch = design.Sketch;
        Part part = box.Part!;
        Change change = edit switch
        {
            PartEdit.Resize resize => Resize(sketch, box, part, resize),
            PartEdit.Move move => Move(box, move),
            PartEdit.Rename rename => rename.Name.Trim() is { Length: > 0 } name
                ? Change.To($"rename to \"{name}\"", new SetName(box.Id, name))
                : Change.Refused("an empty name"),
            PartEdit.Stock stock => Stock(sketch, box, part, stock, library),
            PartEdit.Quantity { Count: >= 1 } quantity => Change.To(
                $"quantity {part.Quantity.ToString(CultureInfo.InvariantCulture)} to {quantity.Count.ToString(CultureInfo.InvariantCulture)}",
                new SetPart(box.Id, part with { Quantity = quantity.Count }),
                part with { Quantity = quantity.Count }),
            PartEdit.Quantity quantity => Change.Refused($"a quantity of {quantity.Count.ToString(CultureInfo.InvariantCulture)}, not at least 1"),
            _ => Change.To("remove", new RemoveEntity(box.Id)),
        };

        // Each of these sets the whole Part: a second, different one for the same part would put the
        // first's field back as it was, silently. The later is refused instead.
        if (change.Why is null && change.Becomes is { } becomes)
        {
            if (changed.TryGetValue(box.Id, out Part? earlier) && earlier != becomes)
            {
                change = Change.Refused("another line already changes what this part is; ask for this once that has landed");
            }
            else
            {
                changed[box.Id] = becomes;
            }
        }

        return change.Why is { } why
            ? ProposalLine.Refusal($"{label}: refused, {why}")
            : new ProposalLine($"{label}: {change.Sentence}", [change.Request!]);
    }

    /// <summary><c>resize</c>: typing the size, exactly as the canvas's dimension field does it.</summary>
    static Change Resize(Sketch sketch, Box box, Part part, PartEdit.Resize resize)
    {
        string typed = resize.Length.Trim();
        if (!Length.TryParse(typed, out Length value, out bool wasRounded))
        {
            return Change.Refused("its new size is not a length napkin reads");
        }

        if (wasRounded)
        {
            return Change.Refused($"{typed} is not a size napkin can hold exactly");
        }

        if (value <= Length.Zero)
        {
            return Change.Refused($"a size of {Written(value)}");
        }

        (ParamRef size, Length now, PartDimension named) = resize.Size switch
        {
            EditedSize.Width => ((ParamRef)new BoxWidthRef(box.Id), box.Width, part.PlanAxes.X),
            EditedSize.Height => (new BoxHeightRef(box.Id), box.Height, part.PlanAxes.Y),
            _ => (new BoxDepthRef(box.Id), box.Depth, part.PlanAxes.OutOfPlane),
        };

        // Typing a size on a rough part firms it: the size and the clearing of the mark, one request (sketch-mode §3.1).
        Request request = RoughEntry.Typed(sketch, box.Id, DimensionEntry.RequestFor(sketch, size, value));
        return Change.To($"{SceneWords.Of(named).ToLowerInvariant()} {Written(now)} to {Written(value)}", request, part.Rough ? part with { Rough = false } : null);
    }

    /// <summary>
    /// <c>move</c>: the Part panel's typed place — the south-west corner of the part's extent to
    /// (x, y), its height kept — through the updater, so what is held to the part follows.
    /// </summary>
    static Change Move(Box box, PartEdit.Move move)
    {
        Length[] at = new Length[2];
        (string Member, string Text)[] asked = [("x", move.X.Trim()), ("y", move.Y.Trim())];
        for (int i = 0; i < asked.Length; i++)
        {
            if (!Length.TryParse(asked[i].Text, out at[i], out bool wasRounded))
            {
                return Change.Refused($"its {asked[i].Member} is not a length napkin reads");
            }

            if (wasRounded)
            {
                return Change.Refused($"{asked[i].Text} is not a place napkin can hold exactly");
            }
        }

        Point3 low = SpaceSnapResolver.Extent(box).Low;
        Point3 corner = new(at[0], at[1], low.Z);
        return Change.To($"move to x {Written(at[0])}, y {Written(at[1])}", new SetPosition(box.Id, box.Anchor + (corner - low)));
    }

    /// <summary>
    /// <c>stock</c>: the Part panel's assignment, for a name the materials library carries —
    /// looked up the way the panel looks up what is typed in its stock field — and nothing
    /// otherwise: the line is refused naming the three stocks nearest the part's sizes
    /// (<see cref="StockSuggestion"/>), and no stock is guessed.
    /// </summary>
    static Change Stock(Sketch sketch, Box box, Part part, PartEdit.Stock stock, MaterialsLibrary library)
    {
        string asked = stock.Name.Trim();
        if (!library.TryFind(asked, out StockItem item))
        {
            ImmutableArray<StockItem> near = StockSuggestion.For(part.SizeOn(box), library);
            string nearest = near.IsEmpty
                ? "nothing in it is near this part's sizes"
                : "nearest for this part's sizes: " + Listed([.. near.Take(3).Select(candidate => candidate.Name)], "or");
            return Change.Refused($"\"{asked}\" is not in napkin's materials library; {nearest}");
        }

        Part becomes = part with { Stock = item.Name };
        return Change.To($"stock {item.Name}", StockAssignment.RequestsFor(sketch, box, becomes, item), becomes);
    }

    /// <summary>
    /// The furniture part a reference names (&#xA7;4.5): exactly <c>[n]</c> for a design line of the
    /// pack, or exactly the name the pack gives an entity. No match, two, or something that is not a
    /// part, and the line is refused with the reason.
    /// </summary>
    static bool TryFindPart(
        string reference,
        Design design,
        ContextPack pack,
        [NotNullWhen(true)] out Box? box,
        out string label,
        [NotNullWhen(false)] out string? refusal)
    {
        box = null;
        string said = reference.Trim();
        EntityId id;
        if (IsItem(said, out int n))
        {
            if (pack.EntityAt(n) is not { } found)
            {
                label = said;
                refusal = $"{said} is not a part in the design";
                return false;
            }

            id = found;
        }
        else
        {
            int[] matches =
            [
                .. pack.Items
                    .Where(item => pack.EntityAt(item.N) is { } entity && string.Equals(DesignWords.NameOf(design, entity).Trim(), said, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.N),
            ];
            label = $"\"{said}\"";
            if (matches.Length != 1)
            {
                refusal = matches.Length == 0
                    ? "no part in the design is called that"
                    : $"{matches.Length.ToString(CultureInfo.InvariantCulture)} parts are called that: "
                      + Listed([.. matches.Select(match => $"[{match.ToString(CultureInfo.InvariantCulture)}]")], "and");
                return false;
            }

            id = pack.EntityAt(matches[0])!.Value;
        }

        label = DesignWords.NameOf(design, id);
        if (design.Sketch.Find<Box>(id) is not { } part || !IsPart(design.Sketch, part))
        {
            refusal = "it is not a part";
            return false;
        }

        box = part;
        refusal = null;
        return true;
    }

    /// <summary>
    /// Whether a box is a furniture part — one the assistant may edit (&#xA7;1): a box with a
    /// <see cref="Part"/> that is not a wall, an opening, a room, a deck or a roof. The Ask box sends
    /// a question as an edit when the selection holds one (&#xA7;19 of the note).
    /// </summary>
    /// <param name="sketch">The design the box is in.</param>
    /// <param name="box">The box.</param>
    public static bool IsPart(Sketch sketch, Box box)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(box);
        return box.Part is not null
               && !Wall.Is(sketch, box) && !Opening.Is(sketch, box) && !Room.Is(sketch, box) && !Deck.Is(sketch, box) && !Roof.Is(sketch, box);
    }

    /// <summary>Whether a reference is exactly <c>[n]</c>, and n.</summary>
    static bool IsItem(string said, out int n)
    {
        n = 0;
        return said.Length > 2
               && said[0] == '['
               && said[^1] == ']'
               && said[1..^1].All(char.IsAsciiDigit)
               && int.TryParse(said[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out n);
    }

    /// <summary>"A", "A or B", "A, B or C".</summary>
    static string Listed(string[] names, string conjunction)
        => names.Length == 1 ? names[0] : $"{string.Join(", ", names[..^1])} {conjunction} {names[^1]}";

    /// <summary>One edit, from its JSON: an <c>edit</c> napkin makes, exactly its members, each of the right kind.</summary>
    static bool TryEdit(JsonElement element, string where, [NotNullWhen(true)] out PartEdit? edit, [NotNullWhen(false)] out string? why)
    {
        edit = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            why = $"{where} is not an object";
            return false;
        }

        if (!element.TryGetProperty("edit", out JsonElement kind) || kind.ValueKind != JsonValueKind.String)
        {
            why = $"{where} does not say which edit it is";
            return false;
        }

        string name = kind.GetString()!;
        if (!Members.TryGetValue(name, out ImmutableArray<string> expected))
        {
            why = $"{where} is not an edit napkin makes: \"{name}\"";
            return false;
        }

        if (!StrictJson.TryMembers(element, expected, where, out Dictionary<string, JsonElement>? members, out why))
        {
            return false;
        }

        foreach (string member in expected.Where(member => member != "quantity"))
        {
            if (members[member].ValueKind != JsonValueKind.String)
            {
                why = $"{where}: \"{member}\" is not text";
                return false;
            }
        }

        string Text(string member) => members[member].GetString()!;
        string part = Text("part");
        switch (name)
        {
            case "resize":
                EditedSize? size = Text("dimension") switch
                {
                    "width" => EditedSize.Width,
                    "height" => EditedSize.Height,
                    "depth" => EditedSize.Depth,
                    _ => null,
                };
                if (size is not { } which)
                {
                    why = $"{where}: \"dimension\" is not width, height or depth";
                    return false;
                }

                edit = new PartEdit.Resize(part, which, Text("length"));
                break;

            case "move":
                edit = new PartEdit.Move(part, Text("x"), Text("y"));
                break;

            case "rename":
                edit = new PartEdit.Rename(part, Text("name"));
                break;

            case "stock":
                edit = new PartEdit.Stock(part, Text("stock"));
                break;

            case "quantity":
                if (members["quantity"].ValueKind != JsonValueKind.Number || !members["quantity"].TryGetInt32(out int count))
                {
                    why = $"{where}: \"quantity\" is not a whole number";
                    return false;
                }

                edit = new PartEdit.Quantity(part, count);
                break;

            default:
                edit = new PartEdit.Remove(part);
                break;
        }

        why = null;
        return true;
    }
}
