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
        int isAwaiting = context.AWAIT()?.GetText().Length > 0 ? 1 : 0;
        string funcName = context.NAME().GetText();
        int argsFound = context.expression().Length;
        int nameIndex = _scriptData.Strings.GetOrAdd(funcName);
        var funcDef = _memberRegister.FuncDefs.FirstOrDefault(x => x.Name == funcName);
        var asyncFuncDef = _memberRegister.AsyncFuncDefs.FirstOrDefault(x => x.Name == funcName);
        VarType returnType = VarType.Undefined;

        if (isAwaiting == 0 && funcDef is not null)
        {
            PushExp([OpCode.Func, nameIndex, argsFound], default);
            returnType = funcDef.ReturnType;
        }
        else if (asyncFuncDef is not null)
        {
            PushExp([OpCode.AsyncFunc, nameIndex, isAwaiting, argsFound], default);
            returnType = VarType.Void;
        }
        else
        {
            _diagnostics.Add(context.GetError($"Method \"{funcName}\" not found: Functions must be defined in the Dialog Bridge before use."));
            return VarType.Undefined;
        }

        List<VarType> argTypesFound = [];

        for (int i = 0; i < argsFound; i++)
        {
            var exp = context.expression()[i];
            argTypesFound.Add(Visit(exp));
        }

        if ((funcDef is not null && !FuncDefMatches(funcDef, argTypesFound))
            || asyncFuncDef is not null && !AsyncFuncDefMatches(asyncFuncDef, argTypesFound))
        {
            _diagnostics.Add(context.GetError($"Method \"{funcName}\" arguments do not match those defined."));
            return VarType.Undefined;
        }

        return returnType;
    }

    private static bool FuncDefMatches(FuncDef funcDef, List<VarType> argTypes)
    {
        if (argTypes.Count != funcDef.ArgTypes.Count)
            return false;

        for (var i = 0; i < funcDef.ArgTypes.Count; i++)
        {
            if (argTypes[i] != funcDef.ArgTypes[i])
                return false;
        }

        return true;
    }

    private static bool AsyncFuncDefMatches(AsyncFuncDef funcDef, List<VarType> argTypes)
    {
        if (argTypes.Count != funcDef.ArgTypes.Count)
            return false;

        for (var i = 0; i < funcDef.ArgTypes.Count; i++)
        {
            if (argTypes[i] != funcDef.ArgTypes[i])
                return false;
        }

        return true;
    }
}
