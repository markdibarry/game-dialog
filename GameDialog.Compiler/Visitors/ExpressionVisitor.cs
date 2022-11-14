using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Compiler;

public partial class ExpressionVisitor : DialogParserBaseVisitor<VarType>
{
    public ExpressionVisitor(DialogScript dialogScript, List<Diagnostic> diagnostics, MemberRegister memberRegister)
    {
        _dialogScript = dialogScript;
        _diagnostics = diagnostics;
        _memberRegister = memberRegister;
    }

    private readonly DialogScript _dialogScript;
    private readonly List<Diagnostic> _diagnostics;
    private readonly MemberRegister _memberRegister;
    private List<int> _currentExp = new();
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

    public Expression GetExpression(ParserRuleContext context, VarType expectedType = default)
    {
        VarType resultType = Visit(context);
        var result = _currentExp;
        _currentExp = new();
        if (expectedType != default && resultType != expectedType)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Type Mismatch: Expected {expectedType}, but returned {resultType}.",
                Severity = DiagnosticSeverity.Error,
            });
        }
        return new Expression(result);
    }

    public override VarType VisitAssignment([NotNull] DialogParser.AssignmentContext context)
    {
        VarDef variable;
        string varName = context.NAME().GetText();
        int varIndex = _dialogScript.Variables.FindIndex(x => x.Name == varName);
        if (varIndex == -1)
        {
            variable = new(varName);
            _dialogScript.Variables.Add(variable);
            varIndex = _dialogScript.Variables.Count - 1;
        }
        else
        {
            variable = _dialogScript.Variables[varIndex];
        }
        VarType newType = PushExp(
            new[] { (int)InstructionLookup[context.op.Type], (int)ExpType.Var, varIndex },
            variable.Type,
            context.right);
        if (newType == VarType.Undefined)
        {
            //Diagnostics.Add(new()
            //{
            //    Range = context.GetRange(),
            //    Message = $"Type Error: Cannot infer expression result type.",
            //    Severity = DiagnosticSeverity.Error,
            //});
            _dialogScript.Variables.Remove(variable);
            return VarType.Undefined;
        }
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

        _currentExp.AddRange(values);

        foreach (var exp in exps)
        {
            // Visit inner expression
            VarType resultType = Visit(exp);
            //if (resultType == VarType.Undefined)
            //{
            //    Diagnostics.Add(new()
            //    {
            //        Range = exp.GetRange(),
            //        Message = $"Type Error: Cannot infer expression result type.",
            //        Severity = DiagnosticSeverity.Error,
            //    });
            //}
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
        }
        return expectedType;
    }
}
