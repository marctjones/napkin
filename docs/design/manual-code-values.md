# Manual code values: a row you read, entered into your project

Status: **Signed off by Marc 2026-09-27 with the recommended decisions (§15).** Design note by Fable, 2026-09-27; umbrella issue **#245**,
slices #246–#249 (§12), milestone M10 Real code. Written for the capability Marc asked for on 2026-09-27: *"Is it possible to put [the code-compliance data question] aside and make it
so that there is a mode for having someone manually confirm certain values against the code so we don't
have to be blocked on getting the code or shipping the code."* Marc chose **"Yes, design it"** over a
stricter local-only alternative and over "not now", so one thing is decided before this note starts:
**a value a person enters persists in the shared `.napkin` project file.** §7 says why that is safe;
it is not re-opened here. Everything else below is a recommended default that a "yes" accepts (§15).

**What this is.** A person who owns a copy of the adopted code, looking at a header napkin says
**No data** for, types the *one row their drawing needs* — the header, jack and king studs the table
prints for this opening's span, this wall's supports and this project's site values — with a required
citation to where in their copy they read it. napkin uses that row for that opening only, labels it
**ENTERED BY HAND** everywhere it appears (panel, message bar, shopping list, every sheet, the
assistant's context), records the exact inputs it was entered for, and drops it the moment any of them
moves or the adopted code changes. It never claims the row as its own.

**What this is not.** #209 is blocked on where an agent may read the 2021 IRC from, so that napkin can
*ship* transcribed, reviewed tables to every user. This note does not touch that: nothing here reads a
code, ships a value, loads a table, or lets a person load one. It is the *UI* for the mechanism
`packs/layers/irc-2021/README.md` already describes in developer terms ("fill from your own copy of the
2021 IRC … following docs/rules-engine.md") — scoped down from a shared pack to one row in one
project, with an honest trust label. #14 and #158 stay the goal for a shareable, no-caveat project.

Read with: [`rules-engine-model.md`](./rules-engine-model.md) (the four-result union, §3; the citation,
§2; site inputs offered, never applied, §5; recompute, §7), [`DESIGN.md`](../../DESIGN.md) §5.3, §5.4 and
§7, [`renovation-sketches.md`](./renovation-sketches.md) §4.3 (the header *you choose* on a not-bearing
wall — a different thing, §1), [`llm-assistant.md`](./llm-assistant.md) §1 and §4.2.

---

## How to use it (a person's clicks, start to finish)

Open `samples/window-in-existing-wall`. Under **Project → Adopted code and site…** choose **CT 2022**,
pick **Bloomfield** in the Town picker and press **Use these values** (ground snow load 30 psf and
wind 120 mph, as Appendix AY p. 157 prints them; seismic B statewide, Table R301.2 p. 131 — the
offer napkin already makes, #210), type the building width, leave the frost depth empty, close the
window. Select **Window 1**.
Its part panel's **Code check** block says what it says today:

> The loaded pack CT 2022 has no header table for exterior-bearing walls, so napkin cannot size this
> header. Nothing is guessed: add the table to the pack directory from your copy of the code
> (docs/rules-engine.md). Where to add tables: docs/rules-engine.md

and, new, under it: **[ I have the code: enter this row… ]**.

Press it. A small window opens, *Enter a header row — Window 1*, and says first, in fixed text:

> You are typing one row from your own copy of the code into this project. napkin will use it for
> this opening only, label it ENTERED BY HAND wherever it appears, and stop using it the moment the
> opening, the wall, the site values or the adopted code change. napkin does not check it. Apply your
> state's amendments yourself: CT 2022 amends Table R602.7(1) (footnote e, p. 145) — *[the amendment's
> verbatim text the pack already carries]*.

Then **This opening**, read from the drawing and not editable: *Wall 1, exterior, bearing · header span
3'-0" (the rough width) · ground snow load 30 psf · wind 120 mph · seismic B · frost depth not entered ·
building width 24'-0" · roof live load not entered · under CT 2022 (pack us-ct-2022)*. Then the fields
you fill:

- **What the wall supports**, typed as the table's column heading prints it (the pack has no table, so
  it has no list to offer).
- **What the row says**: header plies and lumber (the materials library's dimension lumber, the same
  picker the not-bearing wall's header uses), jack studs each side, king studs each side.
- **Where you read it** — all required: the code and edition (*"2021 IRC as adopted by CT 2022"*), the
  table (*"Table R602.7(1)"*), the page and row as printed; and optional notes (a footnote you applied,
  an interpolation you did by hand).
- **Entered by**: your name, prefilled with your computer's user name the first time and with what
  you typed last time after that; always editable, never empty. The date is today's, stamped.

**Use this row** is enabled once every required field has text. Press it. One undo step, *"Entered a
header row for Window 1"*. The Code check block now reads (with **placeholders** for whatever your copy
prints — nothing here is a claim about the code):

> **ENTERED BY HAND** — Header «(2) 2x10», «1» jack stud and «2» king studs each side.
> ENTERED BY HAND — Marc, 2026-09-27, from «2021 IRC Table R602.7(1), p. N, row …» as adopted by
> CT 2022. Typed from a copy of the code; not napkin's data, not reviewed by napkin.
> ▸ How it was found: *Entered for: Wall 1 exterior bearing, supports «roof-ceiling», span 3'-0",
> ground snow load 30 psf, wind 120 mph, seismic B, frost depth not entered, building width 24'-0",
> roof live load not entered, under pack us-ct-2022.* Notes: «…».
> [ Edit row… ] [ Remove row ]

The message bar says *"Header for Window 1 is now entered by hand: «(2) 2x10», «1» jack and «2» king
each side (ENTERED BY HAND — Marc, 2026-09-27, «2021 IRC Table R602.7(1) p. N»)."* The frame uses the
member and the counts; the shopping list's **Framing** section names the opening with the same tag.

Now drag Window 1 six inches wider. The block goes back to **No data**, with a second line:

> The row Marc entered on 2026-09-27 no longer applies: the header span moved from 3'-0" to 3'-6".
> Enter the row for the new inputs, or remove it. [ Enter the row again… ] [ Remove row ]

and the message bar says *"Header for Window 1: the row entered by hand no longer applies (span 3'-0"
→ 3'-6"); No data."* Undo puts the window back; the row applies again and the bar says so. Choose
another adopted code, if one is installed: the same, naming the code. Now type the frost depth, which
was empty when the row was entered: the same, naming it — napkin does not know which site values the
table used, so a value going from not entered to entered is a move too (§6.1).

The day napkin's own CT 2022 pack gains Table R602.7(1) (#14), the block shows **napkin's** result, and
under it: *"napkin now answers this from its own table. The row Marc entered on 2026-09-27 said
«(2) 2x10»; napkin says … . Remove the entered row."* The entered row never overrides napkin's own
answer (§5).

---

## 0. What exists today, and what this note builds on

| Today | Where | What this note does with it |
|---|---|---|
| `HeaderResult` is a closed union of four: `Sized`, `OutOfScope`, `InputMissing`, `NoData`; a reflection test asserts exactly four | `src/Napkin.Core.RulesEngine/HeaderSizing.cs`; `tests/…/EvaluatorTests.cs` "The_result_union_is_closed_with_exactly_four_named_members" | a fifth member, `Entered` (§3); the test says five |
| `NoData(NoTableForWallKind)` is what every real wall says under the shipped CT pack: its `irc-2021` base layer is empty | `HeaderResult.NoData.NoTableExplanation`; `packs/layers/irc-2021/README.md` | the only state in which an entered row is consulted (§5) |
| The site-values offer: the pack *offers* a town's cited values; nothing is set until **Use these values**; the source is recorded as if typed | `SiteOffer.Text/Accept`, `CodeWindow` Town picker, `GUI-CHECK-06` (#210) | the "offer, accept, never silent" shape, generalised to a value the person supplies rather than the pack |
| The frost offer: "CT 2022 says 42" (Table R301.2, p. 131) — use it?", and the frost line is "napkin's comparison of two typed values" | `DeckCheck.Frost`, `MainWindow.Deck.cs` (#199) | the second instance of the same shape; the precedent for the bracing entry (§3.5) |
| The header *you choose* on a not-bearing wall: `TypedHeader`, "your choice, not a code result", 1 jack and 1 king as napkin's placeholder | renovation-sketches §4.3, `WallInputs.Header` | **not** this feature (§1): that is a choice with no citation on a wall napkin does not check; this is a code row with a citation on a wall it does |
| The UNREVIEWED label: `AdoptedCodeRef.UnreviewedText`, one constant appended by `ToString` and `UnreviewedSentence` on every line from an unreviewed pack | `Citation.cs`; `DeckCheck.cs` | the model for the ENTERED BY HAND tag (§4) |
| Recompute is total; `Recompute.Diff` classifies by `ChangeKind`; the message bar says each change, the most serious first | `CodeSelection.cs`, `CodeCheck.Changes/Report/SwitchSummary`, `MainWindow.Building.SayRecompute` | new kinds and sentences (§6) |
| Per-opening data lives in `box.opening` (`fill`, format 13); per-wall data in `box.wall` | `docs/file-format.md` "Deck, shed roof and opening fill", "Building inputs" | `opening.enteredHeader` (format 17, §7); `wall.enteredBracing` later (§3.5) |
| `DirectoryPackSource`: a person may author a whole pack from their copy of the code in their per-user packs folder | `docs/rules-engine.md` "Author a pack from your own copy of the code" | untouched; this is the per-row, in-app, labelled version of the same right |
| The assistant sees each check as `result.ToString()` in its context pack, and the guard refuses any number not in the pack | `ContextPack.CheckLines`, `AnswerGuard` (#229–#232) | the tag rides along in `ToString`; one sentence in `ask.txt` (§9) |

---

## 1. Three kinds of provenance, not two

napkin has two today, and both are *napkin's*:

| Provenance | Who wrote the value | Who checked it | How it is labelled |
|---|---|---|---|
| napkin's reviewed data | a transcriber, from a primary source, into a shipped pack | an independent reviewer, row by row (§8.3 of the rules-engine note) | the citation: code, table, row, source, page, retrieval date, hash |
| napkin's unreviewed data | the same transcriber, not yet reviewed (`review.status` ≠ `signed-off`) | nobody yet | the same citation plus **UNREVIEWED: values not yet checked against the source** |
| **a row entered by hand** (this note) | **one person, for one opening in their own project, from their own copy** | **nobody, and napkin does not claim to** | **ENTERED BY HAND — name, date, the person's stated citation; "not napkin's data, not reviewed by napkin"** |

The third is not a weaker second. An unreviewed pack row is still napkin's: it has a `RowId`, a
`SourceRef` with a hash, a `CitationLayer`, golden tests waiting, and a review that will one day flip
its status. An entered row has none of those and never will; it is what one person read, recorded so
that anyone opening the file can judge it. That is why it is a separate type (§3), a separate label
(§4), and never merged into `Sized` — not a `Sized` with a flag, not a `Citation` with a fourth
`CitationLayer`.

It is also not the not-bearing wall's typed header (renovation-sketches §4.3). That one is a *choice*
on a wall napkin does not code-check at all, labelled "your choice, not a code result", with no
citation asked for. This one is a *code row* on a wall napkin does check, with a citation required,
and it is dropped when its inputs move. The two must not be confused in the UI: the entry form is
offered only under a code-checked wall's No data (§5), and the typed header only on a not-bearing wall.

---

## 2. What gets entered, exactly

### 2.1 One row, not a table

The person types the **outputs** the table prints for the **inputs napkin already has on screen**:
the header (plies and lumber), jack studs each side, king studs each side. They do not type the
table's bands, its footnotes, its domain or any other row. There is nothing to reuse: the entry is
for this opening (§2.3).

The inputs are shown, not asked, with one exception. With no table loaded, the pack declares no
`supports` values, so the wall's **Supports** picker is empty (`CodeCheck.SupportsChoices` returns
nothing and the tip says so). The form therefore takes **what the wall supports** as typed text, "as
the column heading prints it", prefilled from `WallInputs.Supports` if the wall already has one. The
wall's **side** and **bearing** must be said before the button appears (they are how the check is
routed; with either unsaid the result is `InputMissing`, and the panel already says where to say them).

Site values are recorded **as they are**, including "not entered". napkin has no table declaration, so
it cannot know which of the six the row depends on; it records all six and flags the row if any of
them changes — including a value going from not entered to entered (§6). The form says so.

### 2.2 The citation is required; no citation, no save

Three text fields, none may be empty: the **code and edition** the person is reading ("2021 IRC as
adopted by CT 2022", "2022 CSBC amendment"), the **table** as printed, and the **page and row**. A
fourth, **notes**, is optional: the footnote applied, an interpolation done by hand, the printing.
The person's **name** is required too (§8). The date is stamped by napkin.

The form's fixed text tells the person to apply their state's amendments themselves, and — when the
chosen pack lists a pending `amend-footnote` for the table they typed (`LoadedPack.Pending`;
Connecticut's R602.7(1) footnote e and R602.7(3) footnote b ship pending today) — shows that
amendment's verbatim text and page, because a person reading the model-code table alone would miss
it. This is the pack's own cited text, already in the repo, shown; nothing is applied.

### 2.3 One-shot, per opening

An entered row belongs to one opening and answers one question. Two windows with the same span on the
same wall need two entries. This is deliberate: the alternative — typing the row's *conditions* so
napkin can reuse it wherever they recur — is a small table, and a small table is where a person
misapplies a row to a case it does not cover (a different `supports`, a footnote that narrows it).
If two entries per project turns out to be twenty, "Use the same row for Window 2" — offered only
when the recorded inputs match the other opening's exactly — is the later enhancement, not a change
to the model.

---

## 3. The result type: a fifth member

### 3.1 `HeaderResult.Entered`

```csharp
public abstract record HeaderResult
{
    // Sized, OutOfScope, InputMissing, NoData as today, and:

    /// A row a person typed from their own copy of the code, for exactly the inputs on screen.
    /// Never napkin's data: carries who, when, where they read it, and the inputs it was entered
    /// for; every string form starts with EnteredRow.Tag.
    public sealed record Entered(MemberSpec Header, int JackStuds, int KingStuds, EnteredRow Entry) : HeaderResult;
}

/// The provenance of an entered row. Not a Citation: it has no RowId, no SourceRef, no hash, no
/// layer, and nothing in it is napkin's.
public sealed record EnteredRow(
    string Code,              // "2021 IRC as adopted by CT 2022", as typed
    string Table,             // "Table R602.7(1)", as typed
    string Location,          // "p. N, row …", as typed
    string? Notes,
    string EnteredBy,
    DateOnly EnteredOn,
    string PackId,            // the project's pack when entered: what "under CT 2022" means
    ValueList<string> Inputs) // the recorded inputs, in words: the "How it was found" lines
{
    public const string Tag = "ENTERED BY HAND";
    // "ENTERED BY HAND — Marc, 2026-09-27, from 2021 IRC as adopted by CT 2022, Table R602.7(1),
    //  p. N, row …. Typed from a copy of the code; not napkin's data, not reviewed by napkin."
    public override string ToString() => …;
}
```

`Entered.ToString()` is `$"{Tag} — {Header}, {JackStuds} jack, {KingStuds} king — {Entry}"`, so the
assistant's context line (§9), the golden runner's output and any log say the tag before the number.

`BracingResult.Entered` is §3.5. `DeckResult` gets none: the deck tables are real (DCA 6) and the
No data a deck line can still show is for a table the guide does not have, which a person cannot
supply a row for without the same conditions problem as §2.3; if a deck case for this arrives, it is
a later note.

### 3.2 Why a union member, when "Not checked" was kept out of the union

Renovation-sketches §4.3 added no member for the not-bearing wall ("`HeaderResult` gains no member:
'Not checked' is a building-module state … so the rules engine stays a pure lookup"). This note adds
one, and the difference is what the value has to *do*. "Not checked" produces nothing downstream: no
member is bought, no diff is announced, the assistant's line is a sentence. An entered row is an
answer with a member and stud counts, and everything that consumes a header answer switches on
`HeaderResult`: `CodeCheck.Framing` (jack, king, the header bought), `Recompute.Diff`
(`Classify`), `CodeCheck.Words/Short/Sentence/SwitchSummary`, `HelpSections.KindOf`, the context pack,
the cut list's Framing note, and the sheets to come. A parallel building-module state would need a
parallel branch at every one of those, each a place to forget the label. A member makes the closed
union say what the five honest answers are, and the reflection test makes every consumer visit it.

The engine stays a pure lookup all the same, by §3.3.

### 3.3 The engine never constructs it

`RulesEngine.For(pack).SizeHeader(request)` returns exactly the four members it returns today. Only
`CodeCheck.For` (the building module) constructs an `Entered`, and only after the engine answered
`NoData(NoTableForWallKind)` and the opening's stored row matches the live inputs (§5). Two tests pin
this: a property over the fixture packs and random requests asserting the engine's result is never
`Entered`, and the golden runner's expectation vocabulary (`sized`, `outOfScope`, `inputMissing`,
`noData`) gaining no `entered` case.

### 3.4 Every place a result becomes words, and what breaks silently

Adding a member is safe only if every fall-through is visited. These are the ones a grep finds today,
each of which does the wrong thing quietly for a member it does not know:

| Site | Today's default | With `Entered` |
|---|---|---|
| `CodeCheck.Words(HeaderResult, …)` | `_ => NoDataWords((HeaderResult.NoData)result)` — **a cast that throws** | its own `CheckWords`: headline with the tag, the citation line as the tag line, details = recorded inputs and notes |
| `CodeCheck.Short(HeaderResult)` | `_ => "no data to check it against"` | `"(2) 2x10, 1 jack and 2 king each side (ENTERED BY HAND — Marc, 2021 IRC Table R602.7(1) p. N; not napkin's data)"` |
| `Recompute.Classify` | `_ => NoAnswerChanged` | the new kinds (§6.3), matched before the fallbacks |
| `CodeCheck.Sentence` | `_ => "… still cannot be checked"` | the new sentences (§6.3) |
| `CodeCheck.SwitchSummary` | counts flagged and lost by kind | `EnteredToOutOfScope` counted as flagged; `EnteredToNoAnswer` as lost |
| `CodeCheck.Framing` | only `Sized` feeds jack/king/header | `Entered` feeds them the same way (`TryFindLumber`; the "not in the materials library" sentence reused) |
| `HelpSections.KindOf` | `_ => NotChecked` — compiles, mislabels the check as not checked | `ResultKind.Entered`, mapped to the new help section (§9; slice C) |
| `CutListWindow.CodeCheckNote` | `CodeCheck.Short` per opening | the tag arrives through `Short` |
| the closed-union test | exactly four, sorted by name | exactly five: `Entered, InputMissing, NoData, OutOfScope, Sized` |

The slice updates the reflection test first, so the build is red until every row above is done.

### 3.5 Bracing: two typed lengths compared (a later slice)

The bracing check is a method, not a lookup: a required braced length from the section's tables and
adjustment factors, against what the wall line provides through its assigned methods. With no
provisions loaded (`BracingResult.NoData`), `BracingCheck.Methods(pack)` is empty too — the person
cannot assign a method, so napkin cannot compute *provided* either. An entered bracing check is
therefore two typed lengths: the **required** length as the person works it out from their copy
(their tables, their factors, their arithmetic), and the **provided** length as they count it (their
reading of the methods' minimum panel lengths and contribution rules); napkin compares them exactly,
as `DeckCheck.FrostLine` compares the footing depth with the frost depth — "napkin's comparison of
two typed values". `BracingResult.Entered(Length Required, Length Provided, EnteredRow Entry)` with
the shortfall derived; the headline reuses `PassesText`/`FailsText` with the tag in front. Stored on
`WallInputs.EnteredBracing`; its recorded inputs are the pack, the wall's length and height, its
segment lengths (an opening added or moved changes them) and the six site values.

This is weaker than the header entry, where napkin's geometry supplies the span and the person types
only the outputs, and it lands after the header slices have proved the shape (slice D, §12), if
Decision 7 is a yes. #158's real second pack stays the goal.

---

## 4. The label: impossible to miss or strip

Mirroring `AdoptedCodeRef.UnreviewedText`: **one constant**, `EnteredRow.Tag = "ENTERED BY HAND"`,
uppercase, and every string form of an `Entered` result carries it — `Entered.ToString()`,
`EnteredRow.ToString()`, `CodeCheck.Short`, `CheckWords.Headline` and `CheckWords.Citation`. In the
panel the tag is the **first words of the headline**, so a truncated line still shows it, and the
citation line — the always-visible line, per DESIGN.md §7 "not just in a tooltip" — is the full
provenance sentence: name, date, the typed code, table and location, then *"Typed from a copy of the
code; not napkin's data, not reviewed by napkin."* The recorded inputs and notes are the **How it was
found** expander's content, where `Sized` shows its band trace. Visual emphasis beyond the words
(colour, an icon) is the UI slice's call within [`napkin-look.md`](./napkin-look.md); the words are
the guarantee, because the words are what every sheet, list and context line carries.

"Impossible to strip" is testable: a test asserts each string form contains `EnteredRow.Tag`, and
`tools/scripts/mutate.sh` removing the tag from `EnteredRow.ToString` must fail a test.

Anyone opening a shared file therefore sees, beside the number, who typed it, when, from what, and
that napkin did not — and can check the cited row in their own copy. That, not a private flag, is
what makes §7 safe.

---

## 5. Routing: when an entered row is consulted, and when it is not

### 5.1 Only on No data, with a pack chosen and no table

`CodeCheck.For` computes the engine's answer exactly as today. Then, if the opening's box carries an
`enteredHeader`:

| The engine said | The entry's recorded inputs | What the check returns | And beside it (`OpeningCheck`) |
|---|---|---|---|
| `NoData(NoTableForWallKind)` | equal the live inputs | **`Entered`** | — |
| `NoData(NoTableForWallKind)` | differ | `NoData`, as the engine said | `Stale`: the entry and which inputs moved (§6.4) |
| `NoData(NoPackSelected)` | (the pack differs by definition) | `NoData` | `Stale`, naming the code |
| `InputMissing` (side or bearing unsaid) | — | `InputMissing` | `Stale`, naming side or bearing |
| `Sized` or `OutOfScope` (a real table arrived) | — | **napkin's answer** | `Superseded`: the entry, for the panel to say so and offer Remove (§5.2) |
| any, on a wall marked not bearing | — | `null`, `NotChecked` as today | the entry is kept and ignored; the Not checked sentence adds one clause saying so |

The **button** that opens the form appears only in the first row's situation with no entry present:
a pack chosen, the wall's side and bearing said, the engine's answer `NoData(NoTableForWallKind)`.
This is narrower than "NoData or InputMissing", on purpose: `InputMissing(supports)` only arises
when a table *exists* and the person should choose from its values, not type a row past it;
`InputMissing(side|bearing)` means say them first; `NoData(NoPackSelected)` means there is no code to
enter the row under, and nothing for a code switch to invalidate against.

### 5.2 Superseded by napkin's own table, never the reverse

An entered row is consulted only when napkin has nothing. The day the pack gains the table — #14 lands,
or the person authors it in their packs folder — every entered row in every project is superseded on
open: napkin's `Sized` or `OutOfScope` is shown, the recompute announces it (§6.3), and the panel says
what the entered row said next to what napkin says, with **Remove row**. A real table's `OutOfScope`
beating a person's `Sized` reading is the auditability guarantee working, not a regression; the
message bar ranks it first.

### 5.3 Never an override

A person who disagrees with a real, cited, computed `Sized` or `OutOfScope` has no button. Overwriting
napkin's arithmetic with a typed number would make the citation on screen false, which is the one
thing DESIGN.md §7 forbids; the answer to that disagreement is an engineer, or a corrected pack through
the review process. The routing table above is the guard, and a test asserts that under a pack with
the table an entered row is never returned.

---

## 6. Recompute: stale the moment its inputs move

### 6.1 What is recorded, and compared exactly

An entered row stores the inputs it was entered for (`EnteredHeaderInputs`): the **pack id** (not
its revision), the wall's **side**, **what it supports** (as typed), the **header span**, and the
**six site values** (ground snow load, wind speed, seismic design category, frost depth, building
width, roof live load) each as it was — a value or "not entered". At every check the live inputs are
compared field by field, exactly (`Length` and `int`, no tolerance, like every comparison in the
engine); any difference makes the row stale, and the names of the fields that moved are what the panel
and the message bar say. A site value going from not entered to entered is a move.

This is conservative on purpose. napkin has no table, so it cannot know that the row did not depend on,
say, the wind speed; recording all six and flagging any change is the only honest rule without one. It
is also cheap to live with: re-entering is the same form, prefilled with the old outputs and citation
against the new inputs (**Enter the row again…**).

### 6.2 A code switch invalidates; a revision bump alone does not

The pack id is recorded, so choosing another adopted code makes every entered row stale, naming the
code — §5.4's promise, generalised: a row read from one edition for one person's inputs does not
survive a change of edition unflagged. The pack's **revision** is not recorded and does not by itself
invalidate: the shipped CT pack is at revision 5 and moves when a deck table is added, which says
nothing about a header row. A revision that brings the header table supersedes the entry through §5.2
instead — which is the right effect, reached without a rule about revisions.

### 6.3 New `ChangeKind`s, their sentences and their rank

`Recompute.Diff` compares by record equality, so a live entry that has not changed is `Unchanged`
(its name and date are stable). `Classify` gains, matched before the `_` fallbacks:

| `ChangeKind` | When | Sentence (Window 1) | Rank |
|---|---|---|---|
| `EnteredToOutOfScope` | napkin's table arrived and says beyond | "Header for Window 1 is now beyond Table … under CT 2022's own table: get it engineered. The row entered by hand («(2) 2x10») is superseded; remove it." | with `SizedToOutOfScope`, first |
| `EnteredToNoAnswer` | the row no longer applies | "Header for Window 1: the row entered by hand no longer applies (span 3'-0" → 3'-6"); No data." | right after `SizedToNoAnswer` |
| `EnteredToSized` | napkin's table arrived and sizes it | "Header for Window 1 is now sized by napkin from CT 2022 Table …: (2) 2x10 — the same as the row entered by hand." / "… — instead of the row entered by hand («(2) 2x8»). Remove the entered row." | with `SizedToSized` |
| `EnteredChanged` | the row was edited | "Header for Window 1's entered row changed: «(2) 2x8» → «(2) 2x10» (ENTERED BY HAND …)." | with `SizedToSized` |
| `ToEntered` | a row entered, or applies again | "Header for Window 1 is now entered by hand: «(2) 2x10», «1» jack and «2» king each side (ENTERED BY HAND — Marc, 2026-09-27, …)." | with `NoAnswerToSized` |

`SwitchSummary` counts `EnteredToOutOfScope` among the flagged and `EnteredToNoAnswer` among the lost.
`SameRow` (a resize inside one band is not announced) does not apply to `Entered`: there are no bands;
any moved input is a real change. `BracingChangeKind` gains the same five for slice D.

### 6.4 The stale state on screen

Stale is not a result — a stale row must never show a number — so it lives on `OpeningCheck` the way
`NotChecked` does: `Stale(EnteredHeader Row, ImmutableArray<string> Moved)`, and `Superseded(EnteredHeader
Row)`. `CodeCheck.Words(OpeningCheck, …)` appends the stale or superseded sentence to the No data (or
napkin's) words, and the panel shows **Enter the row again…** / **Remove row** under it. The
message-bar sentence for `EnteredToNoAnswer` reads `Moved` from the *after* check to name what moved.

---

## 7. Where it lives in the file, and why sharing it is safe

### 7.1 Scene format 17

Format **16** is current (`FormatStamp.CurrentVersion = 16`, commit `a7ef1c2`, 2026-09-26, #224 —
checked 2026-09-27; check again before slice A starts, it moves fast). This note bumps it to **17**:
`box.opening` gains `enteredHeader`, required, `null` or the object below. Strict both ways, no
converter (beta policy): a format-16 file is refused with the usual message; samples restamped (case
17), fixtures and literals to 17, version theories to 16 and 18 — the format-16 commit's checklist.

```json
"opening": { "fill": "glass",
             "enteredHeader": null | {
               "plies": 9, "lumber": "2x99", "jackStuds": 9, "kingStuds": 9,
               "citation": { "code": "SYNTHETIC — Test Code 2099", "table": "Table T-99",
                             "location": "p. 99, row 9", "notes": null },
               "enteredBy": "A. Person", "enteredOn": "2026-09-27",
               "for": { "pack": "us-zz-test", "side": "exterior", "supports": "test-roof",
                        "span": 36864,
                        "groundSnowLoad": 99, "ultimateWindSpeed": null, "seismicDesignCategory": null,
                        "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null } } }
```

(The values show the shape and are chosen to look unlike any real row, as the rules-engine note's
examples are; a real-looking row in a document is exactly what this note must not ship.) Refused: a missing field; `plies` below 1; a stud count below
0; empty `code`, `table`, `location`, `enteredBy` or `supports`; an empty `lumber`; a date not
`yyyy-MM-dd`; a `for.pack` that is not a pack id; a `span` of 0 or less; a negative load or length;
`side` not `exterior` or `interior`. The lumber is not checked against the library at load, for the
same reason a part's stock name is not. Every field is written, every time.

The stored record is `EnteredHeader` in `Napkin.Core.Geometry/BuildingInputs.cs` beside `TypedHeader`
and `OpeningFill`; `Box.Opening` becomes `OpeningInputs(OpeningFill Fill, EnteredHeader? EnteredHeader)`.
One request, `SetEnteredHeader(EntityId Box, EnteredHeader? Row)`, following `SetOpeningFill`: set,
edit and remove are the same request with a new record or `null`, each one undo step. Slice D adds
`wall.enteredBracing` and format 18 the same way; nothing is reserved for it now.

### 7.2 Why it is safe to share

Marc chose the shared file over local-only settings. The risk of that choice is that a second person
opens the file and reads a number they did not type. It is safe because:

1. **The label is part of the value.** There is no view of an entered row without the tag, the name,
   the date and the typed citation (§4): the panel, the message bar, the shopping list, every sheet
   (§10), the assistant's context. A reader is never shown "(2) 2x10" without "ENTERED BY HAND — who,
   when, from where; not napkin's data".
2. **The recorded inputs travel with it**, so the reader can check the cited row in their own copy
   against exactly the span, supports and site values it was entered for — the same "find it in the
   paper source without the app" test the rules-engine citation is held to (§2 of that note).
3. **It cannot drift.** Any change to those inputs, or to the adopted code, makes it stale before
   anything is drawn (§6); a real table supersedes it (§5.2). A reader who resizes the window sees No
   data, not the old number.
4. **Local-only would be the silent divergence.** The same drawing would say (2) 2x10 on one machine
   and No data on another, and a permit set printed from the file would not match what its author saw
   — the class of quiet error DESIGN.md §5.4 exists to prevent.
5. **It is not napkin's data in any sense.** One person's transcription of one row for their own
   project, in their own file; not shipped, not aggregated, not offered to anyone else. napkin never
   collects entered rows, never suggests one from another project, and never lets the assistant write
   one (§9). #209's question — what napkin may ship — is untouched.

---

## 8. Whose name

napkin has no "who is working on this" concept: no author in the container manifest, no user name
anywhere in the scene (checked across `src/` 2026-09-27). Rather than invent one for this feature, the
form takes a **free-text name**, required, prefilled the first time with the operating system's user
name (`Environment.UserName`) and after that with the name last used, kept in `UserSettings`
(`EnteredBy`; settings version bumped). Always editable before saving. Say plainly on the form that
the name goes into the project file: the OS user name is a reasonable default, and the person sees it
before it is written.

---

## 9. The assistant (M14)

An entered row is a number napkin has on screen, so it is in the context pack like any other check
(`ContextPack.CheckLines` uses `result.ToString()`, which starts with the tag), the guard finds its
numbers in the pack, and the assistant may discuss it. One rule is added, in one place each:

- `ask.txt` (the system prompt): *"A check marked ENTERED BY HAND is a value a person typed from their
  own copy of the code. Whenever you mention it, say it was entered by hand and that napkin did not
  compute or check it."*
- `ResultKind.Entered` in `HelpSections`, with its `KindOf` arm (today's `_ => NotChecked` would
  compile and quietly call an entered check "not checked"), pointing at a new section of
  `docs/building.md`, *"A header row you entered by hand"*, so the help item the pack carries for such
  a check explains what the label means.
- One eval case in the offline set (#235) once that exists: a question about an entered header, whose
  good answer says "entered by hand" and whose bad answer presents the number as napkin's.

By construction the assistant cannot fill the form: `EditProposal`'s closed set of requests
(llm-assistant §4.5) does not include `SetEnteredHeader`, and llm-assistant §1 already says it never
writes a row. That stays so.

---

## 10. Sheets and lists

Every surface that prints a header result prints the entered row with its full tag line, in the same
type as the result, never dropped or shortened to the number:

- **The shopping list's Framing note** (`CutListWindow.CodeCheckNote` via `CodeCheck.Short`) — slice A.
- **The message bar** — §6.3.
- **The permit set** (#225 the sheet framework, #226 the deck set, #227 the window set with its C1
  code page) and **PDF sheets** (#25), and the **shop set** (#211) where it names a header: all open on
  2026-09-27, none of them designs the rendering here. Each gains one acceptance line when it is
  built: *an entered header prints its ENTERED BY HAND line beside the number, and the C1 code page
  lists entered rows under their own heading, apart from napkin's cited results.* The orchestrator
  records that on those issues when this note is signed off.

---

## 11. Non-goals

- **Not a bulk-import tool, and not a way to load a table.** One row, one opening, typed. Loading a
  table is `docs/rules-engine.md`'s packs folder, and shipping one is #14/#158 through #209.
- **Not an override.** Never offered beside a real `Sized` or `OutOfScope`; superseded by one (§5).
- **No change to #209.** What napkin may ship, and from what source an agent may read the IRC, are
  exactly as before. This note ships no value and reads no code.
- **Not a reading of "beyond the table".** In v1 the person can enter a sized row only; "my copy says
  this span is past the table" stays No data with the existing "get an engineer" wording (Decision 6).
- **No reuse across openings**, no copying a row, no per-project row library (§2.3).
- **No assistant involvement** in creating, editing or suggesting a row (§9).
- **No sample project with an entered row.** Samples are shipped; a real-looking entered citation in
  one would be napkin shipping a code value by the side door. The fixture is a test file with a
  synthetic banner (§13).
- **Deck checks** get no entered row in this note (§3.1).

---

## 12. Implementation slices

Each lands alone on `main` through `tools/scripts/gate.sh`, in order; A is the model and is where the
design's guarantees are tested, B is the person's hands on it, C is one sentence and a mapping, D is
the bracing repeat and waits for Decision 7. Feature ids in a new `features/entered-rows.json` (the
catalog README says add a file): **ENTERED-001…** and **GUI-ENTERED-NN**; milestone **M10 Real code**.
Disjoint files except the usual collision points (`napkin.sln`, `Directory.Build.props`,
`ratchet/baseline.json`, `PlannedFeatures.g.cs`) and `MainWindow.Building.cs` / `MainWindow.axaml`,
which B and D touch in sequence. No slice starts before Marc signs this note off.

| Slice | What | Model | Files | Depends on | Issue |
|---|---|---|---|---|---|
| **A** | Model, format and routing: `EnteredHeader`, `EnteredCitation`, `EnteredHeaderInputs`, `OpeningInputs` in Core.Geometry; `SetEnteredHeader`; scene format **17** strict both ways, samples restamped, version theories; `HeaderResult.Entered`, `EnteredRow` with `Tag`; the closed-union test to five; the engine-never-constructs-it tests; `ChangeKind` ×5, `Classify`, rank; `CodeCheck.For` routing (live / stale / superseded / not bearing), `OpeningCheck.Stale/Superseded`, `Words`, `Short`, `Sentence`, `SwitchSummary`, `Framing`; the synthetic fixture scene; `docs/file-format.md`, `docs/building.md` (the help section), `docs/rules-engine.md` one paragraph | **Opus** — a fifth member through every fall-through in §3.4, a format bump, and the routing rule that is the whole guarantee; a missed default is a thrown cast or a silent mislabel | `Core.Geometry/BuildingInputs.cs`, `Request.cs`; `Core.Project/FormatStamp.cs`, `SceneNames.cs`, `SceneReader.cs`, `SceneWriter.cs`, `SceneBinder.cs`; `Core.RulesEngine/HeaderSizing.cs`, `CodeSelection.cs`; `Modules.Building/CodeCheck.cs`; tests in Core.Project, Core.RulesEngine, Modules.Building; `samples/*.scene.json` restamp; `features/entered-rows.json` | sign-off | #246 |
| **B** | The person's hands: the **I have the code: enter this row…** button under No data; `EnteredHeaderWindow` (the fixed text, the pending-amendment line from `LoadedPack.Pending`, the read-only inputs, the fields, the library lumber picker, the name with its `UserSettings` default and settings version bump, **Use this row** enabled only when complete); **Edit row…**, **Enter the row again…**, **Remove row**; the stale and superseded lines in the Code check block; the Framing note; `GUI-ENTERED-01` (No data → enter → tag shown → resize → stale → undo → live), `GUI-ENTERED-02` (edit, remove, undo each) | Sonnet | `Napkin.App/EnteredHeaderWindow.axaml(.cs)` (new), `MainWindow.Building.cs`, `MainWindow.axaml`, `CutListWindow.axaml.cs`, `Settings/UserSettings.cs`, `SettingsStore.cs`; `tests/Napkin.App.GuiTests/Workflows/EnteredRowWorkflows.cs`; `docs/building.md` | A | #247 |
| **C** | Assistant: `ResultKind.Entered` and the `KindOf` arm (until then `_ => NotChecked` mislabels an entered check) → the help section; the `ask.txt` sentence; a `ContextPackTests` case on the fixture asserting the tag in the line and the kind; `AssistantPromptsTests` asserting the sentence; the eval case if #235 has landed | Sonnet | `Modules.Assistant/HelpSections.cs`, `Prompts/ask.txt`; tests; `tests/…/Eval/*.json` if present | A | #248 |
| **D** | Bracing, if Decision 7: `EnteredBracing` on `WallInputs`, scene format **18**, `BracingResult.Entered`, `BracingChangeKind` ×5, `BracingCheck` routing on `NoData(NoProvisions)`, the form (required and provided lengths, citation, name), `GUI-ENTERED-03` | **Opus** | `Core.Geometry/BuildingInputs.cs`, `Core.Project/*`, `Core.RulesEngine/Bracing.cs`, `CodeSelection.cs`, `Modules.Building/BracingCheck.cs`, `Napkin.App/*`, tests | A, B, Marc's yes | #249 |

**Versioning:** each slice bumps the minor. **Ratchet:** A raises Core.Geometry, Core.Project,
Core.RulesEngine and Modules.Building floors as they truly rise; B raises the workflow count; C the
assistant module's. `Napkin.App` is excluded, so the window's text assembly that can live Avalonia-free
(the read-only inputs line, the stale sentence) lives in `CodeCheck`, per `docs/testing/ratchet.md`.

---

## 13. Test plan

Deterministic throughout. The fixture is `tests/Napkin.Modules.Building.Tests/Fixtures/entered-header.scene.json`
(with the shipped CT pack under `RealPacks`, as `SiteOfferTests` uses it, and the synthetic header-table
pack `fx-base` from the rules-engine fixtures): one exterior bearing wall, one window, site values set,
and an entered row whose citation text begins `SYNTHETIC TEST DATA - NOT CODE VALUES` — the repo's
banner phrase — so nothing in it can be mistaken for, or later treated as, a napkin golden citation.
Its lumber is a library size so the framing test can buy it.

**Slice A** (`Core.Project.Tests`, `Core.RulesEngine.Tests`, `Modules.Building.Tests`):

1. The fixture loads at format 17 and writes back identical; at 16 it is refused; each refused field in
   §7.1 is refused with a message naming it (a theory).
2. Under the CT pack (no header table) the check is `Entered` with the row's member and counts;
   `ToString`, `Short`, `Words.Headline` and `Words.Citation` each contain `EnteredRow.Tag`; `Words.Details`
   lists every recorded input.
3. Each of the ten recorded inputs moved in turn (a theory: span, side, supports, pack, the six site
   values — including not entered → entered) gives `NoData` with `Stale.Moved` naming exactly that
   input.
4. Under `fx-base` (a header table) the same opening is napkin's `Sized` or `OutOfScope`, `Superseded`
   is set, and the result is not `Entered` — the §5.3 guard.
5. A wall marked not bearing: `Result` null, `NotChecked` as today with the one added clause.
6. The engine never returns `Entered`: over every fixture pack and generated requests
   (`RulesEngine.For(pack).SizeHeader`), plus `Recompute.Headers`.
7. The closed union has exactly five members, sorted `Entered, InputMissing, NoData, OutOfScope, Sized`.
8. `Diff`: NoData → Entered is `ToEntered`; Entered → NoData is `EnteredToNoAnswer`; Entered → Sized is
   `EnteredToSized`; Entered → OutOfScope is `EnteredToOutOfScope`; an edited row is `EnteredChanged`;
   an unchanged live row is `Unchanged`; ranks as §6.3; each `Sentence` as written there;
   `SwitchSummary` counts.
9. `Framing`: the entered member and counts feed the frame; the shopping list's Framing note carries
   the tag.
10. Mutation (`tools/scripts/mutate.sh`): dropping `Tag` from `EnteredRow.ToString` fails test 2.

**Slice B** (`Napkin.App.GuiTests`, both keyboard and pointer, ≥ 5 actions each, an assertion after
every state change): `GUI-ENTERED-01` — open the sample, pick CT 2022, say the wall's side and bearing,
select the window, see No data and the button, open the form, fill it (Use this row disabled until the
citation is complete), accept, see the tag in the headline and the citation line, resize the window,
see the stale line, undo, see it live again. `GUI-ENTERED-02` — edit (the new member shown, one undo
step), remove (No data, the button back), undo both.

**Slice C** (`Modules.Assistant.Tests`): the fixture's context line contains the tag and `KindOf` is
`Entered`; the prompt contains the sentence (the `AssistantPromptsTests` pattern); the help section
resolves.

**Slice D** repeats 1–10 for bracing with two typed lengths and a segment-length move.

---

## 14. Risks

1. **The person reads the wrong row.** A misread column, a footnote missed, the state amendment not
   applied. napkin cannot catch it, and says so. Mitigations: the citation and the recorded inputs on
   every surface, so an inspector or a second reader can check the row; the pack's pending amendment
   shown in the form (§2.2); the label in front of the number. This is the risk Marc accepted in
   choosing the feature; the note's job is to keep it visible, not to hide it.
2. **The tag becomes wallpaper.** UNREVIEWED already appears on every deck line; a second uppercase tag
   could dull both. Entered rows are rare and per-opening, and their tag names a person, which reads
   differently. Watch it; the wording is one constant to change.
3. **Tedium.** Every opening its own entry; every site change stales every entry. Expected and honest;
   §2.3's reuse offer is the release valve if it bites.
4. **A fixture or sample that looks real.** The only way this feature ships a code value is by a test
   file or sample carrying a real-looking row. The banner rule (§13) and the no-sample rule (§11) are
   the guard; the reviewer of slice A checks every fixture string.
5. **The fifth member's fall-throughs** (§3.4). One is a cast that throws; the rest mislabel silently.
   The reflection test is updated first, and the slice is red until every site is visited.
6. **Superseded noise when #14 lands.** Every entered row in every project announces itself on open.
   That is the right noise — it is the moment a person should remove their rows — and it is ranked
   after the flagged changes.
7. **A name in a shared file.** The OS user name is written unless edited (§8). It is shown before
   saving; the form says it goes into the file.
8. **Two format bumps** (17 now, 18 for bracing). Cheap under the beta policy; the alternative is
   reserving a field, which the policy forbids.
9. **Scope pressure toward an override.** Someone will ask for the button beside a real `OutOfScope`.
   §5.3 is the answer and test 4 keeps it so.

---

## 15. Decisions for Marc

In plain words, each with the default recommended. **Decided by Marc 2026-09-27:** 1 (already made,
recorded below), 2 (`Entered`), 6 (sized rows only in v1), 7 (bracing as a later slice D, not with
A-C), 9 (OS user name as an editable default). The rest follow the same recommended defaults, not yet
individually asked.

1. **An entered row lives in the shared project file.** Decided 2026-09-27 ("Yes, design it"); recorded
   here with why it is safe (§7.2). Nothing to decide.
2. **The member is named `Entered`, not `Confirmed`.** *Recommended:* `Entered` — "confirmed" reads as
   napkin having confirmed something, and the person did not confirm napkin's answer, they supplied
   one. Alternative: `Confirmed`, matching the words used when the feature was asked for.
3. **The tag reads ENTERED BY HAND**, followed by name, date, the typed citation and "not napkin's data,
   not reviewed by napkin", first in every string form. *Recommended:* yes. Alternative wording:
   USER-ENTERED.
4. **Consulted only on No data with a pack chosen and no table; never an override; superseded by
   napkin's own table when one arrives** (§5). *Recommended:* yes. The alternative — a button beside a
   real result — is the one thing this note refuses.
5. **One row per opening, one-shot; all ten inputs recorded; any change stales it, including a site
   value going from not entered to entered; a code switch stales it; a revision bump alone does not**
   (§6). *Recommended:* yes; "use the same row for Window 2" waits for evidence of tedium.
6. **Sized rows only in v1.** A person whose copy says the span is past the table leaves it No data and
   gets today's "get an engineer". *Recommended:* yes; an entered "beyond the table" is a second form
   and a sixth member for a case the safe default already covers.
7. **Bracing as slice D: two typed lengths compared, the frost line's shape**, after A–C have landed.
   *Recommended:* yes, as a separate later landing; alternative: headers only until #158.
8. **The header lumber comes from the materials library's dimension-lumber list**, as the not-bearing
   wall's typed header does, so the frame can buy it. *Recommended:* yes. Alternative: free text, with
   the "not in the materials library" sentence when it is not found.
9. **The name is free text, defaulting to the OS user name and then to the last name used, kept in
   user settings** (§8). *Recommended:* yes. Alternative: no default, type it every time.
10. **The form shows the pack's pending amendment for the table typed** (`LoadedPack.Pending`, §2.2).
    *Recommended:* yes; it is the pack's own cited text and the most likely thing to be missed.
11. **Milestone M10 Real code**, since it is what lets #14's and #158's answers be real for one person
    at a time before a shipped pack exists. *Recommended:* yes.

---

## 16. As built: slice A (#246), and where it departs from this note

The model, format 17 and the routing landed as §12's slice A describes, tests 1–10 of §13 included
(test 10: `mutate.sh` dropping `Tag` from `EnteredRow.ToString` fails
`EnteredHeaderCheckTests.Where_napkin_has_no_table_and_nothing_moved…`). What differs, and why:

1. **`Box.EnteredHeader` is its own property, not `Box.Opening` as `OpeningInputs(Fill, EnteredHeader)`.**
   §7.1's record has a non-nullable `Fill`, but a window in an existing wall need not say its fill —
   this note's own walkthrough window, in `samples/window-in-existing-wall`, has `"opening": null` —
   so the fill had to stay nullable anyway. And `Box.Opening` is read as the fill in about forty
   places across the App, the PDF sheets, the assistant and editing, none of them in this slice's
   files; a separate property changes none of them. The *file* is exactly §7.1's: `enteredHeader`
   sits inside `opening`, beside a `fill` that may now be `null`; `"opening": null` is the one
   spelling of "neither", and `{ "fill": null, "enteredHeader": null }` is refused. `Box`'s
   hand-written `Equals`/`GetHashCode` carry the new property (tested), so undo and the recompute
   see an edited row.
2. **`OpeningCheck.Stale` carries `ValueList<MovedInput>`, not `ImmutableArray<string>`.** Each moved
   input keeps its name, what it was and what it is now, in words, so the panel says "the header
   span moved from 3'-0" to 3'-6"" and the message bar "(the header span 3'-0" → 3'-6")", as §6.4
   and §6.3 want; `ValueList` keeps `OpeningCheck` comparable by value. Inputs are compared as values,
   never as their words: a span one 1024th wider, which reads the same, is a move (tested).
3. **Two rows §5.1's table did not have.** `InputMissing` for a table's own input — what the wall
   supports, or a site value the table needs — means a table exists, so napkin's answer stands with
   `Superseded`, like `Sized` and `OutOfScope`; only `InputMissing(side|bearing)` and every `NoData`
   give `Stale`. A wall whose bearing is no longer said is a move named `bearing`, though bearing is
   not one of the ten recorded inputs (a row is only ever entered on a bearing wall).
4. **The routing is in `CodeCheck.Check`; `CodeCheck.For` stays the engine's own answer.** Nothing
   else calls `For`; every check the app, the sheets and the assistant use comes through `Check`.
5. **Four consumers §3.4 did not list**, found by grepping `src/` on 2026-09-28; none shows a number
   without the tag, and none is in this slice's files, so each is left for its own slice:
   `PermitItems` (the permit set, #225) files an entered header as *not sized* — honest, napkin did
   not size it — with its tagged words; `WindowSetPdf.HeaderSize` (#227) falls through to
   `CodeCheck.Short`, which carries the tag, but the C1 code page does not yet list entered rows
   under their own heading (§10's acceptance line); `ContextPack.Working` gives an entered check no
   working lines, while its line is `ToString`, tagged (slice C); and `MainWindow.Building`'s framing
   headline says "not yet sized" for an opening framed from an entered row (slice B should say
   "entered by hand").
6. **Feature ids are `ENTR-001…007`, not `ENTERED-…`**: `features/README.md` holds a unit id to two to
   five letters (`^[A-Z]{2,5}-\d{3}$`) and a workflow family to two to six, so slice B's workflows
   are `GUI-ENTR-NN`, not `GUI-ENTERED-NN`.
7. **For slice B (#247): the form must write the wall's Supports with the row, in the same undo
   step.** The recorded `supports` is compared exactly with the wall's live `Supports`; with no
   table, the wall's Supports picker is empty, so the typed text in the form is the only way the wall
   comes to say it. A form that writes only `SetEnteredHeader` leaves a row that is stale the moment
   it is saved ("what the wall supports moved from "…" to not chosen").
8. **The fixture follows the CT pack rather than locking a revision**, so the pack's revision moving
   (it goes to 6 when the DCA 6 guide layer comes out) cannot break it; nothing asserts the CT
   pack's revision. The code-switch and superseded tests use the building tests' own synthetic packs
   — `us-zz-brace-a`/`-b` (no header table) and `us-zz-frame` (ZZ-HEADER) — rather than the rules
   engine's `fx-base`, which that test project does not load.
9. **Smaller things.** `CodeCheck.Input` names `headerSpan` ("the header span") and `pack` ("the
   adopted code"); `CodeCheck.Words(HeaderResult)` keeps its `_ => NoDataWords((NoData)result)`
   fallback, now safe — `Entered` has its own arm, so only `NoData` reaches the cast, and the
   closed-union test holds the union at five (a throwing arm there could never be covered). The stale and superseded
   sentences say `ENTERED BY HAND` too, since the superseded one repeats the row's number (§7.2).
   A removed row says "was removed" rather than "no longer applies". `samples restamp`'s case 17 wrote
   the samples through the real `SceneWriter`, which also put `porch-12x10`'s site fields in the
   writer's own order.
