using System.Text.RegularExpressions;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// The closed sets of words a pack file may use. The engine knows its own request fields, never
/// a model code's column names or table numbers: which table serves which wall is the table
/// file's <c>wallKind</c>, and which inputs a table needs is its <c>inputs</c> declaration.
/// </summary>
internal static partial class Vocabulary
{
    public const string HeaderSizingKind = "header-sizing";

    /// <summary>The kind of a base layer's <c>bracing/</c> file.</summary>
    public const string WallBracingKind = "wall-bracing";

    /// <summary>The wall height, which the wall supplies to a bracing check (never a site value).</summary>
    public const string WallHeight = "wallHeight";

    /// <summary>
    /// The inputs a bracing column or condition may name, with the one type each has: site values
    /// the project already asks for, and the wall's own height.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ColumnType> BracingInputs = new Dictionary<string, ColumnType>(StringComparer.Ordinal)
    {
        ["seismicDesignCategory"] = ColumnType.Enum,
        ["groundSnowLoad"] = ColumnType.Psf,
        ["ultimateWindSpeed"] = ColumnType.Mph,
        ["buildingWidth"] = ColumnType.Length,
        [WallHeight] = ColumnType.Length,
    };

    /// <summary>A bracing method's id, as a project stores it for an assignment.</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    public static partial Regex MethodIdPattern();

    public const string HeaderSpan = "headerSpan";

    /// <summary>
    /// The request fields a header-sizing column may name, with the one type each has. A column
    /// named anything else, or declaring a different type, makes the table invalid.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ColumnType> HeaderInputs = new Dictionary<string, ColumnType>(StringComparer.Ordinal)
    {
        ["supports"] = ColumnType.Enum,
        ["seismicDesignCategory"] = ColumnType.Enum,
        ["groundSnowLoad"] = ColumnType.Psf,
        ["ultimateWindSpeed"] = ColumnType.Mph,
        ["buildingWidth"] = ColumnType.Length,
        ["frostDepth"] = ColumnType.Length,
        [HeaderSpan] = ColumnType.Length,
    };

    public static readonly IReadOnlyDictionary<string, AppliesTo> AppliesToNames = new Dictionary<string, AppliesTo>(StringComparer.Ordinal)
    {
        ["permit-application-date"] = AppliesTo.PermitApplicationDate,
        ["permit-issuance-date"] = AppliesTo.PermitIssuanceDate,
        ["see-notes"] = AppliesTo.SeeNotes,
    };

    public static readonly IReadOnlyDictionary<string, ReviewStatus> ReviewNames = new Dictionary<string, ReviewStatus>(StringComparer.Ordinal)
    {
        ["unreviewed"] = ReviewStatus.Unreviewed,
        ["in-review"] = ReviewStatus.InReview,
        ["signed-off"] = ReviewStatus.SignedOff,
    };

    public static readonly IReadOnlyDictionary<string, WallKind> WallKinds = new Dictionary<string, WallKind>(StringComparer.Ordinal)
    {
        ["exterior-bearing"] = WallKind.ExteriorBearing,
        ["interior-bearing"] = WallKind.InteriorBearing,
    };

    public static readonly IReadOnlyDictionary<string, ColumnType> ColumnTypes = new Dictionary<string, ColumnType>(StringComparer.Ordinal)
    {
        ["enum"] = ColumnType.Enum,
        ["psf"] = ColumnType.Psf,
        ["mph"] = ColumnType.Mph,
        ["length"] = ColumnType.Length,
        ["sqft"] = ColumnType.SquareFeet,
    };

    public static readonly IReadOnlyDictionary<string, BandKind> BandKinds = new Dictionary<string, BandKind>(StringComparer.Ordinal)
    {
        ["exact"] = BandKind.Exact,
        ["upper-bound"] = BandKind.UpperBound,
        ["capacity"] = BandKind.Capacity,
        ["lower-bound"] = BandKind.LowerBound,
    };

    public static readonly IReadOnlyDictionary<string, FootnoteEncoding> Encodings = new Dictionary<string, FootnoteEncoding>(StringComparer.Ordinal)
    {
        ["not-encoded"] = FootnoteEncoding.NotEncoded,
        ["as-rows"] = FootnoteEncoding.AsRows,
        ["as-limit"] = FootnoteEncoding.AsLimit,
        ["as-operations"] = FootnoteEncoding.AsOperations,
    };

    /// <summary>The operations an <c>as-operations</c> footnote may declare (design §4.4).</summary>
    public const string SubstituteInputOp = "substitute-input";

    /// <summary>Linear interpolation between two declared columns.</summary>
    public const string InterpolateOp = "interpolate";

    /// <summary>A site input that is never a table column, only a footnote condition: roof live load, whole psf.</summary>
    public const string RoofLiveLoad = "roofLiveLoad";

    /// <summary>
    /// The inputs a footnote operation's condition may test: every numeric header input, plus
    /// inputs that only footnotes ask for. Each is required only when a condition is reached.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ColumnType> ConditionInputs =
        HeaderInputs.Where(p => p.Value != ColumnType.Enum).Append(KeyValuePair.Create(RoofLiveLoad, ColumnType.Psf))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, FootnoteScope> Scopes = new Dictionary<string, FootnoteScope>(StringComparer.Ordinal)
    {
        ["table"] = FootnoteScope.Table,
        ["rows"] = FootnoteScope.Rows,
    };

    public static readonly IReadOnlyDictionary<string, OpKind> Operations = new Dictionary<string, OpKind>(StringComparer.Ordinal)
    {
        ["add"] = OpKind.Add,
        ["amend"] = OpKind.Amend,
        ["delete"] = OpKind.Delete,
        ["add-table"] = OpKind.AddTable,
        ["amend-table"] = OpKind.AmendTable,
        ["delete-table"] = OpKind.DeleteTable,
        ["amend-footnote"] = OpKind.AmendFootnote,
    };

    public static string OpName(OpKind op) => Operations.First(p => p.Value == op).Key;

    public static string WallKindName(WallKind kind) => WallKinds.First(p => p.Value == kind).Key;

    public static string TypeName(ColumnType type) => ColumnTypes.First(p => p.Value == type).Key;

    public static string BandName(BandKind band) => BandKinds.First(p => p.Value == band).Key;
}

/// <summary>Overlay operations (design §1.3): the CT document's Add / Amd / Del, on a row or a table.</summary>
internal enum OpKind
{
    Add,
    Amend,
    Delete,
    AddTable,
    AmendTable,
    DeleteTable,
    AmendFootnote,
}
