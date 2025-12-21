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
        _alternateLookup = _storage.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    private readonly DialogBridgeBase _dialogBridge;
    private readonly Dictionary<string, TextVariant> _storage;
    private readonly Dictionary<string, TextVariant>.AlternateLookup<ReadOnlySpan<char>> _alternateLookup;

    public void ClearLocalStorage() => _storage.Clear();

    public bool Contains(string key) => _storage.ContainsKey(key);

    public VarType GetVariableType(ReadOnlySpan<char> varName)
    {
        VarType varType = _dialogBridge.InternalGetPropertyType(varName);

        if (varType != VarType.Undefined)
            return varType;
        else if (TryGetVariable(varName, out TextVariant value))
            return value.VariantType;

        return VarType.Undefined;
    }

    public bool TryGetVariable(ReadOnlySpan<char> key, [NotNullWhen(true)] out TextVariant value)
    {
        if (_dialogBridge.InternalGetPropertyType(key) != VarType.Undefined)
            value = _dialogBridge.InternalGetProperty(key);
        else if (!_alternateLookup.TryGetValue(key, out value))
            return false;

        return true;
    }

    public bool TryGetVariable<T>(string key, [NotNullWhen(true)] out T? value)
    {
        value = default;

        if (!TryGetVariable(key, out TextVariant variant))
            return false;

        if (!variant.TryGetValue(out value))
            return false;

        return true;
    }

    public void SetVariable(ReadOnlySpan<char> key, TextVariant value)
    {
        if (_dialogBridge.InternalGetPropertyType(key) != VarType.Undefined)
            _dialogBridge.InternalSetProperty(key, value);
        else
            _alternateLookup[key] = value;
    }

    public void SetVariable(ReadOnlySpan<char> key, string value) => SetVariable(key, new TextVariant(value));

    public void SetVariable(ReadOnlySpan<char> key, float value) => SetVariable(key, new TextVariant(value));

    public void SetVariable(ReadOnlySpan<char> key, bool value) => SetVariable(key, new TextVariant(value));

    public VarType GetMethodReturnType(ReadOnlySpan<char> methodName)
    {
        return _dialogBridge.InternalGetMethodReturnType(methodName);
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
