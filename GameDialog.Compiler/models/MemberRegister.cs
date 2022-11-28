namespace GameDialog.Compiler;

public class MemberRegister
{
    public MemberRegister()
    {
        //FuncDefs.Add(new FuncDef(BuiltIn.GET_NAME, VarType.String, new() { new Argument(VarType.String) }));
    }

    public List<VarDef> VarDefs { get; set; } = new();
    public List<FuncDef> FuncDefs { get; set; } = new();
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
    public List<Argument> ArgTypes { get; set; } = new();
}

public struct Argument
{
    public Argument(VarType type, bool isOptional = false)
    {
        Type = type;
        IsOptional = isOptional;
    }

    public VarType Type { get; set; }
    public bool IsOptional { get; set; }
}