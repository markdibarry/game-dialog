using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public partial class ExpressionVisitor
{
    public override VarType VisitConstFloat(DialogParser.ConstFloatContext context)
    {
        _dialogScript.ExpFloats.Add(float.Parse(context.FLOAT().GetText()));
        PushExp(new[] { (int)VarType.Float, _dialogScript.ExpFloats.Count - 1 }, default);
        return VarType.Float;
    }

    public override VarType VisitConstString(DialogParser.ConstStringContext context)
    {
        _dialogScript.ExpStrings.Add(context.STRING().GetText());
        PushExp(new[] { (int)VarType.String, _dialogScript.ExpStrings.Count - 1 }, default);
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
        int index = _dialogScript.Variables.FindIndex(x => x.Name == varName);
        if (index == -1)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Variables must be defined before use.",
                Severity = DiagnosticSeverity.Error,
            });
            return VarType.Undefined;
        }
        VarType type = _dialogScript.Variables[index].Type;
        // Should never happen?
        if (type == VarType.Undefined)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Type Error: Cannot infer expression result type.",
                Severity = DiagnosticSeverity.Error,
            });
        }
        PushExp(new[] { (int)ExpType.Var, index }, type);
        return type;
    }

    public override VarType VisitFunction(DialogParser.FunctionContext context)
    {
        string funcName = context.NAME().GetText();
        int index = _dialogScript.Variables.FindIndex(x => x.Name == funcName);
        if (index == -1)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"{funcName} must be defined in order to be used in dialog.",
                Severity = DiagnosticSeverity.Error,
            });
            return VarType.Undefined;
        }
        FuncDef funcDef = _memberRegister.FuncDefs.First(x => x.Name == funcName);
        int argsFound = context.expression() != null ? context.expression().Length : 0;
        if (funcDef.Args.Count != argsFound)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"{funcName} expects {funcDef.Args.Count} arguments, but received {argsFound}.",
                Severity = DiagnosticSeverity.Error,
            });
            return funcDef.Type;
        }
        // If no expressions, push function with no parameters
        if (context.expression() == null)
        {
            PushExp(new[] { (int)ExpType.Func, index, 0 }, default);
            return funcDef.Type;
        }
        PushExp(new[] { (int)ExpType.Func, index, context.expression().Length }, default);
        for (int i = 0; i < context.expression().Length; i++)
        {
            var exp = context.expression()[i];
            VarType expType = Visit(exp);
            if (expType != funcDef.Args[i].Type)
            {
                _diagnostics.Add(new()
                {
                    Range = exp.GetRange(),
                    Message = $"Argument {i} invalid: cannot convert {expType} to {funcDef.Args[i].Type}.",
                    Severity = DiagnosticSeverity.Error,
                });
            }
        }

        return funcDef.Type;
    }
}
