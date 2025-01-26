using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public class SpeakerIdVisitor : DialogParserBaseVisitor<int>
{
    private readonly ScriptDataExtended _dialogScript;

    public SpeakerIdVisitor(ScriptDataExtended dialogScript)
    {
        _dialogScript = dialogScript;
    }

    public override int VisitSpeaker_ids(Speaker_idsContext context)
    {
        foreach (var nameContext in context.speaker_id())
        {
            string nameText = nameContext.NAME().GetText();

            if (!_dialogScript.SpeakerIds.Contains(nameText))
                _dialogScript.SpeakerIds.Add(nameText);
        }

        return 0;
    }
}
