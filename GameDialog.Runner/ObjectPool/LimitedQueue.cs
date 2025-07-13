using System;
using System.Collections.Generic;

namespace GameDialog.Pooling;

/// <summary>
/// A queue wrapper with an upper limit.
/// </summary>
public class LimitedQueue<T>
{
    public LimitedQueue(int limit = -1)
    {
        Limit = Math.Max(-1, limit);
    }

    /// <summary>
    /// The queue object
    /// </summary>
    private readonly Queue<T> _queue = [];
    /// <summary>
    /// The upper limit for adding items to the queue.
    /// </summary>
    public int Limit { get; set; } = -1;
    /// <summary>
    /// Gets the number of elements contained within the queue.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Adds an object to the end of the queue.
    /// </summary>
    /// <param name="item">The object to add to the queue.</param>
    public void Enqueue(T item)
    {
        if (Limit == -1 || Count < Limit)
            _queue.Enqueue(item);
    }

    /// <summary>
    /// Removes and returns an object at the beginning of the queue.
    /// </summary>
    /// <returns>The object that is removed from the beginning of the queue.</returns>
    public T Dequeue() => _queue.Dequeue();
}

public class PoolQueue<T> : LimitedQueue<T>
{
    public PoolQueue(int limit, Func<T> createFunc)
        : base(limit)
    {
        CreateFunc = createFunc;
    }

    /// <summary>
    /// A delegate to create a new object of the queue's underlying type.
    /// </summary>
    public Func<T> CreateFunc { get; set; }
}