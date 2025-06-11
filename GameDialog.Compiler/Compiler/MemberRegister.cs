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

    public void SetMembersFromFile(string fileName, string rootPath, bool generateRegister)
    {
        if (string.IsNullOrEmpty(rootPath))
            return;

        string[] files = Directory.GetFiles(rootPath, fileName, SearchOption.AllDirectories);

        if (files.Length == 0)
            return;

        foreach (var filePath in files)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
            CompilationUnitSyntax? root = tree.GetCompilationUnitRoot();
            var members = root.DescendantNodes().OfType<MemberDeclarationSyntax>();

            List<VarDef> varDefs = [];
            List<FuncDef> funcDefs = [];
            string fileNamespace = string.Empty;
            string fileClassName = string.Empty;

            foreach (var member in members)
            {
                if (member is BaseNamespaceDeclarationSyntax namespaceDeclaration)
                {
                    fileNamespace = namespaceDeclaration.Name.ToString();
                }
                else if (member is ClassDeclarationSyntax classDeclaration)
                {
                    fileClassName = classDeclaration.Identifier.Text;
                }
                else if (member is PropertyDeclarationSyntax propDeclaration)
                {
                    VarType varType = GetVarType(propDeclaration.Type.ToString());

                    if (varType == VarType.Undefined)
                        continue;

                    VarDef varDef = new(propDeclaration.Identifier.Text, varType);
                    varDefs.Add(varDef);
                }
                else if (member is MethodDeclarationSyntax methodDeclaration)
                {
                    // ignore override methods
                    if (methodDeclaration.Modifiers.Any(x => x.Text == "override"))
                        continue;

                    FuncDef? funcDef = GetFuncDef(methodDeclaration);

                    if (funcDef != null)
                        funcDefs.Add(funcDef);
                }
            }

            VarDefs.AddRange(varDefs);
            FuncDefs.AddRange(funcDefs);

            if (generateRegister)
                GenerateMemberFile(filePath, fileNamespace, fileClassName, varDefs, funcDefs);
        }
    }

    private static void GenerateMemberFile(
        string filePath,
        string fileNamespace,
        string fileClassName,
        List<VarDef> varDefs,
        List<FuncDef> funcDefs)
    {
        string newFilePath = Path.ChangeExtension(filePath, "Generated.cs");

        string content = "using GameCore.GUI;\n\n";

        if (fileNamespace.Length > 0)
            content += $"namespace {fileNamespace};\n\n";

        string varDefContent = string.Join("\n\n        ", varDefs.Select(x =>
        {
            return $$"""
            DialogBridgeRegister.RegisterProperty(
                        name: nameof({{x.Name}}),
                        varType: VarType.{{x.Type}},
                        getter: () => new({{x.Name}}),
                        setter: (x) => {{x.Name}} = x.{{x.Type}});
            """;
        }));

        string funcDefContent = string.Join("\n\n        ", funcDefs.Select(x =>
        {
            string literalArgs = string.Empty;

            for (int i = 0; i < x.ArgTypes.Count; i++)
            {
                if (i > 0)
                    literalArgs += ", ";

                literalArgs += $"args[{i}].{x.ArgTypes[i]}";
            }

            string argTypesArg = string.Join(", ", x.ArgTypes.Select(argType => $"VarType.{argType}"));

            return $$"""
            DialogBridgeRegister.RegisterMethod(
                        name: nameof({{x.Name}}),
                        argTypes: [{{argTypesArg}}],
                        returnType: VarType.{{x.ReturnType}},
                        func: (args) => new({{x.Name}}({{literalArgs}})));
            """;
        }));

        content +=
        $$"""
        public partial class {{fileClassName}}
        {
            public override void RegisterProperties()
            {
                {{varDefContent}}
            }

            public override void RegisterMethods()
            {
                {{funcDefContent}}
            }
        }
        """;
        File.WriteAllText(newFilePath, content);
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
        List<VarType> args = [];
        bool argsValid = true;

        foreach (var parameter in node.ParameterList.Parameters)
        {
            if (parameter.Type == null)
                return null;

            VarType paramType = GetVarType(parameter.Type.ToString());

            if (paramType == VarType.Undefined)
                return null;

            // parameter.Default != null tells if it's optional
            args.Add(paramType);
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
    public FuncDef(string name, VarType returnType, List<VarType> argTypes)
    {
        Name = name;
        ReturnType = returnType;
        ArgTypes = argTypes;
    }

    public string Name { get; set; } = string.Empty;
    public VarType ReturnType { get; set; }
    public List<VarType> ArgTypes { get; set; } = [];
}
