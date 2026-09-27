namespace Napkin.Assistant.Mlx.Tests;

/// <summary>
/// A model folder is checked as the bridge will check it — its refusals in the bridge's words, in
/// its order (Bridge.swift, <c>ModelFolder.validate</c>) — and read for the dialog's line.
/// </summary>
[Trait("Feature", "AST-007")]
public class ModelFolderTests
{
    [Fact]
    public void ACompleteFolderIsReadForTheDialogsLine()
    {
        using TempModel model = TempModel.Complete().Write("README.md", TempModel.Readme).Write("model-2.safetensors", "more");

        ModelFolder folder = model.Folder();

        Assert.Equal(model.Path, folder.Path);
        Assert.Equal("Qwen3-4B-4bit", folder.Name);
        Assert.Equal("Qwen3-4B-4bit", folder.DisplayName);
        Assert.Equal("qwen3", folder.ModelType);
        Assert.Equal(4, folder.QuantizationBits);
        Assert.Equal(64, folder.QuantizationGroupSize);
        Assert.Equal("apache-2.0", folder.Licence);
        Assert.Equal("weights".Length + "more".Length, folder.WeightBytes);
        Assert.Equal("qwen3, 4-bit (group 64), licence: apache-2.0", folder.Description);
    }

    [Fact]
    public void AFolderNapkinsDownloadMadeIsShownByTheRepositorysName()
    {
        using TempModel model = TempModel.Complete("mlx-community--Qwen3-4B-4bit--4dcb3d101c2a");

        ModelFolder folder = model.Folder();

        Assert.Equal("mlx-community--Qwen3-4B-4bit--4dcb3d101c2a", folder.Name);
        Assert.Equal("Qwen3-4B-4bit", folder.DisplayName);
    }

    [Theory]
    [InlineData("my--model")]
    [InlineData("mlx-community--Qwen3-4B-4bit--4DCB3D101C2A")]
    [InlineData("mlx-community--Qwen3-4B-4bit--4dcb3d101c")]
    public void AnyOtherNameIsShownAsItIs(string name)
    {
        using TempModel model = TempModel.Complete(name);

        Assert.Equal(name, model.Folder().DisplayName);
    }

    [Fact]
    public void AFolderThatStatesNothingSaysSo()
    {
        using TempModel model = TempModel.Complete().Write("config.json", "{}");

        ModelFolder folder = model.Folder();

        Assert.Null(folder.ModelType);
        Assert.Null(folder.QuantizationBits);
        Assert.Null(folder.Licence);
        Assert.Equal("model type not stated, quantization not stated, licence not stated in the folder", folder.Description);
    }

    [Theory]
    [InlineData("""{"model_type": "phi3", "quantization": {"bits": 4}}""", "phi3, 4-bit, licence not stated in the folder")]
    [InlineData("""{"model_type": 3, "quantization": {"bits": "4", "group_size": 64}}""", "model type not stated, quantization not stated, licence not stated in the folder")]
    [InlineData("""{"model_type": "qwen3", "quantization": [4, 64]}""", "qwen3, quantization not stated, licence not stated in the folder")]
    [InlineData("""{"model_type": "qwen3", "quantization": {"bits": 4.5, "group_size": 64}}""", "qwen3, quantization not stated, licence not stated in the folder")]
    public void OnlyWhatConfigStatesPlainlyIsShown(string config, string line)
    {
        using TempModel model = TempModel.Complete().Write("config.json", config);

        Assert.Equal(line, model.Folder().Description);
    }

    [Theory]
    [InlineData("---\nlicense: mit\n---\n", "mit")]
    [InlineData("---\r\nlicense: \"apache-2.0\"\r\n---\r\n", "apache-2.0")]
    [InlineData("---\nlicense: 'mit'\n", "mit")]
    public void TheLicenceIsTheCardsFrontMatter(string readme, string licence)
    {
        using TempModel model = TempModel.Complete().Write("README.md", readme);

        Assert.Equal(licence, model.Folder().Licence);
    }

    [Theory]
    [InlineData("# A model\n\nlicense: mit\n")]
    [InlineData("---\nlibrary_name: mlx\n---\nlicense: mit\n")]
    [InlineData("---\n  license: mit\n---\n")]
    [InlineData("---\nlicense:\n---\n")]
    [InlineData("---\nlibrary_name: mlx\n")]
    [InlineData("")]
    public void ALicenceOutsideTheFrontMatterIsNotOne(string readme)
    {
        using TempModel model = TempModel.Complete().Write("README.md", readme);

        Assert.Null(model.Folder().Licence);
        Assert.EndsWith(ModelFolder.LicenceNotStated, model.Folder().Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoFolderIsRefused(string? path)
    {
        Assert.False(ModelFolder.TryParse(path, out ModelFolder? folder, out string? refusal));
        Assert.Null(folder);
        Assert.Equal("No model folder was given.", refusal);
    }

    [Fact]
    public void AFolderThatIsNotThereIsRefusedByItsPath()
    {
        using TempModel model = new();
        string missing = Path.Combine(model.Root, "nothing-here");

        Assert.False(ModelFolder.TryParse(missing, out _, out string? refusal));
        Assert.Equal($"There is no folder at {missing}.", refusal);
    }

    [Fact]
    public void AFileIsNotAFolder()
    {
        using TempModel model = TempModel.Complete();

        Assert.False(ModelFolder.TryParse(model.File("config.json"), out _, out string? refusal));
        Assert.Equal($"{model.File("config.json")} is a file, not a model folder.", refusal);
    }

    [Theory]
    [InlineData("config.json")]
    [InlineData("tokenizer.json")]
    [InlineData("tokenizer_config.json")]
    public void EachFileTheBridgeNeedsIsRefusedByName(string name)
    {
        using TempModel model = TempModel.Complete().Delete(name);

        Assert.False(ModelFolder.TryParse(model.Path, out _, out string? refusal));
        Assert.Equal($"The folder {model.Path} has no {name}.", refusal);
    }

    [Fact]
    public void TheFilesAreNamedInTheBridgesOrder()
    {
        using TempModel model = new();

        Assert.False(ModelFolder.TryParse(model.Path, out _, out string? refusal));
        Assert.Equal($"The folder {model.Path} has no config.json.", refusal);
        Assert.Equal(["config.json", "tokenizer.json", "tokenizer_config.json"], ModelFolder.RequiredFiles);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    public void ATokenizerConfigThatIsNotAnObjectIsRefused(string text)
    {
        using TempModel model = TempModel.Complete().Write("tokenizer_config.json", text);

        Assert.False(ModelFolder.TryParse(model.Path, out _, out string? refusal));
        Assert.Equal($"The folder's tokenizer_config.json is not a JSON object: {model.File("tokenizer_config.json")}.", refusal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"tokenizer_class": null}""")]
    public void ATokenizerConfigThatNamesNoClassIsRefused(string text)
    {
        using TempModel model = TempModel.Complete().Write("tokenizer_config.json", text);

        Assert.False(ModelFolder.TryParse(model.Path, out _, out string? refusal));
        Assert.Equal("The folder's tokenizer_config.json names no tokenizer_class, which napkin needs to read the tokenizer from the folder alone.", refusal);
    }

    [Fact]
    public void AFolderWithoutWeightsIsRefused()
    {
        using TempModel model = TempModel.Complete().Delete("model.safetensors").Write("model.safetensors.index.json", "{}");

        Assert.False(ModelFolder.TryParse(model.Path, out _, out string? refusal));
        Assert.Equal($"The folder {model.Path} has no .safetensors weights.", refusal);
    }

    [Fact]
    public void AConfigThatIsNotAnObjectIsRefused()
    {
        using TempModel model = TempModel.Complete().Write("config.json", "\"qwen3\"");

        Assert.False(ModelFolder.TryParse(model.Path, out _, out string? refusal));
        Assert.Equal($"The folder's config.json is not a JSON object: {model.File("config.json")}.", refusal);
    }

    [Fact]
    public void AnUnreadableTokenizerConfigIsRefusedAsTheBridgeWould()
    {
        using TempModel model = TempModel.Complete();
        using FileStream held = new(model.File("tokenizer_config.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.False(ModelFolder.TryParse(model.Path, out _, out string? refusal));
        Assert.Equal($"The folder's tokenizer_config.json is not a JSON object: {model.File("tokenizer_config.json")}.", refusal);
    }

    [Fact]
    public void AnUnreadableCardStatesNoLicence()
    {
        using TempModel model = TempModel.Complete().Write("README.md", TempModel.Readme);
        using FileStream held = new(model.File("README.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Null(model.Folder().Licence);
    }

    [Fact]
    public void APathIsTakenFullAndWithoutATrailingSeparator()
    {
        using TempModel model = TempModel.Complete();

        Assert.True(ModelFolder.TryParse($"  {model.Path}{Path.DirectorySeparatorChar}  ", out ModelFolder? folder, out _));
        Assert.Equal(model.Path, folder.Path);
    }

    [Theory]
    [InlineData("/Users/marc/models/qwen", "/Users/marc", "~/models/qwen")]
    [InlineData("/Users/marc/models/qwen", "/Users/marc/", "~/models/qwen")]
    [InlineData("/Users/marc", "/Users/marc", "~")]
    [InlineData("/Users/marcus/models/qwen", "/Users/marc", "/Users/marcus/models/qwen")]
    [InlineData("/Volumes/models/qwen", "/Users/marc", "/Volumes/models/qwen")]
    [InlineData("/Users/marc/models/qwen", "", "/Users/marc/models/qwen")]
    public void TheHomeFolderIsWrittenTilde(string path, string home, string shown)
    {
        string native(string unix) => unix.Replace('/', Path.DirectorySeparatorChar);

        Assert.Equal(native(shown), ModelFolder.HomeRelative(native(path), native(home)));
    }

    [Fact]
    public void TheWhereaboutsPathIsTheFoldersUnderThisHome()
    {
        using TempModel model = TempModel.Complete();

        Assert.Equal(
            ModelFolder.HomeRelative(model.Path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            model.Folder().HomeRelativePath);
    }
}
