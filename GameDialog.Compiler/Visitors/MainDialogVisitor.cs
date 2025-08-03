using System.Text;
using Antlr4.Runtime.Tree;
using GameDialog.Common;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor : DialogParserBaseVisitor<int>
{
    /// <summary>
    /// </summary>
    /// <param name="scriptData"></param>
    /// <param name="diagnostics"></param>
    /// <param name="memberRegister"></param>
    public MainDialogVisitor(ScriptData scriptData, List<Diagnostic> diagnostics, MemberRegister memberRegister)
    {
        _scriptData = scriptData;
        _diagnostics = diagnostics;
        _expressionVisitor = new(scriptData, diagnostics, memberRegister);
    }

    private readonly ScriptData _scriptData;
    private readonly List<Diagnostic> _diagnostics;
    private readonly ExpressionVisitor _expressionVisitor;
    private readonly List<(int NestLevel, List<int> Stmt)> _unresolvedStmts = [];
    private int _nestLevel;
    private int _endIndex;
    private readonly List<string> _sections = [];

    public override int VisitScript(ScriptContext context)
    {
        _sections.Clear();

        if (context.section() == null)
            return 0;

        // Initialize all sections
        HashSet<string> titles = [];

        foreach (SectionContext section in context.section())
        {
            string title = section.sectionTitle().TITLE().GetText();
            title = title[2..^2];

            if (titles.Contains(title))
                _diagnostics.AddError(section.sectionTitle(), $"Title \"{title}\" already used in this script.");

            titles.Add(title);
            int titleStringIndex = _scriptData.Strings.GetOrAdd(title);

            if (string.Equals(title, BuiltIn.END, StringComparison.OrdinalIgnoreCase))
                _diagnostics.AddError(section.sectionTitle(), "\"end\" is a reserved name.");

            // Will set the "next" while traversing dialog
            _scriptData.Instructions.Add([InstructionType.Section, 0, titleStringIndex]);
            _sections.Add(title);
        }

        _scriptData.Instructions.Add([InstructionType.End]);
        _endIndex = _scriptData.Instructions.Count - 1;
        int length = context.section().Length;

        for (int i = 0; i < length; i++)
        {
            // Set section "next" to first statement in next section
            _scriptData.Instructions[i][1] = _scriptData.Instructions.Count;

            // Parse each statement
            foreach (var stmt in context.section()[i].sectionBody().stmt())
                Visit(stmt);

            // Resolve all outstanding statements to the end
            ResolveStatements(_endIndex);
        }

        _unresolvedStmts.Clear();
        return 0;
    }

    public override int VisitCondStmt(CondStmtContext context)
    {
        List<int> conditions = [InstructionType.Conditional, 0];
        int condIndex = _scriptData.Instructions.GetOrAdd(conditions);
        ResolveStatements(condIndex);
        _nestLevel++;

        // if
        _unresolvedStmts.Add((_nestLevel, conditions));

        foreach (StmtContext stmt in context.ifStmt().stmt())
            Visit(stmt);

        conditions.AddRange(_expressionVisitor.GetInstruction(context.ifStmt().expression()));
        conditions.AddRange(conditions[1]);
        LowerUnresolvedStatements();

        // else if
        foreach (var elseifstmt in context.elseifStmt())
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
        if (context.elseStmt() != null)
        {
            foreach (StmtContext stmt in context.elseStmt().stmt())
                Visit(stmt);

            LowerUnresolvedStatements();
        }

        _nestLevel--;
        return 0;
    }

    public override int VisitLineStmt(LineStmtContext context)
    {
        StringBuilder sb = new();
        List<int> line = [InstructionType.Line, 0, 0];
        int lineIndex = _scriptData.Instructions.Count;
        _scriptData.Instructions.Add(line);

        // Add speakers
        if (context.UNDERSCORE() == null)
        {
            if (context.speakerIds() == null)
            {
                _diagnostics.AddError(context, "No speaker provided.");
                return 0;
            }

            SpeakerIdContext[] speakerIds = context.speakerIds().speakerId();
            line[^1] = speakerIds.Length;
            List<string> speakers = speakerIds.Select(x => x.NAME().GetText()).ToList();

            if (speakerIds.Length > 1)
            {
                HashSet<string> distinctSpeakers = new(speakers);

                if (distinctSpeakers.Count != speakerIds.Length)
                {
                    _diagnostics.AddError(context, "Duplicate speakers.");
                    return 0;
                }
            }

            // Get speakers
            foreach (var speaker in speakers)
            {
                int speakerIndex = _scriptData.SpeakerIds.IndexOf(speaker);
                line.Add(speakerIndex);
            }
        }

        if (context.lineText()?.textContent() != null)
            HandleTextContent(sb, context.lineText().textContent());
        else
            HandleTextContent(sb, context.mlText().textContent());

        int textIndex = _scriptData.Strings.GetOrAdd(sb.ToString());
        line.Add(textIndex);
        _scriptData.DialogStringIndices.Add(textIndex);

        ResolveStatements(lineIndex);
        _unresolvedStmts.Add((_nestLevel, line));

        if (context.choiceStmt().Length > 0)
            HandleChoices(context.choiceStmt());

        return 0;
    }

    public override int VisitTag(TagContext context)
    {
        HandleStmtTag(context);
        return 0;
    }

    private void HandleTextContent(StringBuilder sb, TextContentContext[] content)
    {
        foreach (var part in content)
            HandleTextContent(sb, part);
    }

    private void HandleTextContent(StringBuilder sb, TextContentContext content)
    {
        // Start building a string of text and instruction identifiers
        for (int i = 0; i < content.children.Count; i++)
        {
            IParseTree parseTree = content.children[i];

            if (parseTree is ITerminalNode node && node.Symbol.Type == LINE_TEXT)
                sb.Append(parseTree.GetText().Replace("\\n", Environment.NewLine));
            else if (parseTree is TagContext tag)
                HandleLineTag(sb, tag);
        }
    }

    private void ResolveStatements(int index)
    {
        for (int i = _unresolvedStmts.Count - 1; i >= 0; i--)
        {
            (int NestLevel, List<int> Stmt) = _unresolvedStmts[i];

            if (NestLevel < _nestLevel)
                continue;

            if (Stmt[0] == InstructionType.Choice)
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
            InstructionType.Instruction,
            0,
            .._expressionVisitor.GetInstruction(context, varType)
        ];
    }
}
