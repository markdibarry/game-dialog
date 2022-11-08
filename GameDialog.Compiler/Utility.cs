using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace GameDialog.Compiler;

public class Utility
{
    public static void PrintTokens(CommonTokenStream stream)
    {
        foreach (var token in stream.GetTokens())
        {
            string text = token.Text
                .Replace("\r", @"\n")
                .Replace("\n", @"\r")
                .Replace("\t", @"\t");
            var tName = DialogLexer.DefaultVocabulary.GetSymbolicName(token.Type);
            Console.WriteLine($"pos:{token.Line},{token.Column,-10} {tName,-20} '{text}'");
        }
    }

    public static void PrintTree(ParserRuleContext context)
    {
        if (context.children == null)
            return;
        Console.WriteLine();
        for (int i = 0; i < context.children.Count; i++)
            GetBranch(context.children[i], "", i == context.ChildCount - 1);
    }

    private static void GetBranch(IParseTree branch, string indent, bool last)
    {
        Console.Write(indent);
        if (last)
        {
            Console.Write("\\--");
            indent += "   ";
        }
        else
        {
            Console.Write("|--");
            indent += "|  ";
        }
        if (branch is ParserRuleContext)
            Console.WriteLine(branch.GetType().Name.Replace("Context", ""));
        else
            Console.WriteLine($"[{branch.GetText().Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}]");
        for (int i = 0; i < branch.ChildCount; i++)
            GetBranch(branch.GetChild(i), indent, i == branch.ChildCount - 1);
    }
}
