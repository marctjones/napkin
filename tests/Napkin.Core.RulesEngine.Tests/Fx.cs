using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The synthetic fixture packs under Fixtures/ — SYNTHETIC TEST DATA - NOT CODE VALUES. Every
/// number in them is made up (99s, 77s, 2x13 members, "IRC 2099") so that a semantics bug can never
/// be mistaken for, or confused with, a transcription of a real table (design §11.1).
/// </summary>
internal static class Fx
{
    public const string Banner = "SYNTHETIC TEST DATA - NOT CODE VALUES";
    public const string Table = "TEST-HEADER-TABLE";
    public const string TablePath = "layers/zz-base-2099/tables/test-header-table.json";
    public const string StateOverlayPath = "packs/us-zz-state/amendments/test-header-table.json";

    public static string Root => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string GoldenRoot => Path.Combine(AppContext.BaseDirectory, "Golden");

    public static InMemoryPackSource Source() => InMemoryPackSource.FromDirectory(Root);

    public static LoadedPack Load(string id) => Loaded(PackLoader.Load(Root, id));

    public static LoadedPack Loaded(PackLoadResult result) => result switch
    {
        PackLoadResult.Loaded loaded => loaded.Pack,
        PackLoadResult.Invalid invalid => throw new Xunit.Sdk.XunitException(invalid.ToString()),
        _ => throw new InvalidOperationException(),
    };

    public static PackLoadResult.Invalid Invalid(PackLoadResult result) => result switch
    {
        PackLoadResult.Invalid invalid => invalid,
        _ => throw new Xunit.Sdk.XunitException("expected the pack to be refused, but it loaded"),
    };

    public static Length Ft(long feet, long inches = 0) => Length.FeetInches(feet, inches);

    public static SiteInputs Site(int? snow = 50, Length? width = null, int? wind = 150)
        => new(snow, wind, null, null, width ?? Ft(30), null);

    public static HeaderRequest Roof(Length span, int? snow = 50, Length? width = null, int? wind = 150, string supports = "test-roof")
        => new(supports, WallKind.ExteriorBearing, span, Site(snow, width, wind));

    public static HeaderResult Size(string pack, HeaderRequest request) => RulesEngine.For(Load(pack)).SizeHeader(request);

    public static HeaderResult.Sized Sized(HeaderResult result)
        => result as HeaderResult.Sized ?? throw new Xunit.Sdk.XunitException($"expected Sized, got {result}");

    public static HeaderResult.OutOfScope OutOfScope(HeaderResult result)
        => result as HeaderResult.OutOfScope ?? throw new Xunit.Sdk.XunitException($"expected OutOfScope, got {result}");
}
