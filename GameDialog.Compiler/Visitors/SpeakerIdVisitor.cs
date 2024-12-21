using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Compiler;

public class SpeakerIdVisitor : DialogParserBaseVisitor<int>
{
    private readonly ScriptData _dialogScript;

    public SpeakerIdVisitor(ScriptData dialogScript)
    {
        _dialogScript = dialogScript;
    }

    public override int VisitSpeaker_ids([NotNull] DialogParser.Speaker_idsContext context)
    {
        foreach(var nameContext in context.speaker_id())
        {
            string nameText = nameContext.NAME().GetText();

            if (!_dialogScript.SpeakerIds.Contains(nameText))
                _dialogScript.SpeakerIds.Add(nameText);
        }

        return 0;
    }
}
