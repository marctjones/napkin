using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>
/// A shelf as its joints say it is (docs/design/furniture-checks.md §3): a part lying thickness-up,
/// inserted by a dado or a butt into parts at both ends of one horizontal axis.
/// </summary>
/// <param name="Shelf">The shelf's box.</param>
/// <param name="Span">The clear span between the two receiving faces: the shelf's length between its jointed ends.</param>
/// <param name="Width">Its width across the span.</param>
/// <param name="Thickness">Its thickness, up.</param>
public sealed record ShelfSpan(Box Shelf, Length Span, Length Width, Length Thickness);

/// <summary>A shelf-sag estimate: an engineering estimate, shown with ≈, never stored.</summary>
/// <param name="Instant">The deflection at midspan under the load, in inches.</param>
/// <param name="LongTerm">The deflection after years under the load: twice the instant (the Handbook's creep sentence).</param>
/// <param name="WithShear">Whether Eq. 9-2's shear term was included (the species has a Table 5–1 row).</param>
/// <param name="Text">The panel's words.</param>
/// <param name="WithinLimit">Against the typed limit: true within it, false over it, null with no limit typed.</param>
public sealed record ShelfSagEstimate(decimal Instant, decimal LongTerm, bool WithShear, string Text, bool? WithinLimit);

/// <summary>
/// Shelf sag, the Wood Handbook's way (FPL-GTR-282, retrieved 2026-09-26): Eq. 9-2 (p. 9–2),
/// δ = k_b W L³ / (E I) + k_s W L / (G A′), with Table 9-1's (p. 9–3) uniformly distributed load on a
/// simply supported span, k_b = 5/384 and k_s = 1/8; I = b h³ / 12 (Eq. 9-3) and A′ = 5/6 b h (Eq. 9-4).
/// E is Table 5–3b's bending E, which footnote c says "can be increased by 10%" to correct for shear
/// deflection: napkin does so when it adds the shear term, with G = E_L × Table 5–1's smaller ratio,
/// and uses the table's E as printed, bending only, when Table 5–1 has no row. Chapter 5 (p. 5–39):
/// after several years the added deflection from creep "may approximately equal the initial,
/// instantaneous elastic deformation", so the long-term figure is twice the instant one.
/// </summary>
public static class ShelfSag
{
    /// <summary>KCMA's shelf test load, lb/sq ft: "we load cabinet shelves and bottoms with 15 pounds of weight per square foot" (kcma.org, "Why choose certified cabinetry?", retrieved 2026-09-26).</summary>
    public const decimal KcmaLoadPsf = 15m;

    /// <summary>Where the default load comes from, as the panel says it.</summary>
    public const string KcmaSource = "KCMA's shelf test load (kcma.org, retrieved 2026-09-26)";

    /// <summary>The shelf a box is, from its joints, or null when it is not one.</summary>
    public static ShelfSpan? Of(Sketch sketch, Box box)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(box);
        if (box.Part is null)
        {
            return null;
        }

        (Point3 low, Point3 high) = JointGeometry.Extent(box);
        Length dx = high.X - low.X, dy = high.Y - low.Y, dz = high.Z - low.Z;
        if (dz >= dx || dz >= dy)
        {
            return null;
        }

        // The jointed ends: contacts on the shelf's own faces, perpendicular to X or Y.
        JointContact[] ends =
        [
            .. sketch.RelationshipsInOrder.OfType<Joint>()
                .Where(joint => joint.Inserted.Box == box.Id && joint.Type is JointType.Groove or JointType.Butt)
                .Select(joint => JointGeometry.Contact(sketch, joint))
                .OfType<JointContact>()
                .Where(contact => contact.Normal is Axis.X or Axis.Y),
        ];

        foreach (Axis axis in (Axis[])[Axis.X, Axis.Y])
        {
            bool atLow = ends.Any(c => c.Normal == axis && c.Low.Component(axis) == low.Component(axis));
            bool atHigh = ends.Any(c => c.Normal == axis && c.Low.Component(axis) == high.Component(axis));
            if (atLow && atHigh)
            {
                return axis == Axis.X ? new ShelfSpan(box, dx, dy, dz) : new ShelfSpan(box, dy, dx, dz);
            }
        }

        return null;
    }

    /// <summary>
    /// The estimate for a shelf of a species under a load, against an optional limit — or why there is
    /// none: a sheet good (napkin has no cited stiffness for panels) or a species not in the table.
    /// </summary>
    /// <param name="shelf">The shelf.</param>
    /// <param name="loadPsf">The typed load, lb/sq ft.</param>
    /// <param name="limit">The typed limit on the long-term sag, or null for no verdict (furniture-checks §9.2).</param>
    /// <param name="library">Where the part's stock is looked up.</param>
    /// <returns>The estimate, or the words that say why there is none.</returns>
    public static (ShelfSagEstimate? Estimate, string? Missing) Estimate(ShelfSpan shelf, decimal loadPsf, Length? limit, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(library);
        Part part = shelf.Shelf.Part!;
        if (library.TryFind(part.Stock, out StockItem stock) && stock is PanelStock)
        {
            return (null, $"Shelf sag: not estimated — {part.Stock} is a sheet good, and napkin has no cited stiffness for panels.");
        }

        if (WoodProperties.Shipped.Find(part.Species) is not { } species)
        {
            return (null, "Shelf sag: input missing — species. Pick one from the table; typed text is never interpreted.");
        }

        decimal L = Inches(shelf.Span), b = Inches(shelf.Width), h = Inches(shelf.Thickness);
        decimal W = loadPsf * L * b / 144m;
        decimal I = b * h * h * h / 12m;
        decimal e = species.ModulusOfElasticityPsi;
        decimal instant;
        if (species.Ratios is { } ratios)
        {
            decimal el = e * 1.1m;
            decimal g = el * ratios.Least;
            decimal area = 5m / 6m * b * h;
            instant = (5m / 384m * W * L * L * L / (el * I)) + (1m / 8m * W * L / (g * area));
        }
        else
        {
            instant = 5m / 384m * W * L * L * L / (e * I);
        }

        decimal longTerm = 2m * instant;
        bool? within = limit is { } l ? longTerm <= Inches(l) : null;
        string load = loadPsf == KcmaLoadPsf ? $"{loadPsf.ToString(CultureInfo.InvariantCulture)} lb/sq ft, {KcmaSource}" : $"{loadPsf.ToString(CultureInfo.InvariantCulture)} lb/sq ft";
        string verdict = limit is { } typed
            ? within == true ? $"; within the {typed.Format(new FeetInchesFormat(16)).Text} you typed" : $"; over the {typed.Format(new FeetInchesFormat(16)).Text} you typed"
            : string.Empty;
        string shear = species.Ratios is { } r
            ? $"bending and shear, G from Table 5–1's \"{r.TableRow}\" row"
            : "bending only (Table 5–1 has no row for it; the table's E already carries some shear)";
        string text = $"Shelf sag ≈ {Words(instant)} now, ≈ {Words(longTerm)} after years, under {load}{verdict}. "
                      + $"An engineering estimate: {species.Name} (Table 5–3b p. {species.Page}), span {shelf.Span.Format(new FeetInchesFormat(16)).Text}, Wood Handbook Eq. 9-2 with Table 9-1, {shear}; long-term twice the instant, per chapter 5's creep sentence.";
        return (new ShelfSagEstimate(instant, longTerm, species.Ratios is not null, text, within), null);
    }

    static decimal Inches(Length length) => length.Units / (decimal)Length.UnitsPerInch;

    /// <summary>A small deflection in inches to two places: "0.06\"", or "under 0.01\"".</summary>
    public static string Words(decimal inches) => inches < 0.005m
        ? "under 0.01\""
        : $"{Math.Round(inches, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture)}\"";
}
