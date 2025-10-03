using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameDialog.Compiler;

public class ScriptData
{
    public List<string> SectionIds { get; set; } = [];
    public List<string> SpeakerIds { get; set; } = [];
    public List<float> Floats { get; set; } = [];
    public List<string> Strings { get; set; } = [];
    public List<List<int>> Instructions { get; set; } = [];
    [JsonIgnore]
    public List<int> DialogStringIndices { get; set; } = [];
}
