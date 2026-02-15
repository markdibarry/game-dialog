using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GameCore.Dialog.Generator;

[Generator]
public partial class DialogGenerator : IIncrementalGenerator
{
    private static INamedTypeSymbol? _taskSymbol;
    private static INamedTypeSymbol? _valueTaskSymbol;
    private static INamedTypeSymbol? _bridgeAttrType;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) =>
                {
                    return s is ClassDeclarationSyntax cds
                        && cds.AttributeLists.Count > 0
                        && cds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                },
                transform: static (ctx, _) =>
                {
                    var cds = (ClassDeclarationSyntax)ctx.Node;
                    INamedTypeSymbol? symbol = ctx.SemanticModel.GetDeclaredSymbol(cds);

                    if (symbol is null)
                        return symbol;

                    bool hasAttr = symbol.GetAttributes()
                        .Any(a => a.AttributeClass?.ToDisplayString() == "GameCore.Dialog.DialogBridgeAttribute");

                    return hasAttr ? symbol : null;
                })
            .Where(x => x is not null)
            .Collect();
        var data = context.CompilationProvider.Combine(classes);
        context.RegisterSourceOutput(data, FindAndGenerate);
    }

    private static void FindAndGenerate(
        SourceProductionContext spc,
        (Compilation Comp, ImmutableArray<INamedTypeSymbol?> Classes) data)
    {
        Compilation compilation = data.Comp;
        ImmutableArray<INamedTypeSymbol?> classes = data.Classes;

        if (classes == null || classes.Length == 0)
            return;

        _bridgeAttrType = compilation.GetTypeByMetadataName("GameCore.Dialog.DialogBridgeAttribute");
        _taskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        _valueTaskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");

        if (_bridgeAttrType == null || _taskSymbol == null || _valueTaskSymbol == null)
            return;

        GenerateBridge(spc, classes);
    }
}
