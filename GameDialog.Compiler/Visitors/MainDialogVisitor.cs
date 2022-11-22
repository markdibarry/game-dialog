using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor : DialogParserBaseVisitor<int>
{
    private readonly DialogScript _dialogScript;
    private readonly List<Diagnostic> _diagnostics;
    private readonly MemberRegister _memberRegister;
    private readonly ExpressionVisitor _expressionVisitor;
    private readonly List<(int, IResolveable)> _unresolvedStmts = new();
    private Section _currentSection = new();
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
            if (title.ToLower() == BuiltIn.END)
            {
                _diagnostics.Add(new Diagnostic()
                {
                    Range = section.section_title().GetRange(),
                    Message = $"\"end\" is a reserved name.",
                    Severity = DiagnosticSeverity.Error,
                });
            }
            _dialogScript.Sections.Add(new() { Name = title });
        }

        for (int i = 0; i < context.section().Length; i++)
        {
            _currentSection = _dialogScript.Sections[i];
            // Parse each statement
            foreach (var stmt in context.section()[i].section_body().stmt())
                Visit(stmt);
            // Resolve all outstanding statements to the end
            if (i == context.section().Length - 1)
                ResolveStatements(new GoTo(StatementType.Section, -1));
        }

        return 0;
    }

    public override int VisitCond_stmt([NotNull] DialogParser.Cond_stmtContext context)
    {
        // Create new goto and conditional
        GoTo newGoto = new(StatementType.Conditional, _dialogScript.ConditionalSets.Count);
        ResolveStatements(newGoto);

        List<InstructionStmt> conditionalSet = new();
        _dialogScript.ConditionalSets.Add(conditionalSet);

        _nestLevel++;
        // if
        InstructionStmt ifExp = new(_expressionVisitor.GetExpression(context.if_stmt().expression()));
        conditionalSet.Add(ifExp);
        _unresolvedStmts.Add((_nestLevel, ifExp));
        foreach (var stmt in context.if_stmt().stmt())
            Visit(stmt);
        LowerUnresolvedStatements();
        // else if
        foreach (var elseifstmt in context.elseif_stmt())
        {
            InstructionStmt elseifExp = new(_expressionVisitor.GetExpression(elseifstmt.expression()));
            conditionalSet.Add(elseifExp);
            _unresolvedStmts.Add((_nestLevel, elseifExp));
            foreach (var stmt in elseifstmt.stmt())
                Visit(stmt);
            LowerUnresolvedStatements();
        }

        // else (always add one)
        InstructionStmt elseExpression = new(null);
        conditionalSet.Add(elseExpression);
        _unresolvedStmts.Add((_nestLevel, elseExpression));
        if (context.else_stmt() != null)
        {
            foreach (var stmt in context.else_stmt().stmt())
                Visit(stmt);
            LowerUnresolvedStatements();
        }

        _nestLevel--;
        return 0;
    }

    public override int VisitLine_stmt([NotNull] DialogParser.Line_stmtContext context)
    {
        Line line = new();
        if (context.UNDERSCORE() == null)
        {
            foreach (var speaker in context.speaker_ids().NAME())
                line.SpeakerIndices.Add(_dialogScript.SpeakerIds.IndexOf(speaker.GetText()));
        }
        var children = context.line_text()?.children ?? context.ml_text()?.children;
        if (children != null)
            HandleLineText(line, children);
        // Create new goto and line
        GoTo newGoto = new(StatementType.Line, _dialogScript.Lines.Count);
        ResolveStatements(newGoto);
        if (line.Next.Type == default)
            _unresolvedStmts.Add((_nestLevel, line));
        _dialogScript.Lines.Add(line);
        if (context.choice_stmt().Length > 0)
            HandleChoices(context.choice_stmt());
        return 0;
    }

    public override int VisitTag([NotNull] DialogParser.TagContext context)
    {
        HandleStmtTag(context);
        return 0;
    }

    private void HandleLineText(Line line, IList<IParseTree> children)
    {
        // Start building a string of text and instruction identifiers
        StringBuilder sb = new();
        foreach (var child in children)
        {
            if (child is ITerminalNode node && node.Symbol.Type == DialogParser.TEXT)
                sb.Append(node.GetText());
            else if (child is DialogParser.TagContext tag)
                HandleLineTag(line, sb, tag);
        }
        line.Text = sb.ToString();
    }

    private void ResolveStatements(GoTo next)
    {
        if (_currentSection.Start.Type == default)
            _currentSection.Start = next;
        foreach (var stmt in _unresolvedStmts.Where(x => x.Item1 >= _nestLevel))
            stmt.Item2.Next = next;
        _unresolvedStmts.RemoveAll(x => x.Item2.Next.Type != default);
    }

    private void LowerUnresolvedStatements()
    {
        for (int i = 0; i < _unresolvedStmts.Count; i++)
        {
            if (_unresolvedStmts[i].Item1 >= _nestLevel)
                _unresolvedStmts[i] = (_nestLevel - 1, _unresolvedStmts[i].Item2);
        }
    }
}
