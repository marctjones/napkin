using Napkin.App.Settings;
using Napkin.Assistant.LocalServer;
using Napkin.Assistant.Mlx;
using Napkin.Modules.Assistant;

namespace Napkin.App;

/// <summary>
/// Builds the model <see cref="MainWindow"/> uses when its caller does not hand it one
/// (docs/design/llm-assistant.md &#xA7;2.4, docs/design/mlx-runtime.md &#xA7;4.3): the only place in
/// the app that reaches for a runtime assembly, so <c>Napkin.Modules.Assistant</c> and everything
/// above it never do.
/// </summary>
public static class AssistantModels
{
    /// <summary>
    /// The model the window's Assistant panel asks: a program on this machine when the settings
    /// choose one with an address napkin accepts (loopback only) and a model name; a model run
    /// inside napkin by MLX when the settings choose it, MLX can run in this process, and the
    /// folder checks out; otherwise napkin's "no model" state, a <see cref="ScriptedModel"/> with no
    /// script — never a partial model.
    /// </summary>
    /// <param name="settings">The person's settings.</param>
    /// <param name="http">
    /// How a local program is reached: null for napkin's own loopback-only handler; the GUI suite
    /// passes a stub so no workflow opens a socket.
    /// </param>
    /// <param name="mlx">The MLX bridge: null for the real one (<see cref="NativeMlx"/>); the GUI suite passes a fake so no workflow loads the library.</param>
    /// <param name="mlxAvailable">
    /// Why MLX cannot run in this process, or null when it can: null for <see cref="MlxAvailability.ForThisProcess"/>;
    /// the GUI suite overrides it so an MLX workflow runs on every CI platform, not only Apple silicon.
    /// </param>
    public static IAssistantModel FromSettings(
        UserSettings settings,
        HttpMessageHandler? http = null,
        INativeMlx? mlx = null,
        Func<string?>? mlxAvailable = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AssistantSettings assistant = settings.Assistant;
        if (assistant.Provider == AssistantProvider.Mlx)
        {
            return (mlxAvailable ?? MlxAvailability.ForThisProcess)() is null
                && double.IsFinite(assistant.Temperature) && assistant.Temperature >= 0
                && ModelFolder.TryParse(assistant.ModelFolder, out ModelFolder? folder, out _)
                    ? new MlxModel(mlx ?? new NativeMlx(), folder, assistant.Temperature)
                    : new ScriptedModel();
        }

        return assistant.Provider == AssistantProvider.LocalServer
            && !string.IsNullOrWhiteSpace(assistant.Model)
            && LocalEndpoint.TryParse(assistant.Endpoint, out LocalEndpoint? endpoint, out _)
            && double.IsFinite(assistant.Temperature) && assistant.Temperature >= 0
                ? new LocalServerModel(endpoint, assistant.Model, assistant.Temperature, http)
                : new ScriptedModel();
    }
}
