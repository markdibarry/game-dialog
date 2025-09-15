using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameDialog.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GameDialog.Compiler;

public class MemberRegister
{
    public MemberRegister() { }

    public List<VarDef> PredefinedVarDefs { get; set; } = [];
    public List<FuncDef> PredefinedFuncDefs { get; set; } = [];
    public List<VarDef> VarDefs { get; set; } = [];
    public List<FuncDef> FuncDefs { get; set; } = [];

    public void ResetDefs()
    {
        VarDefs.Clear();
        VarDefs.AddRange(PredefinedVarDefs);
        FuncDefs.Clear();
        FuncDefs.AddRange(PredefinedFuncDefs);
    }

    public void SetMembersFromFile(string fileName, string rootPath, bool generate)
    {
        if (string.IsNullOrEmpty(rootPath))
            return;

        string[] files = Directory.GetFiles(rootPath, fileName, SearchOption.AllDirectories);

        if (files.Length != 1)
            return;

        PredefinedFuncDefs.Clear();
        PredefinedVarDefs.Clear();
        string filePath = files[0];
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        CompilationUnitSyntax? root = tree.GetCompilationUnitRoot();
        var members = root.DescendantNodes().OfType<MemberDeclarationSyntax>();

        string fileNamespace = string.Empty;
        string fileClassName = string.Empty;
        List<VarDef> varDefs = [];
        List<FuncDef> funcDefs = [];

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
                PredefinedVarDefs.Add(varDef);
            }
            else if (member is MethodDeclarationSyntax methodDeclaration)
            {
                FuncDef? funcDef = GetFuncDef(methodDeclaration);

                if (funcDef != null)
                {
                    funcDefs.Add(funcDef);
                    PredefinedFuncDefs.Add(funcDef);
                }
            }
        }

        if (generate)
            GenerateMemberFile(filePath, fileNamespace, fileClassName, varDefs, funcDefs);
    }

    private static void GenerateMemberFile(
        string filePath,
        string fileNamespace,
        string fileClassName,
        List<VarDef> varDefs,
        List<FuncDef> funcDefs)
    {
        StringBuilder sb = new();
        string newFilePath = Path.ChangeExtension(filePath, "G.cs");
        sb.AppendLine("using System;");
        sb.AppendLine("using GameDialog.Common;");
        sb.AppendLine("using GameDialog.Runner;");
        sb.AppendLine();

        if (fileNamespace.Length > 0)
        {
            sb.AppendLine($"namespace {fileNamespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"public partial class {fileClassName}");
        sb.AppendLine("{");

        sb.AppendLine($$"""
            protected override VarType GetPredefinedMethodReturnType(string funcName)
            {
                return funcName switch
                {
                    {{
                        string.Join(
                            "\n            ",
                            funcDefs.Select(x => $"nameof({x.Name}) => VarType.{x.ReturnType},"))
                    }}
                    _ => VarType.Undefined
                };
            }
        """);

        sb.AppendLine();
        sb.AppendLine($$"""
            protected override VarType GetPredefinedPropertyType(string propertyName)
            {
                return propertyName switch
                {
                    {{
                        string.Join(
                            "\n            ",
                            varDefs.Select(x => $"nameof({x.Name}) => VarType.{x.Type},"))
                    }}
                    _ => VarType.Undefined
                };
            }
        """);

        sb.AppendLine();
        sb.AppendLine($$"""
            protected override TextVariant CallPredefinedMethod(string funcName, ReadOnlySpan<TextVariant> args)
            {
                switch (funcName)
                {
                    {{
                        string.Join(
                        "\n            ",
                        funcDefs.Select(x =>
                        {
                            string args = "";

                            for (int i = 0; i < x.ArgTypes.Count; i++)
                            {
                                if (i > 0)
                                    args += ", ";

                                args += $"args[{i}].{x.ArgTypes[i]}";
                            }

                            if (x.ReturnType == VarType.Void)
                            {
                                return $$"""
                                case nameof({{x.Name}}):
                                                {{x.Name}}({{args}});
                                                return new();
                                """;
                            }
                            else
                            {
                                return $$"""
                                case nameof({{x.Name}}):
                                                return new({{x.Name}}({{args}}));
                                """;
                            }
                        }))
                    }}
                    default:
                        return new();
                }
            }
        """);

        sb.AppendLine();
        sb.AppendLine($$"""
            protected override TextVariant GetPredefinedProperty(string propertyName)
            {
                return propertyName switch
                {
                    {{
                        string.Join(
                            "\n        ",
                            varDefs.Select(x => $"nameof({x.Name}) => new({x.Name}),"))
                    }}
                    _ => new()
                };
            }
        """);

        sb.AppendLine();
        sb.AppendLine($$"""
            protected override void SetPredefinedProperty(string propertyName, TextVariant value)
            {
                switch (propertyName)
                {
                    {{
                        string.Join(
                            "\n        ",
                            varDefs.Select(x =>
                            {
                                return $$"""
                                    case nameof({{x.Name}}):
                                        {{x.Name}} = value.{{x.Type}};
                                        break;
                                    """;
                            }))
                    }}
                    default:
                        break;
                }
            }
        """);
        sb.AppendLine("}");

        File.WriteAllText(newFilePath, sb.ToString());
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

            args.Add(paramType);
        }

        if (!argsValid)
            return null;

        FuncDef funcDef = new(funcName, returnType, args);
        return funcDef;
    }
}

