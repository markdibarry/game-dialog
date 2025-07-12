namespace GameDialog.Compiler;

public class BuiltIn
{
    private static readonly string[] _builtIns =
    [
        AUTO,
        END,
        GOTO,
        PAUSE,
        SPEED
    ];

    public const string AUTO = "auto";
    public const string END = "end";
    public const string GOTO = "goto";
    public const string PAUSE = "pause";
    public const string SPEED = "speed";

    public static bool IsBuiltIn(string text)
    {
        return _builtIns.Contains(text);
    }
}
