using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public partial class MainDialogVisitor : DialogParserBaseVisitor<VarType>
{
    private readonly DialogScript _dialogScript;
    private readonly List<Diagnostic> _diagnostics;
    private List<int> _currentExp = new();

    public MainDialogVisitor(DialogScript dialogScript, List<Diagnostic> diagnostics)
    {
        _dialogScript = dialogScript;
        _diagnostics = diagnostics;
    }

    public override VarType VisitSection_title(DialogParser.Section_titleContext context)
    {
        var text = context.NAME().GetText();
        Section section = new()
        {
            Name = text
        };
        _dialogScript.Sections.Add(section);
        return VarType.Undefined;
    }
}
