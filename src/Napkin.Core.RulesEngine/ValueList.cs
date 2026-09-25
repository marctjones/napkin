using System.Collections;
using System.Collections.Immutable;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// An immutable list with value equality, so that records holding lists (a citation's trace, a
/// result's missing inputs) compare by content. <see cref="ImmutableArray{T}"/> compares by
/// reference, which would make "same pack, same request, same result" untestable (design §11.3 P4).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly ImmutableArray<T> items;

    /// <summary>The empty list.</summary>
    public static readonly ValueList<T> Empty = new(ImmutableArray<T>.Empty);

    /// <summary>Wraps the given items, in order.</summary>
    public ValueList(IEnumerable<T> items) => this.items = items.ToImmutableArray();

    private ValueList(ImmutableArray<T> items) => this.items = items;

    /// <inheritdoc/>
    public T this[int index] => items[index];

    /// <inheritdoc/>
    public int Count => items.Length;

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool Equals(ValueList<T>? other)
        => other is not null && items.SequenceEqual(other.items);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ValueList<T>);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (T item in items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => "[" + string.Join(", ", items) + "]";
}

/// <summary>Factory for <see cref="ValueList{T}"/>.</summary>
public static class ValueList
{
    /// <summary>A list of the given items, in order.</summary>
    public static ValueList<T> Of<T>(params T[] items) => new(items);

    /// <summary>A list of the given items, in order.</summary>
    public static ValueList<T> ToValueList<T>(this IEnumerable<T> items) => new(items);
}
