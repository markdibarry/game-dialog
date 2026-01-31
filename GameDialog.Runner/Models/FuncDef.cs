namespace GameDialog.Runner;

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
    public bool Awaitable { get; set; }
}
