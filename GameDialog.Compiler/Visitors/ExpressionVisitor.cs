using Antlr4.Runtime;
using GameDialog.Common;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class ExpressionVisitor : DialogParserBaseVisitor<VarType>
{
    public ExpressionVisitor(ScriptData scriptData, List<Diagnostic> diagnostics, MemberRegister memberRegister)
    {
        _scriptData = scriptData;
        _diagnostics = diagnostics;
        _memberRegister = memberRegister;
    }

    private readonly ScriptData _scriptData;
    private readonly List<Diagnostic> _diagnostics;
    private readonly MemberRegister _memberRegister;
    private List<int> _currentInst = [];
    private static readonly Dictionary<int, ushort> InstructionLookup = new()
        {
            { DialogLexer.OP_MULT, OpCode.Mult },
            { DialogLexer.OP_DIVIDE, OpCode.Div },
            { DialogLexer.OP_ADD, OpCode.Add },
            { DialogLexer.OP_SUB, OpCode.Sub },
            { DialogLexer.OP_LESS_EQUALS, OpCode.LessEquals },
            { DialogLexer.OP_GREATER_EQUALS, OpCode.GreaterEquals },
            { DialogLexer.OP_LESS, OpCode.Less },
            { DialogLexer.OP_GREATER, OpCode.Greater },
            { DialogLexer.OP_EQUALS, OpCode.Equal },
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
            _diagnostics.AddError(context, $"Type Mismatch: Expected {expectedType}, but returned {resultType}.");

        return result;
    }

    public override VarType VisitAssignment(AssignmentContext context)
    {
        string varName = context.NAME().GetText();
        VarDef? varDef = _memberRegister.VarDefs.FirstOrDefault(x => x.Name == varName);
        int nameIndex = _scriptData.Strings.GetOrAdd(varName);

        if (varDef == null)
        {
            varDef = new(varName);
            _memberRegister.VarDefs.Add(varDef);
        }

        if (context.op.Type != DialogLexer.OP_ASSIGN && varDef.Type != VarType.Float)
        {
            _diagnostics.AddError(context, $"Operator requires variable to be of type {VarType.Float} and already have a value.");
            return VarType.Undefined;
        }

        VarType newType = PushExp(
            [InstructionLookup[context.op.Type], nameIndex],
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

    public override VarType VisitExpMultDiv(ExpMultDivContext context)
    {
        PushExp(context.op, VarType.Float, context.left, context.right);
        return VarType.Float;
    }

    public override VarType VisitExpAddSub(ExpAddSubContext context)
    {
        PushExp(context.op, VarType.Float, context.left, context.right);
        return VarType.Float;
    }

    public override VarType VisitExpComp(ExpCompContext context)
    {
        PushExp(context.op, VarType.Float, context.left, context.right);
        return VarType.Bool;
    }

    public override VarType VisitExpNot(ExpNotContext context)
    {
        PushExp(context.op, VarType.Bool, context.right);
        return VarType.Bool;
    }

    public override VarType VisitExpEqual(ExpEqualContext context)
    {
        PushExp(context.op, default, context.left, context.right);
        return VarType.Bool;
    }

    private VarType PushExp(IToken op, VarType checkType, params ReadOnlySpan<ParserRuleContext> exps)
    {
        return PushExp([InstructionLookup[op.Type]], checkType, exps);
    }

    private VarType PushExp(ReadOnlySpan<int> values, VarType expectedType, params ReadOnlySpan<ParserRuleContext> exps)
    {
        _currentInst.AddRange(values);

        foreach (ParserRuleContext exp in exps)
        {
            // Visit inner expression
            VarType resultType = Visit(exp);

            if (expectedType == VarType.Undefined)
                expectedType = resultType;

            if (resultType != expectedType)
                _diagnostics.AddError(exp, $"Type Mismatch: Expected {expectedType}, but returned {resultType}.");
        }

        return expectedType;
    }
}
