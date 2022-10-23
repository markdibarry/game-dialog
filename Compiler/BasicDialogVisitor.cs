using Antlr4.Runtime.Misc;

namespace GameDialogParser;

public class BasicDialogVisitor : DialogParserBaseVisitor<object>
{
    public DialogScript DialogScript { get; set; } = new();

    public override object VisitSection_title([NotNull] DialogParser.Section_titleContext context)
    {
        var text = context.NAME().GetText();
        Section section = new()
        {
            Name = text
        };
        DialogScript.Sections.Add(section);
        return base.VisitSection_title(context);
    }

    public override object VisitStmt([NotNull] DialogParser.StmtContext context)
    {
        return base.VisitStmt(context);
    }
}
