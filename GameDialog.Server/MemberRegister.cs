using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameDialog.Runner;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GameDialog.Server;

public class MemberRegister
{
    public Dictionary<string, VarDef> PredefinedVarDefs { get; } = [];
    public Dictionary<string, FuncDef> PredefinedFuncDefs { get; } = [];

    private readonly static StringBuilder _tempSB = new();

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
                VarType varType = GetVarType(propDeclaration.Type);

                if (varType == VarType.Undefined)
                    continue;

                string varName = propDeclaration.Identifier.Text;
                VarDef varDef = new()
                {
                    Name = varName,
                    Type = varType
                };
                PredefinedVarDefs.Add(varName, varDef);
            }
            else if (member is MethodDeclarationSyntax methodDeclaration)
            {
                FuncDef? funcDef = GetFuncDef(methodDeclaration);

                if (funcDef != null)
                    PredefinedFuncDefs.Add(funcDef.Name, funcDef);
            }
        }

        if (generate)
            GenerateMemberFile(filePath, fileNamespace, fileClassName, PredefinedVarDefs, PredefinedFuncDefs);
    }

    private static string GetAllFuncCases(Dictionary<string, FuncDef> funcDefs)
    {
        foreach (var kvp in funcDefs)
        {
            var func = kvp.Value;
            _tempSB.AppendLine();

            if (func.ReturnType == VarType.Void)
            {
                _tempSB.Append($$"""
                                case nameof({{func.Name}}):
                                    {{(func.Awaitable ? "_ = " : "")}}{{func.Name}}({{GetArgs(func)}});
                                    return new();
                    """
                );
            }
            else
            {
                _tempSB.Append($$"""
                                case nameof({{func.Name}}):
                                    return new({{func.Name}}({{GetArgs(func)}}));
                    """
                );
            }
        }

        string result = _tempSB.ToString();
        _tempSB.Clear();
        return result;

        static string GetArgs(FuncDef func)
        {
            return string.Join(", ", func.ArgTypes.Select((x, i) => $"args[{i}].{x}"));
        }
    }

    private static string GetAllAsyncFuncCases(Dictionary<string, FuncDef> funcDefs)
    {
        foreach (var kvp in funcDefs)
        {
            var func = kvp.Value;

            if (!func.Awaitable)
                continue;

            _tempSB.AppendLine();
            _tempSB.Append($$"""
                        case nameof({{func.Name}}):
                            InternalLocal{{func.Name}}({{GetArgs(func)}});
                            return;
            """);
        }

        string result = _tempSB.ToString();
        _tempSB.Clear();
        return result;

        static string GetArgs(FuncDef func)
        {
            return string.Join(", ", func.ArgTypes.Select((x, i) => $"args[{i}]"));
        }
    }

    private static string GetAllAsyncLocalFunctions(Dictionary<string, FuncDef> funcDefs)
    {
        foreach (var kvp in funcDefs)
        {
            var func = kvp.Value;

            if (!func.Awaitable)
                continue;

            if (_tempSB.Length == 0)
                _tempSB.AppendLine();

            _tempSB.AppendLine();
            _tempSB.Append($$"""
                    async void InternalLocal{{func.Name}}({{GetParams(func)}})
                    {
                        await {{func.Name}}({{GetArgs(func)}});
                        Dialog.Resume();
                    }
            """);
        }

        string result = _tempSB.ToString();
        _tempSB.Clear();
        return result;

        string GetParams(FuncDef func)
        {
            return string.Join(", ", func.ArgTypes.Select((x, i) => $"TextVariant arg{i}"));
        }

        string GetArgs(FuncDef func)
        {
            return string.Join(", ", func.ArgTypes.Select((x, i) => $"arg{i}.{x}"));
        }
    }

    private static string GetAllSetProperties(Dictionary<string, VarDef> varDefs)
    {
        foreach (var kvp in varDefs)
        {
            VarDef varDef = kvp.Value;
            _tempSB.AppendLine();
            _tempSB.Append($$"""
                        case nameof({{varDef.Name}}):
                            {{varDef.Name}} = value.{{varDef.Type}};
                            break;
            """);
        }

        string result = _tempSB.ToString();
        _tempSB.Clear();
        return result;
    }

    private static string GetAllFuncDefs(Dictionary<string, FuncDef> funcDefs)
    {
        if (funcDefs.Count == 0)
            return string.Empty;

        _tempSB.AppendLine($"{nameof(DialogBridgeBase.InternalFuncDefs)} = new()");
        _tempSB.AppendLine("        {");

        foreach (var kvp in funcDefs)
        {
            var funcDef = kvp.Value;
            _tempSB.AppendLine($$"""
                        ["{{funcDef.Name}}"] = new()
                        {
                            {{nameof(FuncDef.Name)}} = "{{funcDef.Name}}",
                            {{nameof(FuncDef.ReturnType)}} = VarType.{{funcDef.ReturnType}},
                            {{nameof(FuncDef.Awaitable)}} = {{(funcDef.Awaitable ? "true" : "false")}},
                            {{nameof(FuncDef.ArgTypes)}} = [{{string.Join(", ", funcDef.ArgTypes.Select(x => $"VarType.{x}"))}}]
                        },
            """);
        }

        _tempSB.Append("        };");
        string result = _tempSB.ToString();
        _tempSB.Clear();
        return result;
    }

    private static string GetAllVarDefs(Dictionary<string, VarDef> varDefs)
    {
        if (varDefs.Count == 0)
            return string.Empty;

        _tempSB.AppendLine($"{nameof(DialogBridgeBase.InternalVarDefs)} = new()");
        _tempSB.AppendLine("        {");

        foreach (var kvp in varDefs)
        {
            var varDef = kvp.Value;
            _tempSB.AppendLine($$"""
                        ["{{varDef.Name}}"] = new()
                        {
                            {{nameof(VarDef.Name)}} = "{{varDef.Name}}",
                            {{nameof(VarDef.Type)}} = VarType.{{varDef.Type}},
                        },
            """);
        }

        _tempSB.Append("        };");
        string result = _tempSB.ToString();
        _tempSB.Clear();
        return result;
    }

    private static void GenerateMemberFile(
        string filePath,
        string fileNamespace,
        string fileClassName,
        Dictionary<string, VarDef> varDefs,
        Dictionary<string, FuncDef> funcDefs)
    {
        StringBuilder sb = new();
        string newFilePath = Path.ChangeExtension(filePath, "g.cs");
        sb.AppendLine("""
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GameDialog.Runner;

""");

        if (fileNamespace.Length > 0)
        {
            sb.AppendLine($"namespace {fileNamespace};");
            sb.AppendLine();
        }

        sb.AppendLine($$"""
        public partial class {{fileClassName}}
        {
            public {{fileClassName}}(DialogBase dialog)
            {
                Dialog = dialog;
            }

            private DialogBase Dialog { get; }

            [ModuleInitializer]
            public static void InternalSetCreateType()
            {
                InternalCreate = (dialog) => new {{fileClassName}}(dialog);
                {{GetAllFuncDefs(funcDefs)}}
                {{GetAllVarDefs(varDefs)}}
            }

            public override TextVariant InternalCallMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args)
            {
                switch (funcName)
                {{{GetAllFuncCases(funcDefs)}}
                    default:
                        return new();
                }
            }

            public override void InternalCallAsyncMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args)
            {
                switch (funcName)
                {{{GetAllAsyncFuncCases(funcDefs)}}
                    default:
                        return;
                }{{GetAllAsyncLocalFunctions(funcDefs)}}
            }

            public override TextVariant InternalGetProperty(ReadOnlySpan<char> propertyName)
            {
                return propertyName switch
                {{{string.Concat(varDefs.Select(kvp => $"\n            nameof({kvp.Value.Name}) => new({kvp.Value.Name}),"))}}
                    _ => new()
                };
            }

            public override void InternalSetProperty(ReadOnlySpan<char> propertyName, TextVariant value)
            {
                switch (propertyName)
                {{{GetAllSetProperties(varDefs)}}
                    default:
                        break;
                }
            }
        }
        """);

        File.WriteAllText(newFilePath, sb.ToString());
    }

    private static VarType GetVarType(TypeSyntax typeSyntax)
    {
        if (IsType(typeSyntax, SyntaxKind.FloatKeyword, "Float"))
            return VarType.Float;
        else if (IsType(typeSyntax, SyntaxKind.BoolKeyword, "Boolean"))
            return VarType.Bool;
        else if (IsType(typeSyntax, SyntaxKind.StringKeyword, "String"))
            return VarType.String;

        return VarType.Undefined;
    }

    private static MethodType GetMethodReturnType(TypeSyntax typeSyntax)
    {
        if (IsType(typeSyntax, SyntaxKind.FloatKeyword, "Float"))
            return MethodType.Float;
        else if (IsType(typeSyntax, SyntaxKind.BoolKeyword, "Boolean"))
            return MethodType.Bool;
        else if (IsType(typeSyntax, SyntaxKind.StringKeyword, "String"))
            return MethodType.String;
        else if (IsType(typeSyntax, SyntaxKind.VoidKeyword, string.Empty))
            return MethodType.Void;
        else if (IsType(typeSyntax, SyntaxKind.None, "Task"))
            return MethodType.Task;
        else if (IsType(typeSyntax, SyntaxKind.None, "ValueTask"))
            return MethodType.Task;

        return MethodType.Undefined;
    }

    private static VarType GetVarType(MethodType checkType)
    {
        return checkType switch
        {
            MethodType.Bool => VarType.Bool,
            MethodType.Float => VarType.Float,
            MethodType.String => VarType.String,
            MethodType.Task or
            MethodType.Void => VarType.Void,
            _ => VarType.Undefined
        };
    }

    private static FuncDef? GetFuncDef(MethodDeclarationSyntax node)
    {
        MethodType returnType = GetMethodReturnType(node.ReturnType);

        if (returnType == MethodType.Undefined)
            return null;

        string funcName = node.Identifier.Text;
        VarType[] args = new VarType[node.ParameterList.Parameters.Count];

        for (int i = 0; i < node.ParameterList.Parameters.Count; i++)
        {
            ParameterSyntax? arg = node.ParameterList.Parameters[i];

            if (arg.Type == null)
                return null;

            VarType argType = GetVarType(arg.Type);

            if (argType == VarType.Undefined)
                return null;

            args[i] = argType;
        }

        FuncDef funcDef = new()
        {
            Name = funcName,
            ReturnType = GetVarType(returnType),
            Awaitable = returnType == MethodType.Task,
            ArgTypes = args
        };
        return funcDef;
    }

    private static bool IsType(TypeSyntax typeSyntax, SyntaxKind syntaxKind, string valueText)
    {
        if (typeSyntax is PredefinedTypeSyntax pts)
            return pts.Keyword.IsKind(syntaxKind);
        else if (typeSyntax is IdentifierNameSyntax id)
            return id.Identifier.ValueText == valueText;
        else if (typeSyntax is QualifiedNameSyntax qn)
            return qn.Right.Identifier.ValueText == valueText;

        return false;
    }

    private enum MethodType
    {
        Undefined,
        String,
        Bool,
        Float,
        Void,
        Task
    }
}

