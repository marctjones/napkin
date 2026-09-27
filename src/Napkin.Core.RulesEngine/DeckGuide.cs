using System.Text.Json;

namespace Napkin.Core.RulesEngine;

/// <summary>How a scope limit compares its input (docs/design/deck-guide-pack.md §2).</summary>
public enum ScopeForm
{
    /// <summary>The input is strictly greater than a number or a length.</summary>
    Above,

    /// <summary>The input is one category.</summary>
    EqualTo,

    /// <summary>The input is one of the listed categories.</summary>
    In,

    /// <summary>The input is none of the listed categories.</summary>
    NotIn,

    /// <summary>The input, a length, is strictly greater than another length input.</summary>
    AboveInput,
}

/// <summary>A scope limit's condition: the input it tests and how.</summary>
/// <param name="Input">The input: supports, species, member, groundSnowLoad, deckLength or deckWidth.</param>
/// <param name="Form">How it compares.</param>
/// <param name="Value">For <see cref="ScopeForm.Above"/>, the bound; otherwise null.</param>
/// <param name="Values">For <see cref="ScopeForm.EqualTo"/> (one), <see cref="ScopeForm.In"/> and <see cref="ScopeForm.NotIn"/>, the categories; otherwise empty.</param>
/// <param name="OtherInput">For <see cref="ScopeForm.AboveInput"/>, the length it is compared with; otherwise null.</param>
public sealed record ScopeCondition(string Input, ScopeForm Form, CellValue? Value, ValueList<string> Values, string? OtherInput);

/// <summary>
/// A cited limit on what a guide (or one deck table) covers: before every lookup the limits are tried
/// in order, and the first that holds makes the lookup Out of scope, citing it (deck-guide-pack §2).
/// </summary>
/// <param name="Id">Its id, unique in the guide and the table.</param>
/// <param name="When">When it holds.</param>
/// <param name="Text">What the source says, verbatim.</param>
/// <param name="Location">Where it is printed.</param>
public sealed record ScopeLimit(string Id, ScopeCondition When, string Text, string Location);

/// <summary>A scope statement the guide makes that napkin's deck meets by construction: shown once, not checked.</summary>
public sealed record ScopeNote(string Id, string Text, string Location);

/// <summary>One of a guide's own statements about what it is, verbatim, with where it is printed.</summary>
public sealed record GuideCaveat(string Id, string Text, string Location);

/// <summary>
/// A species group as a table prints it (deck-guide-pack §3.6): the table's rows carry the group, and a
/// typed species is mapped to its group before the lookup.
/// </summary>
/// <param name="Group">The group's name as printed; the table's species column values are these names.</param>
/// <param name="Species">The species it places.</param>
/// <param name="Location">Where the grouping is printed.</param>
public sealed record SpeciesGroup(string Group, ValueList<string> Species, string Location);

/// <summary>A pack's <c>guides</c> entry: the guide layer's id and the pack author's note on it.</summary>
public sealed record GuideEntry(string Id, string? Notes);

/// <summary>
/// A guide layer (deck-guide-pack §1.2): a document that is not the adopted code — a guide based on a
/// model code, which the pack declares for its deck tables. Its identity is never the pack's; every
/// answer it gives names it and says what it is not.
/// </summary>
/// <param name="Id">The layer's id (<c>layers/&lt;id&gt;</c>).</param>
/// <param name="ShortName">What a citation calls it ("DCA 6-2015").</param>
/// <param name="Title">Its title as printed.</param>
/// <param name="Publisher">Who publishes it.</param>
/// <param name="Basis">The model code it is based on.</param>
/// <param name="Caveats">What it says about itself, verbatim; never empty.</param>
/// <param name="Limits">Its scope limits, checked before every lookup in its tables.</param>
/// <param name="Notes">Its scope notes, shown once.</param>
/// <param name="Species">The species it covers, as printed.</param>
/// <param name="Sources">The documents its tables were read from.</param>
public sealed record DeckGuide(
    string Id,
    string ShortName,
    string Title,
    string Publisher,
    BaseCode Basis,
    ValueList<GuideCaveat> Caveats,
    ValueList<ScopeLimit> Limits,
    ValueList<ScopeNote> Notes,
    ValueList<string> Species,
    ValueList<SourceDocument> Sources)
{
    /// <summary>
    /// The caveat id a guide gives its own statement that the model code governs where the two differ.
    /// With one, every line's clause adds "; the IRC governs where they differ", citing it.
    /// </summary>
    public const string GovernsCaveat = "irc-governs";

    /// <summary>"2015 IRC": the model code the guide is based on, in the order its cover prints it.</summary>
    public string BasisText => $"{Basis.Year} {Basis.Code}";

    /// <summary>
    /// The clause every deck line from this guide carries (deck-guide-pack §1.2, Decision 5): napkin's own
    /// sentence about where its data comes from, composed from the manifests, never a code's text —
    /// "a guide on the 2015 IRC, not CT 2022's adopted IRC 2021; the IRC governs where they differ (p. 1)".
    /// </summary>
    public string Clause(AdoptedCodeRef code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string governs = Caveats.FirstOrDefault(caveat => caveat.Id == GovernsCaveat) is { } caveat
            ? $"; the {Basis.Code} governs where they differ ({caveat.Location})"
            : string.Empty;
        return $"a guide on the {BasisText}, not {code.ShortName}'s adopted {code.BaseCode}{governs}";
    }

    /// <summary>
    /// The paragraph shown once at the top of a deck's code check: what the guide is and is not, its
    /// caveats verbatim, and its scope limits and notes verbatim, each with where it is printed.
    /// </summary>
    public string Paragraph(AdoptedCodeRef code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string said = string.Join("; ", Caveats.Select(caveat => $"\"{caveat.Text}\" ({caveat.Location})"));
        string scope = string.Join(
            "; ",
            Limits.Select(limit => $"\"{limit.Text}\" ({limit.Location})").Concat(Notes.Select(note => $"\"{note.Text}\" ({note.Location})")));
        return $"Deck checks under {code.ShortName} use {ShortName} ({Title}, {Publisher}), a guide based on the {BasisText}, "
               + $"not {code.ShortName}'s adopted code ({code.BaseCode}). It says: {said}. Its scope: {scope}.";
    }
}

/// <summary>Reads scope limits and notes (deck-guide-pack §2), for a guide's <c>scope</c> and a deck table's <c>limits</c>.</summary>
internal static class ScopeReader
{
    /// <summary>The inputs a scope limit may test, with the one type each has.</summary>
    public static readonly IReadOnlyDictionary<string, ColumnType> Inputs = new Dictionary<string, ColumnType>(StringComparer.Ordinal)
    {
        ["supports"] = ColumnType.Enum,
        ["species"] = ColumnType.Enum,
        ["member"] = ColumnType.Enum,
        ["groundSnowLoad"] = ColumnType.Psf,
        ["deckLength"] = ColumnType.Length,
        ["deckWidth"] = ColumnType.Length,
    };

    static readonly string[] Forms = ["above", "equals", "in", "notIn", "aboveInput"];

    /// <summary>Reads a list of limits; <paramref name="ids"/> collects every id seen, and a repeat is a problem.</summary>
    public static List<ScopeLimit> ReadLimits(JsonObj parent, string name, bool required, HashSet<string> ids, ProblemList problems)
    {
        List<ScopeLimit> limits = [];
        IReadOnlyList<JsonElement>? items = parent.Array(name, required: required);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = parent.Child($"{name}[{i}]");
            JsonObj? o = JsonObj.Create(items[i], path, parent.Where, problems);
            if (o is null)
            {
                continue;
            }

            int before = problems.Count;
            string? id = o.String("id");
            string? text = o.String("text");
            string? location = o.String("location");
            ScopeCondition? when = o.Obj("when") is { } w ? ReadCondition(w, problems) : null;
            o.Done();
            if (id is not null && !ids.Add(id))
            {
                problems.Add(o.Where, $"{path}.id: '{id}' is used twice; every limit and note has its own id.");
            }

            if (problems.Count == before && id is not null && text is not null && location is not null && when is not null)
            {
                limits.Add(new ScopeLimit(id, when, text, location));
            }
        }

        return limits;
    }

    /// <summary>Reads a list of scope notes, each cited.</summary>
    public static List<ScopeNote> ReadNotes(JsonObj parent, string name, HashSet<string> ids, ProblemList problems)
    {
        List<ScopeNote> notes = [];
        IReadOnlyList<JsonElement>? items = parent.Array(name);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = parent.Child($"{name}[{i}]");
            JsonObj? o = JsonObj.Create(items[i], path, parent.Where, problems);
            if (o is null)
            {
                continue;
            }

            int before = problems.Count;
            string? id = o.String("id");
            string? text = o.String("text");
            string? location = o.String("location");
            o.Done();
            if (id is not null && !ids.Add(id))
            {
                problems.Add(o.Where, $"{path}.id: '{id}' is used twice; every limit and note has its own id.");
            }

            if (problems.Count == before && id is not null && text is not null && location is not null)
            {
                notes.Add(new ScopeNote(id, text, location));
            }
        }

        return notes;
    }

    static ScopeCondition? ReadCondition(JsonObj w, ProblemList problems)
    {
        int before = problems.Count;
        string? input = w.String("input");
        List<string> present = [.. Forms.Where(w.Has)];
        foreach (string each in Forms)
        {
            w.MarkUsed(each);
        }

        ColumnType? type = input is not null && Inputs.TryGetValue(input, out ColumnType known) ? known : null;
        if (input is not null && type is null)
        {
            problems.Add(w.Where, $"{w.Child("input")}: '{input}' is not an input a scope limit can test; they are: {string.Join(", ", Inputs.Keys.Order(StringComparer.Ordinal))}.");
        }

        if (present.Count != 1)
        {
            problems.Add(w.Where, $"{w.Path}: exactly one of {string.Join(", ", Forms)}.");
            w.Done();
            return null;
        }

        string form = present[0];
        ScopeCondition? condition = null;
        if (type is { } t)
        {
            condition = form switch
            {
                "above" => Above(w, input!, t, problems),
                "aboveInput" => AboveInput(w, input!, t, problems),
                _ => Categories(w, input!, t, form, problems),
            };
        }

        w.Done();
        return problems.Count > before ? null : condition;
    }

    static ScopeCondition? Above(JsonObj w, string input, ColumnType type, ProblemList problems)
    {
        if (type == ColumnType.Enum)
        {
            problems.Add(w.Where, $"{w.Child("above")}: '{input}' is a category, not a number or a length; test it with equals, in or notIn.");
            return null;
        }

        return TableReader.ReadCell(w.Get("above"), type, w.Child("above"), w.Where, problems) is { } value
            ? new ScopeCondition(input, ScopeForm.Above, value, ValueList<string>.Empty, null)
            : null;
    }

    static ScopeCondition? AboveInput(JsonObj w, string input, ColumnType type, ProblemList problems)
    {
        string? other = w.String("aboveInput");
        if (type != ColumnType.Length)
        {
            problems.Add(w.Where, $"{w.Child("aboveInput")}: compares two lengths, and '{input}' is not a length.");
            return null;
        }

        if (other is null)
        {
            return null;
        }

        if (other == input || Inputs.GetValueOrDefault(other, ColumnType.Enum) != ColumnType.Length)
        {
            problems.Add(w.Where, $"{w.Child("aboveInput")}: '{other}' is not another length input; they are: deckLength, deckWidth.");
            return null;
        }

        return new ScopeCondition(input, ScopeForm.AboveInput, null, ValueList<string>.Empty, other);
    }

    static ScopeCondition? Categories(JsonObj w, string input, ColumnType type, string form, ProblemList problems)
    {
        if (type != ColumnType.Enum)
        {
            problems.Add(w.Where, $"{w.Child(form)}: '{input}' is a {Vocabulary.TypeName(type)}, not a category; test it with above.");
            return null;
        }

        List<string> values = [];
        if (form == "equals")
        {
            if (w.String("equals") is { } value)
            {
                values.Add(value);
            }
        }
        else
        {
            IReadOnlyList<JsonElement>? items = w.Array(form, minItems: 1);
            for (int i = 0; items is not null && i < items.Count; i++)
            {
                string? value = JsonObj.ReadString(items[i], w.Child($"{form}[{i}]"), w.Where, problems);
                if (value is not null && values.Contains(value, StringComparer.Ordinal))
                {
                    problems.Add(w.Where, $"{w.Child($"{form}[{i}]")}: '{value}' is listed twice.");
                }
                else if (value is not null)
                {
                    values.Add(value);
                }
            }
        }

        ScopeForm kind = form switch
        {
            "equals" => ScopeForm.EqualTo,
            "in" => ScopeForm.In,
            _ => ScopeForm.NotIn,
        };
        return values.Count == 0 ? null : new ScopeCondition(input, kind, null, values.ToValueList(), null);
    }
}

/// <summary>
/// Reads a guide layer's <c>layer.json</c> (deck-guide-pack §1.2): <c>kind: "guide"</c>, the guide's
/// identity and basis, its caveats, scope and species, and its sources — strict like every pack file.
/// </summary>
internal static class GuideReader
{
    public const string Kind = "guide";

    public static DeckGuide? Read(IPackSource source, string id, ProblemList problems)
    {
        string file = $"layers/{id}/layer.json";
        JsonObj? root = ManifestReader.OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            int before = problems.Count;
            Where where = new(file);
            string? kind = root.String("kind");
            if (kind is not null && kind != Kind)
            {
                problems.Add(where, $"kind: '{kind}': a pack's guides name guide layers, whose layer.json says kind '{Kind}'.");
                foreach (string name in root.Names.ToList())
                {
                    root.MarkUsed(name);
                }

                return null;
            }

            string? layerId = root.String("id");
            if (layerId is not null && layerId != id)
            {
                problems.Add(where, $"id '{layerId}' does not match its directory 'layers/{id}'.");
            }

            string? shortName = null, title = null, publisher = null;
            BaseCode? basis = null;
            if (root.Obj("guide") is { } g)
            {
                shortName = g.String("shortName");
                title = g.String("title");
                publisher = g.String("publisher");
                basis = ManifestReader.ReadBaseCode(g, "basis", problems);
                g.Done();
            }

            List<GuideCaveat> caveats = ReadCaveats(root, problems);
            List<ScopeLimit> limits = [];
            List<ScopeNote> notes = [];
            if (root.Obj("scope") is { } s)
            {
                HashSet<string> ids = new(StringComparer.Ordinal);
                limits = ScopeReader.ReadLimits(s, "limits", required: true, ids, problems);
                notes = ScopeReader.ReadNotes(s, "notes", ids, problems);
                if (s.Has("limits") && problems.Count == before && !limits.Any(limit => limit.When.Input == "supports"))
                {
                    problems.Add(where, "scope.limits: a guide's scope has a limit on 'supports', so a deck carrying anything but its own loads always has an answer (deck-guide-pack §2).");
                }

                s.Done();
            }

            List<string> species = ReadSpecies(root, problems);
            List<SourceDocument> sources = ManifestReader.ReadSources(root, where, problems);
            root.Done();
            return problems.Count > before || shortName is null || title is null || publisher is null || basis is null
                ? null
                : new DeckGuide(id, shortName, title, publisher, basis, caveats.ToValueList(), limits.ToValueList(), notes.ToValueList(), species.ToValueList(), sources.ToValueList());
        }
    }

    static List<GuideCaveat> ReadCaveats(JsonObj root, ProblemList problems)
    {
        List<GuideCaveat> caveats = [];
        IReadOnlyList<JsonElement>? items = root.Array("caveats", minItems: 1);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            JsonObj? c = JsonObj.Create(items[i], $"caveats[{i}]", root.Where, problems);
            if (c is null)
            {
                continue;
            }

            string? id = c.String("id");
            string? text = c.String("text");
            string? location = c.String("location");
            c.Done();
            if (id is not null && caveats.Any(caveat => caveat.Id == id))
            {
                problems.Add(c.Where, $"caveats[{i}].id: '{id}' is used twice.");
            }
            else if (id is not null && text is not null && location is not null)
            {
                caveats.Add(new GuideCaveat(id, text, location));
            }
        }

        return caveats;
    }

    static List<string> ReadSpecies(JsonObj root, ProblemList problems)
    {
        List<string> species = [];
        IReadOnlyList<JsonElement>? items = root.Array("species", minItems: 1);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string? name = JsonObj.ReadString(items[i], $"species[{i}]", root.Where, problems);
            if (name is not null && species.Contains(name, StringComparer.Ordinal))
            {
                problems.Add(root.Where, $"species[{i}]: '{name}' is listed twice.");
            }
            else if (name is not null)
            {
                species.Add(name);
            }
        }

        return species;
    }
}
