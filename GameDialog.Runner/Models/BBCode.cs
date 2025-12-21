using System;
using System.Collections.Generic;

namespace GameDialog.Runner;

public static class BBCode
{
    public static bool IsSupportedTag(string tag)
    {
        return SupportedTags.Contains(tag);
    }

    public static bool IsSupportedTag(ReadOnlySpan<char> tag)
    {
        return SupportedTags.GetAlternateLookup<ReadOnlySpan<char>>().Contains(tag);
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
        "right",
        "left",
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
        "table",
        "cell",
        "ul",
        "ol",
        "lb",
        "rb",
        "color",
        "bgcolor",
        "fgcolor",
        "outline_size",
        "outline_color",
        "wave",
        "tornado",
        "fade",
        "rainbow",
        "shake"
    ];
}