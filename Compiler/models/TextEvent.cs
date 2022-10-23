namespace GameDialogParser;

public class TextEvent
{
    public TextEvent(Tag tag)
    {
        Name = tag.Name;
        Index = tag.Index;
        Length = tag.Length;
        Attributes = tag.Attributes;
    }

    public string Name { get; set; }
    public bool Seen { get; set; }
    public int Length { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
    public int Index { get; set; }
    private static readonly string[] TextEventTags = new[]
    {
        "speed",
        "pause",
        "mood",
        "auto",
        "nl",
        "next"
    };

    public static bool IsTextEvent(Tag tag)
    {
        return tag.Name.StartsWith('$') || TextEventTags.Contains(tag.Name);
    }

}
