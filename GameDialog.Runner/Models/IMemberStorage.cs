using System;

namespace GameDialog.Runner;

public interface IMemberStorage
{
    bool TryGetVariable(ReadOnlySpan<char> varName, out TextVariant value);
    VarType GetVariableType(ReadOnlySpan<char> varName);
    void SetVariable(ReadOnlySpan<char> varName, TextVariant value);
    VarType GetMethodReturnType(ReadOnlySpan<char> methodName);
    TextVariant CallMethod(ReadOnlySpan<char> methodName, ReadOnlySpan<TextVariant> args);
    TextVariant CallAsyncMethod(ReadOnlySpan<char> methodName, ReadOnlySpan<TextVariant> args);
}