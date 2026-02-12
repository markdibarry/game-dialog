using System;
using System.Collections.Generic;

namespace GameDialog.Runner;

internal static class BuiltIn
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

    /// <summary>
    /// Determines if tag name is a supported tag.
    /// </summary>
    /// <param name="text">The tag</param>
    /// <returns>If true, the tag is supported.</returns>
    public static bool IsSupportedTag(string text)
    {
        return _builtInTags.Contains(text);
    }

    /// <summary>
    /// Determines if tag name is a supported tag.
    /// </summary>
    /// <param name="text">The tag</param>
    /// <returns>If true, the tag is supported.</returns>
    public static bool IsSupportedTag(ReadOnlySpan<char> text)
    {
        return _builtInTags.GetAlternateLookup<ReadOnlySpan<char>>().Contains(text);
    }
}
