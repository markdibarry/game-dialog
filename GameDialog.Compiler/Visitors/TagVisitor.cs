using Antlr4.Runtime.Misc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor
{
    private void HandleStmtTag(DialogParser.TagContext context)
    {
        // Line tags are handled separately.
        if (context.BBCODE_NAME() != null)
        {
            _diagnostics.Add(new Diagnostic()
            {
                Range = context.GetRange(),
                Message = $"BBCode cannot be used in stand-alone expressions.",
                Severity = DiagnosticSeverity.Error,
            });
            return;
        }

        List<int>? ints;

        if (context.expression() != null)
            ints = GetExpressionTagInts(context);
        else if (context.assignment() != null)
            ints = GetAssignTagInts(context);
        else
            ints = GetAttrTagInts(context);

        if (ints == null)
            return;

        if (ints[0] == (int)InstructionType.Goto)
        {
            // If next is decided
            _dialogScript.InstructionStmts.Add(new(null, new(StatementType.Section, ints[1])));
            ResolveStatements(new(StatementType.Instruction, _dialogScript.InstructionStmts.Count - 1));
            return;
        }

        InstructionStmt exp = new(ints);
        _dialogScript.InstructionStmts.Add(exp);
        ResolveStatements(new(StatementType.Instruction, _dialogScript.InstructionStmts.Count - 1));
        _unresolvedStmts.Add((_nestLevel, exp));
    }

    private void HandleLineTag(Line line, StringBuilder sb, DialogParser.TagContext context)
    {
        List<int>? ints;

        if (context.BBCODE_NAME() != null)
        {
            //Add BBCode as a string (I'm not able to provide validation)
            string bbText = context.BBCODE_NAME().GetText();
            if (context.BBCODE_EXTRA_TEXT() != null)
                bbText += context.BBCODE_EXTRA_TEXT().GetText();
            _dialogScript.ExpStrings.Add(bbText);
            ints = BuiltIn.GetBBCodeInts(_dialogScript.ExpStrings.Count - 1);
        }
        else
        {
            if (context.expression() != null)
                ints = GetExpressionTagInts(context);
            else if (context.assignment() != null)
                ints = GetAssignTagInts(context);
            else
                ints = GetAttrTagInts(context);
        }

        if (ints == null)
            return;

        if (ints[0] == (int)InstructionType.Goto)
        {
            line.Next = new(StatementType.Section, ints[1]);
            return;
        }
        _dialogScript.Instructions.Add(ints);
        line.InstructionIndices.Add(_dialogScript.Instructions.Count - 1);
        sb.Append($"[{line.InstructionIndices.Count - 1}]");
    }

    private List<int>? GetExpressionTagInts([NotNull] DialogParser.TagContext context)
    {
        DialogParser.ExpressionContext expContext = context.expression();
        if (expContext is not DialogParser.ConstVarContext varContext)
            return _expressionVisitor.GetExpression(expContext);

        string expName = varContext.NAME().GetText();
        if (!BuiltIn.IsBuiltIn(expName))
            return _expressionVisitor.GetExpression(expContext);

        bool isClose = context.TAG_ENTER().GetText().EndsWith('/');
        switch (expName)
        {
            case BuiltIn.GOTO:
            case BuiltIn.PAUSE:
                _diagnostics.Add(new Diagnostic()
                {
                    Range = context.GetRange(),
                    Message = $"Built-in tag cannot be used as an expression.",
                    Severity = DiagnosticSeverity.Error,
                });
                return null;
            case BuiltIn.END:
                return new() { (int)InstructionType.Goto, -1 };
            case BuiltIn.AUTO:
                return new() { (int)InstructionType.Auto, isClose ? 0 : 1 };
            case BuiltIn.NEWLINE:
                return new() { (int)InstructionType.NewLine };
            case BuiltIn.SPEED:
                if (!isClose)
                {
                    _diagnostics.Add(new Diagnostic()
                    {
                        Range = context.GetRange(),
                        Message = $"Reserved tag \"speed\" can only be assigned to or be part of a closing tag.",
                        Severity = DiagnosticSeverity.Error,
                    });
                    return null;
                }
                _dialogScript.ExpFloats.Add(-1);
                return new() { (int)InstructionType.Speed, _dialogScript.ExpFloats.Count - 1 };
        }
        // Nothing matched
        _diagnostics.Add(new Diagnostic()
        {
            Range = context.GetRange(),
            Message = $"Invalid built-in tag.",
            Severity = DiagnosticSeverity.Error,
        });
        return null;
    }

    private List<int>? GetAssignTagInts([NotNull] DialogParser.TagContext context)
    {
        DialogParser.AssignmentContext assContext = context.assignment();
        string expName = assContext.NAME().GetText();
        if (!BuiltIn.IsBuiltIn(expName))
            return _expressionVisitor.GetExpression(assContext);
        switch (expName)
        {
            case BuiltIn.AUTO:
            case BuiltIn.END:
            case BuiltIn.GOTO:
            case BuiltIn.NEWLINE:
                _diagnostics.Add(new Diagnostic()
                {
                    Range = context.GetRange(),
                    Message = $"Built-in tag is not assignable.",
                    Severity = DiagnosticSeverity.Error,
                });
                return null;
            case BuiltIn.SPEED:
                if (assContext.right is not DialogParser.ConstFloatContext floatContext)
                {
                    _diagnostics.Add(new Diagnostic()
                    {
                        Range = context.GetRange(),
                        Message = $"Type Mismatch: Expected Float.",
                        Severity = DiagnosticSeverity.Error,
                    });
                    return null;
                }
                _dialogScript.ExpFloats.Add(float.Parse(floatContext.GetText()));
                return new() { (int)InstructionType.Speed, _dialogScript.ExpFloats.Count - 1 };
        }
        _diagnostics.Add(new Diagnostic()
        {
            Range = context.GetRange(),
            Message = $"Invalid built-in tag.",
            Severity = DiagnosticSeverity.Error,
        });
        return null;
    }

    private List<int>? GetAttrTagInts([NotNull] DialogParser.TagContext context)
    {
        DialogParser.Attr_expressionContext attContext = context.attr_expression();
        string attName = attContext.NAME().GetText();
        switch (attName)
        {
            case BuiltIn.AUTO:
            case BuiltIn.END:
            case BuiltIn.NEWLINE:
            case BuiltIn.PAUSE:
                _diagnostics.Add(new Diagnostic()
                {
                    Range = context.GetRange(),
                    Message = $"Built-in tag does not have attributes.",
                    Severity = DiagnosticSeverity.Error,
                });
                return null;
            case BuiltIn.GOTO:
                if (attContext.assignment().Length > 0 || attContext.expression().Length != 1)
                {
                    _diagnostics.Add(new Diagnostic()
                    {
                        Range = context.GetRange(),
                        Message = $"Built-in tag has incorrect number of attributes.",
                        Severity = DiagnosticSeverity.Error,
                    });
                    return null;
                }
                string sectionName = attContext.expression()[0].GetText();
                if (sectionName.ToLower() == BuiltIn.END)
                    return new() { (int)InstructionType.Goto, -1 };
                int sectionIndex = _dialogScript.Sections.FindIndex(x => x.Name == sectionName);
                if (sectionIndex == -1)
                {
                    _diagnostics.Add(new Diagnostic()
                    {
                        Range = context.GetRange(),
                        Message = $"Section not found.",
                        Severity = DiagnosticSeverity.Error,
                    });
                    return null;
                }
                return new() { (int)InstructionType.Goto, sectionIndex };

        }
        if (BuiltIn.IsNameExpression(attContext))
        {
            return BuiltIn.GetSpeakerGetInts(attName, _dialogScript);
        }
        else if (BuiltIn.IsSpeakerExpression(attContext))
        {
            if (!_dialogScript.SpeakerIds.Contains(attName))
            {
                _diagnostics.Add(new Diagnostic()
                {
                    Range = context.GetRange(),
                    Message = $"Can only edit speakers that appear in this dialog script.",
                    Severity = DiagnosticSeverity.Error,
                });
                return null;
            }
            return BuiltIn.GetSpeakerSetInts(attContext, _dialogScript);
        }

        _diagnostics.Add(new Diagnostic()
        {
            Range = context.GetRange(),
            Message = $"Unrecognized expression.",
            Severity = DiagnosticSeverity.Error,
        });
        return null;
    }
}
