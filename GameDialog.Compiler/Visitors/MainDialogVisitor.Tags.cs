using System.Text;
using GameDialog.Common;

using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor
{
    private void HandleStmtTag(TagContext context)
    {
        // Line tags are handled separately.
        if (context.BBCODE_NAME() != null)
        {
            _diagnostics.AddError(context, "BBCode cannot be used in stand-alone expressions.");
            return;
        }

        List<int>? ints = GetTagStmt(context);

        if (ints == null || ints.Count == 0)
            return;

        if (ints[0] == InstructionType.Instruction)
        {
            if (ints.Count == 2)
                return;

            if (ints[2] == OpCode.Goto)
            {
                if (ints[3] == -1)
                    ResolveStatements(_endIndex);
                else
                    ResolveStatements(ints[3]);
                return;
            }
        }

        _scriptData.Instructions.Add(ints);
        ResolveStatements(_scriptData.Instructions.Count - 1);
        _unresolvedStmts.Add((_nestLevel, ints));
    }

    private void HandleLineTag(StringBuilder sb, TagContext context)
    {
        if (context.BBCODE_NAME() != null)
        {
            sb.Append(context.GetText());
            return;
        }

        List<int>? ints = GetTagStmt(context);

        if (ints == null || ints.Count == 0)
            return;

        if (ints[0] == InstructionType.Instruction)
        {
            // Something went wrong and we couldn't get instructions.
            if (ints.Count < 3)
                return;

            if (ints[2] == OpCode.Goto)
            {
                _diagnostics.AddError(context, "Goto is only available on a separate line.");
                return;
            }
        }

        _scriptData.Instructions.Add(ints);
        sb.Append($"[{_scriptData.Instructions.Count - 1}]");
    }

    private List<int>? GetTagStmt(TagContext context)
    {
        if (context.expression() != null)
            return GetExpressionStmt(context);
        else if (context.assignment() != null)
            return GetAssignTagStmt(context.assignment());
        else if (context.attrExpression() != null)
            return GetAttrExpStmt(context.attrExpression());
        else if (context.hashCollection() != null)
            return GetHashCollectionStmt(context.hashCollection());
        else if (context.speakerCollection() != null)
            return GetSpeakerCollectionStmt(context.speakerCollection());
        return null;
    }

    private List<int>? GetExpressionStmt(TagContext context)
    {
        ExpressionContext expContext = context.expression();
        List<int> ints = [InstructionType.Instruction, 0];

        // If isn't single word
        if (expContext is not ConstVarContext varContext)
        {
            ints.AddRange(_expressionVisitor.GetInstruction(expContext));
            return ints;
        }

        string expName = varContext.NAME().GetText();

        if (!BuiltIn.IsBuiltIn(expName))
        {
            ints.AddRange(_expressionVisitor.GetInstruction(varContext));
            return ints;
        }

        bool isClose = context.OPEN_BRACKET().GetText().EndsWith('/');

        if (expName != BuiltIn.SPEED && expName != BuiltIn.AUTO && isClose)
            return _diagnostics.AddError(context, $"Tag {expName} is not supported as a closing tag.");
        else if (expName == BuiltIn.SPEED && !isClose)
            return _diagnostics.AddError(context, $"Tag {expName} can only be assigned to or be part of a closing tag.");

        switch (expName)
        {
            case BuiltIn.END:
                if (isClose)
                    return _diagnostics.AddError(context, $"Tag {expName} is not supported as a closing tag.");

                ints.AddRange([OpCode.Goto, -1]);
                return ints;
            case BuiltIn.AUTO:
                ints.AddRange([OpCode.Auto, _scriptData.Floats.GetOrAdd(isClose ? -2 : -1)]);
                return ints;
            case BuiltIn.SPEED:
                ints.AddRange([OpCode.Speed, _scriptData.Floats.GetOrAdd(1)]);
                return ints;
            case BuiltIn.PROMPT:
                if (isClose)
                    return _diagnostics.AddError(context, $"Tag {expName} is not supported as a closing tag.");

                ints.AddRange([OpCode.Prompt]);
                return ints;
            case BuiltIn.PAGE:
                if (isClose)
                    return _diagnostics.AddError(context, $"Tag {expName} is not supported as a closing tag.");

                ints.AddRange([OpCode.Page]);
                return ints;
        }

        // Nothing matched
        return _diagnostics.AddError(context, $"Built-in tag \"{expName}\" cannot be used as an expression.");
    }

    private List<int>? GetAssignTagStmt(AssignmentContext context)
    {
        List<int> ints = [InstructionType.Instruction, 0];
        string expName = context.NAME().GetText();

        if (!BuiltIn.IsBuiltIn(expName))
        {
            ints.AddRange(_expressionVisitor.GetInstruction(context));
            return ints;
        }

        if (expName is BuiltIn.SPEED or BuiltIn.PAUSE or BuiltIn.AUTO)
        {
            if (context.right is not ConstFloatContext floatContext)
                return _diagnostics.AddError(context, "Type Mismatch: Expected Float.");

            float value = float.Parse(floatContext.GetText());

            if (value <= 0)
            {
                if (expName == BuiltIn.SPEED)
                    _diagnostics.AddError(context, "Invalid value: Speed multiplier cannot be zero or lesser.");
                else if (expName == BuiltIn.PAUSE)
                    _diagnostics.AddError(context, "Invalid value: Pause timeout cannot be zero or lesser.");
                else if (expName == BuiltIn.AUTO)
                    _diagnostics.AddError(context, "Invalid value: Auto timeout cannot be zero or lesser.");
                return null;
            }

            ushort opCode = expName switch
            {
                BuiltIn.SPEED => OpCode.Speed,
                BuiltIn.PAUSE => OpCode.Pause,
                BuiltIn.AUTO => OpCode.Auto,
                _ => throw new NotImplementedException()
            };

            ints.AddRange(opCode, _scriptData.Floats.GetOrAdd(value));
            return ints;
        }

        return _diagnostics.AddError(context, $"Built-in tag \"{expName}\" is not assignable.");
    }

    private List<int>? GetAttrExpStmt(AttrExpressionContext context)
    {
        string attName = context.NAME().GetText();

        if (attName != BuiltIn.GOTO)
            return _diagnostics.AddError(context, $"Built-in tag \"{attName}\" does not have attributes.");
        
        if (context.assignment().Length > 0 || context.expression().Length != 1)
            return _diagnostics.AddError(context, "Built-in tag has incorrect number of attributes.");

        List<int> ints = [InstructionType.Instruction, 0];
        string sectionName = context.expression()[0].GetText();

        if (string.Equals(sectionName, BuiltIn.END, StringComparison.OrdinalIgnoreCase))
        {
            ints.AddRange([OpCode.Goto, -1]);
            return ints;
        }

        int sectionIndex = _sections.IndexOf(sectionName);

        if (sectionIndex == -1)
            return _diagnostics.AddError(context, "Section not found.");

        ints.AddRange([OpCode.Goto, sectionIndex]);
        return ints;
    }

    private List<int>? GetHashCollectionStmt(HashCollectionContext context)
    {
        List<int> ints = [InstructionType.Hash, 0];
        return GetHashCollectionInts(context, ints);
    }

    private List<int>? GetHashCollectionInts(HashCollectionContext context, List<int> ints)
    {
        foreach (HashNameContext hash in context.hashName())
        {
            int index = _scriptData.Strings.GetOrAdd(hash.NAME().GetText());
            ints.AddRange([1, index]);
        }

        foreach (HashAssignmentContext hashAssignment in context.hashAssignment())
        {
            string name = hashAssignment.hashName().NAME().GetText();
            int index = _scriptData.Strings.GetOrAdd(name);
            List<int> expInts = _expressionVisitor.GetInstruction(hashAssignment.expression());
            ints.AddRange([2, index, ..expInts]);
        }

        return ints;
    }

    private List<int>? GetSpeakerCollectionStmt(SpeakerCollectionContext context)
    {
        int nameIndex = _scriptData.SpeakerIds.IndexOf(context.NAME().GetText());

        if (nameIndex == -1)
            return _diagnostics.AddError(context, "Can only edit speakers that appear in this dialog script.");

        List<int> ints = [InstructionType.Speaker, 0, nameIndex];
        return GetHashCollectionInts(context.hashCollection(), ints);
    }
}
