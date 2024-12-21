using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Compiler;

public partial class ExpressionVisitor : DialogParserBaseVisitor<VarType>
{
    public ExpressionVisitor(ScriptData dialogScript, List<Diagnostic> diagnostics, MemberRegister memberRegister)
    {
        _dialogScript = dialogScript;
        _diagnostics = diagnostics;
        _memberRegister = memberRegister;
    }

    private readonly ScriptData _dialogScript;
    private readonly List<Diagnostic> _diagnostics;
    private readonly MemberRegister _memberRegister;
    private List<int> _currentInst = [];
    private static readonly Dictionary<int, OpCode> InstructionLookup = new()
        {
            { DialogLexer.OP_MULT, OpCode.Mult },
            { DialogLexer.OP_DIVIDE, OpCode.Div },
            { DialogLexer.OP_ADD, OpCode.Add },
            { DialogLexer.OP_SUB, OpCode.Sub },
            { DialogLexer.OP_LESS_EQUALS, OpCode.LessEquals },
            { DialogLexer.OP_GREATER_EQUALS, OpCode.GreaterEquals },
            { DialogLexer.OP_LESS, OpCode.Less },
            { DialogLexer.OP_GREATER, OpCode.Greater },
            { DialogLexer.OP_EQUALS, OpCode.Equals },
            { DialogLexer.OP_NOT_EQUALS, OpCode.NotEquals },
            { DialogLexer.OP_AND, OpCode.And },
            { DialogLexer.OP_OR, OpCode.Or },
            { DialogLexer.OP_NOT, OpCode.Not },
            { DialogLexer.OP_ASSIGN, OpCode.Assign },
            { DialogLexer.OP_MULT_ASSIGN, OpCode.MultAssign },
            { DialogLexer.OP_DIVIDE_ASSIGN, OpCode.DivAssign },
            { DialogLexer.OP_ADD_ASSIGN, OpCode.AddAssign },
            { DialogLexer.OP_SUB_ASSIGN, OpCode.SubAssign }
        };

    public List<int> GetInstruction(ParserRuleContext context, VarType expectedType = default)
    {
        VarType resultType = Visit(context);
        List<int> result = _currentInst;
        _currentInst = [];

        if (expectedType != default && resultType != expectedType)
        {
            _diagnostics.Add(new()
            {
                Range = context.GetRange(),
                Message = $"Type Mismatch: Expected {expectedType}, but returned {resultType}.",
                Severity = DiagnosticSeverity.Error,
            });
        }

        return result;
    }

    public override VarType VisitAssignment([NotNull] DialogParser.AssignmentContext context)
    {
        string varName = context.NAME().GetText();
        VarDef? varDef = _memberRegister.VarDefs.FirstOrDefault(x => x.Name == varName);
        int nameIndex = _dialogScript.InstStrings.GetOrAdd(varName);

        if (varDef == null)
        {
            varDef = new(varName);
            _memberRegister.VarDefs.Add(varDef);
        }

        if (context.op.Type != DialogLexer.OP_ASSIGN && varDef.Type != VarType.Float)
        {
            _diagnostics.Add(context.GetError($"Operator requires variable to be of type {VarType.Float} and already have a value."));
            return VarType.Undefined;
        }

        VarType newType = PushExp(
            [(int)InstructionLookup[context.op.Type], nameIndex],
            varDef.Type,
            context.right);

        if (newType == VarType.Undefined)
        {
            _memberRegister.VarDefs.Remove(varDef);
            return VarType.Undefined;
        }

        if (varDef.Type == VarType.Undefined)
            varDef.Type = newType;

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

    private VarType PushExp(IToken op, VarType checkType, params ReadOnlySpan<ParserRuleContext> exps)
    {
        return PushExp([(int)InstructionLookup[op.Type]], checkType, exps);
    }

    private VarType PushExp(ReadOnlySpan<int> values, VarType expectedType, params ReadOnlySpan<ParserRuleContext> exps)
    {
        _currentInst.AddRange(values);

        foreach (var exp in exps)
        {
            // Visit inner expression
            VarType resultType = Visit(exp);

            if (expectedType == VarType.Undefined)
                expectedType = resultType;

            if (resultType != expectedType)
            {
                _diagnostics.Add(exp.GetError($"Type Mismatch: Expected {expectedType}, but returned {resultType}."));
            }
        }

        return expectedType;
    }
}
