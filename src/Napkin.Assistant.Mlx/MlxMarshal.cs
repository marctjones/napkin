using System.Runtime.InteropServices;
using System.Text;

namespace Napkin.Assistant.Mlx;

/// <summary>
/// The C structs of <c>napkin_mlx.h</c> laid out byte for byte, and how napkin reads what the bridge
/// returns — everything <see cref="NativeMlx"/> needs except the P/Invoke itself, so it is tested
/// without the library (mlx-runtime.md §13.2 item 2 gives the layout; the tests assert it and read
/// the header's array sizes and status values from the file).
/// </summary>
internal static unsafe class MlxMarshal
{
    /// <summary><c>char architecture[64]</c>.</summary>
    public const int ArchitectureBytes = 64;

    /// <summary><c>char mlx_swift_lm_revision[48]</c>.</summary>
    public const int RevisionBytes = 48;

    /// <summary>Reads a UTF-8 string the bridge returned (an error sentence) and frees it through the bridge; null for NULL.</summary>
    /// <param name="pointer">A <c>char*</c> the bridge owns, or zero.</param>
    /// <param name="free"><c>napkin_mlx_string_free</c>; called exactly once for a non-NULL pointer, even if reading fails.</param>
    public static string? TakeString(IntPtr pointer, Action<IntPtr> free)
    {
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(pointer);
        }
        finally
        {
            free(pointer);
        }
    }

    /// <summary>Reads a <c>napkin_mlx_result*</c> and frees it through the bridge; null for NULL.</summary>
    /// <param name="pointer">The result the bridge allocated, or zero.</param>
    /// <param name="free"><c>napkin_mlx_result_free</c>, which frees the struct and its text; called exactly once for a non-NULL pointer.</param>
    public static MlxGeneration? TakeResult(IntPtr pointer, Action<IntPtr> free)
    {
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            NativeResult result = *(NativeResult*)pointer;
            string text = Marshal.PtrToStringUTF8(result.Text) ?? string.Empty;
            return new MlxGeneration(
                text,
                result.PromptTokens,
                result.GeneratedTokens,
                (MlxStopReason)result.StopReason,
                Seconds(result.PromptSeconds),
                Seconds(result.GenerationSeconds));
        }
        finally
        {
            free(pointer);
        }
    }

    /// <summary>The device struct as napkin's record: the two fixed char arrays read up to their NUL (or their whole length).</summary>
    /// <param name="info">The struct the bridge filled (by value: a local, so its fixed buffers need no pinning).</param>
    public static MlxDeviceInfo ToDeviceInfo(NativeDeviceInfo info) => new(
        info.HasMetal != 0,
        FixedUtf8(new ReadOnlySpan<byte>(info.Architecture, ArchitectureBytes)),
        info.MemoryBytes,
        info.RecommendedWorkingSetBytes,
        FixedUtf8(new ReadOnlySpan<byte>(info.MlxSwiftLmRevision, RevisionBytes)));

    /// <summary>A C <c>char[N]</c> field as text: up to the first NUL, or all N bytes when the bridge filled it to the brim.</summary>
    /// <param name="field">The field's bytes.</param>
    public static string FixedUtf8(ReadOnlySpan<byte> field)
    {
        int end = field.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? field : field[..end]);
    }

    /// <summary>A duration the bridge measured, or zero for one it could not (negative, NaN or infinite).</summary>
    private static TimeSpan Seconds(double seconds) =>
        double.IsFinite(seconds) && seconds >= 0 && seconds < TimeSpan.MaxValue.TotalSeconds ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;

    /// <summary><c>napkin_mlx_device_info</c>: 136 bytes — <c>memory_bytes</c> at 72, <c>mlx_swift_lm_revision</c> at 88.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDeviceInfo
    {
        /// <summary><c>int32_t has_metal</c>, 0 or 1.</summary>
        public int HasMetal;

        /// <summary><c>char architecture[64]</c>.</summary>
        public fixed byte Architecture[ArchitectureBytes];

        /// <summary><c>uint64_t memory_bytes</c>.</summary>
        public ulong MemoryBytes;

        /// <summary><c>uint64_t recommended_working_set_bytes</c>.</summary>
        public ulong RecommendedWorkingSetBytes;

        /// <summary><c>char mlx_swift_lm_revision[48]</c>.</summary>
        public fixed byte MlxSwiftLmRevision[RevisionBytes];
    }

    /// <summary><c>napkin_mlx_result</c>: 40 bytes — <c>prompt_seconds</c> at 24.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeResult
    {
        /// <summary><c>const char* text</c>, bridge-owned.</summary>
        public IntPtr Text;

        /// <summary><c>int32_t prompt_tokens</c>.</summary>
        public int PromptTokens;

        /// <summary><c>int32_t generated_tokens</c>.</summary>
        public int GeneratedTokens;

        /// <summary><c>int32_t stop_reason</c>.</summary>
        public int StopReason;

        /// <summary><c>double prompt_seconds</c>.</summary>
        public double PromptSeconds;

        /// <summary><c>double generation_seconds</c>.</summary>
        public double GenerationSeconds;
    }
}
