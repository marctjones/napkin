namespace Napkin.Assistant.Mlx.Tests;

/// <summary>A model folder on disk for a test: the files the bridge needs, no weights worth the name.</summary>
internal sealed class TempModel : IDisposable
{
    /// <summary>What mlx-community's Qwen3-4B-4bit <c>config.json</c> says about itself, the two keys napkin reads.</summary>
    public const string QwenConfig = """{"model_type": "qwen3", "quantization": {"group_size": 64, "bits": 4}, "vocab_size": 151936}""";

    /// <summary>A <c>tokenizer_config.json</c> that names its class, as every mlx-community folder's does.</summary>
    public const string TokenizerConfig = """{"tokenizer_class": "Qwen2Tokenizer"}""";

    /// <summary>A model card's front matter, as the Hub writes it.</summary>
    public const string Readme = "---\nlibrary_name: mlx\nlicense_link: https://huggingface.co/Qwen/Qwen3-4B/blob/main/LICENSE\nlicense: apache-2.0\npipeline_tag: text-generation\n---\n\n# mlx-community/Qwen3-4B-4bit\n";

    public TempModel(string name = "Qwen3-4B-4bit")
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "napkin-mlx-tests-" + Guid.NewGuid().ToString("N"));
        Path = System.IO.Path.Combine(Root, name);
        Directory.CreateDirectory(Path);
    }

    /// <summary>The folder that holds the model folder (and nothing else).</summary>
    public string Root { get; }

    /// <summary>The model folder.</summary>
    public string Path { get; }

    /// <summary>A folder that passes every check: config, tokenizer, tokenizer config, one small weights file.</summary>
    public static TempModel Complete(string name = "Qwen3-4B-4bit")
    {
        TempModel model = new(name);
        model.Write("config.json", QwenConfig);
        model.Write("tokenizer.json", "{}");
        model.Write("tokenizer_config.json", TokenizerConfig);
        model.Write("model.safetensors", "weights");
        return model;
    }

    /// <summary>The checked folder, failing the test if it is refused.</summary>
    public ModelFolder Folder()
    {
        Assert.True(ModelFolder.TryParse(Path, out ModelFolder? folder, out string? refusal), refusal);
        return folder;
    }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public TempModel Write(string name, string text)
    {
        System.IO.File.WriteAllText(File(name), text);
        return this;
    }

    public TempModel Delete(string name)
    {
        System.IO.File.Delete(File(name));
        return this;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind is not a test failure.
        }
    }
}

/// <summary>The repository, for tests that hold napkin's side to the bridge's own files.</summary>
internal static class Repository
{
    /// <summary>The folder holding napkin.sln, found by walking up (the VersionTests pattern).</summary>
    public static string Root()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "napkin.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException($"napkin.sln not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>A file of the repository as on disk now, line breaks as <c>\n</c>.</summary>
    public static string Text(string relative) =>
        System.IO.File.ReadAllText(System.IO.Path.Combine(Root(), relative)).Replace("\r\n", "\n", StringComparison.Ordinal);
}

/// <summary>Waiting on what another thread does, with a limit so a broken test fails rather than hangs.</summary>
internal static class Eventually
{
    public static async Task True(Func<bool> condition, string what)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, $"Timed out waiting for: {what}");
            await Task.Delay(5);
        }
    }
}
