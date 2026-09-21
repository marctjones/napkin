namespace Napkin.Tools;

/// <summary>
/// An input file is missing, unreadable or malformed. Distinct from a gate failure: the tool
/// could not form an opinion, rather than forming an unfavourable one.
/// </summary>
public sealed class InputException(string message) : Exception(message);
