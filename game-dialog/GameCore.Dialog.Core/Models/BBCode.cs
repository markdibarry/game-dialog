using System;
using System.Collections.Generic;

namespace GameCore.Dialog;

internal static class BBCode
{
    /// <summary>
    /// Determines if BBCode is a Godot supported tag.
    /// </summary>
    /// <param name="tag">The tag</param>
    /// <returns>If true, the tag is supported.</returns>
    public static bool IsSupportedTag(string tag)
    {
        return SupportedTags.Contains(tag);
    }

    /// <summary>
    /// Determines if BBCode is a Godot supported tag.
    /// </summary>
    /// <param name="tag">The tag</param>
    /// <returns>If true, the tag is supported.</returns>
    public static bool IsSupportedTag(ReadOnlySpan<char> tag)
    {
        return SupportedTags.GetAlternateLookup<ReadOnlySpan<char>>().Contains(tag);
    }

    /// <summary>
    /// Is tag that is replaced with a char.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static bool IsReplaceTag(ReadOnlySpan<char> tag)
    {
        return ReplaceTags.GetAlternateLookup<ReadOnlySpan<char>>().Contains(tag);
    }

    private static readonly HashSet<string> SupportedTags =
    [
        "b",
        "i",
        "u",
        "s",
        "code",
        "char",
        "p",
        "br",
        "hr",
        "center",
        "left",
        "right",
        "fill",
        "indent",
        "url",
        "hint",
        "img",
        "font",
        "font_size",
        "dropcap",
        "opentype_features",
        "lang",
        "color",
        "bgcolor",
        "fgcolor",
        "outline_size",
        "outline_color",
        "table",
        "cell",
        "ul",
        "ol",
        "lb",
        "rb",
        "lrm",
        "rlm",
        "lre",
        "rle",
        "lro",
        "rlo",
        "pdf",
        "alm",
        "lri",
        "rli",
        "fsi",
        "pdi",
        "zwj",
        "zwnj",
        "wj",
        "shy",
        "wave",
        "tornado",
        "fade",
        "rainbow",
        "shake"
    ];

    /// <summary>
    /// Contains tags that replaces the tag with a char.
    /// </summary>
    private static readonly HashSet<string> ReplaceTags =
    [
        "hr",
        "br",
        "ul",
        "ol",
        "indent",
        "char",
        "lb",
        "rb",
        "lrm",
        "rlm",
        "lre",
        "rle",
        "lro",
        "rlo",
        "pdf",
        "alm",
        "lri",
        "rli",
        "fsi",
        "pdi",
        "zwj",
        "zwnj",
        "wj",
        "shy",
    ];
}