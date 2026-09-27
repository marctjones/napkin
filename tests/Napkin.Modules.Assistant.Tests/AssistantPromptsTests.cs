namespace Napkin.Modules.Assistant.Tests;

/// <summary>The committed prompts (docs/design/llm-assistant.md §4.1, §11.1 item 7).</summary>
public class AssistantPromptsTests
{
    public static TheoryData<AssistantTask> Tasks => new() { AssistantTask.Ask, AssistantTask.Sketch, AssistantTask.Edit };

    [Theory]
    [MemberData(nameof(Tasks))]
    public void Each_prompt_is_its_committed_file_word_for_word(AssistantTask task)
    {
        // Read from the repository, not the build output: a prompt edited without a rebuild fails here.
        Assert.Equal(Fixtures.RepositoryText(AssistantPrompts.Files[task]), AssistantPrompts.For(task));
    }

    [Theory]
    [MemberData(nameof(Tasks))]
    public void Each_prompt_carries_the_number_rule_the_typed_data_rule_and_the_disclaimer(AssistantTask task)
    {
        string prompt = AssistantPrompts.For(task);
        Assert.Contains(AssistantPrompts.NumberRule, prompt, StringComparison.Ordinal);
        Assert.Contains("napkin did not give me that", prompt, StringComparison.Ordinal);
        Assert.Contains(AssistantPrompts.TypedIsData, prompt, StringComparison.Ordinal);
        Assert.Contains(ContextPack.Disclaimer, prompt, StringComparison.Ordinal);
        Assert.Contains("numbered context", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_proposal_prompts_ask_for_json_only_and_the_answer_prompt_for_text()
    {
        Assert.EndsWith(AssistantPrompts.JsonOnly + "\n", AssistantPrompts.Sketch, StringComparison.Ordinal);
        Assert.EndsWith(AssistantPrompts.JsonOnly + "\n", AssistantPrompts.Edit, StringComparison.Ordinal);
        Assert.DoesNotContain("JSON", AssistantPrompts.Ask, StringComparison.Ordinal);
        Assert.Same(AssistantPrompts.Ask, AssistantPrompts.For(AssistantTask.Ask));
    }

    [Fact]
    public void The_sketch_prompt_asks_for_rough_planks_only()
    {
        Assert.Contains("no stock, no species, no joints, no relationships, no walls, openings, rooms, decks or roofs", AssistantPrompts.Sketch, StringComparison.Ordinal);
        Assert.Contains("no walls' inputs, site values, code choices, phases, joints or cuts", AssistantPrompts.Edit, StringComparison.Ordinal);
    }
}
