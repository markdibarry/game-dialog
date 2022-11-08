using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor : DialogParserBaseVisitor<VarType>
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
        int index = AddVarName(context.NAME().GetText());
        VarType type = _dialogScript.Variables[index].Type;
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
        int index = _dialogScript.Variables.FindIndex(x => x.Name == varName);
        if (context.expression() == null)
        {
            PushExp(new[] { (int)ExpType.Func, index, 0 }, default);
            return VarType.Undefined;
        }

        PushExp(new[] { (int)ExpType.Func, index, context.expression().Length }, default);
        // TODO check each param type against registered func
        foreach (var exp in context.expression())
            Visit(exp);
        return VarType.Undefined;
    }

    private int AddVarName(string varName)
    {
        int index = _dialogScript.Variables.FindIndex(x => x.Name == varName);
        if (index == -1)
        {
            _dialogScript.Variables.Add(new(varName));
            index = _dialogScript.Variables.Count - 1;
        }
        return index;
    }

    private bool CheckFuncRegister(string varName)
    {
        return false;
    }
}
