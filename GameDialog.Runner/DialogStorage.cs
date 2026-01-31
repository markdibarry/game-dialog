using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Runner;

/// <summary>
/// A class for storage and retrieval of dialog variables.
/// </summary>
public class DialogStorage
{
    internal DialogStorage(DialogBridge dialogBridge)
    {
        DialogBridge = dialogBridge;
        _storage = [];
    }

    internal DialogBridge DialogBridge { get; }

    private readonly Dictionary<string, TextVariant> _storage;
    private Dictionary<string, TextVariant>.AlternateLookup<ReadOnlySpan<char>> AltStorageLookup
    {
        get => _storage.GetAlternateLookup<ReadOnlySpan<char>>();
    }
    private static Dictionary<string, FuncDef>.AlternateLookup<ReadOnlySpan<char>> AltFuncDefLookup
    {
        get => DialogBridge.FuncDefs.GetAlternateLookup<ReadOnlySpan<char>>();
    }
    private static Dictionary<string, VarDef>.AlternateLookup<ReadOnlySpan<char>> AltVarDefLookup
    {
        get => DialogBridge.VarDefs.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Clears all the local dialog variables.
    /// </summary>
    public void ClearLocalStorage() => _storage.Clear();

    /// <summary>
    /// Determines whether the storage contains an entry with the specified key.
    /// </summary>
    /// <param name="key">The variable key.</param>
    /// <returns>If true, the storage contains an entry with the specified key.</returns>
    public bool ContainsKey(string key) => _storage.ContainsKey(key);

    /// <summary>
    /// Attempts to get the value associated with the specified key in the storage.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found.</param>
    /// <returns>If true, the storage contains an entry with the specified key.</returns>
    public bool TryGetValue<T>(ReadOnlySpan<char> key, [NotNullWhen(true)] out T? value)
    {
        value = default;

        if (!TryGetVariant(key, out TextVariant variant))
            return false;

        if (!variant.TryGetValue(out value))
            return false;

        return true;
    }

    /// <summary>
    /// Adds the specified key and string value to the storage.
    /// </summary>
    /// <param name="key">The key of the entry to add.</param>
    /// <param name="value">The string value of the entry to add.</param>
    public void SetValue(ReadOnlySpan<char> key, string value) => SetVariant(key, new TextVariant(value), false);

    /// <summary>
    /// Adds the specified key and float value to the storage.
    /// </summary>
    /// <param name="key">The key of the entry to add.</param>
    /// <param name="value">The float value of the entry to add.</param>
    public void SetValue(ReadOnlySpan<char> key, float value) => SetVariant(key, new TextVariant(value), false);

    /// <summary>
    /// Adds the specified key and boolean value to the storage.
    /// </summary>
    /// <param name="key">The key of the entry to add.</param>
    /// <param name="value">The boolean value of the entry to add.</param>
    public void SetValue(ReadOnlySpan<char> key, bool value) => SetVariant(key, new TextVariant(value), false);

    internal VarType GetVariableType(ReadOnlySpan<char> varName)
    {
        if (AltVarDefLookup.TryGetValue(varName, out VarDef varDef))
            return varDef.Type;
        else if (TryGetVariant(varName, out TextVariant value))
            return value.VariantType;

        return VarType.Undefined;
    }

    internal bool TryGetVariant(ReadOnlySpan<char> key, [NotNullWhen(true)] out TextVariant value)
    {
        if (AltVarDefLookup.ContainsKey(key))
            value = DialogBridge.GetProperty(key);
        else if (!AltStorageLookup.TryGetValue(key, out value))
            return false;

        return true;
    }

    internal void SetVariant(ReadOnlySpan<char> key, TextVariant value, bool validateOnly)
    {
        if (AltVarDefLookup.TryGetValue(key, out VarDef varDef))
        {
            if (varDef.Type == value.VariantType && !validateOnly)
                DialogBridge.SetProperty(key, value);
        }
        else
        {
            var lookup = AltStorageLookup;
            lookup[key] = value;
        }
    }

    internal static VarType GetMethodReturnType(ReadOnlySpan<char> methodName)
    {
        if (!AltFuncDefLookup.TryGetValue(methodName, out FuncDef? funcDef))
            return VarType.Undefined;

        return funcDef.ReturnType;
    }

    internal static FuncDef? GetMethodFuncDef(ReadOnlySpan<char> methodName)
    {
        if (AltFuncDefLookup.TryGetValue(methodName, out FuncDef? funcDef))
            return funcDef;

        return null;
    }

    internal TextVariant CallMethod(
        ReadOnlySpan<char> methodName,
        ReadOnlySpan<TextVariant> args,
        bool validateOnly)
    {
        if (!validateOnly)
            return DialogBridge.CallMethod(methodName, args);

        FuncDef? funcDef = GetMethodFuncDef(methodName);

        if (funcDef == null)
            return TextVariant.Undefined;

        if (args.Length != funcDef.ArgTypes.Length)
            return TextVariant.Undefined;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].VariantType != funcDef.ArgTypes[i])
                return TextVariant.Undefined;
        }

        return funcDef.ReturnType switch
        {
            VarType.Float => new(0),
            VarType.String => new(string.Empty),
            VarType.Bool => new(false),
            _ => new()
        };
    }

    internal TextVariant CallAsyncMethod(
        ReadOnlySpan<char> methodName,
        ReadOnlySpan<TextVariant> args,
        bool validateOnly)
    {
        if (!validateOnly)
        {
            DialogBridge.CallAsyncMethod(methodName, args);
            return new();
        }

        FuncDef? funcDef = GetMethodFuncDef(methodName);

        if (funcDef == null || !funcDef.Awaitable)
            return TextVariant.Undefined;

        if (args.Length != funcDef.ArgTypes.Length)
            return TextVariant.Undefined;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].VariantType != funcDef.ArgTypes[i])
                return TextVariant.Undefined;
        }

        return new();
    }
}
