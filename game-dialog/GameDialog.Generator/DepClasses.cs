// Source Generators are dumb and can't share any dependencies with the main project, 
// so these all need copy/pasted and maintained.

namespace GameDialog.Generator;

internal class FuncDef
{
    public FuncDef()
    {
    }

    public FuncDef(string name, VarType returnType, VarType[] argTypes)
    {
        Name = name;
        ReturnType = returnType;
        ArgTypes = argTypes;
    }

    public string Name { get; set; } = string.Empty;
    public VarType ReturnType { get; set; }
    public VarType[] ArgTypes { get; set; } = [];
    public bool IsAwaitable { get; set; }
    public bool IsStatic { get; set; }
}

internal struct VarDef
{
    public VarDef()
    {
    }

    public VarDef(string name)
    {
        Name = name;
    }

    public VarDef(string name, VarType type)
        : this(name)
    {
        Type = type;
    }

    public string Name { get; set; } = string.Empty;
    public VarType Type { get; set; }
    public bool IsStatic { get; set; }
}

internal enum VarType
{
    Undefined = 0,
    Float = 1,
    String = 2,
    Bool = 3,
    Void = 4
}
