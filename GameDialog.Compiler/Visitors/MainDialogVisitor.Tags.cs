using System.Text;

using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor
{
    private void HandleStmtTag(TagContext context)
    {
        // Line tags are handled separately.
        if (context.BBCODE_NAME() != null)
        {
            _diagnostics.Add(context.GetError("BBCode cannot be used in stand-alone expressions."));
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

        if (ints[0] == InstructionType.Instruction && ints[2] == OpCode.Goto)
        {
            _diagnostics.Add(context.GetError("Goto is only available on a separate line."));
            return;
            // List<int> lineInstr = _scriptData.Instructions[lineIndex];
            // lineInstr[3] = ints[3] == -1 ? _endIndex : ints[3];
            // return;
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
        else if (context.attr_expression() != null)
            return GetAttrTagStmt(context.attr_expression());
        else if (context.hash_collection() != null)
            return GetHashCollectionStmt(context.hash_collection());
        else if (context.speaker_collection() != null)
            return GetSpeakerCollectionStmt(context.speaker_collection());
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

        // if single Speaker name
        if (_scriptData.SpeakerIds.Contains(expName))
        {
            int nameIndex = _scriptData.SpeakerIds.GetOrAdd(expName);
            ints.AddRange([OpCode.GetName, nameIndex]);
            return ints;
        }

        if (!BuiltIn.IsBuiltIn(expName))
        {
            ints.AddRange(_expressionVisitor.GetInstruction(varContext));
            return ints;
        }

        bool isClose = context.OPEN_BRACKET().GetText().EndsWith('/');

        if (isClose && expName != BuiltIn.SPEED)
        {
            _diagnostics.Add(context.GetError($"Tag {expName} is not supported as a closing tag."));
            return null;
        }
        else if (!isClose && expName == BuiltIn.SPEED)
        {
            _diagnostics.Add(context.GetError($"Tag {expName} can only be assigned to or be part of a closing tag."));
            return null;
        }

        switch (expName)
        {
            case BuiltIn.END:
                if (isClose)
                {
                    _diagnostics.Add(context.GetError($"Tag {expName} is not supported as a closing tag."));
                    return null;
                }

                ints.AddRange([OpCode.Goto, -1]);
                return ints;
            case BuiltIn.NEWLINE:
                if (isClose)
                {
                    _diagnostics.Add(context.GetError($"Tag {expName} is not supported as a closing tag."));
                    return null;
                }

                ints.Add(OpCode.NewLine);
                return ints;
            case BuiltIn.AUTO:
                ints.AddRange([OpCode.Auto, _scriptData.Floats.GetOrAdd(isClose ? -2 : -1)]);
                return ints;
            case BuiltIn.SPEED:
                if (!isClose)
                {
                    _diagnostics.Add(context.GetError($"Tag {expName} can only be assigned to or be part of a closing tag."));
                    return null;
                }

                ints.AddRange([OpCode.Speed, _scriptData.Floats.GetOrAdd(1)]);
                return ints;
        }

        // Nothing matched
        _diagnostics.Add(context.GetError($"Built-in tag \"{expName}\" cannot be used as an expression."));
        return null;
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
            {
                _diagnostics.Add(context.GetError("Type Mismatch: Expected Float."));
                return null;
            }

            float value = float.Parse(floatContext.GetText());

            if (value <= 0)
            {
                if (expName == BuiltIn.SPEED)
                    _diagnostics.Add(context.GetError("Invalid value: Speed multiplier cannot be zero or lesser."));
                else if (expName == BuiltIn.PAUSE)
                    _diagnostics.Add(context.GetError("Invalid value: Pause timeout cannot be zero or lesser."));
                else if (expName == BuiltIn.AUTO)
                    _diagnostics.Add(context.GetError("Invalid value: Auto timeout cannot be zero or lesser."));
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

        _diagnostics.Add(context.GetError($"Built-in tag \"{expName}\" is not assignable."));
        return null;
    }

    private List<int>? GetAttrTagStmt(Attr_expressionContext context)
    {
        string attName = context.NAME().GetText();

        if (attName == BuiltIn.GOTO)
        {
            if (context.assignment().Length > 0 || context.expression().Length != 1)
            {
                _diagnostics.Add(context.GetError("Built-in tag has incorrect number of attributes."));
                return null;
            }

            List<int> ints = [InstructionType.Instruction, 0];

            string sectionName = context.expression()[0].GetText();

            if (string.Equals(sectionName, BuiltIn.END, StringComparison.OrdinalIgnoreCase))
            {
                ints.AddRange([OpCode.Goto, -1]);
                return ints;
            }

            int sectionIndex = _sections.IndexOf(sectionName);

            if (sectionIndex == -1)
            {
                _diagnostics.Add(context.GetError("Section not found."));
                return null;
            }

            ints.AddRange([OpCode.Goto, sectionIndex]);
            return ints;
        }

        _diagnostics.Add(context.GetError($"Built-in tag \"{attName}\" does not have attributes."));
        return null;
    }

    private List<int>? GetHashCollectionStmt(Hash_collectionContext context)
    {
        List<int> ints = [InstructionType.Hash, 0];
        return GetHashCollectionInts(context, ints);
    }

    private List<int>? GetHashCollectionInts(Hash_collectionContext context, List<int> ints)
    {
        foreach (Hash_nameContext hash in context.hash_name())
        {
            int index = _scriptData.Strings.GetOrAdd(hash.NAME().GetText());
            ints.AddRange([1, index]);
        }

        foreach (Hash_assignmentContext hashAssignment in context.hash_assignment())
        {
            string name = hashAssignment.hash_name().NAME().GetText();
            int index = _scriptData.Strings.GetOrAdd(name);
            List<int> expInts = _expressionVisitor.GetInstruction(hashAssignment.expression());
            ints.AddRange([2, index, ..expInts]);
        }

        return ints;
    }

    private List<int>? GetSpeakerCollectionStmt(Speaker_collectionContext context)
    {
        int nameIndex = _scriptData.SpeakerIds.IndexOf(context.NAME().GetText());

        if (nameIndex == -1)
        {
            _diagnostics.Add(context.GetError("Can only edit speakers that appear in this dialog script."));
            return null;
        }

        List<int> ints = [InstructionType.Speaker, 0, nameIndex];
        return GetHashCollectionInts(context.hash_collection(), ints);
    }
}
