using System;
using System.Collections.Generic;

namespace GameDialog.Runner;

/// <summary>
/// Internal bridge class for source generator. Do not use.
/// </summary>
public class DialogBridge
{
    static DialogBridge()
    {
        Create = (d) => new DialogBridge();
        FuncDefs = [];
        VarDefs = [];
    }

    /// <summary>
    /// Internal delegate for creating a DialogBridge object. Do not use.
    /// </summary>
    protected internal static Func<Dialog, DialogBridge> Create { get; set; }
    /// <summary>
    /// Internal collection of FuncDefs. Do not use.
    /// </summary>
    internal static Dictionary<string, FuncDef> FuncDefs { get; set; }
    /// <summary>
    /// Internal collection of VarDefs. Do not use.
    /// </summary>
    internal static Dictionary<string, VarDef> VarDefs { get; set; }

    /// <summary>
    /// Adds a method entry to the FuncDef lookup.
    /// </summary>
    /// <param name="name">The method's name</param>
    /// <param name="returnType">The method's return type</param>
    /// <param name="argTypes">The argument types, ordered.</param>
    /// <param name="isAwaitable">If true, the method is awaitable.</param>
    protected internal static void AddFuncDef(string name, VarType returnType, VarType[] argTypes, bool isAwaitable)
    {
        FuncDefs[name] = new()
        {
            Name = name,
            ReturnType = returnType,
            ArgTypes = argTypes,
            Awaitable = isAwaitable
        };
    }

    /// <summary>
    /// Adds a property entry to the VarDef lookup.
    /// </summary>
    /// <param name="name">The property's name</param>
    /// <param name="type">The property's type</param>
    protected internal static void AddVarDef(string name, VarType type)
    {
        VarDefs[name] = new()
        {
            Name = name,
            Type = type
        };
    }

    /// <summary>
    /// Gets a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    protected internal virtual TextVariant GetProperty(ReadOnlySpan<char> propertyName) => new();
    /// <summary>
    /// Sets a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    protected internal virtual void SetProperty(ReadOnlySpan<char> propertyName, TextVariant value) { }

    /// <summary>
    /// Calls a predefined method.
    /// </summary>
    /// <param name="funcName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    protected internal virtual TextVariant CallMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args) => new();

    /// <summary>
    /// Calls a predefined async method.
    /// </summary>
    /// <param name="funcName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    protected internal virtual void CallAsyncMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args) { }
}