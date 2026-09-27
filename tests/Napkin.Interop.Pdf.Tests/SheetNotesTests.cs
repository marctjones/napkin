using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// The rules engine's results on the sheet, against the SYNTHETIC packs (their numbers are made up;
/// CodePacks/README.md). <c>us-zz-frame</c> rev 1, ZZ-HEADER, supports zz-roof, snow ≤ 30: a span over
/// 4'-1" and up to 6'-1" is row r.s30.b, (2) 2x10 with 1 jack and 2 kings each side. The pack is not
/// signed off, so every citation carries UNREVIEWED.
/// </summary>
public class SheetNotesTests
{
    static readonly string RootOne = Path.Combine(AppContext.BaseDirectory, "CodePacks", "one");
    static readonly string RootBrace = Path.Combine(AppContext.BaseDirectory, "CodePacks", "brace");
    static readonly CodePacks One = CodePacks.Discover([RootOne]);
    static readonly CodeChoice Frame = new("us-zz-frame", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;
    static readonly LayerId WallLayer = LayerId.New(), OpeningLayer = LayerId.New();

    static Length In(long whole) => Length.Inches(whole);

    /// <summary>A 12 ft exterior bearing wall carrying zz-roof, snow 30, and a 60″ window in it.</summary>
    static Sketch Wall(CodeChoice? code)
    {
        Box wall = new Box(EntityId.New(), WallLayer, Point3.Origin, In(144), Length.Inches(3, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            WallInputs = new WallInputs("zz-roof", null) { Side = WallSide.Exterior, Bearing = true },
        };
        Box window = new Box(EntityId.New(), OpeningLayer, new Point3(In(12), Length.Zero, In(36)), In(60), Length.Inches(3, 1, 2), In(42), BoxFace.Top, Angle.Zero)
        {
            Name = "Window 1",
        };
        return Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(wall)
            .WithEntity(window) with
        {
            Code = code,
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = 30 },
        };
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void A_sized_header_prints_its_headline_and_its_citation_as_the_panel_shows_them_UNREVIEWED_included()
    {
        Sketch sketch = Wall(Frame);
        IReadOnlyList<SheetNote> notes = SheetNotes.Of(sketch, One, Library);

        SheetNote header = notes[0];
        Assert.Equal("Window 1: header", header.Heading);
        CheckWords words = CodeCheck.Words(Assert.Single(CodeCheck.Of(sketch, One)), Library);
        Assert.Equal([words.Headline, words.Citation], header.Lines);
        Assert.Equal("Header (2) 2x10, 1 jack stud and 2 king studs each side.", header.Lines[0]);
        Assert.Contains("Table ZZ-HEADER", header.Lines[1], StringComparison.Ordinal);
        Assert.Contains("row r.s30.b", header.Lines[1], StringComparison.Ordinal);
        Assert.Contains(AdoptedCodeRef.UnreviewedText, header.Lines[1], StringComparison.Ordinal);

        // The wall's bracing: this pack has no bracing provisions, and says so as the panel does.
        SheetNote bracing = notes[1];
        Assert.Equal("Wall 1: bracing", bracing.Heading);
        CheckWords braced = BracingCheck.Words(Assert.Single(BracingCheck.Of(sketch, One)).Result);
        Assert.Equal(braced.Headline, bracing.Lines[0]);

        // Printed, the text layer carries them unchanged.
        PlanSheet sheet = new(
            new TitleBlock("Wall with a window", new DateOnly(2026, 9, 27), SheetNotes.CodeLine(sketch, One)),
            new FeetInchesFormat(16),
            [],
            SheetNotes.Heading(sketch, One),
            notes);
        // A reader's plain text runs a wrapped line straight into the next, so the long citation is
        // compared with its spaces taken out, every character in order.
        ReadBack read = ReadBack.Of(sheet);
        string text = string.Join(" ", read.Text), letters = string.Concat(read.Letters);
        Assert.Contains("Code check under ZZ FRAME (IRC 2099, pack us-zz-frame rev 1)", text, StringComparison.Ordinal);
        Assert.Contains(header.Lines[0], text, StringComparison.Ordinal);
        Assert.Contains(ReadBack.Squash(header.Lines[1]), letters, StringComparison.Ordinal);
        Assert.Contains(ReadBack.Squash(AdoptedCodeRef.UnreviewedText), letters, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_line_names_the_code_its_review_and_its_lock()
    {
        Assert.Equal(
            "Code: ZZ FRAME (IRC 2099), pack us-zz-frame rev 1 — " + AdoptedCodeRef.UnreviewedText + ". Locked on 2026-09-25 to pack us-zz-frame revision 1.",
            SheetNotes.CodeLine(Wall(Frame), One));
        Assert.Equal(
            "Code: ZZ FRAME (IRC 2099), pack us-zz-frame rev 1 — " + AdoptedCodeRef.UnreviewedText + ". " + CodeCheck.FollowingNote("us-zz-frame"),
            SheetNotes.CodeLine(Wall(Frame with { Mode = CodeMode.Following, LockedOn = null }), One));
        Assert.Null(SheetNotes.CodeLine(Wall(null), One));

        // A choice no pack answers says why, as the code window does.
        CodeChoice missing = new("us-zz-nowhere", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));
        Assert.Equal(One.Resolve(missing).Problem, SheetNotes.CodeLine(Wall(missing), One));
        Assert.Equal("Code check", SheetNotes.Heading(Wall(null), One));
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void A_deck_prints_its_frame_and_its_check_lines_as_the_deck_panel_shows_them()
    {
        // The deck tests' §9.1 deck under the SYNTHETIC us-zz-deck: 144 × 120, 36" up, north edge on the house.
        CodePacks packs = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck")]);
        LayerId wallLayer = LayerId.New(), deckLayer = LayerId.New();
        Box house = new(EntityId.New(), wallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), Length.Inches(5, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "House",
            Phase = Phase.Existing,
        };
        DeckInputs inputs = new(
            JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "6x6", 3, Length.Zero, "5/4x6", Length.Inches(0, 1, 8), true, "zz-deck", "zz-fir", In(42), null, null);
        Box deck = new(EntityId.New(), deckLayer, new Point3(In(48), In(-120), Length.Zero), In(144), In(120), In(36), BoxFace.Top, Angle.Zero)
        {
            Name = "Deck 1",
            Deck = inputs,
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(wallLayer, BuildingLayers.Wall)).WithLayer(new Layer(deckLayer, BuildingLayers.Deck)).WithEntity(house).WithEntity(deck) with
        {
            Code = new CodeChoice("us-zz-deck", 1, CodeMode.Locked, new DateOnly(2026, 9, 26)),
            Site = SiteValues.NotEntered with { SoilBearingPsf = 2000, FrostDepth = In(42), GroundSnowLoadPsf = 30 },
        };

        SheetNote note = Assert.Single(SheetNotes.Of(sketch, packs, Library), note => note.Heading == "Deck 1");
        DeckChecks checks = Assert.Single(DeckCheck.Of(sketch, packs, Library));
        Assert.Equal(
            [$"Frame: {Napkin.Modules.Editing.DeckTool.FrameLine(checks.Framing!)}.", .. checks.Guides, .. checks.Lines.Select(line => line.Text)],
            note.Lines);
        Assert.Contains(note.Lines, line => line.Contains(AdoptedCodeRef.UnreviewedText, StringComparison.Ordinal));

        // A deck with no frame says why instead, as the panel does.
        Sketch unframed = sketch.WithEntity(deck with { Deck = inputs with { Joist = "zz-nothing" } });
        DeckChecks refused = Assert.Single(DeckCheck.Of(unframed, packs, Library));
        Assert.Null(refused.Framing);
        SheetNote why = Assert.Single(SheetNotes.Of(unframed, packs, Library), note => note.Heading == "Deck 1");
        Assert.Equal(refused.Refusal!.Text, why.Lines[0]);
    }

    [Fact]
    public void Walls_are_checked_for_bracing_under_a_bracing_pack_and_a_design_with_nothing_to_check_has_no_notes()
    {
        CodePacks brace = CodePacks.Discover([RootBrace]);
        CodeChoice braceA = new("us-zz-brace-a", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));
        Sketch sketch = Wall(braceA);
        SheetNote bracing = SheetNotes.Of(sketch, brace, Library).Single(note => note.Heading == "Wall 1: bracing");
        CheckWords words = BracingCheck.Words(Assert.Single(BracingCheck.Of(sketch, brace)).Result);
        Assert.Equal(((string[])[words.Headline, words.Citation, words.Interpolation]).Where(line => line.Length > 0), bracing.Lines);

        Assert.Empty(SheetNotes.Of(Sketch.Empty, One, Library));
    }
}
