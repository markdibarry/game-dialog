namespace GameDialog.Common;

/// <summary>
/// Provides an index for a span of elements in an array,
/// allowing for indexed access and iteration through the elements.
/// </summary>
public ref struct StateSpan<T>
{
    public StateSpan(T[] array, int startIndex = 0)
    {
        _span = array.AsSpan();
        _index = startIndex;
    }

    private Span<T> _span;
    private int _index;

    /// <summary>
    /// Gets the current index within the span.
    /// </summary>
    public readonly int Index => _index;
    /// <summary>
    /// Gets the current value at the index, or default if the index is out of bounds.
    /// </summary>
    public readonly T? Current
    {
        get
        {
            if (Length == 0 || _index < 0 || _index >= Length)
                return default;

            return _span[_index];
        }
    }
    public T this[int i]
    {
        readonly get => _span[i];
        set => _span[i] = value;
    }
    /// <summary>
    /// Gets the total number of elements in the span.
    /// </summary>
    public readonly int Length => _span.Length;
    /// <summary>
    /// Indicates whether the current index is at or beyond the end of the span.
    /// </summary>
    public readonly bool IsAtEnd => _index >= Length;

    /// <summary>
    /// Gets the current value and increments the index.
    /// </summary>
    /// <returns>The current value</returns>
    public T Read()
    {
        if (IsAtEnd)
            throw new InvalidOperationException("Cannot read past the end of the span.");

        return _span[_index++];
    }

    /// <summary>
    /// Moves to the next element in the Span.
    /// </summary>
    /// <returns>True if the move was successful; otherwise, false.</returns>
    public bool MoveNext()
    {
        if (IsAtEnd)
            return false;

        _index++;
        return true;
    }

    /// <summary>
    /// Resets the index to the beginning of the Span
    /// </summary>
    public void Reset()
    {
        _index = default;
    }
}
