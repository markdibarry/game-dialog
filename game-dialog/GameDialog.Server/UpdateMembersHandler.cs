using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameDialog.Runner;
using MediatR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace GameDialog.Server;

public class UpdateMembersHandler : IJsonRpcRequestHandler<UpdateMembersRequest, UpdateMembersResponse>
{
    public UpdateMembersHandler(ILanguageServerFacade server)
    {
        _server = server;
    }

    public static bool Processing { get; private set; }

    private readonly ILanguageServerFacade _server;
    private static INamedTypeSymbol? _taskSymbol;
    private static INamedTypeSymbol? _valueTaskSymbol;

    public async Task<UpdateMembersResponse> Handle(UpdateMembersRequest request, CancellationToken ct)
    {
        if (!GameDialogServer.FirstTimeTCS.Task.IsCompleted || Processing)
            return new();

        try
        {
            Processing = true;
            string rootPath = _server.ClientSettings.RootPath!;
            await CollectMembersAsync(rootPath, ct);
        }
        finally
        {
            Processing = false;
        }

        return new();
    }

    public static async Task CollectMembersAsync(string root, CancellationToken ct)
    {
        DialogBridge.FuncDefs.Clear();
        DialogBridge.VarDefs.Clear();
        IEnumerable<string> csprojPaths = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories);
        var workspace = MSBuildWorkspace.Create();

        foreach (string csprojPath in csprojPaths)
        {
            if (File.ReadAllText(csprojPath).Contains("GameDialog"))
            {
                Project project = await workspace.OpenProjectAsync(csprojPath, cancellationToken: ct);
                await ScanProjectAsync(project, ct);
            }
        }
    }

    private static async Task ScanProjectAsync(Project project, CancellationToken ct)
    {
        Compilation? compilation = await project.GetCompilationAsync(ct);

        if (compilation == null)
            return;

        _taskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        _valueTaskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        INamedTypeSymbol? attrSymbol = compilation.GetTypeByMetadataName("GameDialog.Runner.DialogBridgeAttribute");

        if (_taskSymbol == null || _valueTaskSymbol == null || attrSymbol == null)
            return;

        Dictionary<INamedTypeSymbol, byte> seen = new(SymbolEqualityComparer.Default);

        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(tree);
            SyntaxNode root = await tree.GetRootAsync(ct);
            IEnumerable<ClassDeclarationSyntax> classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (ClassDeclarationSyntax cds in classDecls)
            {
                if (cds.AttributeLists.Count == 0 || !cds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    continue;

                if (semanticModel.GetDeclaredSymbol(cds, ct) is not INamedTypeSymbol typeSymbol)
                    continue;

                bool hasAttr = typeSymbol.GetAttributes()
                    .Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attrSymbol));

                if (hasAttr && !seen.ContainsKey(typeSymbol))
                {
                    seen.TryAdd(typeSymbol, default);
                    var members = typeSymbol.GetMembers();

                    foreach (ISymbol member in members)
                    {
                        if (member is IMethodSymbol method)
                            AddFuncDef(typeSymbol, method, DialogBridge.FuncDefs);
                        else if (member is IPropertySymbol property)
                            AddVarDef(typeSymbol, property, DialogBridge.VarDefs);
                    }
                }
            }
        }
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

            var type = parameter.Type.SpecialType switch
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
            Awaitable = isAwaitable,
            ArgTypes = argTypes
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
            Type = type
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
}

[Method("dialog/updateMembers")]
public class UpdateMembersRequest : IRequest<UpdateMembersResponse>
{
}

public class UpdateMembersResponse
{
}