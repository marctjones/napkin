using System.Text.Json;

namespace Napkin.Tools.Tests;

/// <summary>
/// The MLX bridge's third-party notices (#243; docs/design/mlx-runtime.md §3.7) are kept by hand,
/// since `licenses check` reads NuGet packages only. This holds the hand-kept table to the pins it
/// describes: every package <c>native/NapkinMlx/Package.resolved</c> pins is named in
/// <c>docs/third-party-notices.md</c> at exactly its revision, so a re-pin that forgets to re-read
/// the licence fails here. The shipped <c>native/NOTICES.txt</c> must carry the licence texts the
/// bridge's MIT, BSD and Apache components ask to travel with a binary copy.
/// </summary>
public class MlxBridgeNoticesTests
{
    private static string Read(string relative) => ReleaseWorkflowRules.Read(relative);

    [Fact]
    public void Every_pinned_swift_package_is_in_the_notices_at_its_revision()
    {
        var notices = Read("docs/third-party-notices.md");
        using var resolved = JsonDocument.Parse(Read("native/NapkinMlx/Package.resolved"));

        var pins = resolved.RootElement.GetProperty("pins").EnumerateArray().ToList();
        Assert.NotEmpty(pins);
        foreach (var pin in pins)
        {
            var identity = pin.GetProperty("identity").GetString()!;
            var revision = pin.GetProperty("state").GetProperty("revision").GetString()!;
            Assert.True(
                notices.Contains(revision, StringComparison.Ordinal),
                $"{identity} is pinned at {revision}, which docs/third-party-notices.md does not name: re-read its licence at that revision and update the table and native/NOTICES.txt.");
        }
    }

    [Fact]
    public void The_shipped_notices_carry_every_licence_text_the_bridge_needs()
    {
        var shipped = Read("native/NOTICES.txt");

        Assert.Contains("Apache License\n                           Version 2.0, January 2004", shipped);
        Assert.Contains("Runtime Library Exception to the Apache 2.0 License", shipped);
        Assert.Contains("Permission is hereby granted, free of charge", shipped);
        Assert.Contains("Redistributions in binary form must reproduce the above copyright notice", shipped);
        Assert.Contains("XGrammar\n\nCopyright (c) 2024 by XGrammar Contributors", shipped);
        Assert.Contains("The SwiftCrypto Project", shipped);
        Assert.Contains("Copyright (C) 2010-2022 Max-Planck-Society", shipped);
        Assert.Contains("Copyright 2009-2010 Cybozu Labs, Inc.", shipped);
        Assert.Contains("GNU\nAffero General Public License v3.0", shipped);
    }
}
