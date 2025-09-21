using System;
using System.Buffers;
using System.Collections.Generic;
using GameDialog.Common;

namespace GameDialog.Runner;

public partial class DialogBase
{
    /// <summary>
    /// Default index of beginning of instructions.
    /// </summary>
    private const int DefaultStartIndex = 2;

    private VarType GetReturnType(ushort[] array, int startIndex = DefaultStartIndex)
    {
        StateSpan<ushort> span = new(array, startIndex);
        return GetReturnType(ref span);
    }

    private VarType GetReturnType(ref StateSpan<ushort> span)
    {
        return span.Current switch
        {
            OpCode.String or
            OpCode.Concat => VarType.String,
            OpCode.Float or
            OpCode.Mult or
            OpCode.Div or
            OpCode.Add or
            OpCode.Sub => VarType.Float,
            OpCode.Bool or
            OpCode.Less or
            OpCode.Greater or
            OpCode.LessEquals or
            OpCode.GreaterEquals or
            OpCode.Equal or
            OpCode.NotEquals or
            OpCode.Not => VarType.Bool,
            OpCode.Func => GetFuncReturnType(ref span),
            OpCode.Var => GetVarType(ref span),
            OpCode.Assign or
            OpCode.MultAssign or
            OpCode.DivAssign or
            OpCode.AddAssign or
            OpCode.SubAssign or
            OpCode.ConcatAssign => VarType.Void,
            _ => default
        };

        VarType GetVarType(ref StateSpan<ushort> span)
        {
            ushort nameIndex = span[span.Index + 1];
            string varName = Strings[nameIndex];
            VarType varType = GetPredefinedPropertyType(varName);

            if (varType != VarType.Undefined)
                return varType;

            if (!TextStorage.TryGetValue(varName, out TextVariant tVar))
                return default;

            return tVar.VariantType;
        }

        VarType GetFuncReturnType(ref StateSpan<ushort> span)
        {
            ushort nameIndex = span[span.Index + 1];
            string funcName = Strings[nameIndex];
            return GetPredefinedMethodReturnType(funcName);
        }
    }

    private float GetFloatInstResult(ushort[] array)
    {
        StateSpan<ushort> span = new(array, DefaultStartIndex);
        return EvalFloatExp(ref span);
    }

    private bool GetBoolInstResult(ushort[] array)
    {
        StateSpan<ushort> span = new(array, DefaultStartIndex);
        return EvalBoolExp(ref span);
    }

    private string GetStringInstResult(ushort[] array)
    {
        StateSpan<ushort> span = new(array, DefaultStartIndex);
        return EvalStringExp(ref span);
    }

    private void EvalVoidExp(ushort[] array)
    {
        StateSpan<ushort> span = new(array, DefaultStartIndex);
        ushort opCode = span.Read();

        switch (opCode)
        {
            case OpCode.Assign:
                EvalAssign(ref span);
                break;
            case OpCode.MultAssign:
            case OpCode.DivAssign:
            case OpCode.AddAssign:
            case OpCode.SubAssign:
                EvalMathAssign(ref span, opCode);
                break;
            case OpCode.ConcatAssign:
                EvalConcatAssign(ref span);
                break;
            case OpCode.Func:
                EvalFunc(ref span);
                break;
        }

        void EvalAssign(ref StateSpan<ushort> span)
        {
            string varName = Strings[span.Read()];
            VarType varType = GetPredefinedPropertyType(varName);

            if (varType != VarType.Undefined)
            {
                TextVariant variant = varType switch
                {
                    VarType.Float => new(EvalFloatExp(ref span)),
                    VarType.Bool => new(EvalBoolExp(ref span)),
                    VarType.String => new(EvalStringExp(ref span)),
                    _ => throw new System.Exception("Unknown Variant type")
                };
                SetPredefinedProperty(varName, variant);
            }
            else
            {
                switch (GetReturnType(ref span))
                {
                    case VarType.Float:
                        TextStorage.SetValue(varName, EvalFloatExp(ref span));
                        break;
                    case VarType.Bool:
                        TextStorage.SetValue(varName, EvalBoolExp(ref span));
                        break;
                    case VarType.String:
                        TextStorage.SetValue(varName, EvalStringExp(ref span));
                        break;
                }
            }
        }

        void EvalMathAssign(ref StateSpan<ushort> span, ushort instructionType)
        {
            string varName = Strings[span.Read()];
            VarType varType = GetPredefinedPropertyType(varName);

            if (varType != VarType.Undefined)
            {
                float originalValue = GetPredefinedProperty(varName).Float;
                float result = GetOpResult(ref span, instructionType, originalValue);
                SetPredefinedProperty(varName, new(result));
            }
            else if (TextStorage != null)
            {
                if (!TextStorage.TryGetValue(varName, out float originalValue))
                    return;

                TextStorage.SetValue(varName, GetOpResult(ref span, instructionType, originalValue));
            }

            float GetOpResult(ref StateSpan<ushort> span, ushort instructionType, float originalValue)
            {
                return instructionType switch
                {
                    OpCode.AddAssign => originalValue + EvalFloatExp(ref span),
                    OpCode.SubAssign => originalValue - EvalFloatExp(ref span),
                    OpCode.MultAssign => originalValue * EvalFloatExp(ref span),
                    OpCode.DivAssign => originalValue / EvalFloatExp(ref span),
                    _ => throw new System.Exception("Operator is invalid.")
                };
            }
        }

        void EvalConcatAssign(ref StateSpan<ushort> span)
        {
            string varName = Strings[span.Read()];
            VarType varType = GetPredefinedPropertyType(varName);
            string result = string.Empty;
            VarType nextType = GetReturnType(ref span);

            if (nextType == VarType.String)
                result = EvalStringExp(ref span);
            else if (nextType == VarType.Float)
                result = EvalFloatExp(ref span).ToString();

            if (varType != VarType.Undefined)
            {
                string originalValue = GetPredefinedProperty(varName).String ?? string.Empty;
                SetPredefinedProperty(varName, new(originalValue + result));
            }
            else if (TextStorage != null)
            {
                if (!TextStorage.TryGetValue(varName, out string? originalValue))
                    originalValue = string.Empty;

                TextStorage.SetValue(varName, originalValue + result);
            }
        }
    }

    private bool EvalBoolExp(ref StateSpan<ushort> span)
    {
        ushort opCode = span.Read();

        return opCode switch
        {
            OpCode.Bool => EvalBool(ref span),
            OpCode.Less => EvalLess(ref span),
            OpCode.Greater => EvalGreater(ref span),
            OpCode.LessEquals => EvalLessEquals(ref span),
            OpCode.GreaterEquals => EvalGreaterEquals(ref span),
            OpCode.Equal => EvalEquals(ref span),
            OpCode.NotEquals => !EvalEquals(ref span),
            OpCode.Not => EvalNot(ref span),
            OpCode.And => EvalAnd(ref span),
            OpCode.Or => EvalOr(ref span),
            OpCode.Var => EvalVar<bool>(ref span),
            OpCode.Func => EvalFunc(ref span).Get<bool>(),
            _ => default
        };

        bool EvalBool(ref StateSpan<ushort> span) => span.Read() == 1;

        bool EvalEquals(ref StateSpan<ushort> span)
        {
            return GetReturnType(ref span) switch
            {
                VarType.Float => EvalFloatExp(ref span) == EvalFloatExp(ref span),
                VarType.Bool => EvalBoolExp(ref span) == EvalBoolExp(ref span),
                VarType.String => EvalStringExp(ref span) == EvalStringExp(ref span),
                _ => default
            };
        }

        bool EvalLess(ref StateSpan<ushort> span) => EvalFloatExp(ref span) < EvalFloatExp(ref span);

        bool EvalGreater(ref StateSpan<ushort> span) => EvalFloatExp(ref span) > EvalFloatExp(ref span);

        bool EvalLessEquals(ref StateSpan<ushort> span) => EvalFloatExp(ref span) <= EvalFloatExp(ref span);

        bool EvalGreaterEquals(ref StateSpan<ushort> span) => EvalFloatExp(ref span) >= EvalFloatExp(ref span);

        bool EvalAnd(ref StateSpan<ushort> span) => EvalBoolExp(ref span) && EvalBoolExp(ref span);

        bool EvalOr(ref StateSpan<ushort> span) => EvalBoolExp(ref span) || EvalBoolExp(ref span);

        bool EvalNot(ref StateSpan<ushort> span) => !EvalBoolExp(ref span);
    }

    private string EvalStringExp(ref StateSpan<ushort> span)
    {
        return span.Read() switch
        {
            OpCode.String => Strings[span.Read()],
            OpCode.Concat => EvalConcat(ref span),
            OpCode.Var => EvalVar<string>(ref span) ?? string.Empty,
            OpCode.Func => EvalFunc(ref span).Get<string>() ?? string.Empty,
            _ => string.Empty
        };

        string EvalConcat(ref StateSpan<ushort> span)
        {
            string result = EvalStringExp(ref span);
            VarType nextType = GetReturnType(ref span);

            if (nextType == VarType.String)
                result += EvalStringExp(ref span);
            else if (nextType == VarType.Float)
                result += EvalFloatExp(ref span).ToString();

            return result;
        }
    }

    private float EvalFloatExp(ref StateSpan<ushort> span)
    {
        return span.Read() switch
        {
            OpCode.Float => EvalFloat(ref span),
            OpCode.Mult => EvalMult(ref span),
            OpCode.Div => EvalDiv(ref span),
            OpCode.Add => EvalAdd(ref span),
            OpCode.Sub => EvalSub(ref span),
            OpCode.Var => EvalVar<float>(ref span),
            OpCode.Func => EvalFunc(ref span).Get<float>(),
            _ => default
        };

        float EvalFloat(ref StateSpan<ushort> span) => Floats[span.Read()];

        float EvalMult(ref StateSpan<ushort> span) => EvalFloatExp(ref span) * EvalFloatExp(ref span);

        float EvalDiv(ref StateSpan<ushort> span) => EvalFloatExp(ref span) / EvalFloatExp(ref span);

        float EvalAdd(ref StateSpan<ushort> span) => EvalFloatExp(ref span) + EvalFloatExp(ref span);

        float EvalSub(ref StateSpan<ushort> span) => EvalFloatExp(ref span) - EvalFloatExp(ref span);
    }

    private T? EvalVar<T>(ref StateSpan<ushort> span)
    {
        string varName = Strings[span.Read()];

        if (GetPredefinedPropertyType(varName) != VarType.Undefined)
            return GetPredefinedProperty(varName).Get<T>();

        if (TextStorage.TryGetValue(varName, out T? tVar))
            return tVar;

        return default;
    }

    private TextVariant EvalFunc(ref StateSpan<ushort> span)
    {
        string funcName = Strings[span.Read()];
        bool isAwait = span.Read() == 1;
        int argNum = span.Read();

        if (argNum == 0)
            return CallPredefinedMethod(funcName, []);

        TextVariant[] args = ArrayPool<TextVariant>.Shared.Rent(argNum);

        for (int i = 0; i < argNum; i++)
        {
            VarType argType = GetReturnType(ref span);
            args[i] = argType switch
            {
                VarType.Float => new(EvalFloatExp(ref span)),
                VarType.Bool => new(EvalBoolExp(ref span)),
                VarType.String => new(EvalStringExp(ref span)),
                _ => throw new System.Exception("Unknown Variant type")
            };
        }

        TextVariant result = CallPredefinedMethod(funcName, args);
        ArrayPool<TextVariant>.Shared.Return(args, true);
        return result;
    }

    private ushort GetConditionResult(ushort[] array)
    {
        StateSpan<ushort> span = new(array, 1);
        ushort result = span.Read();

        while (!span.IsAtEnd && span.Current != OpCode.Undefined)
        {
            if (EvalBoolExp(ref span))
                return span.Current;

            span.MoveNext();
        }

        return result;
    }

    private void GetHashResult(StateSpan<ushort> span, Dictionary<string, string> hashCollection)
    {
        while (!span.IsAtEnd)
        {
            int valueType = span.Read();

            if (valueType == 0)
                break;

            string key = Strings[span.Read()];
            string value = string.Empty;

            if (valueType == 2)
            {
                VarType returnType = GetReturnType(ref span);

                value = returnType switch
                {
                    VarType.String => EvalStringExp(ref span),
                    VarType.Float => EvalFloatExp(ref span).ToString(),
                    VarType.Bool => EvalBoolExp(ref span).ToString(),
                    _ => string.Empty
                };
            }

            hashCollection.Add(key, value);
        }
    }

    private void GetChoices(ushort[] array, List<Choice> choices)
    {
        StateSpan<ushort> span = new(array, 1);

        while (span.Index < span.Length)
        {
            ushort op = span.Read();

            if (op == ChoiceOp.Undefined)
                break;

            if (op == ChoiceOp.Choice)
                AddChoice(ref span, choices, false);
            else
                AddCondChoices(ref span, choices, false);
        }

        void AddChoice(ref StateSpan<ushort> span, List<Choice> choices, bool isDisabled)
        {
            int next = span.Read();
            string text = Strings[span.Read()];
            text = Tr(text);
            text = GetEventParsedText(text, text, null, this);
            choices.Add(new(next, text, isDisabled));
        }

        void AddCondChoices(ref StateSpan<ushort> span, List<Choice> choices, bool outerDisabled)
        {
            bool condMet = GetBoolResult(ref span, outerDisabled);
            bool isDisabled = !condMet;
            ushort op = span.Read();

            while (op != ChoiceOp.EndIf)
            {
                switch (op)
                {
                    case ChoiceOp.Choice:
                        AddChoice(ref span, choices, isDisabled);
                        break;
                    case ChoiceOp.If:
                        AddCondChoices(ref span, choices, isDisabled);
                        break;
                    case ChoiceOp.ElseIf:
                        condMet = condMet || GetBoolResult(ref span, outerDisabled);
                        isDisabled = !condMet || !isDisabled;
                        break;
                    case ChoiceOp.Else:
                        condMet = true;
                        isDisabled = !isDisabled;
                        break;
                    default:
                        throw new System.Exception("Choices are invalid.");
                }

                op = span.Read();
            }

            bool GetBoolResult(ref StateSpan<ushort> span, bool isDisabled)
            {
                ushort[] expIndex = Instructions[span.Read()];
                return !isDisabled && GetBoolInstResult(expIndex);
            }
        }
    }

    /// <summary>
    /// Gets the VarType of a predefined method.
    /// </summary>
    /// <param name="funcName"></param>
    /// <returns></returns>
    protected virtual VarType GetPredefinedMethodReturnType(ReadOnlySpan<char> funcName) => VarType.Undefined;
    /// <summary>
    /// Gets the VarType of a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    protected virtual VarType GetPredefinedPropertyType(ReadOnlySpan<char> propertyName) => new();
    /// <summary>
    /// Calls a predefined method.
    /// </summary>
    /// <param name="funcName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    protected virtual TextVariant CallPredefinedMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args) => new();
    /// <summary>
    /// Gets a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    protected virtual TextVariant GetPredefinedProperty(ReadOnlySpan<char> propertyName) => new();
    /// <summary>
    /// Sets a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    protected virtual void SetPredefinedProperty(ReadOnlySpan<char> propertyName, TextVariant value) { }
}
