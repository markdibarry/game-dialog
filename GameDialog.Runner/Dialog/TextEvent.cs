namespace GameDialog.Runner;

public struct TextEvent
{
    public static TextEvent Undefined { get; set; } = new(default, default, default);
    public static TextEvent Ignore { get; set; } = new(EventType.Ignore, default, default);

    public TextEvent(EventType eventType, int textIndex, double value)
    {
        EventType = eventType;
        TextIndex = textIndex;
        Value = value;
    }

    public EventType EventType { get; set; }
    public int TextIndex { get; set; }
    public double Value { get; set; }
    public bool Seen { get; set; }
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
    Auto
}
