using System.Reflection;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>The seam (docs/design/llm-assistant.md §2.1, §11.1 items 6 and 8): the closed reply and the scripted model.</summary>
public class ModelSeamTests
{
    private static ModelRequest Asking(string question) => new("system", "[1] context", question, null);

    [Fact]
    public void A_reply_is_exactly_one_of_three_and_nothing_else_can_be_one()
    {
        Type reply = typeof(ModelReply);
        Assert.True(reply.IsAbstract);

        // The rules engine's pattern: only the private constructor and the compiler's copy constructor
        // exist; closure is by convention plus this test, since a record's copy constructor is protected.
        Assert.All(
            reply.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            ctor => Assert.True(ctor.IsPrivate || ctor.GetParameters().Select(p => p.ParameterType).SequenceEqual([reply]), ctor.ToString()));

        Type[] kinds = [.. typeof(ModelReply).Assembly.GetTypes().Where(type => type != reply && reply.IsAssignableFrom(type))];
        Assert.Equal(
            ["Json", "Refused", "Text"],
            kinds.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.All(kinds, type => Assert.True(type.IsSealed && type.IsNested && type.DeclaringType == reply));
    }

    [Fact]
    public void A_request_for_an_answer_carries_the_pack_as_text_and_no_schema()
    {
        ContextPack pack = ContextPack.Of([(ContextKind.Site, "Site: ground snow load not entered.")]);
        ModelRequest request = ModelRequest.ForAnswer(AssistantPrompts.Ask, pack, "what is ground snow load");

        Assert.Equal(AssistantPrompts.Ask, request.System);
        Assert.Equal("[1] Site: ground snow load not entered.", request.Context);
        Assert.Equal("what is ground snow load", request.Question);
        Assert.Null(request.Schema);
    }

    [Fact]
    public async Task The_scripted_model_replies_in_script_order_and_records_every_request()
    {
        ScriptedModel model = new(ScriptedReply.Text("first"), ScriptedReply.Json("{\"parts\":[]}"));
        Assert.Equal(ScriptedModel.ScriptedWhereabouts, model.Whereabouts);
        Assert.False(model.IsNone);
        Assert.Equal(2, model.Remaining);

        Assert.Equal(new ModelReply.Text("first"), await model.AskAsync(Asking("one"), CancellationToken.None));
        Assert.Equal(new ModelReply.Json("{\"parts\":[]}"), await model.AskAsync(Asking("two"), CancellationToken.None));

        // Run out: refused, and the request is still recorded.
        ModelReply.Refused refused = Assert.IsType<ModelReply.Refused>(await model.AskAsync(Asking("three"), CancellationToken.None));
        Assert.Equal(ScriptedModel.ScriptRanOut, refused.Reason);
        Assert.Equal(["one", "two", "three"], model.Requests.Select(request => request.Question));
        Assert.Equal(0, model.Remaining);
    }

    [Fact]
    public async Task A_matched_reply_answers_only_the_question_it_names()
    {
        ScriptedModel model = new(
            [
                ScriptedReply.Text("about snow", match: "snow load"),
                ScriptedReply.Text("anything else"),
            ],
            "Local: scripted at 127.0.0.1 — nothing leaves this machine.");
        Assert.Equal("Local: scripted at 127.0.0.1 — nothing leaves this machine.", model.Whereabouts);

        Assert.Equal(new ModelReply.Text("anything else"), await model.AskAsync(Asking("why is this header No data?"), CancellationToken.None));
        Assert.Equal(new ModelReply.Text("about snow"), await model.AskAsync(Asking("What is ground SNOW LOAD?"), CancellationToken.None));
        Assert.IsType<ModelReply.Refused>(await model.AskAsync(Asking("what is ground snow load"), CancellationToken.None));
    }

    [Fact]
    public async Task No_model_says_so_on_the_whereabouts_line_and_to_every_question()
    {
        ScriptedModel none = new();
        Assert.True(none.IsNone);
        Assert.Equal("No model — Assistant → Where the model runs…", none.Whereabouts);

        ModelReply.Refused refused = Assert.IsType<ModelReply.Refused>(await none.AskAsync(Asking("anything"), CancellationToken.None));
        Assert.Equal(ScriptedModel.NoModelReason, refused.Reason);
        Assert.Single(none.Requests);

        // An empty script is the no-model state whatever whereabouts it was given.
        Assert.Equal(ScriptedModel.NoModelWhereabouts, new ScriptedModel([], "Local: somewhere").Whereabouts);
    }

    [Fact]
    public async Task A_delayed_reply_can_be_cancelled_and_then_nothing_is_surfaced()
    {
        ScriptedModel model = new(
            ScriptedReply.Text("too late", delay: TimeSpan.FromSeconds(30)),
            ScriptedReply.Text("in time", delay: TimeSpan.FromMilliseconds(1)));

        using CancellationTokenSource escape = new();
        Task<ModelReply> thinking = model.AskAsync(Asking("what is ground snow load"), escape.Token);
        Assert.False(thinking.IsCompleted);
        escape.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => thinking);

        // The cancelled question used its reply, as a real model spends its time; asking again gets the next.
        Assert.Equal(new ModelReply.Text("in time"), await model.AskAsync(Asking("what is ground snow load"), CancellationToken.None));

        // A question asked with the token already tripped never reaches the script.
        using CancellationTokenSource tripped = new();
        tripped.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.AskAsync(Asking("again"), tripped.Token));
        Assert.Equal(2, model.Requests.Length);
    }

    [Fact]
    public async Task A_script_refuses_an_empty_entry_and_the_model_refuses_no_request()
    {
        Assert.Throws<ArgumentException>(() => new ScriptedModel([null!], "x"));
        Assert.Throws<ArgumentNullException>(() => new ScriptedModel(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => new ScriptedModel([], null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => new ScriptedModel().AskAsync(null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => ModelRequest.ForAnswer("system", null!, "q"));
    }
}
