﻿using System;
using System.Threading.Tasks;

namespace GameDialog.Common;

public class VarDef
{
    public VarDef(Func<TextVariant> getter, Action<TextVariant> setter, VarType varType)
    {
        Getter = getter;
        Setter = setter;
        VarType = varType;
    }

    public VarType VarType { get; }
    public Func<TextVariant> Getter { get; }
    public Action<TextVariant> Setter { get; }
}

public class FuncDef
{
    public FuncDef(
        VarType[] argTypes,
        VarType returnType,
        Func<ReadOnlySpan<TextVariant>, TextVariant> method)
    {
        Method = method;
        ReturnType = returnType;
        ArgTypes = argTypes;
    }

    public VarType[] ArgTypes { get; }
    public VarType ReturnType { get; }
    public Func<ReadOnlySpan<TextVariant>, TextVariant> Method { get; }
}

public class AsyncFuncDef
{
    public AsyncFuncDef(
        VarType[] argTypes,
        Func<TextVariant[], ValueTask> method)
    {
        Method = method;
        ArgTypes = argTypes;
    }

    public VarType[] ArgTypes { get; }
    public Func<TextVariant[], ValueTask> Method { get; }
}
