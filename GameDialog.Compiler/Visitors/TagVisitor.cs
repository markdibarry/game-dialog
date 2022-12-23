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

        List<int>? ints = GetTagInts(context);

        if (ints == null || ints.Count == 0)
            return;

        if (ints[0] == (int)OpCode.Goto)
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
            if (context.TAG_ENTER().GetText().EndsWith('/'))
                bbText = '/' + bbText;
            if (context.BBCODE_EXTRA_TEXT() != null)
                bbText += context.BBCODE_EXTRA_TEXT().GetText();
            int bbCodeIndex = _dialogScript.InstStrings.IndexOf(bbText);
            if (bbCodeIndex == -1)
            {
                _dialogScript.InstStrings.Add(bbText);
                bbCodeIndex = _dialogScript.InstStrings.Count - 1;
            }
            ints = new() { (int)OpCode.BBCode, bbCodeIndex };
        }
        else
        {
            ints = GetTagInts(context);
        }

        if (ints == null || ints.Count == 0)
            return;

        if (ints[0] == (int)OpCode.Goto)
        {
            line.Next = new(StatementType.Section, ints[1]);
            return;
        }
        _dialogScript.Instructions.Add(ints);
        line.InstructionIndices.Add(_dialogScript.Instructions.Count - 1);
        sb.Append($"[{line.InstructionIndices.Count - 1}]");
    }

    private List<int>? GetTagInts([NotNull] DialogParser.TagContext context)
    {
        if (context.expression() != null)
            return GetExpressionTagInts(context);
        else if (context.assignment() != null)
            return GetAssignTagInts(context);
        else if (context.attr_expression() != null)
            return GetAttrTagInts(context);
        return null;
    }

    private List<int>? GetExpressionTagInts([NotNull] DialogParser.TagContext context)
    {
        DialogParser.ExpressionContext expContext = context.expression();
        if (expContext is not DialogParser.ConstVarContext varContext)
            return _expressionVisitor.GetInstruction(expContext);

        string expName = varContext.NAME().GetText();
        if (!BuiltIn.IsBuiltIn(expName))
            return _expressionVisitor.GetInstruction(expContext);

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
                return new() { (int)OpCode.Goto, -1 };
            case BuiltIn.AUTO:
                return new() { (int)OpCode.Auto, isClose ? 0 : 1 };
            case BuiltIn.NEWLINE:
                return new() { (int)OpCode.NewLine };
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
                _dialogScript.InstFloats.Add(-1);
                return new() { (int)OpCode.Speed, _dialogScript.InstFloats.Count - 1 };
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
            return _expressionVisitor.GetInstruction(assContext);
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
                _dialogScript.InstFloats.Add(float.Parse(floatContext.GetText()));
                return new() { (int)OpCode.Speed, _dialogScript.InstFloats.Count - 1 };
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
                    return new() { (int)OpCode.Goto, -1 };
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
                return new() { (int)OpCode.Goto, sectionIndex };

        }
        if (BuiltIn.IsNameExpression(attContext))
        {
            return GetSpeakerGetInts(attName);
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
            return GetSpeakerUpdateInts(attContext);
        }

        _diagnostics.Add(new Diagnostic()
        {
            Range = context.GetRange(),
            Message = $"Unrecognized expression.",
            Severity = DiagnosticSeverity.Error,
        });
        return null;
    }

    public List<int> GetSpeakerGetInts(string name)
    {
        int funcIndex = _dialogScript.InstStrings.IndexOf("GetName");
        if (funcIndex == -1)
        {
            _dialogScript.InstStrings.Add("GetName");
            funcIndex = _dialogScript.InstStrings.Count - 1;
        }
        int nameIndex = _dialogScript.InstStrings.IndexOf(name);
        if (nameIndex == -1)
        {
            _dialogScript.InstStrings.Add(name);
            nameIndex = _dialogScript.InstStrings.Count - 1;
        }
        return new() { (int)OpCode.Func, funcIndex, 1, (int)OpCode.String, nameIndex};
    }

    private List<int> GetSpeakerUpdateInts(DialogParser.Attr_expressionContext context)
    {
        List<int> updateInts = new() { (int)OpCode.SpeakerSet };
        List<int> nameInst = new() { -1 };
        List<int> moodInst = new() { -1 };
        List<int> portraitInst = new() { -1 };
        foreach (var ass in context.assignment())
        {
            List<int> values = _expressionVisitor.GetInstruction(ass.right, VarType.String);
            switch (ass.NAME().GetText())
            {
                case BuiltIn.NAME:
                    nameInst = values;
                    break;
                case BuiltIn.MOOD:
                    moodInst = values;
                    break;
                case BuiltIn.PORTRAIT:
                    portraitInst = values;
                    break;
            }
        }
        updateInts.AddRange(nameInst);
        updateInts.AddRange(moodInst);
        updateInts.AddRange(portraitInst);

        return updateInts;
    }
}
