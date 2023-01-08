using CsvHelper.Configuration.Attributes;

namespace GameDialog.Server
{
    public class TranslationRow
    {
        [Name("keys")]
        public string Keys { get; set; } = string.Empty;
        [Name("en")]
        public string En { get; set; } = string.Empty;
        [Name("es")]
        public string Es { get; set; } = string.Empty;
    }
}
