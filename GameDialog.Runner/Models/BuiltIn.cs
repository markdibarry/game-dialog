using System;
using System.Collections.Generic;

namespace GameDialog.Runner;

public static class BuiltIn
{
    private static readonly HashSet<string> _builtInTags =
    [
        AUTO,
        END,
        GOTO,
        PAUSE,
        SPEED,
        PROMPT,
        SCROLL,
        PAGE
    ];

    public const string AUTO = "auto";
    public const string END = "end";
    public const string GOTO = "goto";
    public const string PAUSE = "pause";
    public const string SPEED = "speed";
    public const string PROMPT = "prompt";
    public const string SCROLL = "scroll";
    public const string PAGE = "page";

    public static bool IsSupportedTag(string text)
    {
        return _builtInTags.Contains(text);
    }

    public static bool IsSupportedTag(ReadOnlySpan<char> text)
    {
        return _builtInTags.GetAlternateLookup<ReadOnlySpan<char>>().Contains(text);
    }
}
