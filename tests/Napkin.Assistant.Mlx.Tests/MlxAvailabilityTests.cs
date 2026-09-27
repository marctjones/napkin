using System.Runtime.InteropServices;

namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// Whether MLX can run here, answered without loading anything off Apple silicon: every
/// combination of platform, architecture and the bridge's three files (mlx-runtime.md §7.2), then
/// the bridge's own word on Metal.
/// </summary>
[Trait("Feature", "AST-007")]
public class MlxAvailabilityTests
{
    private static readonly string App = Path.Combine(Path.GetTempPath(), "napkin-app");

    public static TheoryData<bool, Architecture, bool, bool, bool> EveryCombination()
    {
        TheoryData<bool, Architecture, bool, bool, bool> data = [];
        foreach (bool macOS in new[] { true, false })
        {
            foreach (Architecture architecture in new[] { Architecture.Arm64, Architecture.X64 })
            {
                foreach (bool dylib in new[] { true, false })
                {
                    foreach (bool metallib in new[] { true, false })
                    {
                        foreach (bool span in new[] { true, false })
                        {
                            data.Add(macOS, architecture, dylib, metallib, span);
                        }
                    }
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public void OnlyAppleSiliconWithAllThreeFilesCanRunMlx(bool macOS, Architecture architecture, bool dylib, bool metallib, bool span)
    {
        Dictionary<string, bool> present = new()
        {
            [Native("libNapkinMlx.dylib")] = dylib,
            [Native("mlx.metallib")] = metallib,
            [Native("libswiftCompatibilitySpan.dylib")] = span,
        };

        string? reason = MlxAvailability.Check(macOS, architecture, path => present[path], App);

        string? expected = !macOS || architecture != Architecture.Arm64 ? MlxAvailability.NotAppleSilicon
            : !dylib ? Missing("libNapkinMlx.dylib")
            : !metallib ? Missing("mlx.metallib")
            : !span ? Missing("libswiftCompatibilitySpan.dylib")
            : null;
        Assert.Equal(expected, reason);
    }

    [Theory]
    [InlineData(false, Architecture.X64)]
    [InlineData(false, Architecture.Arm64)]
    [InlineData(true, Architecture.X64)]
    [InlineData(true, Architecture.X86)]
    public void OffAppleSiliconNothingIsLookedFor(bool macOS, Architecture architecture)
    {
        List<string> looked = [];

        string? reason = MlxAvailability.Check(macOS, architecture, path => { looked.Add(path); return true; }, App);

        Assert.Equal("MLX runs on Apple silicon; use a program on this machine (Ollama or llama-server) instead.", reason);
        Assert.Empty(looked);
    }

    [Fact]
    public void AMissingFileIsNamedWithItsPath()
    {
        string? reason = MlxAvailability.Check(true, Architecture.Arm64, path => !path.EndsWith("mlx.metallib", StringComparison.Ordinal), App);

        Assert.Equal(
            $"MLX is not in this build of napkin: {Path.Combine(App, "native", "mlx.metallib")} is missing. Use a program on this machine (Ollama or llama-server) instead.",
            reason);
    }

    [Fact]
    public void ThisProcessIsCheckedWithItsOwnPlatformAndFolder() =>
        Assert.Equal(
            MlxAvailability.Check(OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture, File.Exists, AppContext.BaseDirectory),
            MlxAvailability.ForThisProcess());

    [Fact]
    public void ABridgeWithMetalIsAvailableAndSaysWhatThisMacHas()
    {
        FakeNativeMlx native = new();

        MlxProbe probe = MlxAvailability.Probe(native);

        Assert.True(probe.IsAvailable);
        Assert.Null(probe.Refusal);
        Assert.Equal(FakeNativeMlx.AppleSilicon, probe.Device);
        Assert.Equal(1, native.AbiCalls);
        Assert.Equal(1, native.DeviceCalls);
    }

    [Fact]
    public void NoMetalDeviceIsNotAvailableButStillSaysWhatThisMacHas()
    {
        MlxDeviceInfo headless = new(false, "Unknown", 16UL << 30, 0, "ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb");
        FakeNativeMlx native = new() { DeviceInfo = headless };

        MlxProbe probe = MlxAvailability.Probe(native);

        Assert.False(probe.IsAvailable);
        Assert.Equal("MLX found no Metal device on this Mac.", probe.Refusal);
        Assert.Equal(headless, probe.Device);
    }

    [Fact]
    public void ADeviceTheBridgeCannotDescribeIsRefusedInItsWords()
    {
        FakeNativeMlx native = new() { DeviceStatus = MlxStatus.Error, DeviceError = "napkin_mlx_device was given no struct to fill" };

        MlxProbe probe = MlxAvailability.Probe(native);

        Assert.Equal(new MlxProbe(null, "napkin_mlx_device was given no struct to fill"), probe);
    }

    [Fact]
    public void ABridgeOfAnotherInterfaceVersionIsNotAskedAnythingElse()
    {
        FakeNativeMlx native = new() { Abi = 2 };

        MlxProbe probe = MlxAvailability.Probe(native);

        Assert.Equal("The MLX bridge speaks version 2 of its interface; this napkin speaks version 1.", probe.Refusal);
        Assert.Equal(0, native.DeviceCalls);
    }

    [Theory]
    [MemberData(nameof(LoadFailures))]
    public void ABridgeThatWillNotLoadIsRefusedInTheLoadersWords(Exception failure)
    {
        MlxProbe probe = MlxAvailability.Probe(new FakeNativeMlx { Throws = failure });

        Assert.Equal(new MlxProbe(null, $"napkin could not load the MLX bridge: {failure.Message}"), probe);
    }

    [Fact]
    public void AnyOtherExceptionIsNotHidden() =>
        Assert.Throws<InvalidOperationException>(() => MlxAvailability.Probe(new FakeNativeMlx { Throws = new InvalidOperationException("a bug") }));

    public static TheoryData<Exception> LoadFailures() => new()
    {
        new DllNotFoundException("Unable to load shared library '/app/native/libNapkinMlx.dylib' or one of its dependencies."),
        new EntryPointNotFoundException("Unable to find an entry point named 'napkin_mlx_device'."),
        new BadImageFormatException("An attempt was made to load a program with an incorrect format."),
    };

    private static string Native(string file) => Path.Combine(App, "native", file);

    private static string Missing(string file) =>
        $"MLX is not in this build of napkin: {Native(file)} is missing. Use a program on this machine (Ollama or llama-server) instead.";
}
