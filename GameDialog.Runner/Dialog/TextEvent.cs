namespace GameDialog.Runner;

public struct TextEvent
{
    public static TextEvent Undefined { get; set; } = new(default, default, default);
    public static TextEvent Ignore { get; set; } = new(EventType.Ignore, default, default);

    public TextEvent(EventType eventType, int textIndex, double value, bool isAwait = false)
    {
        EventType = eventType;
        TextIndex = textIndex;
        Value = value;
        IsAwait = isAwait;
    }

    public EventType EventType { get; set; }
    public int TextIndex { get; set; }
    public double Value { get; set; }
    public bool IsAwait { get; set; }
}

public enum EventType
{
    Undefined,
    Ignore,
    Instruction,
    Hash,
    Speaker,
    Pause,
    Speed,
    Auto,
    Prompt,
    Page
}
