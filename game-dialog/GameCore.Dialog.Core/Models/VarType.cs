namespace GameCore.Dialog;

/// <summary>
/// The type of dialog variable.
/// </summary>
public enum VarType
{
    /// <summary>
    /// Is uninitialized or could not be defined.
    /// </summary>
    Undefined = 0,
    /// <summary>
    /// A float type
    /// </summary>
    Float = 1,
    /// <summary>
    /// A string type
    /// </summary>
    String = 2,
    /// <summary>
    /// A boolean type
    /// </summary>
    Bool = 3,
    /// <summary>
    /// No value
    /// </summary>
    Void = 4
}
