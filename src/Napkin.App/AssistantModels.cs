using Napkin.App.Settings;
using Napkin.Modules.Assistant;

namespace Napkin.App;

/// <summary>
/// Builds the model <see cref="MainWindow"/> uses when its caller does not hand it one
/// (docs/design/llm-assistant.md &#xA7;2.4): the only place in the app that would reach for a
/// runtime assembly, so <c>Napkin.Modules.Assistant</c> and everything above it never do.
/// </summary>
/// <remarks>
/// This slice (#230) knows one provider, <c>None</c> &#x2014; the only one <see cref="UserSettings"/>
/// carries before slice C's settings version 3 (#231) adds the others. <c>LocalServer</c> and
/// <c>Claude</c> arrive with their own runtimes, and this method's own cases for them, in slices C
/// and H; until then a real runtime does not exist here to build.
/// </remarks>
public static class AssistantModels
{
    /// <summary>
    /// The model the window's Assistant panel asks: napkin's "no model configured" state, always,
    /// until a provider setting exists to read.
    /// </summary>
    /// <param name="settings">The person's settings, read once providers exist to choose between (unused in this slice).</param>
    public static IAssistantModel FromSettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new ScriptedModel();
    }
}
