
namespace GameDialogParser;

public class Tag
{
    public Tag(string text, int index)
    {
        // strip brackets
        text = text[1..^1];
        Length = -1;
        Index = index;
        string[] tagParts = text.Split(' ');
        if (tagParts.Length == 0)
            return;
        if (tagParts[0].StartsWith('/'))
        {
            IsClosing = true;
            tagParts[0] = tagParts[0][1..];
        }
        Name = tagParts[0].Split('=')[0];
        foreach (var part in tagParts)
        {
            string[] split = part.Split('=');
            if (split.Length == 2)
                Attributes.Add(split[0], split[1]);
            else
                Attributes.Add(split[0], string.Empty);
        }
    }

    public string Name { get; set; } = string.Empty;
    public bool IsClosing { get; set; }
    public int Length { get; set; }
    public int Index { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    private static readonly string[] BBCodeTags = new[]
    {
        "b",
        "i",
        "u",
        "s",
        "code",
        "center",
        "right",
        "fill",
        "indent",
        "url",
        "image",
        "font",
        "table",
        "cell",
        "color",
        "wave",
        "tornado",
        "fade",
        "rainbow",
        "shake"
    };

    public bool IsBBCode()
    {
        return BBCodeTags.Contains(Name);
    }
}
