namespace GameDialog.Compiler;

/// <summary>
/// SectionSet:
/// [Type, NextIndex, NameStringIndex]

/// EndSet:
/// [End]

/// LineSet:
/// [Type, NextIndex, NumberOfSpeakers, SpeakerIndices..., TextStringIndex]
/// (Instruction indices in text will be global)

/// InstructionSet:
/// [Type, NextIndex, InstructionIndex]

/// ConditionSet:
/// [Type, NextIndex, [Expression, NextIndex]...]
/// ex:
/// [
///     ConditionSet,
///     ElseNextIndex,
///     IfExpression, NextIndex
///     ElseIfExpression, NextIndex
///     ...
/// ]

/// ChoiceSet:
/// [Type, [ChoiceOp, [NextIndex, ChoiceStringIndex]...]
/// ex:
/// [
///     ChoiceSet,
///     Choice, NextIndex, StringIndex,
///     If, ExpressionIndex,
///         Choice, NextIndex, StringIndex,
///     Else,
///         Choice, NextIndex, StringIndex,
///         Choice, NextIndex, StringIndex,
///     EndIf
/// ]

/// HashSet:
/// [Type, Next, [NumOfValues, StringIndex, (Exp)]+ ]
/// ex:
/// [
///     HashSet, NextIndex,
///     1, StringIndex,
///     2, StringIndex, Float, FloatIndex,
///     2, StringIndex, String, StringIndex
/// ]
/// 
/// SpeakerSet:
/// [Type, Next, SpeakerId, HashSetBody]
/// </summary>
public enum InstructionType
{
    Undefined,
    Section,
    End,
    Line,
    Instruction,
    Conditional,
    Choice,
    Hash,
    Speaker
}

public enum ChoiceOp
{
    Undefined,
    Choice,
    If,
    ElseIf,
    Else,
    EndIf
}