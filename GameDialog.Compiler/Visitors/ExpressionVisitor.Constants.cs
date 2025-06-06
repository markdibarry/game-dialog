using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class ExpressionVisitor
{
    public override VarType VisitConstFloat(ConstFloatContext context)
    {
        int index = _scriptData.Floats.GetOrAdd(float.Parse(context.FLOAT().GetText()));
        PushExp([(int)VarType.Float, index], default);
        return VarType.Float;
    }

    public override VarType VisitConstString(ConstStringContext context)
    {
        string value = context.STRING().GetText()[1..^1];
        int index = _scriptData.Strings.GetOrAdd(value);
        PushExp([(int)VarType.String, index], default);
        return VarType.String;
    }

    public override VarType VisitConstBool(ConstBoolContext context)
    {
        int val = context.BOOL().GetText() == "true" ? 1 : 0;
        PushExp([(int)VarType.Bool, val], default);
        return VarType.Bool;
    }

    public override VarType VisitConstVar(ConstVarContext context)
    {
        string varName = context.NAME().GetText();
        VarDef? varDef = _memberRegister.VarDefs.FirstOrDefault(x => x.Name == varName);

        if (varDef == null)
        {
            _diagnostics.Add(context.GetError($"Variable \"{varName}\" must be defined before use."));
            return VarType.Undefined;
        }

        // nameIndex shouldn't be -1 here.
        int nameIndex = _scriptData.Strings.GetOrAdd(varName);

        // Should never happen?
        if (varDef.Type == VarType.Undefined)
        {
            _diagnostics.Add(context.GetError("Type Error: Cannot infer expression result type."));
        }

        PushExp([OpCode.Var, nameIndex], varDef.Type);
        return varDef.Type;
    }

    public override VarType VisitFunction(FunctionContext context)
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
        int nameIndex = _scriptData.Strings.GetOrAdd(funcName);

        PushExp([OpCode.Func, nameIndex, argsFound], default);
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
