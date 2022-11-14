namespace GameDialog.Compiler;

public class MemberRegister
{
    public MemberRegister()
    {
        VarDefs.Add(new VarDef(BuiltIn.AUTO, VarType.Bool));
        VarDefs.Add(new VarDef(BuiltIn.G_AUTO, VarType.Bool));
        VarDefs.Add(new VarDef(BuiltIn.GOTO, VarType.String));
        VarDefs.Add(new VarDef(BuiltIn.SPEED, VarType.Float));
        VarDefs.Add(new VarDef(BuiltIn.G_SPEED, VarType.Float));
        VarDefs.Add(new VarDef(BuiltIn.PAUSE, VarType.Float));
        FuncDefs.Add(new FuncDef(BuiltIn.UPDATE_SPEAKER, default,
            new()
            {
                new Argument(VarType.String),
                new Argument(VarType.String),
                new Argument(VarType.String)
            }));
    }

    public List<VarDef> VarDefs { get; set; } = new();
    public List<FuncDef> FuncDefs { get; set; } = new();
}

public class FuncDef
{
    public FuncDef(string name, VarType type, List<Argument> args)
    {
        Name = name;
        Type = type;
        Args = args;
    }

    public string Name { get; set; } = string.Empty;
    public VarType Type { get; set; }
    public List<Argument> Args { get; set; } = new();
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