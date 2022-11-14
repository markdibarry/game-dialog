using Antlr4.Runtime.Misc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor : DialogParserBaseVisitor<int>
{
    private readonly DialogScript _dialogScript;
    private readonly List<Diagnostic> _diagnostics;
    private readonly MemberRegister _memberRegister;
    private readonly ExpressionVisitor _expressionVisitor;
    private readonly List<(int, IResolveable)> _unresolvedStmts = new();
    private int _sectionIndex;
    private bool _inLineStatement;
    private int _nestLevel;

    public MainDialogVisitor(DialogScript dialogScript, List<Diagnostic> diagnostics, MemberRegister memberRegister)
    {
        _dialogScript = dialogScript;
        _diagnostics = diagnostics;
        _memberRegister = memberRegister;
        _expressionVisitor = new(dialogScript, diagnostics, memberRegister);
    }

    public override int VisitScript([NotNull] DialogParser.ScriptContext context)
    {
        if (context.section() == null)
            return 0;

        // Initialize all sections
        foreach (var section in context.section())
        {
            string title = section.section_title().NAME().GetText();
            _dialogScript.Sections.Add(new() { Name = title });
        }

        for (int i = 0; i < context.section().Length; i++)
        {
            _sectionIndex = i;
            // Parse each statement
            foreach (var stmt in context.section()[i].section_body().stmt())
                Visit(stmt);
            // Resolve all outstanding statements to the next section
            if (i < context.section().Length - 1)
                ResolveStatements(new GoTo(StatementType.Section, i + 1));
        }

        return 0;
    }

    public override int VisitCond_stmt([NotNull] DialogParser.Cond_stmtContext context)
    {
        // Create new goto and conditional
        GoTo newGoto = new(StatementType.Conditional, _dialogScript.ConditionalSets.Count);
        Section currentSection = _dialogScript.Sections[_sectionIndex];

        if (currentSection.Start.Type == default)
            currentSection.Start = newGoto;
        else
            ResolveStatements(newGoto);

        List<Expression> conditionalSet = new();
        _dialogScript.ConditionalSets.Add(conditionalSet);

        _nestLevel++;
        // if
        Expression ifExp = _expressionVisitor.GetExpression(context.if_stmt().expression());
        conditionalSet.Add(ifExp);
        _unresolvedStmts.Add((_nestLevel, ifExp));
        foreach (var stmt in context.if_stmt().stmt())
            Visit(stmt);

        // else if
        foreach (var elseifstmt in context.elseif_stmt())
        {
            Expression elseifExp = _expressionVisitor.GetExpression(elseifstmt.expression());
            conditionalSet.Add(elseifExp);
            _unresolvedStmts.Add((_nestLevel, elseifExp));
            foreach (var stmt in elseifstmt.stmt())
                Visit(stmt);
        }

        // else (always add one)
        Expression elseExpression = new(null);
        conditionalSet.Add(elseExpression);
        _unresolvedStmts.Add((_nestLevel, elseExpression));
        if (context.else_stmt() != null)
        {
            foreach (var stmt in context.else_stmt().stmt())
                Visit(stmt);
        }

        _nestLevel--;
        return 0;
    }

    public override int VisitLine_stmt([NotNull] DialogParser.Line_stmtContext context)
    {
        _inLineStatement = true;
        // Create new goto and line
        GoTo newGoto = new(StatementType.Line, _dialogScript.Lines.Count);
        ResolveStatements(newGoto);
        _inLineStatement = false;
        return 0;
    }

    public override int VisitTag([NotNull] DialogParser.TagContext context)
    {
        if (_inLineStatement)
            HandleLineTag(context);
        else
            HandleStmtTag(context);
        return 0;
    }

    private void ResolveStatements(GoTo next)
    {
        foreach (var stmt in _unresolvedStmts.Where(x => x.Item1 >= _nestLevel))
            stmt.Item2.Next = next;
        _unresolvedStmts.RemoveAll(x => x.Item2.Next.Type != default);
    }

    private void HandleLineTag(DialogParser.TagContext context)
    {

    }

    private void HandleStmtTag(DialogParser.TagContext context)
    {
        if (BBCode.IsBBCode(GetTagName(context)))
        {
            _diagnostics.Add(new Diagnostic()
            {
                Range = context.GetRange(),
                Message = $"BBCode cannot be used in stand-alone expressions.",
                Severity = DiagnosticSeverity.Error,
            });
            return;
        }

        Expression? exp;

        if (context.expression() != null)
            exp = GetStmtTagExpressionExpression(context.expression());
        else if (context.assignment() != null)
            exp = _expressionVisitor.GetExpression(context.assignment());
        else
            exp = GetStmtTagAttrExpression(context.attr_expression());

        if (exp == null)
            return;
        GoTo newGoto = new(StatementType.Expression, _dialogScript.Expressions.Count);
        ResolveStatements(newGoto);
        _dialogScript.Expressions.Add(exp);
        _unresolvedStmts.Add((_nestLevel, exp));
    }

    private Expression GetStmtTagExpressionExpression([NotNull] DialogParser.ExpressionContext context)
    {
        if (BuiltIn.IsAutoExpression(context))
            return BuiltIn.GetAutoExpression(_dialogScript);
        else
            return _expressionVisitor.GetExpression(context);
    }

    private Expression? GetStmtTagAttrExpression([NotNull] DialogParser.Attr_expressionContext context)
    {
        if (BuiltIn.IsNameExpression(context))
        {
            return null;
        }
        else if (BuiltIn.IsSpeakerExpression(context))
        {
            if (!_dialogScript.SpeakerIds.Contains(context.NAME().GetText()))
            {
                _diagnostics.Add(new Diagnostic()
                {
                    Range = context.GetRange(),
                    Message = $"Can only edit speakers that appear in this dialog script.",
                    Severity = DiagnosticSeverity.Error,
                });
                return null;
            }
            return BuiltIn.GetSpeakerExpression(context, _dialogScript);
        }

        _diagnostics.Add(new Diagnostic()
        {
            Range = context.GetRange(),
            Message = $"Unrecognized expression.",
            Severity = DiagnosticSeverity.Error,
        });
        return null;
    }

    private string GetTagName([NotNull] DialogParser.TagContext context)
    {
        if (context.expression() != null)
        {
            if (context.expression() is DialogParser.ConstVarContext varContext)
                return varContext.NAME().GetText();
        }
        else if (context.assignment() != null)
        {
            return context.assignment().NAME().GetText();
        }
        else
        {
            return context.attr_expression().NAME().GetText();
        }
        return string.Empty;
    }
}
