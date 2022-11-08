using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor : DialogParserBaseVisitor<VarType>
{
    private bool _expWalkStarted;
    private static readonly Dictionary<int, ExpType> InstructionLookup = new()
        {
            { DialogLexer.OP_MULT, ExpType.Mult },
            { DialogLexer.OP_DIVIDE, ExpType.Div },
            { DialogLexer.OP_ADD, ExpType.Add },
            { DialogLexer.OP_SUB, ExpType.Sub },
            { DialogLexer.OP_LESS_EQUALS, ExpType.LessEquals },
            { DialogLexer.OP_GREATER_EQUALS, ExpType.GreaterEquals },
            { DialogLexer.OP_LESS, ExpType.Less },
            { DialogLexer.OP_GREATER, ExpType.Greater },
            { DialogLexer.OP_EQUALS, ExpType.Equals },
            { DialogLexer.OP_NOT_EQUALS, ExpType.NotEquals },
            { DialogLexer.OP_AND, ExpType.And },
            { DialogLexer.OP_OR, ExpType.Or },
            { DialogLexer.OP_NOT, ExpType.Not },
            { DialogLexer.OP_ASSIGN, ExpType.Assign },
            { DialogLexer.OP_MULT_ASSIGN, ExpType.MultAssign },
            { DialogLexer.OP_DIVIDE_ASSIGN, ExpType.DivAssign },
            { DialogLexer.OP_ADD_ASSIGN, ExpType.AddAssign },
            { DialogLexer.OP_SUB_ASSIGN, ExpType.SubAssign }
        };

    public override VarType VisitAssignment([NotNull] DialogParser.AssignmentContext context)
    {
        // Start assignment expression
        int varIndex = AddVarName(context.NAME().GetText());
        Variable variable = _dialogScript.Variables[varIndex];
        VarType newType = PushExp(
            new[] { (int)InstructionLookup[context.op.Type], (int)ExpType.Var, varIndex },
            variable.Type,
            context.right);
        if (variable.Type == VarType.Undefined)
            variable.Type = newType;

        return VarType.Undefined;
    }

    public override VarType VisitExpMultDiv(DialogParser.ExpMultDivContext context)
    {
        PushExp(context.op, VarType.Float, context.left, context.right);
        return VarType.Float;
    }

    public override VarType VisitExpAddSub(DialogParser.ExpAddSubContext context)
    {
        PushExp(context.op, VarType.Float, context.left, context.right);
        return VarType.Float;
    }

    public override VarType VisitExpComp(DialogParser.ExpCompContext context)
    {
        PushExp(context.op, VarType.Float, context.left, context.right);
        return VarType.Bool;
    }

    public override VarType VisitExpNot([NotNull] DialogParser.ExpNotContext context)
    {
        PushExp(context.op, VarType.Bool, context.right);
        return VarType.Bool;
    }

    public override VarType VisitExpEqual(DialogParser.ExpEqualContext context)
    {
        PushExp(context.op, default, context.left, context.right);
        return VarType.Bool;
    }

    private VarType PushExp(IToken op, VarType checkType, params ParserRuleContext[] exps)
    {
        return PushExp(new[] { (int)InstructionLookup[op.Type] }, checkType, exps);
    }

    private VarType PushExp(int[] values, VarType expectedType, params ParserRuleContext[] exps)
    {
        bool isTopExp = false;
        if (!_expWalkStarted)
        {
            isTopExp = true;
            _expWalkStarted = true;
        }

        foreach (int val in values)
            _currentExp.Add(val);

        foreach (var exp in exps)
        {
            // Visit inner expression
            VarType resultType = Visit(exp);
            if (resultType == VarType.Undefined)
            {
                _diagnostics.Add(new()
                {
                    Range = exp.GetRange(),
                    Message = $"Type Error: Cannot infer expression result type.",
                    Severity = DiagnosticSeverity.Error,
                });
            }
            if (expectedType == VarType.Undefined)
                expectedType = resultType;
            if (resultType != expectedType)
            {
                _diagnostics.Add(new()
                {
                    Range = exp.GetRange(),
                    Message = $"Type Mismatch: Expected {expectedType}, but returned {resultType}.",
                    Severity = DiagnosticSeverity.Error,
                });
            }
        }

        if (isTopExp)
        {
            _expWalkStarted = false;
            string joined = string.Join(", ", _currentExp);
            //Evaluator ev = new(_dialogScript);
            //ev.Evaluate(_currentExp.ToArray());
            _currentExp = new();
        }
        return expectedType;
    }
}
