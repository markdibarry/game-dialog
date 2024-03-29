﻿namespace GameDialog.Compiler;

[Serializable]
public class DialogScript
{
    public List<string> SpeakerIds { get; set; } = new();
    public List<float> InstFloats { get; set; } = new();
    public List<string> InstStrings { get; set; } = new();
    public List<Choice> Choices { get; set; } = new();
    public List<List<int>> ChoiceSets { get; set; } = new();
    public List<Section> Sections { get; set; } = new();
    public List<Line> Lines { get; set; } = new();
    public List<InstructionStmt> InstructionStmts { get; set; } = new();
    public List<List<int>> Instructions { get; set; } = new();
}

public class Section
{
    public string Name { get; set; } = string.Empty;
    public GoTo Next { get; set; }
}

public readonly struct GoTo
{
    public GoTo(StatementType type, int index)
    {
        Type = type;
        Index = index;
    }

    public StatementType Type { get; }
    public int Index { get; }
}

public class Line : IResolveable
{
    public List<int> InstructionIndices { get; set; } = new();
    public GoTo Next { get; set; }
    public List<int> SpeakerIndices { get; set; } = new();
    public string Text { get; set; } = string.Empty;
}

public class InstructionStmt : IResolveable
{
    public InstructionStmt(int index)
        :this(index, new GoTo(default, default))
    {
    }

    public InstructionStmt(int index, GoTo nextStatement)
    {
        Index = index;
        Next = nextStatement;
    }

    public int Index { get; }
    public GoTo Next { get; set; }
}

public class Choice : IResolveable
{
    public GoTo Next { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class VarDef
{
    public VarDef(string name)
    {
        Name = name;
    }

    public VarDef(string name, VarType type)
        : this(name)
    {
        Type = type;
    }

    public string Name { get; }
    public VarType Type { get; set; }
}

public enum StatementType
{
    Undefined,
    Line,
    Conditional,
    Instruction,
    Choice,
    Section,
    End
}

public interface IResolveable
{
    GoTo Next { get; set; }
}