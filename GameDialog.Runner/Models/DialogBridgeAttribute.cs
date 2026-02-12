using System;

namespace GameDialog.Runner;

/// <summary>
/// Instructs the GameDialog source generator to generate source code so the class's members can be used in dialog.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DialogBridgeAttribute : Attribute { }