using System;
using System.Threading.Tasks;

namespace GameDialog.Runner;


public class DialogBridgeBase
{
    static DialogBridgeBase()
    {
        InternalCreate = (d) => new DialogBridgeBase();
    }

    public static Func<DialogBase, DialogBridgeBase> InternalCreate { get; protected set; }

    /// <summary>
    /// Gets the VarType of a predefined property.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public virtual VarType InternalGetPropertyType(ReadOnlySpan<char> propertyName) => VarType.Undefined;
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
    /// Gets the VarType of a predefined method.
    /// </summary>
    /// <param name="funcName"></param>
    /// <returns></returns>
    public virtual VarType InternalGetMethodReturnType(ReadOnlySpan<char> funcName) => VarType.Undefined;

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