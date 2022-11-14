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
        return (ExpType)_expression[_iterator++] switch
        {
            ExpType.Bool => EvalBool(),
            ExpType.Less => EvalLess(),
            ExpType.Greater => EvalGreater(),
            ExpType.LessEquals => EvalLessEquals(),
            ExpType.GreaterEquals => EvalGreaterEquals(),
            ExpType.Equals => EvalEquals(),
            ExpType.NotEquals => !EvalEquals(),
            ExpType.Not => EvalNot(),
            ExpType.And => EvalAnd(),
            ExpType.Or => EvalOr(),
            ExpType.Var => EvalVar<bool>(),
            ExpType.Func => EvalFunc<bool>(),
            _ => throw new NotSupportedException()
        };
    }

    public string EvalStringExp()
    {
        return (ExpType)_expression[_iterator++] switch
        {
            ExpType.String => EvalString(),
            ExpType.Var => EvalVar<string>(),
            ExpType.Func => EvalFunc<string>(),
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
        return (ExpType)_expression[_iterator++] switch
        {
            ExpType.Float => EvalFloat(),
            ExpType.Mult => EvalMult(),
            ExpType.Div => EvalDiv(),
            ExpType.Add => EvalAdd(),
            ExpType.Sub => EvalSub(),
            ExpType.Var => EvalVar<float>(),
            ExpType.Func => EvalFunc<float>(),
            _ => throw new NotSupportedException()
        };
    }

    public VarType GetExpValueType(int index)
    {
        return (ExpType)_expression[index] switch
        {
            ExpType.String => VarType.String,
            ExpType.Float or
            ExpType.Mult or
            ExpType.Div or
            ExpType.Add or
            ExpType.Sub => VarType.Float,
            ExpType.Bool or
            ExpType.Less or
            ExpType.Greater or
            ExpType.LessEquals or
            ExpType.GreaterEquals or
            ExpType.Equals or
            ExpType.NotEquals or
            ExpType.Not or
            ExpType.And or
            ExpType.Or => VarType.Bool,
            ExpType.Var or
            ExpType.Func => _script.Variables[_expression[index + 1]].Type,
            _ => VarType.Undefined
        };
    }
}
