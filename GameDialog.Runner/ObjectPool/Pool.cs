using System;
using System.Collections.Generic;
using System.Text;

namespace GameDialog.Pooling;

/// <summary>
/// Represents a pool of objects that can be borrowed and returned. 
/// </summary>
public static class Pool
{
    private static readonly Dictionary<Type, LimitedQueue<IPoolable>> s_pool = [];
    private static readonly StringBuilder s_sb = new();

    public static string PrintPool()
    {
        s_sb.AppendLine();
        s_sb.Append("|--Current pool types and counts--|");
        s_sb.AppendLine();

        foreach (var kvp in s_pool)
        {
            s_sb.Append($"{kvp.Key}: {kvp.Value.Count}");
        }

        s_sb.AppendLine();
        string result = s_sb.ToString();
        s_sb.Clear();
        return result;
    }

    public static void ClearPool()
    {
        s_pool.Clear();
    }

    /// <summary>
    /// Populates the provided queue with the specified number of objects.
    /// </summary>
    /// <typeparam name="T">The pool type to allocate to.</typeparam>
    /// <param name="amount">The amount of objects to allocate to the pool.</param>
    public static void Allocate<T>(int amount) where T : IPoolable, new()
    {
        Type type = typeof(T);
        LimitedQueue<IPoolable> limitedQueue = GetLimitedQueue(type);
        int toAllocate = Math.Max(amount, 0);

        if (limitedQueue.Limit != -1)
            toAllocate = Math.Min(toAllocate, limitedQueue.Limit - limitedQueue.Count);

        for (int i = 0; i < toAllocate; i++)
        {
            IPoolable obj = new T();
            limitedQueue.Enqueue(obj);
        }
    }

    public static T? GetSameTypeOrNull<T>(this T poolable) where T : IPoolable
    {
        Type type = poolable.GetType();
        LimitedQueue<IPoolable> limitedQueue = GetLimitedQueue(type);
        return limitedQueue.Count > 0 ? (T)limitedQueue.Dequeue() : default;
    }

    /// <summary>
    /// Retrieves an object from the pool of a registered type.
    /// If the pool is empty, a new object is created.
    /// </summary>
    /// <typeparam name="T">The type of object to borrow.</typeparam>
    /// <returns>An object of the specified type</returns>
    public static T Get<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);
        LimitedQueue<IPoolable> limitedQueue = GetLimitedQueue(type);
        return limitedQueue.Count > 0 ? (T)limitedQueue.Dequeue() : new();
    }

    /// <summary>
    /// Retrieves an object from the pool of the provided type.
    /// If the pool is empty, null is returned.
    /// </summary>
    /// <param name="type">The type of object to retrieve.</param>
    /// <returns>The retrieved object or null.</returns>
    public static object? GetOrNull(Type type)
    {
        LimitedQueue<IPoolable> limitedQueue = GetLimitedQueue(type);
        return limitedQueue.Count > 0 ? limitedQueue.Dequeue() : default;
    }

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    public static void Return(IPoolable poolable)
    {
        poolable.ClearObject();
        Type type = poolable.GetType();
        LimitedQueue<IPoolable> limitedQueue = GetLimitedQueue(type);
        limitedQueue.Enqueue(poolable);
    }

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    public static void ReturnToPool(this IPoolable poolable) => Return(poolable);

    private static LimitedQueue<IPoolable> GetLimitedQueue(Type type)
    {
        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
        {
            limitedQueue = new();
            s_pool[type] = limitedQueue;
        }

        return limitedQueue;
    }
}
