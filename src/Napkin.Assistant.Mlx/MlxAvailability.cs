using System.Runtime.InteropServices;

namespace Napkin.Assistant.Mlx;

/// <summary>What <see cref="MlxAvailability.Probe"/> found: the device, and why MLX cannot run here if it cannot.</summary>
/// <param name="Device">What the bridge reported about this Mac; null when it could not be asked.</param>
/// <param name="Refusal">Why MLX cannot run here, in a sentence; null when it can.</param>
public sealed record MlxProbe(MlxDeviceInfo? Device, string? Refusal)
{
    /// <summary>Whether MLX can run here.</summary>
    public bool IsAvailable => Refusal is null;
}

/// <summary>
/// Whether MLX can run in this process, and if not, why — without loading anything on a machine
/// where it cannot (docs/design/mlx-runtime.md §3.4, §4.1).
/// </summary>
/// <remarks>
/// Two steps. <see cref="Check"/> is pure — the platform and the bridge's three files — and is all
/// that is asked before the bridge is ever loaded, so Windows, Linux and an Intel Mac never
/// <c>dlopen</c> it. <see cref="Probe"/> then loads the bridge (only where <see cref="Check"/> said
/// yes) and asks it about Metal and memory; a library that is present but does not load is a
/// refusal, not a crash.
/// </remarks>
public static class MlxAvailability
{
    /// <summary>Off Apple silicon (Windows, Linux, an Intel Mac, an x64 process under Rosetta) — the sentence of mlx-runtime.md's "How to use it".</summary>
    public const string NotAppleSilicon = "MLX runs on Apple silicon; use a program on this machine (Ollama or llama-server) instead.";

    /// <summary>The bridge loaded but Metal found no device (a headless or virtualized session) — mlx-runtime.md §4.1's refusal.</summary>
    public const string NoMetal = "MLX found no Metal device on this Mac.";

    /// <summary>
    /// Why MLX cannot run for a build whose executable sits in <paramref name="baseDirectory"/>, or
    /// null when it can: macOS on arm64, with <c>libNapkinMlx.dylib</c>, <c>mlx.metallib</c> and
    /// <c>libswiftCompatibilitySpan.dylib</c> all in <c>native/</c>. Loads nothing.
    /// </summary>
    /// <param name="isMacOS">Whether this is macOS.</param>
    /// <param name="processArchitecture">
    /// The process's architecture. An arm64 dylib loads only into an arm64 process, so this — not the
    /// OS's — is the one that matters: an x64 napkin under Rosetta on Apple silicon is refused.
    /// </param>
    /// <param name="fileExists">Whether a file exists (<see cref="File.Exists"/>; a test passes its own).</param>
    /// <param name="baseDirectory">The executable's folder.</param>
    public static string? Check(bool isMacOS, Architecture processArchitecture, Func<string, bool> fileExists, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        if (!isMacOS || processArchitecture != Architecture.Arm64)
        {
            return NotAppleSilicon;
        }

        foreach (string file in MlxBridge.Files)
        {
            string path = Path.Combine(MlxBridge.NativeDirectory(baseDirectory), file);
            if (!fileExists(path))
            {
                return MissingFile(path);
            }
        }

        return null;
    }

    /// <summary><see cref="Check"/> for this process: this OS, this process's architecture, the disk, <see cref="AppContext.BaseDirectory"/>.</summary>
    public static string? ForThisProcess() =>
        Check(OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture, File.Exists, MlxBridge.BaseDirectory);

    /// <summary>
    /// Asks the bridge whether it speaks this napkin's interface and whether Metal found a device;
    /// call only where <see cref="Check"/> said yes. Loading the bridge happens here, on the first
    /// call, and blocks; call off the UI thread.
    /// </summary>
    /// <param name="native">The bridge.</param>
    public static MlxProbe Probe(INativeMlx native)
    {
        ArgumentNullException.ThrowIfNull(native);
        try
        {
            int abi = native.AbiVersion();
            if (abi != MlxBridge.AbiVersion)
            {
                return new MlxProbe(null, WrongAbi(abi));
            }

            MlxStatus status = native.Device(out MlxDeviceInfo device, out string? error);
            if (status != MlxStatus.Ok)
            {
                return new MlxProbe(null, MlxModel.Refusal(status, error));
            }

            return new MlxProbe(device, device.HasMetal ? null : NoMetal);
        }
        catch (Exception exception) when (IsLoadFailure(exception))
        {
            return new MlxProbe(null, CouldNotLoad(exception));
        }
    }

    /// <summary>A file of the bridge is not in <c>native/</c>: "MLX is not in this build" (§3.1), naming the file.</summary>
    internal static string MissingFile(string path) =>
        $"MLX is not in this build of napkin: {path} is missing. Use a program on this machine (Ollama or llama-server) instead.";

    /// <summary>The bridge speaks another version of <c>napkin_mlx.h</c> than this napkin.</summary>
    internal static string WrongAbi(int abi) =>
        $"The MLX bridge speaks version {abi} of its interface; this napkin speaks version {MlxBridge.AbiVersion}.";

    /// <summary>The runtime could not load the bridge: the loader's own words, which name the path.</summary>
    internal static string CouldNotLoad(Exception exception) =>
        $"napkin could not load the MLX bridge: {exception.Message}";

    /// <summary>What the runtime throws when a native library cannot be loaded or lacks a function — and nothing else.</summary>
    internal static bool IsLoadFailure(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;
}
