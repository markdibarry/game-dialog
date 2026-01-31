using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GameDialog.Generator;

public partial class DialogGenerator
{
    private record BridgeObject
    {
        public BridgeObject(
            string className,
            string? ns,
            Dictionary<string, FuncDef> funcDefs,
            Dictionary<string, VarDef> varDefs)
        {
            ClassName = className;
            NS = ns;
            FullName = NS == null ? ClassName : NS + '.' + ClassName;
            FuncDefs = funcDefs;
            VarDefs = varDefs;
        }

        public string ClassName { get; }
        public string? NS { get; }
        public string FullName { get; }
        public Dictionary<string, FuncDef> FuncDefs { get; }
        public Dictionary<string, VarDef> VarDefs { get; }
    }

    private static void GenerateBridge(SourceProductionContext spc, ImmutableArray<INamedTypeSymbol?> classSymbols)
    {
        List<BridgeObject> bridgeObjects = new(classSymbols.Length);

        foreach (INamedTypeSymbol? classSymbol in classSymbols)
        {
            if (classSymbol == null)
                continue;

            string? ns = null;

            if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
                ns = (string?)classSymbol.ContainingNamespace.ToDisplayString();

            ImmutableArray<ISymbol> members = classSymbol.GetMembers();
            Dictionary<string, FuncDef> funcDefs = [];
            Dictionary<string, VarDef> varDefs = [];

            foreach (ISymbol member in members)
            {
                if (member is IMethodSymbol method)
                    AddFuncDef(classSymbol, method, funcDefs);
                else if (member is IPropertySymbol property)
                    AddVarDef(classSymbol, property, varDefs);
            }

            bridgeObjects.Add(new(classSymbol.Name, ns, funcDefs, varDefs));
        }

        string sourceText = GenerateBridgeText(bridgeObjects);
        spc.AddSource($"GameDialog.Runner.GeneratedBridge.g", SourceText.From(sourceText, Encoding.UTF8));
    }

    private static void AddFuncDef(INamedTypeSymbol classSymbol, IMethodSymbol method, Dictionary<string, FuncDef> funcDefs)
    {
        if (method.MethodKind != MethodKind.Ordinary
            || method.DeclaredAccessibility != Accessibility.Public
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, classSymbol))
        {
            return;
        }

        (VarType returnType, bool isAwaitable) = GetMethodReturnType(method.ReturnType);

        if (returnType == VarType.Undefined)
            return;

        VarType[] argTypes = new VarType[method.Parameters.Length];

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            IParameterSymbol parameter = method.Parameters[i];

            VarType type = parameter.Type.SpecialType switch
            {
                SpecialType.System_Boolean => VarType.Bool,
                SpecialType.System_Single => VarType.Float,
                SpecialType.System_String => VarType.String,
                _ => VarType.Undefined
            };

            if (type == VarType.Undefined)
                return;

            argTypes[i] = type;
        }

        funcDefs.Add(method.Name, new()
        {
            Name = method.Name,
            ReturnType = returnType,
            IsAwaitable = isAwaitable,
            ArgTypes = argTypes,
            IsStatic = method.IsStatic
        });
    }

    private static void AddVarDef(INamedTypeSymbol classSymbol, IPropertySymbol prop, Dictionary<string, VarDef> varDefs)
    {
        if (prop.DeclaredAccessibility != Accessibility.Public
            || !SymbolEqualityComparer.Default.Equals(prop.ContainingType, classSymbol))
        {
            return;
        }

        VarType type = prop.Type.SpecialType switch
        {
            SpecialType.System_Boolean => VarType.Bool,
            SpecialType.System_Single => VarType.Float,
            SpecialType.System_String => VarType.String,
            _ => VarType.Undefined
        };

        if (type == VarType.Undefined)
            return;

        varDefs.Add(prop.Name, new()
        {
            Name = prop.Name,
            Type = type,
            IsStatic = prop.IsStatic
        });
    }

    private static (VarType, bool) GetMethodReturnType(ITypeSymbol typeSymbol)
    {
        VarType returnType = typeSymbol.SpecialType switch
        {
            SpecialType.System_Boolean => VarType.Bool,
            SpecialType.System_Single => VarType.Float,
            SpecialType.System_String => VarType.String,
            SpecialType.System_Void => VarType.Void,
            _ => VarType.Undefined
        };

        bool isAwaitable = false;

        if (returnType == VarType.Undefined)
        {
            if (IsTaskOrValueTask(typeSymbol, _taskSymbol, _valueTaskSymbol))
            {
                returnType = VarType.Void;
                isAwaitable = true;
            }
        }

        return (returnType, isAwaitable);

        static bool IsTaskOrValueTask(ITypeSymbol t, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        {
            if (t is not INamedTypeSymbol nts)
                return false;

            if (taskSymbol != null && SymbolEqualityComparer.Default.Equals(nts.OriginalDefinition, taskSymbol))
                return true;

            return valueTaskSymbol != null && SymbolEqualityComparer.Default.Equals(nts.OriginalDefinition, valueTaskSymbol);
        }
    }

    private static string GenerateBridgeText(List<BridgeObject> bridgeObjects)
    {
        StringBuilder tempSB = new();
        StringBuilder sb = new();
        sb.AppendLine("""
        // <auto-generated />
        using System;
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;
        using System.Threading.Tasks;
        using GameDialog.Runner;

        """);

        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject? x = bridgeObjects[i];

            if (x.NS != null)
            {
                sb.AppendLine($"namespace {x.NS}");
                sb.AppendLine("{");
            }

            sb.AppendLine($$"""
                public partial class {{x.ClassName}}
                {
                    public {{x.ClassName}}(GameDialog.Runner.Dialog dialog)
                    {
                        Dialog = dialog;
                    }

                    public GameDialog.Runner.Dialog Dialog { get; }
                }
            """);

            if (x.NS != null)
                sb.AppendLine("}");

            sb.AppendLine();
        }

        sb.AppendLine($$"""
        namespace GameDialog.Runner
        {
            public class GeneratedBridge : DialogBridge
            {
                public GeneratedBridge(Dialog dialog)
                {{{CollectConstructorLogic(bridgeObjects, tempSB)}}
                }{{CollectPrivateFields(bridgeObjects, tempSB)}}

                [ModuleInitializer]
                public static void SetCreateType()
                {
                    DialogBridge.Create = (dialog) => new GeneratedBridge(dialog);{{CollectFuncDefs(bridgeObjects, tempSB)}}{{CollectVarDefs(bridgeObjects, tempSB)}}
                }

                protected override TextVariant CallMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args)
                {
                    switch (funcName)
                    {{{CollectCallMethodSwitch(bridgeObjects, tempSB)}}
                        default:
                            return new();
                    }
                }

                protected override void CallAsyncMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args)
                {
                    switch (funcName)
                    {{{CollectCallAsyncMethodSwitch(bridgeObjects, tempSB)}}
                        default:
                            return;
                    }{{CollectAsyncLocalFunctions(bridgeObjects, tempSB)}}
                }

                protected override TextVariant GetProperty(ReadOnlySpan<char> propertyName)
                {
                    return propertyName switch
                    {{{CollectGetPropertySwitch(bridgeObjects, tempSB)}}
                        _ => new()
                    };
                }

                protected override void SetProperty(ReadOnlySpan<char> propertyName, TextVariant value)
                {
                    switch (propertyName)
                    {{{CollectSetPropertySwitch(bridgeObjects, tempSB)}}
                        default:
                            break;
                    }
                }
            }
        }

        """);

        return sb.ToString();
    }

    private static string CollectConstructorLogic(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            tempSB.Append($"\n            _userBridge_{i} = new(dialog);");
        }

        string result = tempSB.ToString();
        tempSB.Clear();
        return result;
    }

    private static string CollectPrivateFields(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            if (i == 0)
                tempSB.AppendLine();

            BridgeObject x = bridgeObjects[i];
            tempSB.Append($"\n        private {x.FullName} _userBridge_{i};");
        }

        string result = tempSB.ToString();
        tempSB.Clear();
        return result;
    }

    private static string CollectFuncDefs(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject bridgeObject = bridgeObjects[i];

            if (bridgeObject.FuncDefs.Count == 0)
                continue;

            foreach (var kvp in bridgeObject.FuncDefs)
            {
                FuncDef funcDef = kvp.Value;
                tempSB.AppendLine();
                tempSB.Append($$"""
                            DialogBridge.AddFuncDef(
                                nameof({{bridgeObject.FullName}}.{{funcDef.Name}}),
                                VarType.{{funcDef.ReturnType}},
                                [{{string.Join(", ", funcDef.ArgTypes.Select(x => $"VarType.{x}"))}}],
                                {{(funcDef.IsAwaitable ? "true" : "false")}});
                """);
            }
        }

        string result = tempSB.ToString();
        tempSB.Clear();
        return result;
    }

    private static string CollectVarDefs(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject bridgeObject = bridgeObjects[i];

            if (bridgeObject.VarDefs.Count == 0)
                continue;

            foreach (var kvp in bridgeObject.VarDefs)
            {
                var varDef = kvp.Value;
                tempSB.Append($"\n            DialogBridge.AddVarDef(nameof({bridgeObject.FullName}.{varDef.Name}), VarType.{varDef.Type});");
            }
        }

        string result = tempSB.ToString();
        tempSB.Clear();
        return result;
    }

    private static string CollectCallMethodSwitch(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject bridgeObject = bridgeObjects[i];

            foreach (var kvp in bridgeObject.FuncDefs)
            {
                var func = kvp.Value;
                tempSB.AppendLine();
                string callName = func.IsStatic ? bridgeObject.FullName : "_userBridge_" + i;

                if (func.ReturnType == VarType.Void)
                {
                    tempSB.Append($$"""
                                    case nameof({{bridgeObject.FullName}}.{{func.Name}}):
                                        {{(func.IsAwaitable ? "_ = " : "")}}{{callName}}.{{func.Name}}({{GetArgs(func)}});
                                        return new();
                    """
                    );
                }
                else
                {
                    tempSB.Append($$"""
                                    case nameof({{bridgeObject.FullName}}.{{func.Name}}):
                                        return new({{callName}}.{{func.Name}}({{GetArgs(func)}}));
                    """
                    );
                }
            }
        }

        string result = tempSB.ToString();
        tempSB.Clear();
        return result;

        static string GetArgs(FuncDef func)
        {
            return string.Join(", ", func.ArgTypes.Select((x, i) => $"args[{i}].{x}"));
        }
    }

    private static string CollectCallAsyncMethodSwitch(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject bridgeObject = bridgeObjects[i];

            foreach (var kvp in bridgeObject.FuncDefs)
            {
                var func = kvp.Value;

                if (!func.IsAwaitable)
                    continue;

                tempSB.AppendLine();
                tempSB.Append($$"""
                                case nameof({{bridgeObject.FullName}}.{{func.Name}}):
                                    InternalLocal{{func.Name}}({{GetArgs(func)}});
                                    return;
                """);
            }
        }
        string result = tempSB.ToString();
        tempSB.Clear();
        return result;

        static string GetArgs(FuncDef func)
        {
            return string.Join(", ", func.ArgTypes.Select((x, i) => $"args[{i}]"));
        }
    }

    private static string CollectAsyncLocalFunctions(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject bridgeObject = bridgeObjects[i];

            foreach (var kvp in bridgeObject.FuncDefs)
            {
                var func = kvp.Value;

                if (!func.IsAwaitable)
                    continue;

                if (tempSB.Length == 0)
                    tempSB.AppendLine();

                string callName = func.IsStatic ? bridgeObject.FullName : "_userBridge_" + i;
                tempSB.AppendLine();
                tempSB.Append($$"""
                            async void InternalLocal{{func.Name}}({{GetParams(func)}})
                            {
                                await {{callName}}.{{func.Name}}({{GetArgs(func)}});
                                _userBridge_{{i}}.Dialog.Resume();
                            }
                """);
            }
        }

        string result = tempSB.ToString();
        tempSB.Clear();
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

    private static string CollectGetPropertySwitch(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject bridgeObject = bridgeObjects[i];

            foreach (var kvp in bridgeObject.VarDefs)
            {
                VarDef varDef = kvp.Value;
                string callName = varDef.IsStatic ? bridgeObject.FullName : "_userBridge_" + i;
                tempSB.Append($"\n                nameof({bridgeObject.FullName}.{varDef.Name}) => new({callName}.{varDef.Name}),");
            }
        }

        string result = tempSB.ToString();
        tempSB.Clear();
        return result;
    }

    private static string CollectSetPropertySwitch(List<BridgeObject> bridgeObjects, StringBuilder tempSB)
    {
        for (int i = 0; i < bridgeObjects.Count; i++)
        {
            BridgeObject bridgeObject = bridgeObjects[i];

            foreach (var kvp in bridgeObject.VarDefs)
            {
                VarDef varDef = kvp.Value;
                string callName = varDef.IsStatic ? bridgeObject.FullName : "_userBridge_" + i;
                tempSB.AppendLine();
                tempSB.Append($$"""
                                case nameof({{bridgeObject.FullName}}.{{varDef.Name}}):
                                    {{callName}}.{{varDef.Name}} = value.{{varDef.Type}};
                                    break;
                """);
            }
        }

        string result = tempSB.ToString();
        tempSB.Clear();
        return result;
    }
}