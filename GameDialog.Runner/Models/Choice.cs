namespace GameDialog.Runner;

public struct Choice
{
    public Choice(int next, string text, bool disabled)
    {
        Next = next;
        Text = text;
        Disabled = disabled;
    }

    public int Next { get; set; }
    public string Text { get; set; }
    public bool Disabled { get; set; }
}
