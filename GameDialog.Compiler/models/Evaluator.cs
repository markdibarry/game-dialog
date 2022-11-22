namespace GameDialog.Compiler;

internal class Evaluator
{
    public Evaluator(DialogScript script)
    {
        _script = script;
    }
    private int[] _expression;
    private int _iterator;
    private DialogScript _script;

    public void Evaluate(int[] arr)
    {
        if (arr == null || arr.Length == 0)
            return;
        _expression = arr;

        switch (GetExpValueType(_iterator))
        {
            case VarType.Float:
                float floaty = EvalFloatExp();
                break;
            case VarType.Bool:
                bool booly = EvalBoolExp();
                break;
            case VarType.String:
                string stringy = EvalStringExp();
                break;
        };
    }

    public bool EvalEquals()
    {
        return GetExpValueType(_iterator) switch
        {
            VarType.Float => EvalFloatExp() == EvalFloatExp(),
            VarType.Bool => EvalBoolExp() == EvalBoolExp(),
            VarType.String => EvalStringExp() == EvalStringExp(),
            _ => throw new NotSupportedException()
        };
    }

    public VarDef GetVar(int index) => _script.Variables[index];

    public T EvalVar<T>()
    {
        VarDef variable = GetVar(_iterator);
        return default;
    }

    public T EvalFunc<T>()
    {
        VarDef variable = GetVar(++_iterator);
        int argNum = _expression[++_iterator];
        if (argNum == 0)
            return default;
        for (int i = 0; i < argNum; i++)
        {
            switch (GetExpValueType(_iterator + 1))
            {
                case VarType.Float:
                    EvalFloatExp();
                    break;
                case VarType.Bool:
                    EvalBoolExp();
                    break;
                case VarType.String:
                    EvalStringExp();
                    break;
            };
        }
        return default;
    }

    public bool EvalBool() => _expression[_iterator++] != 0;

    public bool EvalLess() => EvalFloatExp() < EvalFloatExp();

    public bool EvalGreater() => EvalFloatExp() > EvalFloatExp();

    public bool EvalLessEquals() => EvalFloatExp() <= EvalFloatExp();

    public bool EvalGreaterEquals() => EvalFloatExp() >= EvalFloatExp();

    public bool EvalAnd() => EvalBoolExp() && EvalBoolExp();

    public bool EvalOr() => EvalBoolExp() || EvalBoolExp();

    public bool EvalNot() => !EvalBoolExp();

    public bool EvalBoolExp()
    {
        return (InstructionType)_expression[_iterator++] switch
        {
            InstructionType.Bool => EvalBool(),
            InstructionType.Less => EvalLess(),
            InstructionType.Greater => EvalGreater(),
            InstructionType.LessEquals => EvalLessEquals(),
            InstructionType.GreaterEquals => EvalGreaterEquals(),
            InstructionType.Equals => EvalEquals(),
            InstructionType.NotEquals => !EvalEquals(),
            InstructionType.Not => EvalNot(),
            InstructionType.And => EvalAnd(),
            InstructionType.Or => EvalOr(),
            InstructionType.Var => EvalVar<bool>(),
            InstructionType.Func => EvalFunc<bool>(),
            _ => throw new NotSupportedException()
        };
    }

    public string EvalStringExp()
    {
        return (InstructionType)_expression[_iterator++] switch
        {
            InstructionType.String => EvalString(),
            InstructionType.Var => EvalVar<string>(),
            InstructionType.Func => EvalFunc<string>(),
            _ => throw new NotSupportedException()
        };
    }

    public string EvalString()
    {
        return _script.ExpStrings[_expression[_iterator++]];
    }

    public float EvalFloat() => _script.ExpFloats[_expression[_iterator++]];

    public float EvalMult() => EvalFloatExp() * EvalFloatExp();

    public float EvalDiv() => EvalFloatExp() / EvalFloatExp();

    public float EvalAdd() => EvalFloatExp() + EvalFloatExp();

    public float EvalSub() => EvalFloatExp() - EvalFloatExp();

    public float EvalFloatExp()
    {
        return (InstructionType)_expression[_iterator++] switch
        {
            InstructionType.Float => EvalFloat(),
            InstructionType.Mult => EvalMult(),
            InstructionType.Div => EvalDiv(),
            InstructionType.Add => EvalAdd(),
            InstructionType.Sub => EvalSub(),
            InstructionType.Var => EvalVar<float>(),
            InstructionType.Func => EvalFunc<float>(),
            _ => throw new NotSupportedException()
        };
    }

    public VarType GetExpValueType(int index)
    {
        return (InstructionType)_expression[index] switch
        {
            InstructionType.String => VarType.String,
            InstructionType.Float or
            InstructionType.Mult or
            InstructionType.Div or
            InstructionType.Add or
            InstructionType.Sub => VarType.Float,
            InstructionType.Bool or
            InstructionType.Less or
            InstructionType.Greater or
            InstructionType.LessEquals or
            InstructionType.GreaterEquals or
            InstructionType.Equals or
            InstructionType.NotEquals or
            InstructionType.Not or
            InstructionType.And or
            InstructionType.Or => VarType.Bool,
            InstructionType.Var or
            InstructionType.Func => _script.Variables[_expression[index + 1]].Type,
            _ => VarType.Undefined
        };
    }
}
