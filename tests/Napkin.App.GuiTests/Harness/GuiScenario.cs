using System.Reflection;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// Marks a static <c>void (IGuiDriver)</c> method as a scenario that runs on both hosts (#151):
/// headless, through a <see cref="GuiWorkflowAttribute"/> test that passes it to
/// <see cref="GuiWorkflow.Run"/>, and live, through <c>tools/Napkin.Demo</c>, which finds it by
/// this attribute.
/// </summary>
/// <remarks>
/// The id is the same feature id its headless workflow claims, so the two cannot describe
/// different things; the title is what the runner's <c>list</c> prints.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GuiScenarioAttribute(string id, string title) : Attribute
{
    /// <summary>The scenario's id: the feature id of its headless workflow.</summary>
    public string Id { get; } = id;

    /// <summary>A one-line title for a person choosing what to watch.</summary>
    public string Title { get; } = title;
}

/// <summary>A scenario found by <see cref="GuiScenarios.Discover"/>.</summary>
/// <param name="Id">The scenario's id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Body">The scenario body, runnable on either host.</param>
public sealed record GuiScenarioInfo(string Id, string Title, Action<IGuiDriver> Body);

/// <summary>Finds the scenarios that run on both hosts.</summary>
public static class GuiScenarios
{
    /// <summary>
    /// Every <see cref="GuiScenarioAttribute"/> method in an assembly, sorted by id.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A marked method has the wrong shape, or two share an id.
    /// </exception>
    public static IReadOnlyList<GuiScenarioInfo> Discover(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var found = new List<GuiScenarioInfo>();
        foreach (Type type in assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<GuiScenarioAttribute>() is not { } marker)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (!method.IsStatic || method.ReturnType != typeof(void)
                    || parameters.Length != 1 || parameters[0].ParameterType != typeof(IGuiDriver))
                {
                    throw new InvalidOperationException(
                        $"{type.Name}.{method.Name} is marked [GuiScenario] but is not a static void method taking one IGuiDriver.");
                }

                found.Add(new GuiScenarioInfo(marker.Id, marker.Title, method.CreateDelegate<Action<IGuiDriver>>()));
            }
        }

        string? duplicate = found.GroupBy(s => s.Id).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Two scenarios claim the id {duplicate}.");
        }

        return [.. found.OrderBy(s => s.Id, StringComparer.Ordinal)];
    }

    /// <summary>The scenarios in this assembly.</summary>
    public static IReadOnlyList<GuiScenarioInfo> All => Discover(typeof(GuiScenarios).Assembly);
}
