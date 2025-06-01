using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GameDialog.Compiler;

public class MemberRegister
{
    public MemberRegister() { }

    public MemberRegister(MemberRegister memberRegister)
    {
        VarDefs = [.. memberRegister.VarDefs];
        FuncDefs = [.. memberRegister.FuncDefs];
    }

    public List<VarDef> VarDefs { get; set; } = [];
    public List<FuncDef> FuncDefs { get; set; } = [];

    public void SetMembersFromFile(string fileName, string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath))
            return;

        var files = Directory.GetFiles(rootPath, fileName, SearchOption.AllDirectories);

        if (files.Length != 1)
            return;

        string code = new StreamReader(files[0]).ReadToEnd();
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
        CompilationUnitSyntax? root = tree.GetCompilationUnitRoot();
        var members = root.DescendantNodes().OfType<MemberDeclarationSyntax>();

        foreach (var member in members)
        {
            if (member is PropertyDeclarationSyntax propDeclaration)
            {
                VarType varType = GetVarType(propDeclaration.Type.ToString());

                if (varType == VarType.Undefined)
                    continue;

                VarDef varDef = new(propDeclaration.Identifier.Text, varType);
                VarDefs.Add(varDef);
            }
            else if (member is MethodDeclarationSyntax methodDeclaration)
            {
                // ignore override methods
                if (methodDeclaration.Modifiers.Any(x => x.Text == "override"))
                    continue;

                FuncDef? funcDef = GetFuncDef(methodDeclaration);

                if (funcDef != null)
                    FuncDefs.Add(funcDef);
            }
        }
    }

    private static VarType GetVarType(string typeName)
    {
        return typeName switch
        {
            "float" => VarType.Float,
            "string" => VarType.String,
            "bool" => VarType.Bool,
            "void" => VarType.Void,
            _ => VarType.Undefined
        };
    }

    private static FuncDef? GetFuncDef(MethodDeclarationSyntax node)
    {
        VarType returnType = GetVarType(node.ReturnType.ToString());

        if (returnType == VarType.Undefined)
            return null;

        string funcName = node.Identifier.Text;
        List<Argument> args = [];
        bool argsValid = true;

        foreach (var parameter in node.ParameterList.Parameters)
        {
            if (parameter.Type == null)
                return null;

            VarType paramType = GetVarType(parameter.Type.ToString());

            if (paramType == VarType.Undefined)
                return null;

            args.Add(new(paramType, parameter.Default != null));
        }

        if (!argsValid)
            return null;

        FuncDef funcDef = new(funcName, returnType, args);
        return funcDef;
    }
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