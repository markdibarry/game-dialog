namespace GameDialog.Compiler;

public partial class ExpressionVisitor
{
    public override VarType VisitConstFloat(DialogParser.ConstFloatContext context)
    {
        int index = _dialogScript.InstFloats.GetOrAdd(float.Parse(context.FLOAT().GetText()));
        PushExp([(int)VarType.Float, index], default);
        return VarType.Float;
    }

    public override VarType VisitConstString(DialogParser.ConstStringContext context)
    {
        string value = context.STRING().GetText()[1..^1];
        int index = _dialogScript.InstStrings.GetOrAdd(value);
        PushExp([(int)VarType.String, index], default);
        return VarType.String;
    }

    public override VarType VisitConstBool(DialogParser.ConstBoolContext context)
    {
        int val = context.BOOL().GetText() == "true" ? 1 : 0;
        PushExp([(int)VarType.Bool, val], default);
        return VarType.Bool;
    }

    public override VarType VisitConstVar(DialogParser.ConstVarContext context)
    {
        string varName = context.NAME().GetText();
        VarDef? varDef = _memberRegister.VarDefs.FirstOrDefault(x => x.Name == varName);

        if (varDef == null)
        {
            _diagnostics.Add(context.GetError("Variables must be defined before use."));
            return VarType.Undefined;
        }

        // nameIndex shouldn't be -1 here.
        int nameIndex = _dialogScript.InstStrings.GetOrAdd(varName);

        // Should never happen?
        if (varDef.Type == VarType.Undefined)
        {
            _diagnostics.Add(context.GetError("Type Error: Cannot infer expression result type."));
        }

        PushExp([(int)OpCode.Var, nameIndex], varDef.Type);
        return varDef.Type;
    }

    public override VarType VisitFunction(DialogParser.FunctionContext context)
    {
        string funcName = context.NAME().GetText();
        var funcDefs = _memberRegister.FuncDefs.Where(x => x.Name == funcName);

        if (!funcDefs.Any())
        {
            _diagnostics.Add(context.GetError("Function not found: Functions must be defined in the Dialog Bridge before use."));
            return VarType.Undefined;
        }

        VarType returnType = funcDefs.First().ReturnType;
        int argsFound = context.expression().Length;
        int nameIndex = _dialogScript.InstStrings.GetOrAdd(funcName);

        PushExp([(int)OpCode.Func, nameIndex, argsFound], default);
        List<VarType> argTypesFound = [];

        for (int i = 0; i < argsFound; i++)
        {
            var exp = context.expression()[i];
            argTypesFound.Add(Visit(exp));
        }

        FuncDef? funcDef = FindMatchingFuncDef(funcName, returnType, argTypesFound);

        if (funcDef == null)
        {
            _diagnostics.Add(context.GetError("Function not found: Functions must be defined in the Dialog Bridge before use."));
            return VarType.Undefined;
        }

        return returnType;
    }

    private FuncDef? FindMatchingFuncDef(string name, VarType returnType, List<VarType> argTypes)
    {
        return _memberRegister.FuncDefs.FirstOrDefault(FuncDefMatches);

        bool FuncDefMatches(FuncDef funcDef)
        {
            if (funcDef.Name != name
                || funcDef.ReturnType != returnType
                || argTypes.Count > funcDef.ArgTypes.Count)
            {
                return false;
            }

            for (var i = 0; i < funcDef.ArgTypes.Count; i++)
            {
                // if match but has more parameters, check if optional
                if (i >= argTypes.Count)
                    return funcDef.ArgTypes.Skip(argTypes.Count).All(y => y.IsOptional);

                if (argTypes[i] != funcDef.ArgTypes[i].Type)
                    return false;
            }
            return true;
        }
    }

}
