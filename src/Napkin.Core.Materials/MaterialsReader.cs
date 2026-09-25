using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

using Napkin.Core.Geometry;

namespace Napkin.Core.Materials;

/// <summary>
/// Reads the reference tables out of their data files. The one door into the library
/// (<c>MAT-005</c>): the shipped tables come through it and so does anything a person adds.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Strict, and it never repairs.</strong> An unknown field, a field written twice, a
/// missing or empty citation, a dimension that is not an exact tape-measure fraction, and two
/// entries whose names mean the same item are each a refusal naming the file and the entry. A
/// reference table is something a person cuts lumber against; half of one is worse than none.
/// </para>
/// <para>
/// <strong>Dimensions are written the way the standard prints them</strong> — <c>"1-1/2"</c>,
/// <c>"23/32"</c>, <c>"7-1/4"</c> — and parsed with <see cref="Length.TryParse"/>. This differs on
/// purpose from the scene format, where a length is an integer count of units
/// (<c>docs/file-format.md</c> rule 1): a scene file is written by the app and read by the app,
/// while these files are transcribed by hand from a printed table and checked by a reviewer
/// against that table. Nobody can check <c>1536</c> against a cell that says <c>1-1/2</c>. The
/// strictness is kept instead by refusing any text that does not land exactly on the 1/1024 inch
/// grid, so a value that would have to be rounded to be stored fails the load rather than being
/// quietly approximated.
/// </para>
/// </remarks>
public static class MaterialsReader
{
    /// <summary>The <c>tableVersion</c> this build reads, and no other.</summary>
    /// <remarks>
    /// Bumped whenever a data file changes meaning. napkin is a pre-1.0 beta: there is no
    /// migration code and no compatibility shim, so an old file is refused, not converted
    /// (DESIGN.md §12).
    /// </remarks>
    public const int TableVersion = 1;

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32,
    };

    /// <summary>Reads one table file's bytes.</summary>
    /// <param name="fileName">What to call the file in a refusal.</param>
    /// <param name="content">The bytes.</param>
    public static MaterialsLoadResult Read(string fileName, Stream content)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(content);

        return ReadAll([new NamedSource(fileName, () => content)]);
    }

    /// <summary>Reads one table file from disk.</summary>
    /// <param name="path">The file to read.</param>
    public static MaterialsLoadResult ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return ReadAll([new NamedSource(Path.GetFileName(path), () => File.OpenRead(path))]);
    }

    /// <summary>
    /// Reads every <c>*.json</c> in a directory as one library, in file-name order so that a
    /// refusal reads the same way twice.
    /// </summary>
    /// <param name="directory">The directory to read.</param>
    public static MaterialsLoadResult ReadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        string[] paths;
        try
        {
            paths = Directory.GetFiles(directory, "*.json");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Refuse(MaterialsProblemKind.Unreadable, directory, string.Empty, exception.Message);
        }

        Array.Sort(paths, StringComparer.Ordinal);
        return ReadAll(paths.Select(path => new NamedSource(Path.GetFileName(path), () => File.OpenRead(path))).ToList());
    }

    internal static MaterialsLoadResult ReadAll(IReadOnlyList<NamedSource> sources)
    {
        List<MaterialsProblem> problems = [];
        List<StockTable> tables = [];
        List<SpacingTable> spacingTables = [];
        Dictionary<string, string> tableIds = new(StringComparer.Ordinal);
        Dictionary<string, string> keys = new(StringComparer.Ordinal);

        foreach (NamedSource source in sources)
        {
            TableReader reader = new(source.Name, problems);
            object? table = reader.Read(source.Open);
            if (table is null)
            {
                continue;
            }

            (string id, IEnumerable<(string Name, string Key)> rows) = table switch
            {
                StockTable stock => (stock.Id, stock.Items.Select(item => (item.Name, item.Key))),
                SpacingTable spacings => (spacings.Id, spacings.Spacings.Select(row => (row.Name, row.Key))),
                _ => throw new InvalidOperationException($"Unknown table kind {table.GetType().Name}."),
            };

            if (!tableIds.TryAdd(id, source.Name))
            {
                problems.Add(new MaterialsProblem(
                    MaterialsProblemKind.DuplicateTable,
                    source.Name,
                    "/id",
                    $"The table id \"{id}\" is already used by {tableIds[id]}."));
                continue;
            }

            foreach ((string name, string key) in rows)
            {
                if (!keys.TryAdd(key, source.Name))
                {
                    problems.Add(new MaterialsProblem(
                        MaterialsProblemKind.DuplicateEntry,
                        source.Name,
                        $"/entries/{name}",
                        $"\"{name}\" means the same thing as an entry already read from {keys[key]} "
                        + $"(both normalise to \"{key}\")."));
                }
            }

            if (table is StockTable stockTable)
            {
                tables.Add(stockTable);
            }
            else
            {
                spacingTables.Add((SpacingTable)table);
            }
        }

        if (problems.Count > 0)
        {
            return new MaterialsRefused([.. problems]);
        }

        if (tables.Count == 0 && spacingTables.Count == 0)
        {
            return Refuse(
                MaterialsProblemKind.Empty,
                sources.Count == 1 ? sources[0].Name : "(no files)",
                string.Empty,
                "There is nothing to read: the library holds no tables.");
        }

        return new MaterialsLoaded(new MaterialsLibrary([.. tables], [.. spacingTables]));
    }

    private static MaterialsRefused Refuse(MaterialsProblemKind kind, string file, string location, string message)
        => new([new MaterialsProblem(kind, file, location, message)]);

    /// <summary>A file the reader has not opened yet, so that a failure to open is a refusal.</summary>
    internal sealed record NamedSource(string Name, Func<Stream> Open);

    /// <summary>
    /// Reads one file. Every problem is collected rather than thrown, so a person fixing a data
    /// file sees everything wrong with it at once.
    /// </summary>
    private sealed class TableReader(string file, List<MaterialsProblem> problems)
    {
        private readonly int _problemsBefore = problems.Count;

        private bool Failed => problems.Count > _problemsBefore;

        public object? Read(Func<Stream> open)
        {
            JsonDocument document;
            try
            {
                using Stream stream = open();
                document = JsonDocument.Parse(stream, ParseOptions);
            }
            catch (JsonException exception)
            {
                Add(MaterialsProblemKind.Malformed, string.Empty, exception.Message);
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                Add(MaterialsProblemKind.Unreadable, string.Empty, exception.Message);
                return null;
            }

            using (document)
            {
                return ReadTable(document.RootElement);
            }
        }

        private object? ReadTable(JsonElement root)
        {
            JsonFields? fields = ReadFields(root, string.Empty, "the table");
            if (fields is null)
            {
                return null;
            }

            int version = TakeInt(fields, "tableVersion") ?? 0;
            if (Failed)
            {
                return null;
            }

            if (version != TableVersion)
            {
                Add(
                    MaterialsProblemKind.UnsupportedTableVersion,
                    "/tableVersion",
                    $"This build reads tableVersion {TableVersion}, and this file says {version}. "
                    + "napkin is a pre-1.0 beta: a file of another version is refused, never converted.");
                return null;
            }

            string kind = TakeText(fields, "kind") ?? string.Empty;
            if (Failed)
            {
                return null;
            }

            return kind switch
            {
                "stock" => ReadStockTable(fields),
                "spacings" => ReadSpacingTable(fields),
                _ => UnknownKind(kind),
            };
        }

        private object? UnknownKind(string kind)
        {
            Add(
                MaterialsProblemKind.UnknownValue,
                "/kind",
                $"\"{kind}\" is not a kind of table this build reads. They are: \"stock\", \"spacings\".");
            return null;
        }

        private SpacingTable? ReadSpacingTable(JsonFields fields)
        {
            string id = TakeText(fields, "id") ?? string.Empty;
            string title = TakeText(fields, "title") ?? string.Empty;
            Citation? source = TakeCitation(fields, "citation");

            ImmutableArray<SupportSpacing> spacings = TakeSpacings(fields, source);

            RejectUnknownFields(fields);

            return Failed || source is null ? null : new SpacingTable(id, title, source, file, spacings);
        }

        private ImmutableArray<SupportSpacing> TakeSpacings(JsonFields fields, Citation? tableSource)
        {
            JsonElement? element = fields.Take("spacings");
            if (element is not { } spacings)
            {
                Add(MaterialsProblemKind.MissingField, "/spacings", "The table has no \"spacings\".");
                return [];
            }

            if (spacings.ValueKind != JsonValueKind.Array)
            {
                Add(MaterialsProblemKind.Malformed, "/spacings", $"Expected \"spacings\" to be an array, and found {Describe(spacings)}.");
                return [];
            }

            ImmutableArray<SupportSpacing>.Builder builder = ImmutableArray.CreateBuilder<SupportSpacing>();
            HashSet<string> keysInFile = new(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement each in spacings.EnumerateArray())
            {
                string path = $"/spacings/{index}";
                index++;

                JsonFields? row = ReadFields(each, path, "a spacing");
                if (row is null)
                {
                    continue;
                }

                string name = TakeText(row, "name") ?? string.Empty;
                string key = NominalName.Normalize(name);
                string endUse = TakeText(row, "endUse") ?? string.Empty;
                string derivation = TakeText(row, "derivation") ?? string.Empty;
                Length spacing = TakeLength(row, path, "spacing");

                RejectUnknownFields(row);

                if (Failed || tableSource is null)
                {
                    continue;
                }

                if (!keysInFile.Add(key))
                {
                    Add(
                        MaterialsProblemKind.DuplicateEntry,
                        $"{path}/name",
                        $"\"{name}\" means the same spacing as an earlier entry in this file.");
                    continue;
                }

                builder.Add(new SupportSpacing
                {
                    Name = name,
                    Key = key,
                    EndUse = endUse,
                    Spacing = spacing,
                    Source = tableSource,
                    Derivation = derivation,
                });
            }

            if (builder.Count == 0 && !Failed)
            {
                Add(MaterialsProblemKind.Empty, "/spacings", "The table holds no spacings.");
            }

            return builder.ToImmutable();
        }

        private StockTable? ReadStockTable(JsonFields fields)
        {
            string id = TakeText(fields, "id") ?? string.Empty;
            string title = TakeText(fields, "title") ?? string.Empty;
            StockCategory category = TakeCategory(fields, "category");
            Citation? source = TakeCitation(fields, "citation");
            Citation? lengthSource = fields.Take("standardLengthCitation") is null
                ? null
                : TakeCitation(fields, "standardLengthCitation");

            ImmutableArray<StockItem> items = TakeEntries(fields, category, source, lengthSource);

            RejectUnknownFields(fields);

            return Failed || source is null ? null : new StockTable(id, title, category, source, file, items);
        }

        private ImmutableArray<StockItem> TakeEntries(
            JsonFields fields,
            StockCategory category,
            Citation? tableSource,
            Citation? lengthSource)
        {
            JsonElement? element = fields.Take("entries");
            if (element is not { } entries)
            {
                Add(MaterialsProblemKind.MissingField, "/entries", "The table has no \"entries\".");
                return [];
            }

            if (entries.ValueKind != JsonValueKind.Array)
            {
                Add(MaterialsProblemKind.Malformed, "/entries", $"Expected \"entries\" to be an array, and found {Describe(entries)}.");
                return [];
            }

            ImmutableArray<StockItem>.Builder builder = ImmutableArray.CreateBuilder<StockItem>();
            HashSet<string> keysInFile = new(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                StockItem? item = ReadEntry(entry, $"/entries/{index}", category, tableSource, lengthSource);
                index++;
                if (item is null)
                {
                    continue;
                }

                if (!keysInFile.Add(item.Key))
                {
                    Add(
                        MaterialsProblemKind.DuplicateEntry,
                        $"/entries/{index - 1}/name",
                        $"\"{item.Name}\" means the same item as an earlier entry in this file "
                        + $"(both normalise to \"{item.Key}\").");
                    continue;
                }

                builder.Add(item);
            }

            if (builder.Count == 0 && !Failed)
            {
                Add(MaterialsProblemKind.Empty, "/entries", "The table holds no entries.");
            }

            return builder.ToImmutable();
        }

        private StockItem? ReadEntry(
            JsonElement element,
            string path,
            StockCategory category,
            Citation? tableSource,
            Citation? lengthSource)
        {
            JsonFields? fields = ReadFields(element, path, "an entry");
            if (fields is null)
            {
                return null;
            }

            string name = TakeText(fields, "name") ?? string.Empty;
            string key = NominalName.Normalize(name);
            if (key.Length == 0 && name.Length > 0)
            {
                Add(MaterialsProblemKind.EmptyValue, $"{path}/name", $"\"{name}\" is not a name anything can be looked up by.");
            }

            string derivation = TakeText(fields, "derivation") ?? string.Empty;
            Citation? source = fields.Take("citation") is null
                ? tableSource
                : TakeCitation(fields, "citation");

            Common common = new(name, key, category, source, derivation);
            StockItem? item = category switch
            {
                StockCategory.DimensionalLumber or StockCategory.Decking => ReadLumber(fields, path, common),
                StockCategory.SheetGood => ReadPanel(fields, path, common),
                StockCategory.HardwoodBoard => ReadHardwood(fields, path, common),
                StockCategory.Fastener => ReadFastener(fields, path, common),

                // Not a file problem: every category this build defines is read above, so this is
                // only reachable by adding one to the enum and forgetting to read its entries.
                _ => throw new InvalidOperationException($"No entry reader for the \"{category}\" category."),
            };

            item = TakeStandardLengths(fields, path, item, lengthSource);

            RejectUnknownFields(fields);
            return Failed || source is null ? null : item;
        }

        /// <summary>The fields every entry has, whatever its category.</summary>
        private readonly record struct Common(
            string Name,
            string Key,
            StockCategory Category,
            Citation? Source,
            string Derivation);

        private StockItem? TakeStandardLengths(JsonFields fields, string path, StockItem? item, Citation? lengthSource)
        {
            JsonElement? element = fields.Take("standardLengths");
            string derivation = fields.Take("standardLengthDerivation") is { } derivationElement
                && derivationElement.ValueKind == JsonValueKind.String
                    ? derivationElement.GetString() ?? string.Empty
                    : string.Empty;

            if (element is not { } lengths)
            {
                return item;
            }

            if (lengths.ValueKind != JsonValueKind.Array)
            {
                Add(
                    MaterialsProblemKind.Malformed,
                    $"{path}/standardLengths",
                    $"Expected \"standardLengths\" to be an array, and found {Describe(lengths)}.");
                return item;
            }

            if (lengthSource is null)
            {
                Add(
                    MaterialsProblemKind.MissingField,
                    $"{path}/standardLengths",
                    "This entry lists standard lengths, and the table has no \"standardLengthCitation\". "
                    + "A stock-length list comes from a grading agency's rulebook rather than from the size "
                    + "standard, so it names its own source or it is not carried.");
                return item;
            }

            ImmutableArray<Length>.Builder builder = ImmutableArray.CreateBuilder<Length>();
            int index = 0;
            Length previous = Length.Zero;
            foreach (JsonElement each in lengths.EnumerateArray())
            {
                string where = $"{path}/standardLengths/{index}";
                index++;

                if (each.ValueKind != JsonValueKind.String
                    || !Length.TryParse(each.GetString(), out Length value, out bool wasRounded)
                    || wasRounded)
                {
                    Add(MaterialsProblemKind.NotALength, where, $"{Describe(each)} is not an exact length.");
                    continue;
                }

                if (value <= previous)
                {
                    Add(
                        MaterialsProblemKind.InvalidValue,
                        where,
                        "Standard lengths are listed shortest first and each one only once, so the shopping "
                        + "list can take the first that fits.");
                    continue;
                }

                previous = value;
                builder.Add(value);
            }

            if (builder.Count == 0)
            {
                Add(MaterialsProblemKind.Empty, $"{path}/standardLengths", "\"standardLengths\" is there and lists nothing.");
                return item;
            }

            if (derivation.Length == 0)
            {
                Add(
                    MaterialsProblemKind.MissingField,
                    $"{path}/standardLengthDerivation",
                    "An entry that lists standard lengths says how they were read out of the cited clause.");
                return item;
            }

            return item is null
                ? null
                : item with
                {
                    StandardLengths = builder.ToImmutable(),
                    StandardLengthSource = lengthSource,
                    StandardLengthDerivation = derivation,
                };
        }

        private StockItem? ReadLumber(JsonFields fields, string path, Common common)
        {
            Length nominalThickness = TakeLength(fields, path, "nominalThickness");
            Length nominalWidth = TakeLength(fields, path, "nominalWidth");
            Length thickness = TakeLength(fields, path, "thickness");
            Length width = TakeLength(fields, path, "width");
            SizeClass sizeClass = TakeSizeClass(fields, path, "sizeClass");

            if (Failed || common.Source is null)
            {
                return null;
            }

            return new LumberStock
            {
                Name = common.Name,
                Key = common.Key,
                Category = common.Category,
                Source = common.Source,
                Derivation = common.Derivation,
                NominalThickness = nominalThickness,
                NominalWidth = nominalWidth,
                Thickness = thickness,
                Width = width,
                SizeClass = sizeClass,
            };
        }

        private StockItem? ReadPanel(JsonFields fields, string path, Common common)
        {
            string performanceCategory = TakeText(fields, "performanceCategory") ?? string.Empty;
            Length thickness = TakeLength(fields, path, "thickness");
            Length sheetWidth = TakeLength(fields, path, "sheetWidth");
            Length sheetLength = TakeLength(fields, path, "sheetLength");

            if (Failed || common.Source is null)
            {
                return null;
            }

            return new PanelStock
            {
                Name = common.Name,
                Key = common.Key,
                Category = common.Category,
                Source = common.Source,
                Derivation = common.Derivation,
                PerformanceCategory = performanceCategory,
                Thickness = thickness,
                SheetWidth = sheetWidth,
                SheetLength = sheetLength,
            };
        }

        private StockItem? ReadHardwood(JsonFields fields, string path, Common common)
        {
            Length roughThickness = TakeLength(fields, path, "roughThickness");
            Length surfacedTwoSides = TakeLength(fields, path, "surfacedTwoSides");

            if (Failed || common.Source is null)
            {
                return null;
            }

            if (surfacedTwoSides >= roughThickness)
            {
                Add(
                    MaterialsProblemKind.InvalidValue,
                    $"{path}/surfacedTwoSides",
                    "Surfacing takes thickness off: the surfaced thickness has to be less than the rough one.");
                return null;
            }

            return new HardwoodStock
            {
                Name = common.Name,
                Key = common.Key,
                Category = common.Category,
                Source = common.Source,
                Derivation = common.Derivation,
                RoughThickness = roughThickness,
                SurfacedTwoSides = surfacedTwoSides,
            };
        }

        private StockItem? ReadFastener(JsonFields fields, string path, Common common)
        {
            // A brad row is named by length and wire; only some carry a penny-size trade designation, and the others leave the field out.
            string pennySize = fields.Take("pennySize") is null ? string.Empty : TakeText(fields, "pennySize") ?? string.Empty;
            string family = TakeText(fields, "family") ?? string.Empty;
            if (family.Length > 0 && family is not ("nail" or "brad"))
            {
                Add(MaterialsProblemKind.UnknownValue, path + ".family", $"A fastener's family is \"nail\" or \"brad\", not \"{family}\".");
            }

            Length length = TakeLength(fields, path, "length");
            double shankDiameter = TakeDecimalInches(fields, path, "shankDiameterInches");

            if (Failed || common.Source is null)
            {
                return null;
            }

            return new FastenerStock
            {
                Name = common.Name,
                Key = common.Key,
                Category = common.Category,
                Source = common.Source,
                Derivation = common.Derivation,
                PennySize = pennySize,
                Family = family,
                FastenerLength = length,
                ShankDiameterInches = shankDiameter,
            };
        }

        // -----------------------------------------------------------------------------------
        // JSON primitives
        // -----------------------------------------------------------------------------------

        private JsonFields? ReadFields(JsonElement element, string path, string what)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                Add(MaterialsProblemKind.Malformed, path, $"Expected {what} to be an object, and found {Describe(element)}.");
                return null;
            }

            JsonFields fields = new(path);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!fields.TryAdd(property))
                {
                    Add(
                        MaterialsProblemKind.DuplicateField,
                        $"{path}/{property.Name}",
                        $"The field \"{property.Name}\" appears more than once.");
                }
            }

            return fields;
        }

        private void RejectUnknownFields(JsonFields fields)
        {
            foreach (string name in fields.Unused)
            {
                Add(
                    MaterialsProblemKind.UnknownField,
                    $"{fields.Path}/{name}",
                    $"\"{name}\" is not a field this format defines. napkin reads its own data strictly: "
                    + "a field it does not know is a transcription mistake, not an extension point.");
            }
        }

        private string? TakeText(JsonFields fields, string name)
        {
            string path = $"{fields.Path}/{name}";
            if (fields.Take(name) is not { } element)
            {
                Add(MaterialsProblemKind.MissingField, path, $"\"{name}\" is required and is not there.");
                return null;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                Add(MaterialsProblemKind.Malformed, path, $"Expected \"{name}\" to be text, and found {Describe(element)}.");
                return null;
            }

            string text = element.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Add(MaterialsProblemKind.EmptyValue, path, $"\"{name}\" is empty. Every field this format defines has to say something.");
                return null;
            }

            return text;
        }

        private int? TakeInt(JsonFields fields, string name)
        {
            string path = $"{fields.Path}/{name}";
            if (fields.Take(name) is not { } element)
            {
                Add(MaterialsProblemKind.MissingField, path, $"\"{name}\" is required and is not there.");
                return null;
            }

            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
            {
                Add(MaterialsProblemKind.Malformed, path, $"Expected \"{name}\" to be a whole number, and found {Describe(element)}.");
                return null;
            }

            return value;
        }

        private Length TakeLength(JsonFields fields, string path, string name)
        {
            string where = $"{path}/{name}";
            if (fields.Take(name) is not { } element)
            {
                Add(MaterialsProblemKind.MissingField, where, $"\"{name}\" is required and is not there.");
                return Length.Zero;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                Add(
                    MaterialsProblemKind.Malformed,
                    where,
                    $"Expected \"{name}\" to be text written the way the standard prints it — \"1-1/2\" — and found {Describe(element)}.");
                return Length.Zero;
            }

            string text = element.GetString() ?? string.Empty;
            if (!Length.TryParse(text, out Length value, out bool wasRounded))
            {
                Add(MaterialsProblemKind.NotALength, where, $"\"{text}\" is not a length napkin understands.");
                return Length.Zero;
            }

            if (wasRounded)
            {
                Add(
                    MaterialsProblemKind.NotALength,
                    where,
                    $"\"{text}\" does not land on the 1/{Length.UnitsPerInch} inch grid, so it cannot be stored exactly. "
                    + "A printed standard states tape-measure fractions; a value that has to be rounded is a transcription mistake.");
                return Length.Zero;
            }

            if (value <= Length.Zero)
            {
                Add(MaterialsProblemKind.InvalidValue, where, $"\"{text}\" is not a positive size.");
                return Length.Zero;
            }

            return value;
        }

        /// <summary>
        /// A wire diameter, which a fastener specification genuinely states as a decimal of an
        /// inch rather than as a tape-measure fraction. Written as text so the data file reads
        /// exactly like the specification's cell — <c>".162"</c> — and so no JSON number formatting
        /// can come between the two.
        /// </summary>
        private double TakeDecimalInches(JsonFields fields, string path, string name)
        {
            string where = $"{path}/{name}";
            if (fields.Take(name) is not { } element)
            {
                Add(MaterialsProblemKind.MissingField, where, $"\"{name}\" is required and is not there.");
                return 0;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                Add(
                    MaterialsProblemKind.Malformed,
                    where,
                    $"Expected \"{name}\" to be text written the way the specification prints it — \".162\" — "
                    + $"and found {Describe(element)}.");
                return 0;
            }

            string text = element.GetString() ?? string.Empty;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                Add(MaterialsProblemKind.Malformed, where, $"\"{text}\" is not a decimal number.");
                return 0;
            }

            if (value <= 0)
            {
                Add(MaterialsProblemKind.InvalidValue, where, $"\"{text}\" is not a positive diameter.");
                return 0;
            }

            return value;
        }

        private StockCategory TakeCategory(JsonFields fields, string name)
        {
            string path = $"{fields.Path}/{name}";
            string? text = TakeText(fields, name);
            if (text is null)
            {
                return default;
            }

            if (!Enum.TryParse(text, ignoreCase: true, out StockCategory category) || !Enum.IsDefined(category))
            {
                Add(
                    MaterialsProblemKind.UnknownValue,
                    path,
                    $"\"{text}\" is not a category this build knows. They are: {string.Join(", ", Enum.GetNames<StockCategory>())}.");
                return default;
            }

            return category;
        }

        private SizeClass TakeSizeClass(JsonFields fields, string path, string name)
        {
            string where = $"{path}/{name}";
            if (fields.Take(name) is not { } element || element.ValueKind != JsonValueKind.String)
            {
                Add(MaterialsProblemKind.MissingField, where, $"\"{name}\" is required and is not there as text.");
                return default;
            }

            string text = element.GetString() ?? string.Empty;
            if (!Enum.TryParse(text, ignoreCase: true, out SizeClass sizeClass) || !Enum.IsDefined(sizeClass))
            {
                Add(
                    MaterialsProblemKind.UnknownValue,
                    where,
                    $"\"{text}\" is not a size class this build knows. They are: {string.Join(", ", Enum.GetNames<SizeClass>())}.");
                return default;
            }

            return sizeClass;
        }

        private Citation? TakeCitation(JsonFields fields, string name)
        {
            string path = $"{fields.Path}/{name}";
            if (fields.Take(name) is not { } element)
            {
                Add(
                    MaterialsProblemKind.MissingField,
                    path,
                    "There is no \"citation\". Every table in this library names the standard it was read from; "
                    + "a table without one is a table nobody can check.");
                return null;
            }

            JsonFields? citationFields = ReadFields(element, path, "the citation");
            if (citationFields is null)
            {
                return null;
            }

            string? designation = TakeText(citationFields, "designation");
            string? standard = TakeText(citationFields, "standard");
            string? publisher = TakeText(citationFields, "publisher");
            string? where = TakeText(citationFields, "where");
            string? url = TakeText(citationFields, "url");
            string? retrieved = TakeText(citationFields, "retrieved");

            RejectUnknownFields(citationFields);

            if (designation is null || standard is null || publisher is null || where is null || url is null || retrieved is null)
            {
                return null;
            }

            if (!DateOnly.TryParseExact(retrieved, "yyyy-MM-dd", out DateOnly retrievedOn))
            {
                Add(MaterialsProblemKind.NotADate, $"{path}/retrieved", $"\"{retrieved}\" is not a date written as yyyy-mm-dd.");
                return null;
            }

            return new Citation(designation, standard, publisher, where, url, retrievedOn);
        }

        private static string Describe(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => "an object",
            JsonValueKind.Array => "an array",
            JsonValueKind.String => $"the text {element.GetRawText()}",
            JsonValueKind.Number => $"the number {element.GetRawText()}",
            JsonValueKind.True or JsonValueKind.False => $"the value {element.GetRawText()}",
            _ => "null",
        };

        private void Add(MaterialsProblemKind kind, string location, string message)
            => problems.Add(new MaterialsProblem(kind, file, location, message));
    }
}
