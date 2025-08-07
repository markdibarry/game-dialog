using System.Collections.Generic;

namespace GameDialog.Common;

public class BuiltIn
{
    private static readonly HashSet<string> _builtInTags =
    [
        AUTO,
        END,
        GOTO,
        PAUSE,
        SPEED,
        PROMPT,
        PAGE
    ];

    public const string AUTO = "auto";
    public const string END = "end";
    public const string GOTO = "goto";
    public const string PAUSE = "pause";
    public const string SPEED = "speed";
    public const string PROMPT = "prompt";
    public const string PAGE = "page";

    public const string GET_NAME_METHOD = "GetName";
    public const string GET_RAND_METHOD = "GetRand";

    public static bool IsBuiltIn(string text)
    {
        return _builtInTags.Contains(text);
    }
}
