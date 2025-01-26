using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text;
using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor : DialogParserBaseVisitor<int>
{
    private readonly ScriptDataExtended _scriptData;
    private readonly List<Diagnostic> _diagnostics;
    private readonly MemberRegister _memberRegister;
    private readonly ExpressionVisitor _expressionVisitor;
    private readonly List<(int NestLevel, List<int> Stmt)> _unresolvedStmts = [];
    private int _nestLevel;
    private int _endIndex;
    private readonly List<string> _sections = [];

    /// <summary>
    /// </summary>
    /// <param name="scriptData"></param>
    /// <param name="diagnostics"></param>
    /// <param name="memberRegister"></param>
    public MainDialogVisitor(ScriptDataExtended scriptData, List<Diagnostic> diagnostics, MemberRegister memberRegister)
    {
        _scriptData = scriptData;
        _diagnostics = diagnostics;
        _memberRegister = memberRegister;
        _expressionVisitor = new(scriptData, diagnostics, memberRegister);
    }

    public override int VisitScript(ScriptContext context)
    {
        _sections.Clear();

        if (context.section() == null)
            return 0;

        // Initialize all sections
        foreach (SectionContext section in context.section())
        {
            string title = section.section_title().NAME().GetText();

            if (string.Equals(title, BuiltIn.END, StringComparison.OrdinalIgnoreCase))
                _diagnostics.Add(section.section_title().GetError("\"end\" is a reserved name."));

            _scriptData.Instructions.Add([(int)InstructionType.Section, 0]);
            _sections.Add(title);
        }

        _scriptData.Instructions.Add([(int)InstructionType.End]);
        _endIndex = _scriptData.Instructions.Count - 1;
        int length = context.section().Length;

        for (int i = 0; i < length; i++)
        {
            // Set section "next" to first statement in next section
            _scriptData.Instructions[i][1] = _scriptData.Instructions.Count;

            // Parse each statement
            foreach (var stmt in context.section()[i].section_body().stmt())
                Visit(stmt);

            // Resolve all outstanding statements to the end
            ResolveStatements(_endIndex);
        }

        return 0;
    }

    public override int VisitCond_stmt(Cond_stmtContext context)
    {
        List<int> conditions = [(int)InstructionType.Conditional, 0];
        int condIndex = _scriptData.Instructions.GetOrAdd(conditions);
        ResolveStatements(condIndex);
        _nestLevel++;

        // if
        _unresolvedStmts.Add((_nestLevel, conditions));

        foreach (StmtContext stmt in context.if_stmt().stmt())
            Visit(stmt);

        conditions.AddRange(_expressionVisitor.GetInstruction(context.if_stmt().expression()));
        conditions.AddRange(conditions[1]);
        LowerUnresolvedStatements();

        // else if
        foreach (var elseifstmt in context.elseif_stmt())
        {
            _unresolvedStmts.Add((_nestLevel, conditions));

            foreach (StmtContext stmt in elseifstmt.stmt())
                Visit(stmt);

            conditions.AddRange(_expressionVisitor.GetInstruction(elseifstmt.expression()));
            conditions.AddRange(conditions[1]);
            LowerUnresolvedStatements();
        }

        _unresolvedStmts.Add((_nestLevel, conditions));

        // else
        if (context.else_stmt() != null)
        {
            foreach (StmtContext stmt in context.else_stmt().stmt())
                Visit(stmt);

            LowerUnresolvedStatements();
        }

        _nestLevel--;
        return 0;
    }

    public override int VisitLine_stmt(Line_stmtContext context)
    {
        StringBuilder sb = new();
        List<int> line = [(int)InstructionType.Line, 0, 0];
        int lineIndex = _scriptData.Instructions.Count;
        _scriptData.Instructions.Add(line);

        // Add speakers
        if (context.UNDERSCORE() == null)
        {
            var speakerIds = context.speaker_ids().speaker_id();
            line[^1] = speakerIds.Length;

            // Get speakers
            foreach (var speaker in speakerIds)
            {
                int speakerIndex = _scriptData.SpeakerIds.IndexOf(speaker.NAME().GetText());
                line.Add(speakerIndex);
            }
        }

        var children = context.line_text()?.children ?? context.ml_text()?.children;

        if (children != null)
        {
            HandleLineText(sb, children);
            int textIndex = _scriptData.Strings.GetOrAdd(sb.ToString());
            line.Add(textIndex);
            _scriptData.LineIndices.Add(textIndex);
        }

        ResolveStatements(lineIndex);
        _unresolvedStmts.Add((_nestLevel, line));

        if (context.choice_stmt().Length > 0)
            HandleChoices(context.choice_stmt());

        return 0;
    }

    public override int VisitTag(TagContext context)
    {
        HandleStmtTag(context);
        return 0;
    }

    private void HandleLineText(StringBuilder sb, IList<IParseTree> children)
    {
        // Start building a string of text and instruction identifiers
        foreach (var child in children)
        {
            if (child is ITerminalNode node && node.Symbol.Type == TEXT)
                sb.Append(node.GetText());
            else if (child is TagContext tag)
                HandleLineTag(sb, tag);
        }
    }

    private void ResolveStatements(int index)
    {
        for (int i = _unresolvedStmts.Count - 1; i >= 0; i--)
        {
            (int NestLevel, List<int> Stmt) = _unresolvedStmts[i];

            if (NestLevel >= _nestLevel)
            {
                if (Stmt[0] == (int)InstructionType.Choice)
                {
                    for (int j = 0; j < Stmt.Count; j++)
                    {
                        if (Stmt[j] == -1)
                            Stmt[j] = index;
                    }
                }
                else
                {
                    Stmt[1] = index;
                }

                _unresolvedStmts.RemoveAt(i);
            }
        }
    }

    private void LowerUnresolvedStatements()
    {
        for (int i = 0; i < _unresolvedStmts.Count; i++)
        {
            if (_unresolvedStmts[i].NestLevel >= _nestLevel)
                _unresolvedStmts[i] = _unresolvedStmts[i] with { NestLevel = _nestLevel - 1 };
        }
    }

    private List<int> GetInstrStmt(ExpressionContext context, VarType varType)
    {
        return
        [
            (int)InstructionType.Instruction,
            0,
            .._expressionVisitor.GetInstruction(context, varType)
        ];
    }
}
