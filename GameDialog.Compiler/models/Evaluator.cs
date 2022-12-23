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

    //public VarDef GetVar(int index) => _script.Variables[index];

    public T EvalVar<T>()
    {
        //VarDef variable = GetVar(_iterator);
        return default;
    }

    public T EvalFunc<T>()
    {
        //VarDef variable = GetVar(++_iterator);
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
        return (OpCode)_expression[_iterator++] switch
        {
            OpCode.Bool => EvalBool(),
            OpCode.Less => EvalLess(),
            OpCode.Greater => EvalGreater(),
            OpCode.LessEquals => EvalLessEquals(),
            OpCode.GreaterEquals => EvalGreaterEquals(),
            OpCode.Equals => EvalEquals(),
            OpCode.NotEquals => !EvalEquals(),
            OpCode.Not => EvalNot(),
            OpCode.And => EvalAnd(),
            OpCode.Or => EvalOr(),
            OpCode.Var => EvalVar<bool>(),
            OpCode.Func => EvalFunc<bool>(),
            _ => throw new NotSupportedException()
        };
    }

    public string EvalStringExp()
    {
        return (OpCode)_expression[_iterator++] switch
        {
            OpCode.String => EvalString(),
            OpCode.Var => EvalVar<string>(),
            OpCode.Func => EvalFunc<string>(),
            _ => throw new NotSupportedException()
        };
    }

    public string EvalString()
    {
        return _script.InstStrings[_expression[_iterator++]];
    }

    public float EvalFloat() => _script.InstFloats[_expression[_iterator++]];

    public float EvalMult() => EvalFloatExp() * EvalFloatExp();

    public float EvalDiv() => EvalFloatExp() / EvalFloatExp();

    public float EvalAdd() => EvalFloatExp() + EvalFloatExp();

    public float EvalSub() => EvalFloatExp() - EvalFloatExp();

    public float EvalFloatExp()
    {
        return (OpCode)_expression[_iterator++] switch
        {
            OpCode.Float => EvalFloat(),
            OpCode.Mult => EvalMult(),
            OpCode.Div => EvalDiv(),
            OpCode.Add => EvalAdd(),
            OpCode.Sub => EvalSub(),
            OpCode.Var => EvalVar<float>(),
            OpCode.Func => EvalFunc<float>(),
            _ => throw new NotSupportedException()
        };
    }

    public VarType GetExpValueType(int index)
    {
        return (OpCode)_expression[index] switch
        {
            OpCode.String => VarType.String,
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
            OpCode.Equals or
            OpCode.NotEquals or
            OpCode.Not or
            OpCode.And or
            OpCode.Or => VarType.Bool,
            //InstructionType.Var or
            //InstructionType.Func => _script.Variables[_expression[index + 1]].Type,
            _ => VarType.Undefined
        };
    }
}
