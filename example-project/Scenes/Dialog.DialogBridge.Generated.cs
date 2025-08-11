using System;
using GameDialog.Common;
using GameDialog.Runner;

namespace ExampleProject;

public partial class Dialog
{
    protected override VarType GetPredefinedMethodReturnType(string funcName)
    {
        return funcName switch
        {
            nameof(Flash) => VarType.Void,
            _ => VarType.Undefined
        };
    }

    protected override VarType GetPredefinedPropertyType(string propertyName)
    {
        return propertyName switch
        {
            
            _ => VarType.Undefined
        };
    }

    protected override TextVariant CallPredefinedMethod(string funcName, ReadOnlySpan<TextVariant> args)
    {
        switch (funcName)
        {
            case nameof(Flash):
                Flash();
                return new();
            default:
                return new();
        }
    }

    protected override TextVariant GetPredefinedProperty(string propertyName)
    {
        return propertyName switch
        {
            
            _ => new()
        };
    }

    protected override void SetPredefinedProperty(string propertyName, TextVariant value)
    {
        switch (propertyName)
        {
            
            default:
                break;
        }
    }
}
