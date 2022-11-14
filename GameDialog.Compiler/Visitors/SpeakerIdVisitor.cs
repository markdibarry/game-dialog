using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Compiler;

public class SpeakerIdVisitor : DialogParserBaseVisitor<int>
{
    private readonly DialogScript _dialogScript;

    public SpeakerIdVisitor(DialogScript dialogScript)
    {
        _dialogScript = dialogScript;
    }

    public override int VisitSpeaker_ids([NotNull] DialogParser.Speaker_idsContext context)
    {
        foreach(var nameContext in context.NAME())
        {
            if (!_dialogScript.SpeakerIds.Contains(nameContext.GetText()))
                _dialogScript.SpeakerIds.Add(nameContext.GetText());
        }

        return 0;
    }
}
