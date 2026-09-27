using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// napkin's side of the C ABI held to the bridge's: the structs byte for byte (mlx-runtime.md
/// §13.2 item 2, which slice A's Swift test asserts from the C side), the header's status values
/// and array sizes read from <c>napkin_mlx.h</c> itself, and every conversion of what the bridge
/// returns — without loading the library.
/// </summary>
[Trait("Feature", "AST-007")]
public unsafe class MarshallingTests
{
    private const string Header = "native/NapkinMlx/include/napkin_mlx.h";

    [Fact]
    public void TheDeviceStructIs136BytesWithTheBridgesOffsets()
    {
        Assert.Equal(136, sizeof(MlxMarshal.NativeDeviceInfo));
        Assert.Equal(136, Marshal.SizeOf<MlxMarshal.NativeDeviceInfo>());
        Assert.Equal(0, Offset<MlxMarshal.NativeDeviceInfo>(nameof(MlxMarshal.NativeDeviceInfo.HasMetal)));
        Assert.Equal(4, Offset<MlxMarshal.NativeDeviceInfo>(nameof(MlxMarshal.NativeDeviceInfo.Architecture)));
        Assert.Equal(72, Offset<MlxMarshal.NativeDeviceInfo>(nameof(MlxMarshal.NativeDeviceInfo.MemoryBytes)));
        Assert.Equal(80, Offset<MlxMarshal.NativeDeviceInfo>(nameof(MlxMarshal.NativeDeviceInfo.RecommendedWorkingSetBytes)));
        Assert.Equal(88, Offset<MlxMarshal.NativeDeviceInfo>(nameof(MlxMarshal.NativeDeviceInfo.MlxSwiftLmRevision)));
    }

    [Fact]
    public void TheResultStructIs40BytesWithTheBridgesOffsets()
    {
        Assert.Equal(40, sizeof(MlxMarshal.NativeResult));
        Assert.Equal(40, Marshal.SizeOf<MlxMarshal.NativeResult>());
        Assert.Equal(0, Offset<MlxMarshal.NativeResult>(nameof(MlxMarshal.NativeResult.Text)));
        Assert.Equal(8, Offset<MlxMarshal.NativeResult>(nameof(MlxMarshal.NativeResult.PromptTokens)));
        Assert.Equal(12, Offset<MlxMarshal.NativeResult>(nameof(MlxMarshal.NativeResult.GeneratedTokens)));
        Assert.Equal(16, Offset<MlxMarshal.NativeResult>(nameof(MlxMarshal.NativeResult.StopReason)));
        Assert.Equal(24, Offset<MlxMarshal.NativeResult>(nameof(MlxMarshal.NativeResult.PromptSeconds)));
        Assert.Equal(32, Offset<MlxMarshal.NativeResult>(nameof(MlxMarshal.NativeResult.GenerationSeconds)));
    }

    [Fact]
    public void TheStatusIsFourBytesAsTheCEnumIs() => Assert.Equal(4, Unsafe.SizeOf<MlxStatus>());

    [Fact]
    public void EveryStatusInTheHeaderHasItsValueHereAndNoOtherExists()
    {
        string header = Repository.Text(Header);
        Dictionary<string, int> declared = Regex.Matches(header, @"NAPKIN_MLX_([A-Z_]+)\s*=\s*(\d+)")
            .ToDictionary(match => Pascal(match.Groups[1].Value), match => int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(6, declared.Count);
        Assert.Equal(
            declared.OrderBy(pair => pair.Value).Select(pair => (pair.Key, pair.Value)),
            Enum.GetValues<MlxStatus>().Select(status => (status.ToString(), (int)status)));
    }

    [Fact]
    public void TheHeadersFixedArraysAndMemberOrderAreTheStructsHere()
    {
        string header = Repository.Text(Header);
        Assert.Equal(MlxMarshal.ArchitectureBytes, ArraySize(header, "architecture"));
        Assert.Equal(MlxMarshal.RevisionBytes, ArraySize(header, "mlx_swift_lm_revision"));
        Assert.Equal(
            ["has_metal", "architecture", "memory_bytes", "recommended_working_set_bytes", "mlx_swift_lm_revision"],
            Members(header, "napkin_mlx_device_info"));
        Assert.Equal(
            ["text", "prompt_tokens", "generated_tokens", "stop_reason", "prompt_seconds", "generation_seconds"],
            Members(header, "napkin_mlx_result"));
    }

    [Fact]
    public void TheHeaderDeclaresTheEightFunctionsNativeMlxImports()
    {
        string header = Repository.Text(Header);
        string[] declared = [.. Regex.Matches(header, @"\b(napkin_mlx_[a-z_]+)\s*\(").Select(match => match.Groups[1].Value).Distinct().Order()];
        string native = Repository.Text("src/Napkin.Assistant.Mlx/NativeMlx.cs");
        string[] imported = [.. Regex.Matches(native, @"private static partial \w+ (napkin_mlx_[a-z_]+)\(").Select(match => match.Groups[1].Value).Order()];

        Assert.Equal(8, declared.Length);
        Assert.Equal(declared, imported);
    }

    [Fact]
    public void TheBridgeWasBuiltForThisNapkinsInterfaceVersion()
    {
        string errors = Repository.Text("native/NapkinMlx/Sources/NapkinMlx/Errors.swift");
        Match version = Regex.Match(errors, @"static let abiVersion: Int32 = (\d+)");

        Assert.True(version.Success);
        Assert.Equal(MlxBridge.AbiVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), version.Groups[1].Value);
    }

    [Fact]
    public void TheDeviceStructReadsItsTextUpToTheNul()
    {
        MlxMarshal.NativeDeviceInfo native = default;
        native.HasMetal = 1;
        native.MemoryBytes = 24UL << 30;
        native.RecommendedWorkingSetBytes = 19_069_992_960;
        Fill(native.Architecture, MlxMarshal.ArchitectureBytes, "applegpu_g17g");
        Fill(native.MlxSwiftLmRevision, MlxMarshal.RevisionBytes, "ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb");

        MlxDeviceInfo info = MlxMarshal.ToDeviceInfo(native);

        Assert.Equal(new MlxDeviceInfo(true, "applegpu_g17g", 24UL << 30, 19_069_992_960, "ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb"), info);
    }

    [Fact]
    public void AFixedArrayFilledToTheBrimIsReadWhole()
    {
        MlxMarshal.NativeDeviceInfo native = default;
        string full = new('a', MlxMarshal.RevisionBytes);
        Fill(native.MlxSwiftLmRevision, MlxMarshal.RevisionBytes, full);

        MlxDeviceInfo info = MlxMarshal.ToDeviceInfo(native);

        Assert.False(info.HasMetal);
        Assert.Equal(string.Empty, info.Architecture);
        Assert.Equal(full, info.MlxSwiftLmRevision);
    }

    [Fact]
    public void AFixedArrayIsUtf8() =>
        Assert.Equal("Äpple", MlxMarshal.FixedUtf8([.. Encoding.UTF8.GetBytes("Äpple"), 0, (byte)'x']));

    [Fact]
    public void AnErrorSentenceIsReadThenFreedThroughTheBridgeOnce()
    {
        IntPtr sentence = Marshal.StringToCoTaskMemUTF8("There is no Metal library at /x/mlx.metallib.");
        List<IntPtr> freed = [];

        string? read = MlxMarshal.TakeString(sentence, pointer =>
        {
            freed.Add(pointer);
            Marshal.FreeCoTaskMem(pointer);
        });

        Assert.Equal("There is no Metal library at /x/mlx.metallib.", read);
        Assert.Equal([sentence], freed);
    }

    [Fact]
    public void NoSentenceIsNullAndFreesNothing()
    {
        int frees = 0;
        Assert.Null(MlxMarshal.TakeString(IntPtr.Zero, _ => frees++));
        Assert.Null(MlxMarshal.TakeResult(IntPtr.Zero, _ => frees++));
        Assert.Equal(0, frees);
    }

    [Fact]
    public void AResultIsReadWholeThenFreedThroughTheBridgeOnce()
    {
        IntPtr text = Marshal.StringToCoTaskMemUTF8("{\"parts\":[],\"note\":\"é\"}");
        MlxMarshal.NativeResult* native = (MlxMarshal.NativeResult*)NativeMemory.AllocZeroed((nuint)sizeof(MlxMarshal.NativeResult));
        *native = new MlxMarshal.NativeResult
        {
            Text = text,
            PromptTokens = 812,
            GeneratedTokens = 57,
            StopReason = 3,
            PromptSeconds = 0.25,
            GenerationSeconds = 1.5,
        };
        List<IntPtr> freed = [];

        MlxGeneration? generation = MlxMarshal.TakeResult((IntPtr)native, pointer =>
        {
            freed.Add(pointer);
            Marshal.FreeCoTaskMem(text);
            NativeMemory.Free((void*)pointer);
        });

        Assert.Equal(
            new MlxGeneration("{\"parts\":[],\"note\":\"é\"}", 812, 57, MlxStopReason.SchemaComplete, TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(1.5)),
            generation);
        Assert.Equal([(IntPtr)native], freed);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1.0)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e30)]
    public void AResultWithNoTextAndTimesTheBridgeCouldNotMeasureReadsAsEmptyAndZero(double seconds)
    {
        MlxMarshal.NativeResult native = new() { Text = IntPtr.Zero, StopReason = 0, PromptSeconds = seconds, GenerationSeconds = seconds };

        MlxGeneration? generation = MlxMarshal.TakeResult((IntPtr)(&native), _ => { });

        Assert.Equal(new MlxGeneration(string.Empty, 0, 0, MlxStopReason.StopToken, TimeSpan.Zero, TimeSpan.Zero), generation);
    }

    [Fact]
    public void ACancelFlagIsFourBytesOfZeroUntilSet()
    {
        using CancelFlag flag = new();

        Assert.False(flag.IsInvalid);
        Assert.False(flag.IsSet);
        Assert.Equal(0, Marshal.ReadInt32(flag.DangerousGetHandle()));

        flag.Set();

        Assert.True(flag.IsSet);
        Assert.Equal(1, Marshal.ReadInt32(flag.DangerousGetHandle()));
    }

    [Fact]
    public void ACancelFlagIsSetByItsToken()
    {
        using CancelFlag flag = new();
        using CancellationTokenSource cancel = new();
        using CancellationTokenRegistration registration = flag.SetWhenCancelled(cancel.Token);

        Assert.False(flag.IsSet);
        cancel.Cancel();
        Assert.True(flag.IsSet);
    }

    [Fact]
    public void AReleasedCancelFlagReadsAsSetAndIgnoresASet()
    {
        CancelFlag flag = new();
        flag.Dispose();

        Assert.True(flag.IsSet);
        flag.Set();
        Assert.True(flag.IsClosed);
    }

    [Fact]
    public void TheBridgeIsLoadedFromNativeBesideTheExecutable()
    {
        string native = Path.Combine("/app", "native");

        Assert.Equal(native, MlxBridge.NativeDirectory("/app"));
        Assert.Equal(Path.Combine(native, "libNapkinMlx.dylib"), MlxBridge.LibraryPath("/app"));
        Assert.Equal(Path.Combine(native, "mlx.metallib"), MlxBridge.MetallibPath("/app"));
        Assert.Equal(["libNapkinMlx.dylib", "mlx.metallib", "libswiftCompatibilitySpan.dylib"], MlxBridge.Files);
        Assert.Equal(AppContext.BaseDirectory, MlxBridge.BaseDirectory);
        Assert.True(MlxModelHandle.None.IsNone);
        Assert.False(new MlxModelHandle(7).IsNone);
    }

    private static int Offset<T>(string field) => (int)Marshal.OffsetOf<T>(field);

    private static void Fill(byte* field, int length, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        Assert.True(bytes.Length <= length);
        bytes.CopyTo(new Span<byte>(field, length));
    }

    private static int ArraySize(string header, string member)
    {
        Match match = Regex.Match(header, $@"\bchar\s+{member}\[(\d+)\]");
        Assert.True(match.Success, $"no char {member}[N] in the header");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string[] Members(string header, string type)
    {
        Match body = Regex.Match(header, @"typedef struct \{([^}]*)\}\s*" + type + ";");
        Assert.True(body.Success, $"no struct {type} in the header");
        return [.. Regex.Matches(body.Groups[1].Value, @"\b([a-z_]+)(?:\[\d+\])?;").Select(match => match.Groups[1].Value)];
    }

    private static string Pascal(string upperSnake) =>
        string.Concat(upperSnake.Split('_').Select(word => word[..1] + word[1..].ToLowerInvariant()));
}
