using System.Windows.Input;

namespace Napkin.App;

/// <summary>
/// The smallest command there is: it runs an action and is always enabled.
/// </summary>
/// <remarks>
/// A key binding needs an <see cref="ICommand"/>, and napkin's dependency policy (DESIGN.md
/// &#xA7;2.1) says a package earns its place. Six lines earn theirs here; when the application
/// grows commands that can be disabled, undone or bound to a menu's enabled state, this is the
/// point at which an MVVM toolkit is worth the dependency review.
/// </remarks>
/// <param name="execute">What the command does.</param>
public sealed class RelayCommand(Action execute) : ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => execute();
}
