﻿namespace GameDialog.Common;

public static class InstructionType
{
    public const ushort Undefined = 0;
    /// <summary>
    /// SectionSet pattern.
    /// <para>Pattern: [Type, NextIndex, NameStringIndex]</para>
    /// </summary>
    public const ushort Section = 1;
    /// <summary>
    /// EndSet pattern.
    /// <para>Pattern: [End]</para>
    /// </summary>
    public const ushort End = 2;
    /// <summary>
    /// LineSet pattern.
    /// <para>Pattern: [Type, NextIndex, NumberOfSpeakers, SpeakerIndices..., TextStringIndex]</para>
    /// (Instruction indices in text will be global)
    /// </summary>
    public const ushort Line = 3;
    /// <summary>
    /// InstructionSet pattern.
    /// <para>Reference Pattern: [Type, NextIndex, InstructionIndex]</para>
    /// <para>Expression Pattern: [Type, 0, (Expression)]</para>
    /// </summary>
    public const ushort Instruction = 4;
    /// <summary>
    /// ConditionSet pattern.
    /// <para>Pattern: [Type, NextIndex, [Expression, NextIndex]...]</para>
    /// <para>Example:</para>
    /// <code>
    /// [
    ///     Conditional,
    ///     ElseNextIndex,
    ///     IfExpression, NextIndex,
    ///     ElseIfExpression, NextIndex,
    ///     ...
    /// ]
    /// </code>
    /// </summary>
    public const ushort Conditional = 5;
    /// <summary>
    /// ChoiceSet pattern.
    /// <para>Pattern: [Type, [ChoiceOp, [NextIndex, ChoiceStringIndex]...]]</para>
    /// <para>Example:</para>
    /// <code>
    /// [
    ///     Choice,
    ///     ChoiceSet, NextIndex, StringIndex,
    ///     If, ExpressionIndex,
    ///         Choice, NextIndex, StringIndex,
    ///     Else,
    ///         Choice, NextIndex, StringIndex,
    ///         Choice, NextIndex, StringIndex,
    ///     EndIf
    /// ]
    /// </code>
    /// </summary>
    public const ushort Choice = 6;
    /// <summary>
    /// HashSet pattern.
    /// <para>Pattern: [Type, Next, [NumOfValues, StringIndex, (Exp)]+]</para>
    /// <para>Example:</para>
    /// <code>
    /// [
    ///     HashSet, NextIndex,
    ///     1, StringIndex,
    ///     2, StringIndex, Float, FloatIndex,
    ///     2, StringIndex, String, StringIndex
    /// ]
    /// </code>
    /// </summary>
    public const ushort Hash = 7;
    /// <summary>
    /// SpeakerSet pattern.
    /// <para>Pattern: [Type, Next, SpeakerId, HashSetBody]</para>
    /// </summary>
    public const ushort Speaker = 8;
}
