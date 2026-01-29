using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace GameDialog.Runner;

[StructLayout(LayoutKind.Explicit)]
public readonly record struct TextVariant
{
    public TextVariant()
    {
        VariantType = VarType.Void;
    }

    public TextVariant(bool boolValue)
    {
        VariantType = VarType.Bool;
        Bool = boolValue;
    }

    public TextVariant(float floatValue)
    {
        VariantType = VarType.Float;
        Float = floatValue;
    }

    public TextVariant(string stringValue)
    {
        VariantType = VarType.String;
        Chars = stringValue.AsMemory();
    }

    public TextVariant(ReadOnlyMemory<char> memValue)
    {
        VariantType = VarType.String;
        Chars = memValue;
    }

    private TextVariant(VarType varType)
    {
        VariantType = varType;
    }

    public readonly static TextVariant Undefined = new(VarType.Undefined);

    [FieldOffset(0)]
    public readonly VarType VariantType;
    [FieldOffset(8)]
    public readonly bool Bool;
    [FieldOffset(8)]
    public readonly float Float;
    [FieldOffset(16)]
    public readonly ReadOnlyMemory<char> Chars = default;
    public readonly string String => Chars.ToString();

    public T? Get<T>()
    {
        if (VariantType == VarType.Bool)
        {
            if (Bool is T tBool)
                return tBool;
        }
        else if (VariantType == VarType.String)
        {
            if (Chars is T tString)
                return tString;
        }
        else if (VariantType == VarType.Float)
        {
            if (Float is T tFloat)
                return tFloat;
        }

        return default;
    }

    public bool TryGetValue<T>([NotNullWhen(true)] out T? value)
    {
        if (VariantType == VarType.Bool)
        {
            if (Bool is T tBool)
            {
                value = tBool;
                return true;
            }
        }
        else if (VariantType == VarType.String)
        {
            if (Chars.ToString() is T tString)
            {
                value = tString;
                return true;
            }
        }
        else if (VariantType == VarType.Float)
        {
            if (Float is T tFloat)
            {
                value = tFloat;
                return true;
            }
        }

        value = default;
        return false;
    }

    public override string ToString() => VariantType switch
    {
        VarType.Bool => Bool.ToString(),
        VarType.Float => Float.ToString(),
        VarType.String => Chars.ToString(),
        _ => string.Empty
    };
}