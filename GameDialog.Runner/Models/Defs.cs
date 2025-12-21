using System.Collections.Generic;
using GameDialog.Pooling;

namespace GameDialog.Runner;

public class VarDef : IPoolable
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

    public void ClearObject()
    {
        Name = string.Empty;
        Type = VarType.Undefined;
    }
}

public class FuncDef : IPoolable
{
    public FuncDef()
    {
    }

    public FuncDef(string name, VarType returnType, List<VarType> argTypes)
    {
        Name = name;
        ReturnType = returnType;
        ArgTypes = argTypes;
    }

    public string Name { get; set; } = string.Empty;
    public VarType ReturnType { get; set; }
    public List<VarType> ArgTypes { get; set; } = [];
    public bool Awaitable { get; set; }

    public void ClearObject()
    {
        Name = string.Empty;
        ReturnType = VarType.Undefined;
        ArgTypes.Clear();
    }
}
