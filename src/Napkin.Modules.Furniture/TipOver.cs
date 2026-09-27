using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>One of the three stability estimates (§4.1): its words and its margin in lb·in (positive stands).</summary>
/// <param name="Section">"9.2.1", "9.2.2" or "9.2.3".</param>
/// <param name="Margin">Restoring less tipping moment about the front edge, lb·in; null when not estimated.</param>
/// <param name="Text">The line the lists show.</param>
public sealed record TipOverLine(string Section, decimal? Margin, string Text);

/// <summary>The tip-over block: the scope flag, the three estimates or why there are none, and the notes.</summary>
/// <param name="Scope">The F2057-23 §1.1 scope sentence, or why napkin cannot say.</param>
/// <param name="Lines">The three estimates, when the design is marked clothing storage and every part has a density.</param>
/// <param name="Notes">What napkin assumed, said once.</param>
public sealed record TipOverReport(string Scope, ImmutableArray<TipOverLine> Lines, ImmutableArray<string> Notes);

/// <summary>
/// The tip-over estimate (docs/design/furniture-checks.md §4): three static moment balances about the
/// unit's front floor edge, one per ASTM F2057-23 stability test as the Federal Register restates it
/// (88 FR 28405, Data/f2057-fr.json). An estimate that approximates the test as described, never "complies".
/// </summary>
public static class TipOver
{
    static readonly Lazy<JsonElement> Data = new(() =>
    {
        using Stream stream = typeof(TipOver).Assembly.GetManifestResourceStream("Napkin.Modules.Furniture.Data.f2057-fr.json")!;
        return JsonDocument.Parse(stream).RootElement.Clone();
    });

    static decimal Number(params string[] path)
    {
        JsonElement at = Data.Value;
        foreach (string step in path)
        {
            at = at.GetProperty(step);
        }

        return decimal.Parse(at.GetString()!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    /// <summary>A species' density at 12 % MC, lb/ft³: Wood Handbook Eq. 4-14 (p. 4–12), ρ₁₂ = ρ_w × G₁₂ × (1 + 12/100), ρ_w = 62.43 lb/ft³ (p. 4–11).</summary>
    public static decimal DensityLbPerFt3(WoodSpecies species)
    {
        ArgumentNullException.ThrowIfNull(species);
        return Number("woodDensity", "waterDensityLbPerFt3") * species.SpecificGravity * (1m + (Number("woodDensity", "moisturePercent") / 100m));
    }

    sealed record Piece(Box Box, decimal Weight, decimal Y, decimal Z, Length? Drawer);

    /// <summary>The tip-over block for a design (§4): the scope flag always; the three estimates when it is marked clothing storage.</summary>
    public static TipOverReport Of(Sketch sketch, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(library);
        Box[] parts = [.. sketch.Entities.Values.OfType<Box>().Where(box => box.Part is not null).OrderBy(box => box.Id)];
        if (parts.Length == 0)
        {
            return new TipOverReport(string.Empty, [], []);
        }

        ImmutableArray<Drawer> drawers = JointGroups.Drawers(sketch);
        Dictionary<EntityId, Length> opens = [];
        foreach (Drawer drawer in drawers)
        {
            foreach (Box member in drawer.Members)
            {
                opens[member.Id] = drawer.Extension;
            }
        }

        // Weight, part by part: volume × the species' density; a sheet good or an untabled species has none.
        List<string> missing = [];
        List<Piece> pieces = [];
        foreach (Box box in parts)
        {
            Part part = box.Part!;
            WoodSpecies? species = library.TryFind(part.Stock, out StockItem stock) && stock is PanelStock ? null : WoodProperties.Shipped.Find(part.Species);
            if (species is null)
            {
                missing.Add(box.Name);
                continue;
            }

            (Point3 low, Point3 high) = JointGeometry.Extent(box);
            decimal volume = Inches(high.X - low.X) * Inches(high.Y - low.Y) * Inches(high.Z - low.Z) / 1728m;
            decimal weight = volume * DensityLbPerFt3(species) * part.Quantity;
            pieces.Add(new Piece(box, weight, (Inches(low.Y) + Inches(high.Y)) / 2m, (Inches(low.Z) + Inches(high.Z)) / 2m, opens.TryGetValue(box.Id, out Length o) ? o : null));
        }

        (Point3 Low, Point3 High)[] extents = [.. parts.Select(JointGeometry.Extent)];
        Length floor = extents.Min(e => e.Low.Z);
        decimal height = Inches(extents.Max(e => e.High.Z) - floor);
        (Point3 Low, Point3 High)[] feet = [.. extents.Where(e => e.Low.Z == floor)];
        decimal edge = Inches(feet.Min(e => e.Low.Y));
        decimal depth = Inches(feet.Max(e => e.High.Y)) - edge;
        decimal storage = drawers.Sum(drawer => Volume(drawer.Members) / 1728m);

        List<string> notes =
        [
            "napkin reads the front as the south side, as its Front view does; drawers open toward it. It balances moments about the front edge of what stands on the floor, front to back only.",
            "Doors are not modelled, and hardware is not weighed.",
        ];
        notes.AddRange(pieces.Where(piece => piece.Box.Part!.Quantity > 1).Select(piece => $"{piece.Box.Name} stands for {piece.Box.Part!.Quantity} pieces, weighed where it is drawn."));
        notes.AddRange(drawers.Where(drawer => drawer.Note is not null).Select(drawer => drawer.Note!));

        string threshold = "27 in. or greater in height, 30 lb or greater in mass, and 3.2 ft³ or greater of enclosed storage volume (F2057-23 sec. 1.1, quoted at 88 FR 28404)";
        string scope;
        if (missing.Count > 0)
        {
            scope = $"Clothing storage scope: napkin cannot say — input missing: density, for {string.Join(", ", missing)} (a sheet good or a species not in the table).";
        }
        else
        {
            decimal weight = pieces.Sum(piece => piece.Weight);
            bool inScope = height >= Number("scope", "heightIn") && weight >= Number("scope", "massLb") && storage >= Number("scope", "enclosedStorageFt3");
            string sizes = $"≈{Round(height)} in. tall, ≈{Round(weight)} lb, ≈{storage.ToString("0.0", CultureInfo.InvariantCulture)} ft³ of drawers";
            scope = inScope
                ? $"This is a clothing storage unit as ASTM F2057-23 defines it ({sizes}; {threshold}): if sold, 16 CFR 1261 applies."
                : $"Not a clothing storage unit by F2057-23's scope ({sizes}; it covers units {threshold}). Enclosed storage counts drawers only.";
        }

        if (sketch.Furniture.Kind != FurnitureKind.ClothingStorage)
        {
            return new TipOverReport(scope, [], []);
        }

        string anchored = sketch.Furniture.Anchored ? " Anchored to the wall; the test is of the unit unanchored, so the estimate is shown anyway." : string.Empty;
        if (missing.Count > 0)
        {
            return new TipOverReport(scope, [new TipOverLine("9.2", null, $"Tip-over: not estimated — input missing: density, for {string.Join(", ", missing)}. A partial weight would be wrong either way.{anchored}")], [.. notes]);
        }

        if (depth <= Number("childWeightOnCarpet", "tiltBlockIn") || depth <= 0)
        {
            return new TipOverReport(scope, [new TipOverLine("9.2", null, $"Tip-over: not estimated — what stands on the floor is not deeper than the {Number("childWeightOnCarpet", "tiltBlockIn")}\" test block.{anchored}")], [.. notes]);
        }

        decimal Opened(Piece piece) => piece.Y - (piece.Drawer is { } o ? Inches(o) : 0m);
        decimal restoring = pieces.Sum(piece => piece.Weight * (Opened(piece) - edge));

        // §9.2.1: every drawer open and, since napkin's only storage is its drawers and all are open, filled.
        decimal fillDensity = Number("simulatedClothingLoad", "fillDensityLbPerFt3");
        decimal fill = drawers.Sum(drawer =>
        {
            (decimal y, decimal _) = Centre(drawer.Members);
            return fillDensity * Volume(drawer.Members) / 1728m * (y - Inches(drawer.Extension) - edge);
        });
        decimal margin1 = restoring + fill;
        TipOverLine line1 = new("9.2.1", margin1, $"§9.2.1, every drawer open and filled at {fillDensity} lb/ft³ of its whole box (more than its inside, so it errs toward tipping): {Verdict(margin1)}.");

        // §9.2.2: the pull at the highest handhold up to 56".
        decimal force = Number("horizontalForce", "forceLbf");
        decimal hold = Math.Min(height, Number("horizontalForce", "maxHandholdHeightIn"));
        decimal margin2 = restoring - (force * hold);
        TipOverLine line2 = new("9.2.2", margin2, $"§9.2.2, {force} lbf pulled forward {Round(hold)} in. up, drawers open and empty (88 FR 28405 does not say; napkin opens them): {Verdict(margin2)}.");

        // §9.2.3: 60 lb on the front edge of the worst open drawer, tilted forward on the block.
        TipOverLine line3;
        if (drawers.IsEmpty)
        {
            line3 = new("9.2.3", null, "§9.2.3: not estimated — no part is marked a drawer, so there is no open drawer to stand the weight on.");
        }
        else
        {
            decimal block = Number("childWeightOnCarpet", "tiltBlockIn");
            double theta = Math.Asin((double)(block / depth));
            decimal cos = (decimal)Math.Cos(theta), sin = (decimal)Math.Sin(theta);
            decimal Lever(decimal y, decimal z) => ((y - edge) * cos) - ((z - Inches(floor)) * sin);
            decimal tilted = pieces.Sum(piece => piece.Weight * Lever(Opened(piece), piece.Z));
            decimal child = Number("childWeightOnCarpet", "weightLb");
            (decimal Margin, Drawer Drawer) worst = drawers
                .Select(drawer =>
                {
                    decimal front = drawer.Members.Min(member => Inches(JointGeometry.Extent(member).Low.Y)) - Inches(drawer.Extension);
                    decimal top = drawer.Members.Max(member => Inches(JointGeometry.Extent(member).High.Z));
                    return (tilted + (child * Lever(front, top)), drawer);
                })
                .MinBy(pair => pair.Item1);
            line3 = new("9.2.3", worst.Margin, $"§9.2.3, {child} lb on the front edge of {worst.Drawer.Marked.Name}'s open drawer, the unit tilted forward on a {block}\" block, drawers otherwise empty: {Verdict(worst.Margin)}.");
        }

        notes.Insert(0, $"Tip-over estimate — approximates ASTM F2057-23 §9.2 as described at 88 FR 28405, never 'complies'.{anchored}");
        return new TipOverReport(scope, [line1, line2, line3], [.. notes]);
    }

    static string Verdict(decimal margin) => margin >= 0
        ? $"stands, with ≈{Round(margin)} lb·in to spare"
        : $"tips, short by ≈{Round(-margin)} lb·in";

    static string Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    static decimal Inches(Length length) => length.Units / (decimal)Length.UnitsPerInch;

    static decimal Volume(ImmutableArray<Box> members)
    {
        (Point3 low, Point3 high) = Bounds(members);
        return Inches(high.X - low.X) * Inches(high.Y - low.Y) * Inches(high.Z - low.Z);
    }

    static (decimal Y, decimal Z) Centre(ImmutableArray<Box> members)
    {
        (Point3 low, Point3 high) = Bounds(members);
        return ((Inches(low.Y) + Inches(high.Y)) / 2m, (Inches(low.Z) + Inches(high.Z)) / 2m);
    }

    /// <summary>The world box around a drawer's parts.</summary>
    static (Point3 Low, Point3 High) Bounds(ImmutableArray<Box> members)
    {
        (Point3 low, Point3 high) = JointGeometry.Extent(members[0]);
        foreach (Box member in members)
        {
            (Point3 l, Point3 h) = JointGeometry.Extent(member);
            low = new Point3(Length.Min(low.X, l.X), Length.Min(low.Y, l.Y), Length.Min(low.Z, l.Z));
            high = new Point3(Length.Max(high.X, h.X), Length.Max(high.Y, h.Y), Length.Max(high.Z, h.Z));
        }

        return (low, high);
    }
}
