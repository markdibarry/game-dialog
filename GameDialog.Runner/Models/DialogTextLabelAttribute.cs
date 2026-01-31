using System;

namespace GameDialog.Runner;

/// <summary>
/// Provides a code fix to replace the file contents with a DialogTextLabel class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DialogTextLabelAttribute : Attribute { }