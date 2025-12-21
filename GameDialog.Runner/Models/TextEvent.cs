namespace GameDialog.Runner;

public struct TextEvent
{
    public readonly static TextEvent Undefined = new() { EventType = EventType.Undefined };

    public EventType EventType;
    public int TextIndex;
    public double Param1;
    public double Param2;
}

public enum EventType
{
    Undefined,
    Append,
    Evaluate,
    Await,
    Pause,
    Speed,
    Auto,
    Prompt,
    Scroll
}
