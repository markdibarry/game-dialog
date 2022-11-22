namespace GameDialog.Compiler;

public class BuiltIn
{
    private static readonly string[] _builtIns =
    {
        AUTO,
        END,
        GOTO,
        NEWLINE,
        PAUSE,
        SPEED
    };

    private static readonly string[] _validSpeakerAttributes = { MOOD, NAME, PORTRAIT };

    public const string AUTO = "auto";
    public const string END = "end";
    public const string GOTO = "goto";
    public const string NEWLINE = "nl";
    public const string PAUSE = "pause";
    public const string SPEED = "speed";
    public const string NAME = "name";
    public const string PORTRAIT = "portrait";
    public const string MOOD = "mood";
    public const string UPDATE_SPEAKER = "UpdateSpeaker";

    public static bool IsBuiltIn(string text)
    {
        return _builtIns.Contains(text);
    }

    public static List<int> GetBBCodeInts(int stringIndex)
    {
        return new () { (int)InstructionType.BBCode, stringIndex };
    }

    public static List<int> GetSpeakerGetInts(string name, DialogScript dialogScript)
    {
        int index = dialogScript.ExpStrings.IndexOf(name);
        if (index == -1)
        {
            dialogScript.ExpStrings.Add(name);
            index = dialogScript.ExpStrings.Count - 1;
        }
        return new() { (int)InstructionType.SpeakerGet, index };
    }

    public static List<int> GetSpeakerSetInts(DialogParser.Attr_expressionContext context, DialogScript dialogScript)
    {
        List<int> updateInts = new() { (int)InstructionType.SpeakerSet, -1, -1, -1 };
        foreach (var ass in context.assignment())
        {
            string value = ((DialogParser.ConstStringContext)ass.right).STRING().GetText();
            int index = dialogScript.ExpStrings.IndexOf(value);
            if (index == -1)
            {
                dialogScript.ExpStrings.Add(value);
                index = dialogScript.ExpStrings.Count - 1;
            }

            switch (ass.NAME().GetText())
            {
                case NAME:
                    updateInts[1] = index;
                    break;
                case MOOD:
                    updateInts[2] = index;
                    break;
                case PORTRAIT:
                    updateInts[3] = index;
                    break;
            }
        }
        
        return updateInts;
    }

    public static bool IsNameExpression(DialogParser.Attr_expressionContext context)
    {
        return context.assignment().Length == 0
            && context.expression().Length == 1
            && context.expression()[0] is DialogParser.ConstVarContext varContext
            && varContext.NAME().GetText() == NAME;
    }

    public static bool IsSpeakerExpression(DialogParser.Attr_expressionContext context)
    {
        bool namesValid = false;
        if (context.assignment().Any() && context.expression().Length == 0)
        {
            var names = context.assignment().Select(x => x.NAME().GetText());
            namesValid = names.Distinct().Count() == names.Count();
            namesValid = namesValid && names.All(x => _validSpeakerAttributes.Contains(x));
            // All assignments must be strings
            namesValid = namesValid && context.assignment().All(x => x.right is DialogParser.ConstStringContext);
        }

        return namesValid;
    }
}
