using Antlr4.Runtime.Misc;

namespace GameDialog.Compiler;

public class BuiltIn
{
    private static readonly string[] _builtIns =
    {
        AUTO,
        SPEED,
        GOTO,
        G_AUTO,
        G_SPEED,
        END,
        PAUSE
    };

    private static readonly string[] _validSpeakerAttributes = { MOOD, NAME, PORTRAIT };

    public const string AUTO = "auto";
    public const string SPEED = "speed";
    public const string GOTO = "goto";
    public const string G_AUTO = "g_auto";
    public const string G_SPEED = "g_speed";
    public const string END = "end";
    public const string PAUSE = "pause";
    public const string NAME = "name";
    public const string PORTRAIT = "portrait";
    public const string MOOD = "mood";
    public const string UPDATE_SPEAKER = "UpdateSpeaker";

    public static bool IsBuiltIn(string text)
    {
        return _builtIns.Contains(text);
    }

    public static Expression GetAutoExpression(DialogScript dialogScript)
    {
        // TODO make this less magic-y
        List<int> autoInts = new()
        {
            (int)ExpType.Assign,
            (int)ExpType.Var,
            dialogScript.Variables.FindIndex(x => x.Name == AUTO),
            (int)VarType.Bool,
            1
        };
        return new Expression(autoInts);
    }

    public static Expression GetSpeakerExpression([NotNull] DialogParser.Attr_expressionContext context, DialogScript dialogScript)
    {
        SpeakerUpdate speakerUpdate = new();
        foreach (var ass in context.assignment())
        {
            string value = ((DialogParser.ConstStringContext)ass.right).STRING().GetText();
            
            switch (context.NAME().GetText())
            {
                case NAME:
                    speakerUpdate.Name = value;
                    break;
                case PORTRAIT:
                    speakerUpdate.Portrait = value;
                    break;
                case MOOD:
                    speakerUpdate.Mood= value;
                    break;
            }
        }
        dialogScript.SpeakerUpdates.Add(speakerUpdate);
        List<int> updateInts = new() { (int)ExpType.SpeakerUpdate, dialogScript.SpeakerUpdates.Count - 1};
        return new(updateInts);
    }

    public static bool IsAutoExpression(DialogParser.ExpressionContext context)
    {
        return context is DialogParser.ConstVarContext varContext
                && varContext.NAME().GetText() == AUTO;
    }

    public static bool IsNameExpression([NotNull] DialogParser.Attr_expressionContext context)
    {
        return context.assignment() == null
            && context.expression() != null
            && context.expression().Length == 1
            && context.expression()[0] is DialogParser.ConstVarContext varContext
            && varContext.NAME().GetText() == NAME;
    }

    public static bool IsSpeakerExpression([NotNull] DialogParser.Attr_expressionContext context)
    {
        bool namesValid = false;
        if (context.assignment() != null)
        {
            var names = context.assignment().Select(x => x.NAME().GetText());
            namesValid = names.Distinct().Count() == names.Count();
            namesValid = namesValid && names.All(x => _validSpeakerAttributes.Contains(x));
            // All assignments must be strings
            namesValid = namesValid && context.assignment().All(x => x.right is DialogParser.ConstStringContext);
        }

        return context.expression() == null && namesValid;
    }
}
