namespace GameDialog.Compiler;

[Serializable]
public class ScriptData
{
    public List<string> SpeakerIds { get; set; } = [];
    public List<float> InstFloats { get; set; } = [];
    public List<string> InstStrings { get; set; } = [];
    public List<Choice> Choices { get; set; } = [];
    public List<List<int>> ChoiceSets { get; set; } = [];
    public List<Section> Sections { get; set; } = [];
    public List<Line> Lines { get; set; } = [];
    public List<InstructionStmt> InstructionStmts { get; set; } = [];
    public List<List<int>> Instructions { get; set; } = [];
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
    public List<int> InstructionIndices { get; set; } = [];
    public GoTo Next { get; set; }
    public List<int> SpeakerIndices { get; set; } = [];
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