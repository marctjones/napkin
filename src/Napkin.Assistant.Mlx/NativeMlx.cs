using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Napkin.Assistant.Mlx;

/// <summary>
/// The real bridge: <c>libNapkinMlx.dylib</c> in <c>native/</c> beside the executable, called through
/// source-generated P/Invoke (docs/design/mlx-runtime.md §4.1). The only file in napkin that
/// P/Invokes. Everything it reads back is converted by <see cref="MlxMarshal"/>, which is tested.
/// </summary>
/// <remarks>
/// <para>
/// <b>Loading.</b> Nothing is loaded when an instance is made. The first call loads the library
/// from exactly <see cref="LibraryPath"/> with <see cref="NativeLibrary.Load(string)"/>, whose
/// <see cref="DllNotFoundException"/> names that path and the loader's reason; the assembly's import
/// resolver then hands that handle to every <c>[LibraryImport]</c> below and resolves no other name.
/// The library is loaded once per process and never unloaded.
/// </para>
/// <para>
/// <b>Threads.</b> Every call blocks for as long as the bridge works (a load, a whole generation);
/// callers run them on a thread-pool thread (<see cref="MlxModel"/> does).
/// </para>
/// </remarks>
// Decision 6 (mlx-runtime.md §12, §7.2): excluded from the coverage ratchet. Its lines are the
// generated P/Invoke stubs, the resolver and one-line wrappers; the only honest test of them loads
// the real library and needs Metal, which no unit or GUI test may. `napkin-tools assistant
// mlx-smoke` is that test, run by a person on an Apple silicon Mac.
[ExcludeFromCodeCoverage(Justification = "P/Invoke into the native MLX bridge; exercised by `assistant mlx-smoke` on a Mac, never by unit tests (mlx-runtime.md Decision 6).")]
public sealed partial class NativeMlx : INativeMlx
{
    private const string Library = "NapkinMlx";

    private static readonly object Gate = new();
    private static readonly Action<IntPtr> FreeString = napkin_mlx_string_free;
    private static readonly Action<IntPtr> FreeResult = napkin_mlx_result_free;
    private static IntPtr s_library;

    static NativeMlx() =>
        NativeLibrary.SetDllImportResolver(typeof(NativeMlx).Assembly, Resolve);

    /// <summary>Where the bridge is loaded from: <c>native/libNapkinMlx.dylib</c> beside the executable.</summary>
    public static string LibraryPath => MlxBridge.LibraryPath(MlxBridge.BaseDirectory);

    /// <inheritdoc/>
    public int AbiVersion()
    {
        EnsureLoaded();
        return napkin_mlx_abi_version();
    }

    /// <inheritdoc/>
    public MlxStatus Init(string metallibPath, out string? error)
    {
        ArgumentNullException.ThrowIfNull(metallibPath);
        EnsureLoaded();
        MlxStatus status = napkin_mlx_init(metallibPath, out IntPtr sentence);
        error = MlxMarshal.TakeString(sentence, FreeString);
        return status;
    }

    /// <inheritdoc/>
    public MlxStatus Device(out MlxDeviceInfo info, out string? error)
    {
        EnsureLoaded();
        MlxStatus status = napkin_mlx_device(out MlxMarshal.NativeDeviceInfo native, out IntPtr sentence);
        error = MlxMarshal.TakeString(sentence, FreeString);
        info = status == MlxStatus.Ok ? MlxMarshal.ToDeviceInfo(native) : default;
        return status;
    }

    /// <inheritdoc/>
    public MlxStatus Load(string modelFolder, CancelFlag cancel, out MlxModelHandle handle, out string? error)
    {
        ArgumentNullException.ThrowIfNull(modelFolder);
        ArgumentNullException.ThrowIfNull(cancel);
        EnsureLoaded();
        MlxStatus status = napkin_mlx_load(modelFolder, cancel, out IntPtr model, out IntPtr sentence);
        error = MlxMarshal.TakeString(sentence, FreeString);
        handle = status == MlxStatus.Ok ? new MlxModelHandle(model) : MlxModelHandle.None;
        return status;
    }

    /// <inheritdoc/>
    public MlxStatus Generate(
        MlxModelHandle model,
        string system,
        string user,
        string? jsonSchema,
        int maxTokens,
        float temperature,
        float topP,
        bool enableThinking,
        CancelFlag cancel,
        out MlxGeneration? result,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(cancel);
        EnsureLoaded();
        MlxStatus status = napkin_mlx_generate(
            model.Value, system, user, jsonSchema, maxTokens, temperature, topP, enableThinking ? 1 : 0, cancel, out IntPtr reply, out IntPtr sentence);
        error = MlxMarshal.TakeString(sentence, FreeString);
        result = MlxMarshal.TakeResult(reply, FreeResult);
        return status;
    }

    /// <inheritdoc/>
    public void Unload(MlxModelHandle model)
    {
        if (model.IsNone)
        {
            return;
        }

        EnsureLoaded();
        napkin_mlx_unload(model.Value);
    }

    /// <summary>Loads the library from <see cref="LibraryPath"/> once; a failure throws <see cref="DllNotFoundException"/> naming the path, from here.</summary>
    private static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (s_library == IntPtr.Zero)
            {
                s_library = NativeLibrary.Load(LibraryPath);
            }
        }
    }

    /// <summary>The assembly's one import resolver ("Only one import resolver can be set for a given assembly"): the loaded bridge for its name, nothing for any other.</summary>
    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == Library ? s_library : IntPtr.Zero;

    [LibraryImport(Library)]
    private static partial int napkin_mlx_abi_version();

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial MlxStatus napkin_mlx_init(string metallibPath, out IntPtr error);

    [LibraryImport(Library)]
    private static partial MlxStatus napkin_mlx_device(out MlxMarshal.NativeDeviceInfo info, out IntPtr error);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial MlxStatus napkin_mlx_load(string modelDir, CancelFlag cancel, out IntPtr model, out IntPtr error);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial MlxStatus napkin_mlx_generate(
        IntPtr model,
        string system,
        string user,
        string? jsonSchema,
        int maxTokens,
        float temperature,
        float topP,
        int enableThinking,
        CancelFlag cancel,
        out IntPtr result,
        out IntPtr error);

    [LibraryImport(Library)]
    private static partial void napkin_mlx_result_free(IntPtr result);

    [LibraryImport(Library)]
    private static partial void napkin_mlx_string_free(IntPtr value);

    [LibraryImport(Library)]
    private static partial void napkin_mlx_unload(IntPtr model);
}
