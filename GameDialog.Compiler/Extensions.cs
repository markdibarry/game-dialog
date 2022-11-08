using Antlr4.Runtime;
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
}
