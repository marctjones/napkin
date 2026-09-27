using System.Security.Cryptography;

namespace Napkin.Core.Project;

/// <summary>
/// The images a project carries beside its scene (container version 2, docs/design/permit-set.md §5.4):
/// a PNG or a JPEG, told apart by their first bytes rather than a name, and named in the container by
/// the SHA-256 of their bytes. napkin never decodes one here; the app does, to draw it.
/// </summary>
public static class ProjectAssets
{
    static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>".png" or ".jpg" by the bytes' own signature, or null for anything else.</summary>
    public static string? Extension(ReadOnlySpan<byte> bytes)
        => bytes.StartsWith(PngSignature) ? ".png" : bytes.StartsWith(JpegSignature) ? ".jpg" : null;

    /// <summary>The SHA-256 of the bytes, as 64 lowercase hex digits: the asset's name.</summary>
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The entry an asset is stored under: <c>assets/&lt;sha256&gt;.png</c> or <c>.jpg</c>.</summary>
    internal static string EntryName(string hash, string extension) => $"{ContainerNames.AssetsDirectory}{hash}{extension}";

    /// <summary>Whether an entry name has an asset's shape, and its hash and extension if it does.</summary>
    internal static bool TryEntry(string name, out string hash, out string extension)
    {
        hash = extension = string.Empty;
        if (!name.StartsWith(ContainerNames.AssetsDirectory, StringComparison.Ordinal))
        {
            return false;
        }

        string file = name[ContainerNames.AssetsDirectory.Length..];
        string ext = Path.GetExtension(file);
        string stem = Path.GetFileNameWithoutExtension(file);
        if (ext is not (".png" or ".jpg") || !Napkin.Core.Geometry.SurveyUnderlayRules.IsHash(stem))
        {
            return false;
        }

        hash = stem;
        extension = ext;
        return true;
    }
}
