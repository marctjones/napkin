using System.Collections.Immutable;
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
        Dictionary<string, string> tableIds = new(StringComparer.Ordinal);
        Dictionary<string, string> keys = new(StringComparer.Ordinal);

        foreach (NamedSource source in sources)
        {
            TableReader reader = new(source.Name, problems);
            StockTable? table = reader.Read(source.Open);
            if (table is null)
            {
                continue;
            }

            if (!tableIds.TryAdd(table.Id, source.Name))
            {
                problems.Add(new MaterialsProblem(
                    MaterialsProblemKind.DuplicateTable,
                    source.Name,
                    "/id",
                    $"The table id \"{table.Id}\" is already used by {tableIds[table.Id]}."));
                continue;
            }

            foreach (StockItem item in table.Items)
            {
                if (!keys.TryAdd(item.Key, source.Name))
                {
                    problems.Add(new MaterialsProblem(
                        MaterialsProblemKind.DuplicateEntry,
                        source.Name,
                        $"/entries/{item.Name}",
                        $"\"{item.Name}\" means the same item as an entry already read from {keys[item.Key]} "
                        + $"(both normalise to \"{item.Key}\")."));
                }
            }

            tables.Add(table);
        }

        if (problems.Count > 0)
        {
            return new MaterialsRefused([.. problems]);
        }

        if (tables.Count == 0)
        {
            return Refuse(
                MaterialsProblemKind.Empty,
                sources.Count == 1 ? sources[0].Name : "(no files)",
                string.Empty,
                "There is nothing to read: the library holds no tables.");
        }

        return new MaterialsLoaded(new MaterialsLibrary([.. tables]));
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

        public StockTable? Read(Func<Stream> open)
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

        private StockTable? ReadTable(JsonElement root)
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

            string id = TakeText(fields, "id") ?? string.Empty;
            string title = TakeText(fields, "title") ?? string.Empty;
            StockCategory category = TakeCategory(fields, "category");
            Citation? source = TakeCitation(fields, "citation");

            ImmutableArray<StockItem> items = TakeEntries(fields, category, source);

            RejectUnknownFields(fields);

            return Failed || source is null ? null : new StockTable(id, title, category, source, file, items);
        }

        private ImmutableArray<StockItem> TakeEntries(JsonFields fields, StockCategory category, Citation? tableSource)
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
                StockItem? item = ReadEntry(entry, $"/entries/{index}", category, tableSource);
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

        private StockItem? ReadEntry(JsonElement element, string path, StockCategory category, Citation? tableSource)
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

            StockItem? item = category switch
            {
                StockCategory.DimensionalLumber or StockCategory.Decking
                    => ReadLumber(fields, path, name, key, category, source, derivation),
                _ => UnsupportedCategory(path, category),
            };

            RejectUnknownFields(fields);
            return Failed || source is null ? null : item;
        }

        private StockItem? UnsupportedCategory(string path, StockCategory category)
        {
            Add(
                MaterialsProblemKind.UnknownValue,
                $"{path}",
                $"This build does not know how to read the entries of a \"{category}\" table yet.");
            return null;
        }

        private StockItem? ReadLumber(
            JsonFields fields,
            string path,
            string name,
            string key,
            StockCategory category,
            Citation? source,
            string derivation)
        {
            Length nominalThickness = TakeLength(fields, path, "nominalThickness");
            Length nominalWidth = TakeLength(fields, path, "nominalWidth");
            Length thickness = TakeLength(fields, path, "thickness");
            Length width = TakeLength(fields, path, "width");
            SizeClass sizeClass = TakeSizeClass(fields, path, "sizeClass");

            if (Failed || source is null)
            {
                return null;
            }

            return new LumberStock
            {
                Name = name,
                Key = key,
                Category = category,
                Source = source,
                Derivation = derivation,
                NominalThickness = nominalThickness,
                NominalWidth = nominalWidth,
                Thickness = thickness,
                Width = width,
                SizeClass = sizeClass,
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

            string? standard = TakeText(citationFields, "standard");
            string? publisher = TakeText(citationFields, "publisher");
            string? where = TakeText(citationFields, "where");
            string? url = TakeText(citationFields, "url");
            string? retrieved = TakeText(citationFields, "retrieved");

            RejectUnknownFields(citationFields);

            if (standard is null || publisher is null || where is null || url is null || retrieved is null)
            {
                return null;
            }

            if (!DateOnly.TryParseExact(retrieved, "yyyy-MM-dd", out DateOnly retrievedOn))
            {
                Add(MaterialsProblemKind.NotADate, $"{path}/retrieved", $"\"{retrieved}\" is not a date written as yyyy-mm-dd.");
                return null;
            }

            return new Citation(standard, publisher, where, url, retrievedOn);
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
