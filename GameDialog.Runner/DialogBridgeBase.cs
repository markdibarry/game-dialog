using System;
using System.Collections.Generic;

namespace GameDialog.Runner;


public class DialogBridgeBase
{
    static DialogBridgeBase()
    {
        InternalCreate = (d) => new DialogBridgeBase();
        InternalFuncDefs = [];
        InternalVarDefs = [];
    }

    public static Func<DialogBase, DialogBridgeBase> InternalCreate { get; protected set; }
    public static Dictionary<string, FuncDef> InternalFuncDefs { get; protected set; }
    public static Dictionary<string, VarDef> InternalVarDefs { get; protected set; }

    /// <summary>
    /// Gets a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public virtual TextVariant InternalGetProperty(ReadOnlySpan<char> propertyName) => new();
    /// <summary>
    /// Sets a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    public virtual void InternalSetProperty(ReadOnlySpan<char> propertyName, TextVariant value) { }

    /// <summary>
    /// Calls a predefined method.
    /// </summary>
    /// <param name="funcName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public virtual TextVariant InternalCallMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args) => new();

    /// <summary>
    /// Calls a predefined async method.
    /// </summary>
    /// <param name="funcName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public virtual void InternalCallAsyncMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args) { }
}