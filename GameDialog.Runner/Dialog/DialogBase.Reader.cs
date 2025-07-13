using System;
using System.Collections.Generic;
using System.Text;
using GameDialog.Common;
using GameDialog.Pooling;

namespace GameDialog.Runner;

public partial class DialogBase
{
    private const int EndScript = -2;
    private const int Wait = -1;

    private void ReadStatements(int startIndex)
    {
        int instrIndex = startIndex;

        while (instrIndex >= 0 && instrIndex < Instructions.Count)
            instrIndex = ReadStatement(Instructions[instrIndex]);

        if (instrIndex == Wait)
            return;

        ScriptEnded?.Invoke(this, null);
    }

    private int ReadStatement(ushort[] instr)
    {
        ushort instructionType = instr[0];
        return instructionType switch
        {
            InstructionType.Section => HandleSectionStatement(),
            InstructionType.Line => HandleLineStatement(),
            InstructionType.Instruction => HandleInstructionStatement(),
            InstructionType.Conditional => HandleConditionalStatement(),
            InstructionType.Choice => HandleChoiceStatement(),
            InstructionType.Hash => HandleHashStatement(),
            InstructionType.Speaker => HandleSpeakerStatement(),
            InstructionType.Undefined or _ => EndScript // End script as fallback
        };

        int HandleSectionStatement()
        {
            int next = instr[1];
            return next;
        }

        int HandleLineStatement()
        {
            DialogLine line = Pool.Get<DialogLine>();
            line.Next = instr[1];
            ushort numOfSpeakers = instr[2];
            ushort speakerStart = 3;
            ReadOnlySpan<ushort> speakerIndices = new(instr, speakerStart, numOfSpeakers);

            foreach (ushort speakerIndex in speakerIndices)
            {
                string speakerId = SpeakerIds[speakerIndex];
                line.SpeakerIds.Add(speakerId);
            }

            int stringIndex = instr[speakerStart + numOfSpeakers];
            string text = Strings[stringIndex];
            line.Text = Tr(text);
            OnDialogLineStarted(line);
            return Wait;
        }

        int HandleInstructionStatement()
        {
            int next = instr[1];

            if (instr[2] == OpCode.AsyncFunc)
            {
                (AsyncFuncDef, TextVariant[]) tuple = EvalInstrAsyncFunc(instr);

                if (instr[4] == 1) // Is awaiting
                {
                    _ = RunAsyncFunc(tuple.Item1, tuple.Item2, next);
                    return Wait;
                }
                else
                {
                    _ = RunAsyncFunc(tuple.Item1, tuple.Item2);
                    return next;
                }
            }
            else
            {
                EvaluateInstructions(instr);
                return next;
            }
        }

        int HandleConditionalStatement()
        {
            return GetConditionResult(instr);
        }

        int HandleChoiceStatement()
        {
            List<Choice> choices = GetChoices(instr);
            OnChoice(choices);
            return -1;
        }

        int HandleHashStatement()
        {
            int next = instr[1];
            StateSpan<ushort> span = new(instr, 2);
            Dictionary<string, string> hashCollection = GetHashResult(span);
            OnHash(hashCollection);
            return next;
        }

        int HandleSpeakerStatement()
        {
            int next = instr[1];
            int speakerId = instr[2];
            StateSpan<ushort> span = new(instr, 3);
            Dictionary<string, string> hashCollection = GetHashResult(span);
            OnSpeakerHash(SpeakerIds[speakerId], hashCollection);
            return next;
        }
    }

    private ushort EvaluateInstructions(ushort[] instr)
    {
        ushort opCode = instr[2];
        switch (opCode)
        {
            case OpCode.Assign:
            case OpCode.MultAssign:
            case OpCode.DivAssign:
            case OpCode.AddAssign:
            case OpCode.SubAssign:
            case OpCode.Func:
                HandleEvaluate();
                break;
            case OpCode.Speed:
                HandleSpeed();
                break;
            case OpCode.Auto:
                HandleAuto();
                break;
        }

        return default;

        void HandleEvaluate()
        {
            // Even though we don't use the return value,
            // the method may mutate something somewhere,
            // so it's better to call non-Void methods.
            VarType returnType = GetReturnType(instr);

            switch (returnType)
            {
                case VarType.String:
                    GetStringInstResult(instr);
                    break;
                case VarType.Float:
                    GetFloatInstResult(instr);
                    break;
                case VarType.Bool:
                    GetBoolInstResult(instr);
                    break;
                case VarType.Void:
                    EvalVoidExp(instr);
                    break;
            }
        }

        void HandleSpeed() => SpeedMultiplier = Floats[instr[1]];

        void HandleAuto()
        {
            float value = Floats[instr[1]];
            AutoProceedGlobalEnabled = value == -2;
            AutoProceedGlobalTimeout = value;
        }
    }

    public TextEvent ParseTextEvent(ReadOnlySpan<char> tagContent, int renderedIndex, StringBuilder sb)
    {
        if (!int.TryParse(tagContent, out int instIndex))
            return TextEvent.Undefined;

        ushort[] instr = Instructions[instIndex];
        ushort instructionType = instr[0];
        // ushort unusedNextIndex = instr[1];
        ushort opCode = instr[2];
        ushort floatIndex = instr[3];

        if (instructionType == InstructionType.Hash)
        {
            return new(EventType.Hash, renderedIndex, instIndex);
        }
        else if (instructionType == InstructionType.Speaker)
        {
            return new(EventType.Speaker, renderedIndex, instIndex);
        }
        else if (instructionType == InstructionType.Instruction)
        {
            return opCode switch
            {
                OpCode.Assign or
                OpCode.MultAssign or
                OpCode.DivAssign or
                OpCode.AddAssign or
                OpCode.SubAssign => AddAsTextEvent(),
                OpCode.GetName or
                OpCode.String or
                OpCode.Float or
                OpCode.Mult or
                OpCode.Div or
                OpCode.Add or
                OpCode.Sub or
                OpCode.Var or
                OpCode.Func => AppendResult(),
                OpCode.Speed => AddSpeedEvent(),
                OpCode.Pause => AddPauseEvent(),
                OpCode.Auto => HandleAuto(),
                _ => TextEvent.Undefined
            };
        }

        return TextEvent.Undefined;

        TextEvent AddAsTextEvent()
        {
            return new(EventType.Instruction, renderedIndex, instIndex);
        }

        TextEvent AppendResult()
        {
            string result = string.Empty;
            VarType returnType = GetReturnType(instr);

            switch (returnType)
            {
                case VarType.String:
                    result = GetStringInstResult(instr);
                    break;
                case VarType.Float:
                    result = GetFloatInstResult(instr).ToString();
                    break;
                case VarType.Void:
                    return AddAsTextEvent();
            }

            sb.Append(result.AsSpan());
            return TextEvent.Ignore;
        }

        TextEvent AddSpeedEvent()
        {
            float speed = Floats[floatIndex];
            return new TextEvent(EventType.Speed, renderedIndex, speed);
        }

        TextEvent AddPauseEvent()
        {
            float time = Floats[floatIndex];
            return new TextEvent(EventType.Pause, renderedIndex, time);
        }

        TextEvent HandleAuto()
        {
            float time = Floats[floatIndex];
            return new TextEvent(EventType.Auto, renderedIndex, time);
        }
    }

    public void HandleTextEvent(TextEvent textEvent)
    {
        int value = (int)textEvent.Value;

        switch (textEvent.EventType)
        {
            case EventType.Instruction:
                EvaluateInstructions(Instructions[value]);
                break;
            case EventType.Hash:
                ushort[] hashInstr = Instructions[value];
                StateSpan<ushort> hashSpan = new(hashInstr, 2);
                Dictionary<string, string> hashCollection = GetHashResult(hashSpan);
                OnHash(hashCollection);
                break;
            case EventType.Speaker:
                ushort[] speakerInstr = Instructions[value];
                ushort speakerId = speakerInstr[2];
                StateSpan<ushort> speakerSpan = new(speakerInstr, 3);
                Dictionary<string, string> speakerCollection = GetHashResult(speakerSpan);
                OnSpeakerHash(SpeakerIds[speakerId], speakerCollection);
                break;
        }
    }
}
