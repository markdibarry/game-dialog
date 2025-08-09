using System;
using System.Collections.Generic;
using GameDialog.Common;
using GameDialog.Pooling;

namespace GameDialog.Runner;

public partial class DialogBase
{
    private const int EndScript = -2;
    private const int SuspendScript = -1;
    private Dictionary<string, string> _cacheDict = [];

    /// <summary>
    /// Begins a loaded dialog script.
    /// </summary>
    public void StartScript()
    {
        Next = 0;
        Resume();
    }

    public void Resume()
    {
        // Next has not been set, so a line must be in progress.
        if (Next == null)
        {
            OnDialogLineResumed();
            return;
        }

        int? nextIndex = Next;
        Next = null;

        while (nextIndex >= 0 && nextIndex < Instructions.Count)
            nextIndex = ReadStatement(Instructions[nextIndex.Value]);

        if (nextIndex == SuspendScript)
            return;

        ScriptEnded?.Invoke(this);
    }

    public void ReadNext(int? nextIndex = null)
    {
        if (!nextIndex.HasValue)
            nextIndex = Next.HasValue ? Next : EndScript;

        Next = null;

        while (nextIndex >= 0 && nextIndex < Instructions.Count)
            nextIndex = ReadStatement(Instructions[nextIndex.Value]);

        if (nextIndex == SuspendScript)
            return;

        ScriptEnded?.Invoke(this);
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
            return SuspendScript;
        }

        int HandleInstructionStatement()
        {
            int next = instr[1];
            bool isAwait = instr[2] == OpCode.Func && instr[3] == 1;
            EvaluateInstructions(instr);
            return isAwait ? SuspendScript : next;
        }

        int HandleConditionalStatement()
        {
            return GetConditionResult(instr);
        }

        int HandleChoiceStatement()
        {
            List<Choice> choices = ListPool.Get<Choice>();
            GetChoices(instr, choices);
            OnChoice(choices);
            ListPool.Return(choices);
            return -1;
        }

        int HandleHashStatement()
        {
            int next = instr[1];
            StateSpan<ushort> span = new(instr, 2);
            GetHashResult(span, _cacheDict);
            OnHash(_cacheDict);
            _cacheDict.Clear();
            return next;
        }

        int HandleSpeakerStatement()
        {
            int next = instr[1];
            int speakerId = instr[2];
            StateSpan<ushort> span = new(instr, 3);
            GetHashResult(span, _cacheDict);
            OnSpeakerHash(SpeakerIds[speakerId], _cacheDict);
            _cacheDict.Clear();
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
}
