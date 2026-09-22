using System.Text.Json;

namespace Napkin.Core.Project;

/// <summary>
/// The two directions of <c>manifest.json</c>, written beside each other for the same reason the
/// scene's two directions are: a field added to one and not the other stops compiling.
/// </summary>
/// <remarks>
/// Read as strictly as the scene is (<see cref="SceneBinder"/>): the container version is judged
/// before anything else, and an unknown field, a repeated field or a value of the wrong JSON type
/// is a refusal. With the version pinned there is no legitimate reason for an unknown field.
/// </remarks>
internal static class ManifestJson
{
    /// <summary>Where the manifest lives, for a message that names it.</summary>
    internal const string Where = ContainerNames.Manifest;

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8,
    };

    /// <summary>The manifest document, as UTF-8 bytes, laid out like every other napkin file.</summary>
    internal static byte[] Write(ProjectManifest manifest)
    {
        using MemoryStream stream = new();

        using (Utf8JsonWriter writer = new(stream, JsonLayout.Options))
        {
            writer.WriteStartObject();
            writer.WriteNumber(ManifestNames.ContainerVersion, manifest.ContainerVersion);
            writer.WriteString(ManifestNames.AppVersion, manifest.AppVersion);

            // Written whether or not one is chosen, because this format has no optional fields:
            // "adoptedCode": null is how a project says it has not chosen a code
            // (docs/design/rules-engine-model.md §7.1).
            if (manifest.AdoptedCode is { } code)
            {
                writer.WriteStartObject(ManifestNames.AdoptedCode);
                writer.WriteString(ManifestNames.Pack, code.Pack);
                writer.WriteNumber(ManifestNames.Revision, code.Revision);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull(ManifestNames.AdoptedCode);
            }

            writer.WriteEndObject();
            writer.Flush();
        }

        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    /// <summary>
    /// Reads the manifest, or adds to <paramref name="problems"/> and returns
    /// <see langword="null"/>.
    /// </summary>
    internal static ProjectManifest? Read(byte[] bytes, List<LoadProblem> problems)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, ParseOptions);
        }
        catch (JsonException exception)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.Malformed,
                Where,
                $"The manifest is not JSON: {exception.Message}"));
            return null;
        }

        using (document)
        {
            return Read(document.RootElement, problems);
        }
    }

    private static ProjectManifest? Read(JsonElement root, List<LoadProblem> problems)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.Malformed, Where, "The manifest is not a JSON object."));
            return null;
        }

        Fields fields = new(Where, root, problems);

        // The container version is judged first and alone: a container from another version is
        // refused for that reason, whatever else the manifest says.
        if (fields.Integer(ManifestNames.ContainerVersion) is not { } version)
        {
            return null;
        }

        if (version != ProjectManifest.CurrentContainerVersion)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.UnsupportedContainerVersion,
                $"{Where}/{ManifestNames.ContainerVersion}",
                $"The project is container version {ManifestNames.Number(version)}; this build of napkin reads "
                + $"container version {ManifestNames.Number(ProjectManifest.CurrentContainerVersion)} and no other. "
                + "napkin is a beta and writes no migration code: a file from another version cannot be opened."));
            return null;
        }

        int before = problems.Count;

        string? appVersion = fields.Text(ManifestNames.AppVersion);
        AdoptedCode? adoptedCode = ReadAdoptedCode(fields, problems);
        fields.RejectWhatIsLeft();

        if (problems.Count > before || appVersion is null)
        {
            return null;
        }

        return new ProjectManifest((int)version, appVersion, adoptedCode);
    }

    private static AdoptedCode? ReadAdoptedCode(Fields fields, List<LoadProblem> problems)
    {
        if (fields.Take(ManifestNames.AdoptedCode) is not { } element)
        {
            return null;
        }

        string path = $"{Where}/{ManifestNames.AdoptedCode}";

        // No code chosen: the state a new project is in, and the state a project falls into when
        // its pack turns out to be unavailable.
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.Malformed,
                path,
                $"\"{ManifestNames.AdoptedCode}\" is the adopted code this project is locked to, or null. "
                + "It is neither here."));
            return null;
        }

        Fields code = new(path, element, problems);
        string? pack = code.Text(ManifestNames.Pack);
        long? revision = code.Integer(ManifestNames.Revision);
        code.RejectWhatIsLeft();

        if (pack is null || revision is not { } number)
        {
            return null;
        }

        // The shape is checked and nothing else: this build does not know what a pack id means and
        // must not refuse a project because a future pack is unfamiliar.
        if (pack.Length == 0)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.InvalidValue,
                $"{path}/{ManifestNames.Pack}",
                "An adopted code names the pack it comes from; this one names nothing."));
            return null;
        }

        if (number < 0 || number > int.MaxValue)
        {
            problems.Add(new LoadProblem(
                LoadProblemKind.InvalidValue,
                $"{path}/{ManifestNames.Revision}",
                $"A pack revision counts up from zero; this one is {ManifestNames.Number(number)}."));
            return null;
        }

        return new AdoptedCode(pack, (int)number);
    }

    /// <summary>
    /// One JSON object's fields and which of them have been read, the same bargain
    /// <see cref="SceneBinder"/> strikes: what is left when everything known has been taken is, by
    /// definition, an unknown field.
    /// </summary>
    private sealed class Fields
    {
        private readonly string path;
        private readonly List<LoadProblem> problems;
        private readonly Dictionary<string, JsonElement> values = new(StringComparer.Ordinal);
        private readonly List<string> unused = [];

        internal Fields(string path, JsonElement element, List<LoadProblem> problems)
        {
            this.path = path;
            this.problems = problems;

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (values.TryAdd(property.Name, property.Value))
                {
                    unused.Add(property.Name);
                    continue;
                }

                problems.Add(new LoadProblem(
                    LoadProblemKind.DuplicateField,
                    $"{path}/{property.Name}",
                    $"The field \"{property.Name}\" appears more than once."));
            }
        }

        internal JsonElement? Take(string name)
        {
            if (!values.TryGetValue(name, out JsonElement element))
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.MissingField,
                    $"{path}/{name}",
                    $"The field \"{name}\" is required and is not there."));
                return null;
            }

            unused.Remove(name);
            return element;
        }

        internal string? Text(string name)
        {
            if (Take(name) is not { } element)
            {
                return null;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.Malformed,
                    $"{path}/{name}",
                    $"Expected \"{name}\" to be text."));
                return null;
            }

            return element.GetString();
        }

        internal long? Integer(string name)
        {
            if (Take(name) is not { } element)
            {
                return null;
            }

            string where = $"{path}/{name}";
            if (element.ValueKind != JsonValueKind.Number)
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.Malformed, where, $"Expected \"{name}\" to be a whole number."));
                return null;
            }

            string raw = element.GetRawText();
            if (raw.Contains('.', StringComparison.Ordinal)
                || raw.Contains('e', StringComparison.OrdinalIgnoreCase))
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.NotAnInteger, where, $"\"{name}\" is {raw}, which is not a whole number."));
                return null;
            }

            if (!element.TryGetInt64(out long value))
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.OutOfRange, where, $"\"{name}\" is {raw}, which does not fit in a 64-bit integer."));
                return null;
            }

            return value;
        }

        internal void RejectWhatIsLeft()
        {
            foreach (string name in unused)
            {
                problems.Add(new LoadProblem(
                    LoadProblemKind.UnknownField,
                    $"{path}/{name}",
                    $"\"{name}\" is not a field the manifest defines. napkin reads its own format strictly: "
                    + "with the container version pinned there is no legitimate reason for an unknown field."));
            }
        }
    }
}

/// <summary>How every JSON document napkin writes is laid out.</summary>
/// <remarks>
/// <para>
/// Two-space indents, <c>\n</c> line endings on every platform, and a trailing newline, so that
/// what a machine saves does not depend on which machine saved it.
/// </para>
/// <para>
/// <strong>Text is escaped as little as JSON allows.</strong> The default encoder escapes
/// everything that could matter if the output were pasted into a web page — <c>+</c>, <c>&amp;</c>,
/// <c>&lt;</c> and every character outside ASCII — which would write this build's own version
/// string as <c>0.5.0-beta+1a2b3c4</c> and a layer called <c>Étage</c> as six escapes. These
/// documents are files on disk, read by a person with a text editor and diffed by git; they are
/// never HTML and never script. So the relaxed encoder is used, which still escapes the quote, the
/// backslash and every control character — everything JSON itself requires — and leaves the rest
/// legible. It is as deterministic as the strict one.
/// </para>
/// </remarks>
internal static class JsonLayout
{
    internal static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
        SkipValidation = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
