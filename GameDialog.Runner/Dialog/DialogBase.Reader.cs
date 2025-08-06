using System;
using System.Collections.Generic;
using System.Text;
using GameDialog.Common;
using GameDialog.Pooling;

namespace GameDialog.Runner;

public partial class DialogBase
{
    private const int EndScript = -2;
    private const int SuspendScript = -1;
    private Dictionary<string, string> _cacheDict = [];

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

    public TextEvent ParseTextEvent(ReadOnlySpan<char> tagContent, int renderedIndex, StringBuilder sb)
    {
        if (!int.TryParse(tagContent, out int instIndex))
            return ParseEventString(tagContent, renderedIndex, sb);

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
                OpCode.Prompt => HandlePrompt(),
                OpCode.Page => HandlePage(),
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
                    bool isAwait = instr[4] == 1;
                    return new(EventType.Instruction, renderedIndex, instIndex, isAwait);
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

        TextEvent HandlePrompt()
        {
            return new TextEvent(EventType.Prompt, renderedIndex - 1, 0);
        }

        TextEvent HandlePage()
        {
            return new TextEvent(EventType.Page, renderedIndex - 1, 0);
        }
    }

    private TextEvent ParseEventString(ReadOnlySpan<char> tagContent, int renderedIndex, StringBuilder sb)
    {
        tagContent = tagContent.Trim();

        if (tagContent.StartsWith(nameof(GetName)))
        {
            tagContent = tagContent[nameof(GetName).Length..];

            if (!tagContent.StartsWith('(') || !tagContent.EndsWith(')'))
                return TextEvent.Undefined;

            tagContent = tagContent[1..^1].Trim();

            if (!tagContent.StartsWith('\"') || !tagContent.EndsWith('\"'))
                return TextEvent.Undefined;

            tagContent = tagContent[1..^1];
            sb.Append(GetName(tagContent.ToString()));
            return TextEvent.Ignore;
        }
        else if (tagContent.StartsWith(BuiltIn.SPEED))
        {
            tagContent = tagContent[BuiltIn.SPEED.Length..].Trim();

            if (!tagContent.StartsWith('='))
                return TextEvent.Undefined;

            tagContent = tagContent[1..].TrimStart();

            if (!float.TryParse(tagContent, out float speed))
                return TextEvent.Undefined;

            return new TextEvent(EventType.Speed, renderedIndex, speed);
        }
        else if (tagContent.StartsWith('/'))
        {
            if (!tagContent[1..].Trim().SequenceEqual(BuiltIn.SPEED))
                return TextEvent.Undefined;

            return new TextEvent(EventType.Speed, renderedIndex, 1);
        }
        else if (tagContent.StartsWith(BuiltIn.PAUSE))
        {
            tagContent = tagContent[BuiltIn.PAUSE.Length..].Trim();

            if (!tagContent.StartsWith('='))
                return TextEvent.Undefined;

            tagContent = tagContent[1..].TrimStart();

            if (!float.TryParse(tagContent, out float pauseTime))
                return TextEvent.Undefined;

            return new TextEvent(EventType.Pause, renderedIndex, pauseTime);
        }
        else if (tagContent.SequenceEqual(BuiltIn.PROMPT))
        {
            return new TextEvent(EventType.Prompt, renderedIndex - 1, 0);
        }
        else if (tagContent.SequenceEqual(BuiltIn.PAGE))
        {
            return new TextEvent(EventType.Page, renderedIndex - 1, 0);
        }

        return TextEvent.Undefined;
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
                GetHashResult(hashSpan, _cacheDict);
                OnHash(_cacheDict);
                _cacheDict.Clear();
                break;
            case EventType.Speaker:
                ushort[] speakerInstr = Instructions[value];
                ushort speakerId = speakerInstr[2];
                StateSpan<ushort> speakerSpan = new(speakerInstr, 3);
                GetHashResult(speakerSpan, _cacheDict);
                OnSpeakerHash(SpeakerIds[speakerId], _cacheDict);
                _cacheDict.Clear();
                break;
        }
    }
}
