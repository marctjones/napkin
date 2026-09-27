using System.Collections.Immutable;

namespace Napkin.Assistant.Mlx;

/// <summary>
/// Where the bridge lives and what it is made of (docs/design/mlx-runtime.md §3.1, §3.3, §13.1
/// items 1, 7 and 9): three files, built by <c>tools/scripts/build-mlx.sh</c> into
/// <c>native/NapkinMlx/out/</c> and shipped together in <c>native/</c> beside the executable.
/// </summary>
/// <remarks>
/// They must stay together: MLX 0.31 loads its Metal kernels only from the <c>mlx.metallib</c> beside
/// the binary it is compiled into, and the dylib finds <c>libswiftCompatibilitySpan.dylib</c> through
/// an <c>@loader_path</c> rpath (macOS 26 has its own copy; 14 and 15 need this one). The dylib is
/// kept out of a single-file bundle for that reason — extracted elsewhere, it would lose its
/// neighbours (§3.3).
/// </remarks>
public static class MlxBridge
{
    /// <summary>The header version this napkin speaks (<c>napkin_mlx_abi_version()</c>).</summary>
    public const int AbiVersion = 1;

    /// <summary>The folder beside the executable that holds the three files.</summary>
    public const string FolderName = "native";

    /// <summary>The bridge itself: the eight functions of <c>napkin_mlx.h</c>.</summary>
    public const string LibraryFile = "libNapkinMlx.dylib";

    /// <summary>MLX's Metal kernels, which MLX loads from beside the dylib and nowhere else.</summary>
    public const string MetallibFile = "mlx.metallib";

    /// <summary>The Swift runtime's Span back-deployment library the dylib links for its macOS 14 floor.</summary>
    public const string SpanLibraryFile = "libswiftCompatibilitySpan.dylib";

    /// <summary>The three files, in the order a missing one is named.</summary>
    public static ImmutableArray<string> Files { get; } = [LibraryFile, MetallibFile, SpanLibraryFile];

    /// <summary>The folder the bridge is loaded from for a build whose files sit in <paramref name="baseDirectory"/>.</summary>
    /// <param name="baseDirectory">The executable's folder (<see cref="AppContext.BaseDirectory"/>).</param>
    public static string NativeDirectory(string baseDirectory) => Path.Combine(baseDirectory, FolderName);

    /// <summary>The dylib's path under <paramref name="baseDirectory"/>.</summary>
    /// <param name="baseDirectory">The executable's folder.</param>
    public static string LibraryPath(string baseDirectory) => Path.Combine(NativeDirectory(baseDirectory), LibraryFile);

    /// <summary>The metallib's path under <paramref name="baseDirectory"/> — what <see cref="INativeMlx.Init"/> is given.</summary>
    /// <param name="baseDirectory">The executable's folder.</param>
    public static string MetallibPath(string baseDirectory) => Path.Combine(NativeDirectory(baseDirectory), MetallibFile);

    /// <summary>This process's executable folder: <see cref="AppContext.BaseDirectory"/> ("To access files next to the executable").</summary>
    public static string BaseDirectory => AppContext.BaseDirectory;
}
