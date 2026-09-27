using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant;

/// <summary>
/// One part of a sketch proposal exactly as the model wrote it (docs/design/llm-assistant.md &#xA7;4.4):
/// every length feet-inch text, not yet read. Nothing here is trusted until
/// <see cref="SketchProposal.Plan"/> has read, snapped and limited it.
/// </summary>
/// <param name="Name">What to call the part; empty for napkin's next "Part N".</param>
/// <param name="Width">The plan size along X, as text.</param>
/// <param name="Height">The plan size along Y, as text.</param>
/// <param name="Depth">The third size, along Z, as text.</param>
/// <param name="X">The anchor's (south-west corner's) X in the plan, as text.</param>
/// <param name="Y">The anchor's Y in the plan, as text.</param>
/// <param name="Quantity">How many identical pieces the part stands for.</param>
public sealed record SketchPart(string Name, string Width, string Height, string Depth, string X, string Y, int Quantity);

/// <summary>
/// Sketch from words (docs/design/llm-assistant.md &#xA7;4.3&#x2013;&#xA7;4.4, &#xA7;9.2): the JSON the
/// model is asked to fill, napkin's strict parser for it, and the plan it becomes — one rough plank
/// per part, exactly the plank Rough mode's rectangle tool draws (sketch-mode &#xA7;2.3), and nothing
/// else: nothing stated, no relationship, no stock, no joint. Firm up does all of that afterwards.
/// </summary>
/// <param name="Parts">The parts, in the reply's order.</param>
/// <param name="Note">The model's one-line note. Required by the schema and parsed, never shown (see &#xA7;18 of the note).</param>
public sealed record SketchProposal(ImmutableArray<SketchPart> Parts, string Note)
{
    /// <summary>
    /// The JSON schema the model fills, byte for byte the one the MLX bridge compiles ahead of the
    /// first proposal (<c>native/NapkinMlx/Sources/NapkinMlxSchemas/Schemas.swift</c>): every field
    /// required, no other member. A test holds the two equal.
    /// </summary>
    public const string Schema = """{"type":"object","properties":{"parts":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"width":{"type":"string"},"height":{"type":"string"},"depth":{"type":"string"},"x":{"type":"string"},"y":{"type":"string"},"quantity":{"type":"integer"}},"required":["name","width","height","depth","x","y","quantity"],"additionalProperties":false}},"note":{"type":"string"}},"required":["parts","note"],"additionalProperties":false}""";

    /// <summary>The undo step, and the gesture the accepted planks land in: "Assistant sketch" (&#xA7;4.3).</summary>
    public const string What = "Assistant sketch";

    /// <summary>What the note says when the reply is not a document this parser reads (&#xA7;4.3); nothing else happens.</summary>
    public const string Unreadable = "The assistant's reply was not a proposal napkin could read.";

    /// <summary>What the note says for a readable proposal with no parts in it.</summary>
    public const string NoParts = "The assistant proposed no parts.";

    /// <summary>At most this many parts: a napkin sketch, not a kitchen (&#xA7;4.4). Later ones are refused, each on its line.</summary>
    public const int MaxParts = 24;

    /// <summary>At most this many pieces per part (&#xA7;4.4).</summary>
    public const int MaxQuantity = 12;

    /// <summary>
    /// The step the four plan lengths — width, height and the anchor's x and y — snap to: the rough
    /// ladder's floor, a whole inch (<see cref="SnapGrid.RoughStepInches"/> never goes below it), so
    /// a proposed plank is as round as one dragged out in Rough mode close up.
    /// </summary>
    public const double PlanStepInches = 1;

    /// <summary>The farthest an anchor coordinate may be from the origin, either way: 1000&#x2033; (&#xA7;4.4).</summary>
    public static readonly Length AnchorLimit = Length.Inches(1000);

    /// <summary>
    /// The step the depth snaps to: the ladder's finest rung, &#xBC;&#x2033;
    /// (<see cref="SnapGrid.Ladder"/>). Not the whole inch the plan lengths snap to — that would
    /// make the quick bench's 3/4&#x2033; planks an inch thick, against &#xA7;9.2's own sheet and the
    /// 3/4&#x2033; default depth a hand-drawn plank has (&#xA7;18 of the note).
    /// </summary>
    public static readonly double DepthStepInches = SnapGrid.Ladder[0];

    static readonly string[] PartMembers = ["name", "width", "height", "depth", "x", "y", "quantity"];

    static readonly LengthFormat InchesExactly = LengthFormat.Exact;

    /// <summary>Reads a reply, or null when it is not a proposal this parser reads.</summary>
    /// <param name="document">The reply's JSON text.</param>
    public static SketchProposal? Parse(string document) => TryParse(document, out SketchProposal? proposal, out _) ? proposal : null;

    /// <summary>
    /// Reads a reply strictly (&#xA7;4.3): a JSON object with exactly <c>parts</c> and <c>note</c>, each
    /// part an object with exactly the seven members of <see cref="Schema"/>, every length a string
    /// and the quantity a whole number written as one. An unknown member, a missing one, a member
    /// written twice, a null or a value of the wrong kind refuses the whole reply; nothing is
    /// defaulted or guessed.
    /// </summary>
    /// <param name="document">The reply's JSON text.</param>
    /// <param name="proposal">The proposal, when it was read.</param>
    /// <param name="why">Why it was not, in napkin's words, when it was not.</param>
    public static bool TryParse(string document, [NotNullWhen(true)] out SketchProposal? proposal, [NotNullWhen(false)] out string? why)
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
            if (!StrictJson.TryMembers(json.RootElement, ["parts", "note"], "the reply", out Dictionary<string, JsonElement>? root, out why))
            {
                return false;
            }

            if (root["parts"].ValueKind != JsonValueKind.Array)
            {
                why = "\"parts\" is not a list";
                return false;
            }

            if (root["note"].ValueKind != JsonValueKind.String)
            {
                why = "\"note\" is not text";
                return false;
            }

            List<SketchPart> parts = [];
            int k = 0;
            foreach (JsonElement element in root["parts"].EnumerateArray())
            {
                k++;
                if (!TryPart(element, $"part {k.ToString(CultureInfo.InvariantCulture)}", out SketchPart? part, out why))
                {
                    return false;
                }

                parts.Add(part);
            }

            proposal = new SketchProposal([.. parts], root["note"].GetString()!);
            why = null;
            return true;
        }
    }

    /// <summary>
    /// The sheet for this proposal (&#xA7;4.4): one line per part, in order. A part napkin can draw
    /// is one <see cref="AddEntity"/> of a rough plank — <see cref="Box.AsDrawn"/> at its snapped
    /// anchor with its snapped sizes, carrying <c>Part(Stock: null, Species: null, Quantity,
    /// PlanAxes) { Rough = true }</c> with the longer plan side its length, as the rectangle tool's
    /// plank does (<see cref="RoughEntry.PlanAxesFor"/>), named as the model named it or napkin's
    /// next "Part N". Every length is read by napkin's own parser and snapped
    /// (<see cref="PlanStepInches"/>, <see cref="DepthStepInches"/>), so a value off the grid never
    /// reaches the design. Refused, each on its own line with the reason: a length napkin cannot
    /// read, a size of zero or less once snapped, an anchor coordinate beyond
    /// &#xB1;<see cref="AnchorLimit"/>, a quantity outside 1&#x2013;<see cref="MaxQuantity"/>, and every
    /// part after the <see cref="MaxParts"/>th.
    /// </summary>
    /// <param name="sketch">The design the model was asked about: the plan is refused against any other (the stale rule).</param>
    /// <param name="layer">The layer new parts go on (<see cref="DesignEditor.LayerForNewParts"/>).</param>
    /// <param name="nextName">Napkin's next part name, asked only for a part the model left unnamed that is not refused.</param>
    public ProposalPlan Plan(Sketch sketch, LayerId layer, Func<string> nextName)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(nextName);

        (string? Refusal, string Name, Point2 Anchor, Length Width, Length Height, Length Depth, int Quantity)[] read =
            [.. Parts.Select((part, i) => Read(part, i + 1))];

        // Ids in the reply's order, as parts drawn one after another would have them: Firm up reads
        // the lower id as the older part (sketch-mode §3.2), and ids made in one millisecond are not
        // otherwise in the order they were made.
        Queue<EntityId> ids = new(Enumerable.Range(0, read.Count(part => part.Refusal is null)).Select(_ => EntityId.New()).Order());

        List<ProposalLine> lines = [];
        foreach ((string? refusal, string name, Point2 anchor, Length width, Length height, Length depth, int quantity) in read)
        {
            if (refusal is not null)
            {
                lines.Add(ProposalLine.Refusal(refusal));
                continue;
            }

            string called = name.Length > 0 ? name : nextName();
            Box plank = Box.AsDrawn(ids.Dequeue(), layer, anchor, width, height, depth, Angle.Zero) with
            {
                Name = called,
                Part = new Part(Stock: null, Species: null, quantity, RoughEntry.PlanAxesFor(width, height)) { Rough = true },
            };

            string pieces = quantity == 1 ? string.Empty : $", {quantity.ToString(CultureInfo.InvariantCulture)} pieces";
            lines.Add(new ProposalLine(
                $"{called}: {Inches(width)} × {Inches(height)} × {Inches(depth)} at ({Inches(anchor.X)}, {Inches(anchor.Y)}){pieces}",
                [new AddEntity(plank)]));
        }

        return new ProposalPlan(What, sketch, [.. lines]);
    }

    /// <summary>
    /// The note's closing line once the ticked planks are drawn (&#xA7;4.4): "Drew 4 rough parts.
    /// Next: F to firm up."
    /// </summary>
    /// <param name="drawn">How many planks landed.</param>
    public static string ClosingLine(int drawn)
        => drawn == 0 ? "Drew nothing." : $"Drew {RoughParts(drawn)}. Next: F to firm up.";

    /// <summary>The message bar's line after an acceptance (&#xA7;8): "Assistant sketch: drew 4 rough parts."</summary>
    /// <param name="drawn">How many planks landed.</param>
    public static string MessageLine(int drawn)
        => drawn == 0 ? $"{What}: drew nothing." : $"{What}: drew {RoughParts(drawn)}.";

    static string RoughParts(int n) => $"{n.ToString(CultureInfo.InvariantCulture)} rough part{(n == 1 ? string.Empty : "s")}";

    /// <summary>One part read, snapped and limited: a refusal sentence, or what to draw.</summary>
    static (string? Refusal, string Name, Point2 Anchor, Length Width, Length Height, Length Depth, int Quantity) Read(SketchPart part, int k)
    {
        string name = part.Name.Trim();
        string label = name.Length > 0 ? name : $"Unnamed part {k.ToString(CultureInfo.InvariantCulture)}";

        // Width, height, depth, x, y: each read by napkin's own parser, then snapped — which makes
        // the parser's wasRounded moot, since whatever it rounded onto the 1/1024" grid is rounded
        // again onto a far coarser step (§4.4).
        (string Member, string Text, double Step)[] asked =
        [
            ("width", part.Width, PlanStepInches),
            ("height", part.Height, PlanStepInches),
            ("depth", part.Depth, DepthStepInches),
            ("x", part.X, PlanStepInches),
            ("y", part.Y, PlanStepInches),
        ];
        Length[] snapped = new Length[asked.Length];
        string? unreadable = null;
        for (int i = 0; i < asked.Length && unreadable is null; i++)
        {
            if (Length.TryParse(asked[i].Text, out Length parsed, out _))
            {
                snapped[i] = SnapGrid.Snap(parsed, asked[i].Step);
            }
            else
            {
                unreadable = asked[i].Member;
            }
        }

        (Length width, Length height, Length depth, Length x, Length y) = (snapped[0], snapped[1], snapped[2], snapped[3], snapped[4]);
        int flat = Array.FindIndex([width, height, depth], size => size <= Length.Zero);

        string? refusal =
            k > MaxParts ? $"more than {MaxParts.ToString(CultureInfo.InvariantCulture)} parts"
            : unreadable is not null ? $"its {unreadable} is not a length napkin reads"
            : flat >= 0 ? $"a size of {Inches(snapped[flat])}"
            : Length.Abs(x) > AnchorLimit || Length.Abs(y) > AnchorLimit ? $"an anchor at ({Inches(x)}, {Inches(y)}), beyond ±{Inches(AnchorLimit)}"
            : part.Quantity is < 1 or > MaxQuantity ? $"a quantity of {part.Quantity.ToString(CultureInfo.InvariantCulture)}, not 1 to {MaxQuantity.ToString(CultureInfo.InvariantCulture)}"
            : null;

        return (refusal is null ? null : $"{label}: refused, {refusal}", name, new Point2(x, y), width, height, depth, part.Quantity);
    }

    /// <summary>A length as the sheet writes it: plain inches and a reduced fraction, no mark — "48", "3/4", "-6".</summary>
    static string Inches(Length length) => length.Format(InchesExactly).Text.TrimEnd('"');

    /// <summary>A part, from its JSON: exactly the seven members, six strings and a whole number.</summary>
    static bool TryPart(JsonElement element, string where, [NotNullWhen(true)] out SketchPart? part, [NotNullWhen(false)] out string? why)
    {
        part = null;
        if (!StrictJson.TryMembers(element, PartMembers, where, out Dictionary<string, JsonElement>? members, out why))
        {
            return false;
        }

        foreach (string member in PartMembers[..^1])
        {
            if (members[member].ValueKind != JsonValueKind.String)
            {
                why = $"{where}: \"{member}\" is not text";
                return false;
            }
        }

        if (members["quantity"].ValueKind != JsonValueKind.Number || !members["quantity"].TryGetInt32(out int quantity))
        {
            why = $"{where}: \"quantity\" is not a whole number";
            return false;
        }

        part = new SketchPart(
            members["name"].GetString()!,
            members["width"].GetString()!,
            members["height"].GetString()!,
            members["depth"].GetString()!,
            members["x"].GetString()!,
            members["y"].GetString()!,
            quantity);
        return true;
    }
}
