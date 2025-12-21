using System;
using System.Collections.Generic;
using System.Text;

namespace GameDialog.Pooling;

/// <summary>
/// Represents a pool of objects that can be borrowed and returned. 
/// </summary>
public static class ListPool
{
    private static readonly Dictionary<Type, LimitedQueue<object>> s_listPool = [];
    private static readonly StringBuilder s_sb = new();

    public static string PrintPool()
    {
        s_sb.AppendLine();
        s_sb.Append("|--Current pool types and counts--|");
        s_sb.AppendLine();

        foreach (var kvp in s_listPool)
        {
            s_sb.Append($"List<{kvp.Key}>: {kvp.Value.Count}");
        }

        s_sb.AppendLine();
        string result = s_sb.ToString();
        s_sb.Clear();
        return result;
    }

    public static void ClearPool()
    {
        s_listPool.Clear();
    }

    public static void SetLimit<T>(int limit) where T : IPoolable, new()
    {
        Type type = typeof(T);
        LimitedQueue<object> limitedQueue = GetLimitedQueue(type);
        limitedQueue.Limit = limit;
    }

    /// <summary>
    /// Populates the provided queue with the specified number of lists.
    /// </summary>
    /// <typeparam name="T">The pool list type to allocate to.</typeparam>
    /// <param name="amount">The amount of objects to allocate to the pool.</param>
    public static void Allocate<T>(int amount)
    {
        Type type = typeof(T);
        LimitedQueue<object> limitedQueue = GetLimitedQueue(type);
        int toAllocate = Math.Max(amount, 0);

        if (limitedQueue.Limit != -1)
            toAllocate = Math.Min(toAllocate, limitedQueue.Limit - limitedQueue.Count);

        for (int i = 0; i < toAllocate; i++)
        {
            List<T> obj = [];
            limitedQueue.Enqueue(obj);
        }
    }

    /// <summary>
    /// Retrieves a List from the pool of the provided type.
    /// If the pool is empty, null is returned.
    /// </summary>
    /// <param name="type">The type of List to retrieve.</param>
    /// <returns>The retrieved List or null.</returns>
    public static object? GetOrNull(Type type)
    {
        if (!s_listPool.TryGetValue(type, out LimitedQueue<object>? limitedQueue))
            return null;

        return limitedQueue.Count > 0 ? limitedQueue.Dequeue() : default;
    }

    /// <summary>
    /// Retrieves a List from the pool of a registered type.
    /// If the pool is empty, a new List is created.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static List<T> Get<T>()
    {
        Type type = typeof(T);
        LimitedQueue<object> limitedQueue = GetLimitedQueue(type);
        return limitedQueue.Count > 0 ? (List<T>)limitedQueue.Dequeue() : [];
    }

    /// <summary>
    /// Returns the provided List to the pool of the underlying registered type.
    /// If the List contains IPoolable objects, they will be returned to their pool as well.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static void Return<T>(List<T> list)
    {
        Type type = typeof(T);

        foreach (T item in list)
        {
            if (item is IPoolable poolable)
                Pool.Return(poolable);
        }

        list.Clear();
        LimitedQueue<object> limitedQueue = GetLimitedQueue(type);
        limitedQueue.Enqueue(list);
    }

    /// <summary>
    /// Returns the items in the provided List to the pool, then clears the List.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static void ReturnItems<T>(List<T> list)
    {
        foreach (T item in list)
        {
            if (item is IPoolable poolable)
                Pool.Return(poolable);
        }

        list.Clear();
    }

    private static LimitedQueue<object> GetLimitedQueue(Type type)
    {
        if (!s_listPool.TryGetValue(type, out LimitedQueue<object>? limitedQueue))
        {
            limitedQueue = new();
            s_listPool[type] = limitedQueue;
        }

        return limitedQueue;
    }
}
