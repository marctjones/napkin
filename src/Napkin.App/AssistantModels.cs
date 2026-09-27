using Napkin.App.Settings;
using Napkin.Assistant.LocalServer;
using Napkin.Modules.Assistant;

namespace Napkin.App;

/// <summary>
/// Builds the model <see cref="MainWindow"/> uses when its caller does not hand it one
/// (docs/design/llm-assistant.md &#xA7;2.4): the only place in the app that reaches for a runtime
/// assembly, so <c>Napkin.Modules.Assistant</c> and everything above it never do.
/// </summary>
public static class AssistantModels
{
    /// <summary>
    /// The model the window's Assistant panel asks: a program on this machine when the settings
    /// choose one with an address napkin accepts (loopback only) and a model name; otherwise
    /// napkin's "no model" state, a <see cref="ScriptedModel"/> with no script.
    /// </summary>
    /// <param name="settings">The person's settings.</param>
    /// <param name="http">
    /// How a local program is reached: null for napkin's own loopback-only handler; the GUI suite
    /// passes a stub so no workflow opens a socket.
    /// </param>
    public static IAssistantModel FromSettings(UserSettings settings, HttpMessageHandler? http = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AssistantSettings assistant = settings.Assistant;
        return assistant.Provider == AssistantProvider.LocalServer
            && !string.IsNullOrWhiteSpace(assistant.Model)
            && LocalEndpoint.TryParse(assistant.Endpoint, out LocalEndpoint? endpoint, out _)
            && double.IsFinite(assistant.Temperature) && assistant.Temperature >= 0
                ? new LocalServerModel(endpoint, assistant.Model, assistant.Temperature, http)
                : new ScriptedModel();
    }
}
