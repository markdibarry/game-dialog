using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Runner;

public class DialogStorage : IMemberStorage
{
    public DialogStorage(DialogBridgeBase dialogBridge)
    {
        _dialogBridge = dialogBridge;
        _storage = [];
    }

    private readonly DialogBridgeBase _dialogBridge;
    private readonly Dictionary<string, TextVariant> _storage;
    private Dictionary<string, TextVariant>.AlternateLookup<ReadOnlySpan<char>> AltStorageLookup
    {
        get => _storage.GetAlternateLookup<ReadOnlySpan<char>>();
    }
    private static Dictionary<string, FuncDef>.AlternateLookup<ReadOnlySpan<char>> AltFuncDefLookup
    {
        get => DialogBridgeBase.InternalFuncDefs.GetAlternateLookup<ReadOnlySpan<char>>();
    }
    private static Dictionary<string, VarDef>.AlternateLookup<ReadOnlySpan<char>> AltVarDefLookup
    {
        get => DialogBridgeBase.InternalVarDefs.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public void ClearLocalStorage() => _storage.Clear();

    public bool Contains(string key) => _storage.ContainsKey(key);

    public VarType GetVariableType(ReadOnlySpan<char> varName)
    {
        if (AltVarDefLookup.TryGetValue(varName, out VarDef varDef))
            return varDef.Type;
        else if (TryGetVariant(varName, out TextVariant value))
            return value.VariantType;

        return VarType.Undefined;
    }

    public bool TryGetVariant(ReadOnlySpan<char> key, [NotNullWhen(true)] out TextVariant value)
    {
        if (AltVarDefLookup.ContainsKey(key))
            value = _dialogBridge.InternalGetProperty(key);
        else if (!AltStorageLookup.TryGetValue(key, out value))
            return false;

        return true;
    }

    public bool TryGetValue<T>(ReadOnlySpan<char> key, [NotNullWhen(true)] out T? value)
    {
        value = default;

        if (!TryGetVariant(key, out TextVariant variant))
            return false;

        if (!variant.TryGetValue(out value))
            return false;

        return true;
    }

    public void SetValue(ReadOnlySpan<char> key, TextVariant value)
    {
        if (AltVarDefLookup.TryGetValue(key, out VarDef varDef))
        {
            if (varDef.Type == value.VariantType)
                _dialogBridge.InternalSetProperty(key, value);
        }
        else
        {
            var lookup = AltStorageLookup;
            lookup[key] = value;
        }
    }

    public void SetValue(ReadOnlySpan<char> key, string value) => SetValue(key, new TextVariant(value));

    public void SetValue(ReadOnlySpan<char> key, float value) => SetValue(key, new TextVariant(value));

    public void SetValue(ReadOnlySpan<char> key, bool value) => SetValue(key, new TextVariant(value));

    public VarType GetMethodReturnType(ReadOnlySpan<char> methodName)
    {
        if (!AltFuncDefLookup.TryGetValue(methodName, out FuncDef? funcDef))
            return VarType.Undefined;

        return funcDef.ReturnType;
    }

    public FuncDef? GetMethodFuncDef(ReadOnlySpan<char> methodName)
    {
        if (AltFuncDefLookup.TryGetValue(methodName, out FuncDef? funcDef))
            return funcDef;

        return null;
    }

    public TextVariant CallMethod(ReadOnlySpan<char> methodName, ReadOnlySpan<TextVariant> args)
    {
        return _dialogBridge.InternalCallMethod(methodName, args);
    }

    public TextVariant CallAsyncMethod(ReadOnlySpan<char> methodName, ReadOnlySpan<TextVariant> args)
    {
        _dialogBridge.InternalCallAsyncMethod(methodName, args);
        return new();
    }
}
