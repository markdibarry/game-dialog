using System.Runtime.InteropServices;

namespace GameDialog.Common;

[StructLayout(LayoutKind.Explicit)]
public readonly record struct TextVariant
{
    public TextVariant()
    {
        VariantType = VarType.Void;
    }

    public TextVariant(bool myBool)
    {
        VariantType = VarType.Bool;
        Bool = myBool;
    }

    public TextVariant(float myFloat)
    {
        VariantType = VarType.Float;
        Float = myFloat;
    }

    public TextVariant(string myString)
    {
        VariantType = VarType.String;
        String = myString;
    }

    [FieldOffset(0)]
    public readonly VarType VariantType;
    [FieldOffset(8)]
    public readonly bool Bool;
    [FieldOffset(8)]
    public readonly float Float;
    [FieldOffset(16)]
    public readonly string String = default!;

    public T? Get<T>()
    {
        if (VariantType == VarType.Bool)
        {
            if (Bool is T tBool)
                return tBool;
        }
        else if (VariantType == VarType.String)
        {
            if (String is T tString)
                return tString;
        }
        else if (VariantType == VarType.Float)
        {
            if (Float is T tFloat)
                return tFloat;
        }

        return default;
    }

    public bool TryGetValue<T>(out T? value)
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
            if (String is T tString)
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
        VarType.Void => "<void>",
        VarType.Bool => Bool.ToString(),
        VarType.Float => Float.ToString(),
        VarType.String => String,
        _ => string.Empty
    };
}