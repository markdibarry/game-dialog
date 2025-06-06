using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace GameDialog.Compiler;

public static class Utility
{
    private static readonly StringBuilder s_sb = new();

    public static string PrintTokens(CommonTokenStream stream)
    {
        foreach (var token in stream.GetTokens())
        {
            string text = token.Text
                .Replace("\r", @"\n")
                .Replace("\n", @"\r")
                .Replace("\t", @"\t");
            var tName = DialogLexer.DefaultVocabulary.GetSymbolicName(token.Type);
            s_sb.AppendLine($"pos:{token.Line},{token.Column,-10} {tName,-20} '{text}'");
        }

        string result = s_sb.ToString();
        s_sb.Clear();
        return result;
    }

    public static string PrintTree(ParserRuleContext context)
    {
        if (context.children == null)
            return string.Empty;

        s_sb.AppendLine();

        for (int i = 0; i < context.children.Count; i++)
            GetBranch(context.children[i], "", i == context.ChildCount - 1);

        string result = s_sb.ToString();
        s_sb.Clear();
        return result;
    }

    private static void GetBranch(IParseTree branch, string indent, bool last)
    {
        s_sb.Append(indent);

        if (last)
        {
            s_sb.Append("\\--");
            indent += "   ";
        }
        else
        {
            s_sb.Append("|--");
            indent += "|  ";
        }

        if (branch is ParserRuleContext)
            s_sb.AppendLine(branch.GetType().Name.Replace("Context", ""));
        else
            s_sb.AppendLine($"[{branch.GetText().Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}]");

        for (int i = 0; i < branch.ChildCount; i++)
            GetBranch(branch.GetChild(i), indent, i == branch.ChildCount - 1);
    }
}
