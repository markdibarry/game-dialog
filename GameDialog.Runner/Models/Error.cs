namespace GameDialog.Runner;

/// <summary>
/// Represents a dialog validation error.
/// </summary>
public class Error
{
    /// <summary>
    /// Creates an Error instance
    /// </summary>
    /// <param name="line">The line number where the error occurred.</param>
    /// <param name="start">The starting char index where the error occurred.</param>
    /// <param name="end">The ending char index where the error occurred.</param>
    /// <param name="message">The description of the error.</param>
    public Error(int line, int start, int end, string message)
    {
        Line = line;
        Start = start;
        End = end;
        Message = message;
    }

    /// <summary>
    /// The line number where the error occurred.
    /// </summary>
    public int Line { get; set; }
    /// <summary>
    /// The starting char index where the error occurred.
    /// </summary>
    public int Start { get; set; }
    /// <summary>
    /// The ending char index where the error occurred.
    /// </summary>
    public int End { get; set; }
    /// <summary>
    /// The description of the error.
    /// </summary>
    public string Message { get; set; }
}