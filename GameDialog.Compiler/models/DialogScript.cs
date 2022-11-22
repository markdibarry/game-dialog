namespace GameDialog.Compiler;

[Serializable]
public class DialogScript
{
    public List<string> SpeakerIds { get; set; } = new();
    public List<float> ExpFloats { get; set; } = new();
    public List<string> ExpStrings { get; set; } = new();
    public List<Choice> Choices { get; set; } = new();
    public List<List<int>> ChoiceSets { get; set; } = new();
    public List<Section> Sections { get; set; } = new();
    public List<VarDef> Variables { get; set; } = new();
    public List<Line> Lines { get; set; } = new();
    public List<List<InstructionStmt>> ConditionalSets { get; set; } = new();
    public List<InstructionStmt> InstructionStmts { get; set; } = new();
    public List<List<int>> Instructions { get; set; } = new();
}

public class Section
{
    public string Name { get; set; } = string.Empty;
    public GoTo Start { get; set; }
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
    public InstructionStmt(List<int>? values)
        :this(values, new GoTo(default, default))
    {
    }

    public InstructionStmt(List<int>? values, GoTo nextStatement)
    {
        Values = values;
        Next = nextStatement;
    }

    public List<int>? Values { get; }
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

public class SpeakerUpdate
{
    public int SpeakerId { get; set; }
    public string? Name { get; set; }
    public string? Portrait { get; set; }
    public string? Mood { get; set; }
}

public enum StatementType
{
    Undefined,
    Line,
    Conditional,
    Instruction,
    Choice,
    Section
}

public interface IResolveable
{
    GoTo Next { get; set; }
}