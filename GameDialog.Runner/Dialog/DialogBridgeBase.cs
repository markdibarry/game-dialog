using System;
using System.Collections.Generic;
using GameDialog.Common;

namespace GameDialog.Runner;

public abstract class DialogBridgeBase
{
    public static DialogBridgeBase DialogBridge { get; private set; } = null!;
    public static Dictionary<string, VarDef> Properties { get; private set; } = [];
    public static Dictionary<string, FuncDef> Methods { get; private set; } = [];
    private static readonly Random s_random = new();

    public static float Rand() => s_random.NextSingle();

    public static string GetName(string name) => DialogBase.CurrentDialog?.GetName(name) ?? string.Empty;

    public static void Init<T>() where T : DialogBridgeBase, new()
    {
        T dialogBridge = new();
        dialogBridge.RegisterProperties();
        dialogBridge.RegisterMethods();
        DialogBridge = dialogBridge;
    }

    public static void RegisterProperty(
        string name,
        VarType varType,
        Func<TextVariant> getter,
        Action<TextVariant> setter)
    {
        Properties.Add(name, new VarDef(getter, setter, varType));
    }

    public static void RegisterMethod(
        string name,
        VarType[] argTypes,
        VarType returnType,
        Func<ReadOnlySpan<TextVariant>, TextVariant> func)
    {
        Methods.Add(name, new FuncDef(argTypes, returnType, func));
    }

    public virtual void RegisterProperties()
    {
    }

    public virtual void RegisterMethods()
    {
        RegisterMethod(
            name: nameof(Rand),
            argTypes: [],
            returnType: VarType.Float,
            func: (args) => new(Rand()));

        RegisterMethod(
            name: nameof(GetName),
            argTypes: [VarType.String],
            returnType: VarType.String,
            func: (args) => new(GetName(args[0].String)));
    }
}
