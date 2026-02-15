namespace GameCore.Dialog;

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
}
