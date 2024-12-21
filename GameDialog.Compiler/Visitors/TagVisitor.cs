using Antlr4.Runtime.Misc;
using System.Text;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor
{
    private void HandleStmtTag(DialogParser.TagContext context)
    {
        // Line tags are handled separately.
        if (context.BBCODE_NAME() != null)
        {
            _diagnostics.Add(context.GetError("BBCode cannot be used in stand-alone expressions."));
            return;
        }

        List<int>? ints = GetTagInts(context);

        if (ints == null || ints.Count == 0)
            return;

        if (ints[0] == (int)OpCode.Goto)
        {
            // If next is decided
            GoTo next = ints[1] == -1 ? new(StatementType.End, 0) : new(StatementType.Section, ints[1]);
            InstructionStmt goToExp = new(-1, next);
            _dialogScript.InstructionStmts.Add(goToExp);
            ResolveStatements(next);
            return;
        }

        InstructionStmt exp = new(_dialogScript.Instructions.GetOrAdd(ints));
        _dialogScript.InstructionStmts.Add(exp);
        ResolveStatements(new(StatementType.Instruction, _dialogScript.InstructionStmts.Count - 1));
        _unresolvedStmts.Add((_nestLevel, exp));
    }

    private void HandleLineTag(Line line, StringBuilder sb, DialogParser.TagContext context)
    {
        if (context.BBCODE_NAME() != null)
        {
            sb.Append(context.GetText());
            return;
        }

        List<int>? ints = GetTagInts(context);

        if (ints == null || ints.Count == 0)
            return;

        if (ints[0] == (int)OpCode.Goto)
        {
            if (ints[1] == -1)
                line.Next = new GoTo(StatementType.End, 0);
            else
                line.Next = new GoTo(StatementType.Section, ints[1]);
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
                _diagnostics.Add(context.GetError("Built-in tag cannot be used as an expression."));
                return null;
            case BuiltIn.END:
                return [(int)OpCode.Goto, -1];
            case BuiltIn.AUTO:
                return [(int)OpCode.Auto, isClose ? 0 : 1];
            case BuiltIn.NEWLINE:
                return [(int)OpCode.NewLine];
            case BuiltIn.SPEED:
                if (!isClose)
                {
                    _diagnostics.Add(context.GetError("Reserved tag \"speed\" can only be assigned to or be part of a closing tag."));
                    return null;
                }
                _dialogScript.InstFloats.Add(1);
                return [(int)OpCode.Speed, _dialogScript.InstFloats.Count - 1];
        }

        // Nothing matched
        _diagnostics.Add(context.GetError("Invalid built-in tag."));
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
                _diagnostics.Add(context.GetError("Built-in tag is not assignable."));
                return null;
            case BuiltIn.SPEED:
                if (assContext.right is not DialogParser.ConstFloatContext speedFloatContext)
                {
                    _diagnostics.Add(context.GetError("Type Mismatch: Expected Float."));
                    return null;
                }

                float speed = float.Parse(speedFloatContext.GetText());

                if (speed <= 0)
                {
                    _diagnostics.Add(context.GetError("Invalid value: Speed multiplier cannot be zero or lesser."));
                    return null;
                }

                _dialogScript.InstFloats.Add(speed);
                return [(int)OpCode.Speed, _dialogScript.InstFloats.Count - 1];
            case BuiltIn.PAUSE:
                if (assContext.right is not DialogParser.ConstFloatContext pauseFloatContext)
                {
                    _diagnostics.Add(context.GetError("Type Mismatch: Expected Float."));
                    return null;
                }

                float time = float.Parse(pauseFloatContext.GetText());

                if (time < 0)
                {
                    _diagnostics.Add(context.GetError("Invalid value: Pause timeout cannot be less than zero."));
                    return null;
                }

                _dialogScript.InstFloats.Add(time);
                return [(int)OpCode.Pause, _dialogScript.InstFloats.Count - 1];
        }

        _diagnostics.Add(context.GetError("Invalid built-in tag."));
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
                _diagnostics.Add(context.GetError("Built-in tag does not have attributes."));
                return null;
            case BuiltIn.GOTO:
                if (attContext.assignment().Length > 0 || attContext.expression().Length != 1)
                {
                    _diagnostics.Add(context.GetError("Built-in tag has incorrect number of attributes."));
                    return null;
                }

                string sectionName = attContext.expression()[0].GetText();

                if (string.Equals(sectionName, BuiltIn.END, StringComparison.OrdinalIgnoreCase))
                    return [(int)OpCode.Goto, -1];

                int sectionIndex = _dialogScript.Sections.FindIndex(x => x.Name == sectionName);

                if (sectionIndex == -1)
                {
                    _diagnostics.Add(context.GetError("Section not found."));
                    return null;
                }

                return [(int)OpCode.Goto, sectionIndex];
        }

        if (BuiltIn.IsNameExpression(attContext))
            return GetSpeakerGetInts(attName);

        if (BuiltIn.IsSpeakerExpression(attContext))
        {
            int nameIndex = _dialogScript.SpeakerIds.IndexOf(attName);

            if (nameIndex == -1)
            {
                _diagnostics.Add(context.GetError("Can only edit speakers that appear in this dialog script."));
                return null;
            }

            return GetSpeakerUpdateInts(attContext, nameIndex);
        }

        _diagnostics.Add(context.GetError("Unrecognized expression."));
        return null;
    }

    public List<int> GetSpeakerGetInts(string name)
    {
        int funcIndex = _dialogScript.InstStrings.GetOrAdd("GetName");
        int nameIndex = _dialogScript.InstStrings.GetOrAdd(name);
        return [(int)OpCode.Func, funcIndex, 1, (int)OpCode.String, nameIndex];
    }

    /// <summary>
    /// Gets instructions for speaker update tags
    /// </summary>
    /// <example>
    /// [28, 5, 30, 1, 29, 0]
    /// SpeakerSetOpCode, SpeakerId index, update type code, update instruction index, etc.
    /// </example>
    /// <param name="context"></param>
    /// <param name="nameIndex"></param>
    /// <returns></returns>
    private List<int> GetSpeakerUpdateInts(DialogParser.Attr_expressionContext context, int nameIndex)
    {
        List<int> updateInts = [(int)OpCode.SpeakerSet, nameIndex];

        foreach (var ass in context.assignment())
            updateInts.AddRange(GetSpeakerUpdateAttribute(ass.NAME().GetText(), ass.right));

        return updateInts;
    }

    private List<int> GetSpeakerUpdateAttribute(string type, DialogParser.ExpressionContext value)
    {
        return
        [
            type switch
            {
                BuiltIn.NAME => (int)OpCode.SpeakerSetName,
                BuiltIn.PORTRAIT => (int)OpCode.SpeakerSetPortrait,
                BuiltIn.MOOD => (int)OpCode.SpeakerSetMood,
                _ => throw new NotImplementedException()
            },
            _dialogScript.Instructions.GetOrAdd(_expressionVisitor.GetInstruction(value, VarType.String))
        ];
    }
}
