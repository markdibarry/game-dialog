namespace GameCore.Dialog;

/// <summary>
/// Translation file types
/// </summary>
public enum TranslationFileType
{
    /// <summary>
    /// Project uses no translations
    /// </summary>
    None,
    /// <summary>
    /// Project contains .csv translations
    /// </summary>
    CSV,
    /// <summary>
    /// Project contains .po/.mo translations
    /// </summary>
    POT
}
