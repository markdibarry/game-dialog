using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using GameDialogParser;

var fileName = "test.txt";
var fileContents = File.ReadAllText(fileName);

var inputStream = new AntlrInputStream(fileContents);
var lexer = new DialogLexer(inputStream);
var commonTokenStream = new CommonTokenStream(lexer);
var parser = new DialogParser(commonTokenStream);
var context = parser.script();
PrintTokens(commonTokenStream);
PrintTree(context);
var visitor = new BasicDialogVisitor();
visitor.Visit(context);

void PrintTokens(CommonTokenStream stream)
{
    foreach (var token in commonTokenStream.GetTokens())
    {
        string text = token.Text
            .Replace("\r", @"\n")
            .Replace("\n", @"\r")
            .Replace("\t", @"\t");
        var tName = DialogLexer.DefaultVocabulary.GetSymbolicName(token.Type);
        Console.WriteLine($"pos:{token.Line},{token.Column,-10} {tName,-20} '{text}'");
    }
}

void PrintTree(DialogParser.ScriptContext context)
{
    if (context.children == null)
        return;
    Console.WriteLine();
    for (int i = 0; i < context.children.Count; i++)
        GetBranch(context.children[i], "", i == context.ChildCount - 1);
}

void GetBranch(IParseTree branch, string indent, bool last)
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