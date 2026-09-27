using System.Text.Json;
using System.Text.RegularExpressions;

namespace Napkin.Core.RulesEngine;

/// <summary>Reads <c>pack.json</c> (design §1.6) and a base layer's <c>layer.json</c>.</summary>
internal static partial class ManifestReader
{
    /// <summary>
    /// Opens a pack file and checks its <c>schemaVersion</c> before anything else, so an
    /// unsupported file is reported as that and not as a pile of shape errors (design §9.1 step 1).
    /// </summary>
    public static JsonObj? OpenVersioned(IPackSource source, string file, ProblemList problems, out JsonDocument? document)
    {
        document = JsonFile.Parse(source, file, PackLoader.MaxFileBytes, problems);
        if (document is null)
        {
            return null;
        }

        JsonObj? root = JsonObj.Create(document.RootElement, string.Empty, new Where(file), problems);
        if (root is null)
        {
            return null;
        }

        int before = problems.Count;
        int? version = root.Int("schemaVersion");
        if (version is null)
        {
            return problems.Count > before ? null : root;
        }

        if (version != PackLoader.SupportedSchemaVersion)
        {
            problems.Add(
                new Where(file),
                $"unsupported pack schema version {version}; this napkin reads schema version {PackLoader.SupportedSchemaVersion} only. "
                + "There is no converter (beta policy): rewrite the file in the current format.");
            return null;
        }

        root.MarkUsed("notes");
        if (root.Has("notes"))
        {
            root.String("notes");
        }

        return root;
    }

    public static PackManifest? Read(IPackSource source, string file, ProblemList problems)
    {
        JsonObj? root = OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            int before = problems.Count;
            Where where = new(file);

            string? id = root.String("id");
            if (id is not null && !PackLoader.PackIdPattern().IsMatch(id))
            {
                problems.Add(where, $"id: '{id}' is not a pack id (lower case: country-state-designation).");
            }

            int? revision = root.Int("revision", min: 1);

            Jurisdiction? jurisdiction = null;
            JsonObj? j = root.Obj("jurisdiction");
            if (j is not null)
            {
                string? country = j.String("country");
                if (country is not null && country != "US")
                {
                    problems.Add(where, $"jurisdiction.country: '{country}' is not supported; napkin encodes US codes only.");
                }

                string? state = j.String("state");
                if (state is not null && !StatePattern().IsMatch(state))
                {
                    problems.Add(where, $"jurisdiction.state: '{state}' must be two capital letters.");
                }

                string? municipality = j.String("municipality", nullable: true);
                j.Done();
                jurisdiction = country is null || state is null ? null : new Jurisdiction(country, state, municipality);
            }

            Adoption? adoption = null;
            JsonObj? a = root.Obj("adoption");
            if (a is not null)
            {
                string? name = a.String("name");
                string? shortName = a.String("shortName");
                string? adoptedBy = a.String("adoptedBy");
                DateOnly? from = null;
                DateOnly? to = null;
                JsonObj? inForce = a.Obj("inForce");
                if (inForce is not null)
                {
                    from = inForce.Date("from");
                    to = inForce.Date("to", nullable: true);
                    inForce.Done();
                    if (from is not null && to is not null && to < from)
                    {
                        problems.Add(where, "adoption.inForce: 'to' is before 'from'.");
                    }
                }

                AppliesTo? appliesTo = a.Enum("appliesTo", Vocabulary.AppliesToNames);
                string? transitionNotes = a.String("transitionNotes", nullable: true);
                if (appliesTo == AppliesTo.SeeNotes && transitionNotes is null)
                {
                    problems.Add(where, "adoption.transitionNotes: required when appliesTo is 'see-notes'.");
                }

                a.Done();
                if (name is not null && shortName is not null && adoptedBy is not null && from is not null && appliesTo is not null)
                {
                    adoption = new Adoption(name, shortName, adoptedBy, from.Value, to, appliesTo.Value, transitionNotes);
                }
            }

            BaseCode? baseCode = ReadBaseCode(root, "baseCode", problems);

            List<string> layers = [];
            IReadOnlyList<JsonElement>? layerItems = root.Array("layers", minItems: 1);
            if (layerItems is not null)
            {
                for (int i = 0; i < layerItems.Count; i++)
                {
                    string? layer = JsonObj.ReadString(layerItems[i], $"layers[{i}]", where, problems);
                    if (layer is not null)
                    {
                        layers.Add(layer);
                    }
                }
            }

            List<SourceDocument> sources = ReadSources(root, where, problems);
            List<GuideEntry> guides = ReadGuides(root, where, problems);

            PackReview? review = null;
            JsonObj? r = root.Obj("review");
            if (r is not null)
            {
                ReviewStatus? status = r.Enum("status", Vocabulary.ReviewNames);
                string? checklist = r.String("checklist", nullable: true);
                r.Done();
                if (status == ReviewStatus.SignedOff && checklist is null)
                {
                    problems.Add(where, "review.checklist: a signed-off pack must name its review checklist (design §8.3).");
                }

                if (checklist is not null && source.FileLength(checklist) is null)
                {
                    problems.Add(where, $"review.checklist: '{checklist}' does not exist under the packs root.");
                }

                review = status is null ? null : new PackReview(status.Value, checklist);
            }

            root.Done();
            if (problems.Count > before || id is null || revision is null || jurisdiction is null || adoption is null
                || baseCode is null || review is null || layers.Count == 0)
            {
                return null;
            }

            return new PackManifest(id, revision.Value, jurisdiction, adoption, baseCode, layers.ToValueList(), sources.ToValueList(), review)
            {
                Guides = guides.ToValueList(),
            };
        }
    }

    /// <summary>
    /// A pack's <c>guides</c> (deck-guide-pack §1.2): the guide layers it declares for its deck tables,
    /// each at most once. Optional; a pack without it has no guide.
    /// </summary>
    private static List<GuideEntry> ReadGuides(JsonObj root, Where where, ProblemList problems)
    {
        List<GuideEntry> guides = [];
        IReadOnlyList<JsonElement>? items = root.Array("guides", required: false);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            JsonObj? g = JsonObj.Create(items[i], $"guides[{i}]", where, problems);
            if (g is null)
            {
                continue;
            }

            string? id = g.String("id");
            string? notes = g.Has("notes") ? g.String("notes") : null;
            g.MarkUsed("notes");
            g.Done();
            if (id is not null && !LayerIdPattern().IsMatch(id))
            {
                problems.Add(where, $"guides[{i}].id: '{id}' is not a layer id (lower case letters, digits, '.', '-').");
            }
            else if (id is not null && guides.Any(guide => guide.Id == id))
            {
                problems.Add(where, $"guides[{i}].id: guide '{id}' is listed twice.");
            }
            else if (id is not null)
            {
                guides.Add(new GuideEntry(id, notes));
            }
        }

        return guides;
    }

    public static LayerManifest? ReadLayer(IPackSource source, string file, ProblemList problems)
    {
        JsonObj? root = OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            int before = problems.Count;
            Where where = new(file);
            if (root.Has("kind"))
            {
                // A base layer has no kind; a guide layer's says so, and a guide is never a base layer.
                string kind = root.String("kind") ?? string.Empty;
                problems.Add(
                    where,
                    kind == GuideReader.Kind
                        ? "kind: 'guide': a guide layer is never a pack's base layer; list it under the pack's 'guides', not in 'layers' (deck-guide-pack §1.2)."
                        : $"kind: '{kind}' is not a layer kind; a base layer has none and a guide layer says '{GuideReader.Kind}'.");
            }

            string? id = root.String("id");
            BaseCode? code = ReadBaseCode(root, "baseCode", problems);
            List<SourceDocument> sources = ReadSources(root, where, problems);
            root.Done();
            return problems.Count > before || id is null || code is null
                ? null
                : new LayerManifest(id, code, sources.ToValueList());
        }
    }

    /// <summary>A model code: a pack's or base layer's <c>baseCode</c>, or a guide's <c>basis</c>.</summary>
    public static BaseCode? ReadBaseCode(JsonObj parent, string name, ProblemList problems)
    {
        JsonObj? b = parent.Obj(name);
        if (b is null)
        {
            return null;
        }

        string? publisher = b.String("publisher");
        string? code = b.String("code");
        int? year = b.Int("year", min: 1);
        b.Done();
        if (publisher is not null && publisher != "ICC")
        {
            problems.Add(b.Where, $"{b.Child("publisher")}: '{publisher}' is not supported; only 'ICC' model codes are encoded.");
        }

        if (code is not null && code != "IRC")
        {
            problems.Add(b.Where, $"{b.Child("code")}: '{code}' is not supported; only 'IRC' is encoded.");
        }

        return publisher is null || code is null || year is null ? null : new BaseCode(publisher, code, year.Value);
    }

    public static List<SourceDocument> ReadSources(JsonObj root, Where where, ProblemList problems)
    {
        List<SourceDocument> sources = [];
        IReadOnlyList<JsonElement>? items = root.Array("sources", minItems: 1);
        if (items is null)
        {
            return sources;
        }

        for (int i = 0; i < items.Count; i++)
        {
            JsonObj? s = JsonObj.Create(items[i], $"sources[{i}]", where, problems);
            if (s is null)
            {
                continue;
            }

            string? id = s.String("id");
            string? title = s.String("title");
            string? publisher = s.String("publisher");
            string? url = s.String("url");
            string? printing = s.String("printing");
            DateOnly? retrieved = s.Date("retrievedOn");
            string? sha = s.String("sha256");
            s.Done();
            if (sha is not null && !ShaPattern().IsMatch(sha))
            {
                problems.Add(where, $"sources[{i}].sha256: must be 64 lower-case hex digits, the hash of the document as retrieved.");
                continue;
            }

            if (id is null || title is null || publisher is null || url is null || printing is null || retrieved is null || sha is null)
            {
                continue;
            }

            if (sources.Any(x => x.Id == id))
            {
                problems.Add(where, $"sources[{i}].id: '{id}' is listed twice.");
                continue;
            }

            sources.Add(new SourceDocument(id, title, publisher, url, printing, retrieved.Value, sha));
        }

        return sources;
    }

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex StatePattern();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex ShaPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*$")]
    private static partial Regex LayerIdPattern();
}
