namespace GameDialog.Compiler;

public class MemberRegister
{
    public MemberRegister() { }

    public MemberRegister(MemberRegister memberRegister)
    {
        VarDefs = [..memberRegister.VarDefs];
        FuncDefs = [..memberRegister.FuncDefs];
    }

    public List<VarDef> VarDefs { get; set; } = [];
    public List<FuncDef> FuncDefs { get; set; } = [];
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


public class FuncDef
{
    public FuncDef(string name, VarType returnType, List<Argument> argTypes)
    {
        Name = name;
        ReturnType = returnType;
        ArgTypes = argTypes;
    }

    public string Name { get; set; } = string.Empty;
    public VarType ReturnType { get; set; }
    public List<Argument> ArgTypes { get; set; } = [];
}

public struct Argument(VarType type, bool isOptional = false)
{
    public VarType Type { get; set; } = type;
    public bool IsOptional { get; set; } = isOptional;
}