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

    private static readonly string[] _validSpeakerAttributes = [MOOD, NAME, PORTRAIT];

    public const string AUTO = "auto";
    public const string END = "end";
    public const string GOTO = "goto";
    public const string NEWLINE = "nl";
    public const string PAUSE = "pause";
    public const string SPEED = "speed";
    public const string NAME = "name";
    public const string PORTRAIT = "portrait";
    public const string MOOD = "mood";

    public static bool IsBuiltIn(string text)
    {
        return _builtIns.Contains(text);
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
        if (context.assignment().Length > 0 && context.expression().Length == 0)
        {
            var names = context.assignment().Select(x => x.NAME().GetText());
            namesValid = names.Distinct().Count() == names.Count();
            namesValid = namesValid && names.All(x => _validSpeakerAttributes.Contains(x));
        }

        return namesValid;
    }
}
