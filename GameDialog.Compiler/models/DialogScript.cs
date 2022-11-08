using System.Text.Json.Serialization;

namespace GameDialog.Compiler;

[Serializable]
public class DialogScript
{
    public List<string> ActorIds { get; set; } = new();
    public List<float> ExpFloats { get; set; } = new();
    public List<string> ExpStrings { get; set; } = new();
    public List<Section> Sections { get; set; } = new();
    public List<Variable> Variables { get; set; } = new();
    public List<Function> Functions { get; set; } = new();
}

public class Section
{
    public string Name { get; set; } = string.Empty;
    public List<Line> Lines { get; set; } = new();
}

public class Line
{
    public List<Choice> Choices { get; set; }
    public List<int> Condition { get; set; }
    public List<List<int>> Expressions { get; set; }
    public LineIndex NextLine { get; set; }
    public List<int> SpeakerIndices { get; set; }
    public List<Tag> Tags { get; set; }
    public string Text { get; set; }
}

public class Choice
{
    public List<int> Condition { get; set; }
    public LineIndex NextLine { get; set; }
    public string Text { get; set; }
}

public class Tag
{
    public int Position { get; set; }
    public int Type { get; set; }
}

public struct LineIndex
{
    public int Line { get; set; }
    public int Section { get; set; }
}

public class Variable
{
    public Variable(string name)
    {
        Name = name;
    }

    public Variable(string name, VarType type)
        : this(name)
    {
        Type = type;
    }

    public string Name { get; }
    public VarType Type { get; set; }
    [JsonIgnore]
    public List<int> Starts { get; set; }
}

public class Function
{
    public string Name { get; set; }
    public VarType Type { get; set; }
    [JsonIgnore]
    public List<int> Starts { get; set; }
}