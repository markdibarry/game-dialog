using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public partial class ExpressionVisitor
{
    public override VarType VisitConstFloat(DialogParser.ConstFloatContext context)
    {
        _dialogScript.InstFloats.Add(float.Parse(context.FLOAT().GetText()));
        PushExp(new[] { (int)VarType.Float, _dialogScript.InstFloats.Count - 1 }, default);
        return VarType.Float;
    }

    public override VarType VisitConstString(DialogParser.ConstStringContext context)
    {
        _dialogScript.InstStrings.Add(context.STRING().GetText());
        PushExp(new[] { (int)VarType.String, _dialogScript.InstStrings.Count - 1 }, default);
        return VarType.String;
    }

    public override VarType VisitConstBool(DialogParser.ConstBoolContext context)
    {
        int val = context.BOOL().GetText() == "true" ? 1 : 0;
        PushExp(new[] { (int)VarType.Bool, val }, default);
        return VarType.Bool;
    }

    public override VarType VisitConstVar(DialogParser.ConstVarContext context)
    {
        string varName = context.NAME().GetText();
        VarDef? varDef = _memberRegister.VarDefs.FirstOrDefault(x => x.Name == varName);
        if (varDef == null)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Variables must be defined before use.",
                Severity = DiagnosticSeverity.Error,
            });
            return VarType.Undefined;
        }
        // nameIndex shouldn't be -1 here.
        int nameIndex = _dialogScript.InstStrings.IndexOf(varName);
        // Should never happen?
        if (varDef.Type == VarType.Undefined)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Type Error: Cannot infer expression result type.",
                Severity = DiagnosticSeverity.Error,
            });
        }
        PushExp(new[] { (int)InstructionType.Var, nameIndex }, varDef.Type);
        return varDef.Type;
    }

    public override VarType VisitFunction(DialogParser.FunctionContext context)
    {
        string funcName = context.NAME().GetText();
        var funcDefs = _memberRegister.FuncDefs.Where(x => x.Name == funcName);
        if (!funcDefs.Any())
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Function not found: Functions must be defined in the Dialog Bridge before use.",
                Severity = DiagnosticSeverity.Error,
            });
            return VarType.Undefined;
        }
        VarType returnType = funcDefs.First().ReturnType;
        int argsFound = context.expression().Length;
        int nameIndex = _dialogScript.InstStrings.IndexOf(funcName);
        if (nameIndex == -1)
        {
            _dialogScript.InstStrings.Add(funcName);
            nameIndex = _dialogScript.InstStrings.Count - 1;
        }

        PushExp(new[] { (int)InstructionType.Func, nameIndex, argsFound }, default);
        List<VarType> argTypesFound = new();
        for (int i = 0; i < argsFound; i++)
        {
            var exp = context.expression()[i];
            argTypesFound.Add(Visit(exp));
        }
        FuncDef? funcDef = FindMatchingFuncDef(funcName, returnType, argTypesFound);
        if (funcDef == null)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Function not found: Functions must be defined in the Dialog Bridge before use.",
                Severity = DiagnosticSeverity.Error,
            });
            return VarType.Undefined;
        }
        return returnType;
    }

    private FuncDef? FindMatchingFuncDef(string name, VarType returnType, List<VarType> argTypes)
    {
        return _memberRegister.FuncDefs.Where(funcDef =>
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
        }).FirstOrDefault();
    }

}
