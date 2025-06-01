using static GameDialog.Compiler.DialogParser;

namespace GameDialog.Compiler;

public class SpeakerIdVisitor : DialogParserBaseVisitor<int>
{
    public SpeakerIdVisitor(ScriptData dialogScript)
    {
        _dialogScript = dialogScript;
    }

    private readonly ScriptData _dialogScript;

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
