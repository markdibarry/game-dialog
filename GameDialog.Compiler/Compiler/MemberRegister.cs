using GameDialog.Common;
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
        AsyncFuncDefs = [.. memberRegister.AsyncFuncDefs];
    }

    public List<VarDef> VarDefs { get; set; } = [];
    public List<FuncDef> FuncDefs { get; set; } = [];
    public List<AsyncFuncDef> AsyncFuncDefs { get; set; } = [];

    private readonly HashSet<string> s_ignoreNames =
    [
        "DialogBridge",
        "Properties",
        "Methods",
        "AsyncMethods",
        "Init",
        "RegisterProperty",
        "RegisterMethod",
        "RegisterAsyncMethod",
        "RegisterProperties",
        "RegisterMethods",
        "RegisterAsyncMethods",
    ];

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

            string fileNamespace = string.Empty;
            string fileClassName = string.Empty;
            List<VarDef> varDefs = [];
            List<FuncDef> funcDefs = [];
            List<AsyncFuncDef> asyncFuncDefs = [];

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

                    if (s_ignoreNames.Contains(propDeclaration.Identifier.Text))
                        continue;

                    VarDef varDef = new(propDeclaration.Identifier.Text, varType);
                    varDefs.Add(varDef);
                    VarDefs.Add(varDef);
                }
                else if (member is MethodDeclarationSyntax methodDeclaration)
                {
                    // ignore override methods
                    if (methodDeclaration.Modifiers.Any(x => x.Text == "override"))
                        continue;

                    if (s_ignoreNames.Contains(methodDeclaration.Identifier.Text))
                        continue;

                    string returnTypeString = methodDeclaration.ReturnType.ToString();

                    if (returnTypeString == "Task" || returnTypeString == "ValueTask")
                    {
                        AsyncFuncDef? funcDef = GetAsyncFuncDef(methodDeclaration);

                        if (funcDef != null)
                        {
                            asyncFuncDefs.Add(funcDef);
                            AsyncFuncDefs.Add(funcDef);
                        }
                    }
                    else
                    {
                        FuncDef? funcDef = GetFuncDef(methodDeclaration);

                        if (funcDef != null)
                        {
                            funcDefs.Add(funcDef);
                            FuncDefs.Add(funcDef);
                        }
                    }
                }
            }

            if (generateRegister)
                GenerateMemberFile(filePath, fileNamespace, fileClassName, varDefs, funcDefs, asyncFuncDefs);
        }
    }

    private static void GenerateMemberFile(
        string filePath,
        string fileNamespace,
        string fileClassName,
        List<VarDef> varDefs,
        List<FuncDef> funcDefs,
        List<AsyncFuncDef> asyncFuncDefs)
    {
        string newFilePath = Path.ChangeExtension(filePath, "Generated.cs");

        string content = "using GameCore.GUI.GameDialog;\n\n";

        if (fileNamespace.Length > 0)
            content += $"namespace {fileNamespace};\n\n";

        string varDefContent = string.Join("\n\n        ", varDefs.Select(x =>
        {
            return $$"""
            DialogBridgeBase.RegisterProperty(
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
            string func = "";

            if (x.ReturnType == VarType.Void)
                func = $"(args) => {{ {x.Name}({literalArgs}); return new(); }});";
            else
                func = $"(args) => new({x.Name}({literalArgs})));";

            return $$"""
            DialogBridgeBase.RegisterMethod(
                        name: nameof({{x.Name}}),
                        argTypes: [{{argTypesArg}}],
                        returnType: VarType.{{x.ReturnType}},
                        func: {{func}}
            """;
        }));

        string asyncFuncDefContent = string.Join("\n\n        ", asyncFuncDefs.Select(x =>
        {
            string literalArgs = string.Empty;

            for (int i = 0; i < x.ArgTypes.Count; i++)
            {
                if (i > 0)
                    literalArgs += ", ";

                literalArgs += $"args[{i}].{x.ArgTypes[i]}";
            }

            string argTypesArg = string.Join(", ", x.ArgTypes.Select(argType => $"VarType.{argType}"));
            string func = $"async (args) => await {x.Name}({literalArgs})";

            return $$"""
            DialogBridgeBase.RegisterAsyncMethod(
                        name: nameof({{x.Name}}),
                        argTypes: [{{argTypesArg}}],
                        func: {{func}});
            """;
        }));

        content +=
        $$"""
        public partial class {{fileClassName}}
        {
            public override void RegisterProperties()
            {
                base.RegisterProperties();
                {{varDefContent}}
            }

            public override void RegisterMethods()
            {
                base.RegisterMethods();
                {{funcDefContent}}
            }

            public override void RegisterAsyncMethods()
            {
                base.RegisterAsyncMethods();
                {{asyncFuncDefContent}}
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
    
    private static AsyncFuncDef? GetAsyncFuncDef(MethodDeclarationSyntax node)
    {
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

        AsyncFuncDef funcDef = new(funcName, args);
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

public class AsyncFuncDef
{
    public AsyncFuncDef(string name, List<VarType> argTypes)
    {
        Name = name;
        ArgTypes = argTypes;
    }

    public string Name { get; set; } = string.Empty;
    public List<VarType> ArgTypes { get; set; } = [];
}