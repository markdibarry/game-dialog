using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GameDialog.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DialogAnalyzer : DiagnosticAnalyzer
{
    public const string DialogTextLabelId = "DIA001";
    public const string DialogBridgeId = "DIA002";
    private static readonly DiagnosticDescriptor DialogTextLabelRule = new(
#pragma warning disable RS2008
        DialogTextLabelId,
#pragma warning restore RS2008
        "Replacement not performed",
        "Class '{0}' has [DialogTextLabel] attribute but has not been replaced",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor DialogBridgeRule = new(
#pragma warning disable RS2008
        DialogBridgeId,
#pragma warning restore RS2008
        "Class not partial",
        "Class '{0}' has [DialogBridge] attribute but is not a partial class",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DialogBridgeRule, DialogTextLabelRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        INamedTypeSymbol namedType = (INamedTypeSymbol)context.Symbol;

        if (namedType.TypeKind != TypeKind.Class)
            return;

        ImmutableArray<AttributeData> attributes = namedType.GetAttributes();

        foreach (var attribute in attributes)
        {
            string? attr = attribute.AttributeClass?.ToDisplayString();

            if (attr == "GameDialog.Runner.DialogTextLabelAttribute")
            {
                Location location = namedType.Locations.FirstOrDefault() ?? Location.None;
                Diagnostic diag = Diagnostic.Create(DialogTextLabelRule, location, namedType.Name);
                context.ReportDiagnostic(diag);
                return;
            }
            else if (attr == "GameDialog.Runner.DialogBridgeAttribute" && !IsPartial(namedType))
            {
                Location location = namedType.Locations.FirstOrDefault() ?? Location.None;
                Diagnostic diag = Diagnostic.Create(DialogBridgeRule, location, namedType.Name);
                context.ReportDiagnostic(diag);
                return;
            }
        }
    }

    private static bool IsPartial(INamedTypeSymbol namedType)
    {
        return namedType.DeclaringSyntaxReferences
            .Any(x =>
            {
                if (x.GetSyntax() is not BaseTypeDeclarationSyntax dec)
                    return false;

                return dec.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword));
            });
    }
}