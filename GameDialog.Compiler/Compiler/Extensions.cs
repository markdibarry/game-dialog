using System.Collections.Generic;
using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace GameDialog.Compiler;

public static class Extensions
{
    public static Range GetRange(this ParserRuleContext context)
    {
        return new Range(
            context.Start.Line - 1,
            context.Start.Column,
            context.Stop.Line - 1,
            context.Stop.Column + context.Stop.Text.Length);
    }

    /// <summary>
    /// Adds item if not in collection, then retrieves index
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <returns>Index of item</returns>
    public static int GetOrAdd<T>(this List<T> collection, T item)
    {
        int index = collection.IndexOf(item);

        if (index == -1)
        {
            collection.Add(item);
            index = collection.Count - 1;
        }

        return index;
    }

    public static List<int>? AddError(
        this List<Diagnostic> diagnostics,
        ParserRuleContext context,
        string message)
    {
        Diagnostic diagnostic = new()
        {
            Range = context.GetRange(),
            Message = message,
            Severity = DiagnosticSeverity.Error,
        };

        diagnostics.Add(diagnostic);
        return null;
    }
}
