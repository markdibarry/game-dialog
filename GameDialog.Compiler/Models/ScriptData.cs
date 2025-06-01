namespace GameDialog.Compiler;

public class ScriptData
{
    public List<string> SpeakerIds { get; set; } = [];
    public List<float> Floats { get; set; } = [];
    public List<string> Strings { get; set; } = [];
    public List<List<int>> Instructions { get; set; } = [];
}
