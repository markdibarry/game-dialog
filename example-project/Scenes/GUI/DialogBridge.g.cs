using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GameDialog.Runner;

namespace ExampleProject;

public partial class DialogBridge
{
    public DialogBridge(DialogBase dialog)
    {
        Dialog = dialog;
    }

    private DialogBase Dialog { get; }

    [ModuleInitializer]
    public static void InternalSetCreateType()
    {
        InternalCreate = (dialog) => new DialogBridge(dialog);
        InternalFuncDefs = new()
        {
            ["Shake"] = new()
            {
                Name = "Shake",
                ReturnType = VarType.Void,
                Awaitable = true,
                ArgTypes = []
            },
            ["Flash"] = new()
            {
                Name = "Flash",
                ReturnType = VarType.Void,
                Awaitable = false,
                ArgTypes = []
            },
            ["GetTimesTalked"] = new()
            {
                Name = "GetTimesTalked",
                ReturnType = VarType.Float,
                Awaitable = false,
                ArgTypes = [VarType.String]
            },
        };
        InternalVarDefs = new()
        {
            ["MyString"] = new()
            {
                Name = "MyString",
                Type = VarType.String,
            },
        };
    }

    public override TextVariant InternalCallMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args)
    {
        switch (funcName)
        {
            case nameof(Shake):
                _ = Shake();
                return new();
            case nameof(Flash):
                Flash();
                return new();
            case nameof(GetTimesTalked):
                return new(GetTimesTalked(args[0].String));
            default:
                return new();
        }
    }

    public override void InternalCallAsyncMethod(ReadOnlySpan<char> funcName, ReadOnlySpan<TextVariant> args)
    {
        switch (funcName)
        {
            case nameof(Shake):
                InternalLocalShake();
                return;
            default:
                return;
        }

        async void InternalLocalShake()
        {
            await Shake();
            Dialog.Resume();
        }
    }

    public override TextVariant InternalGetProperty(ReadOnlySpan<char> propertyName)
    {
        return propertyName switch
        {
            nameof(MyString) => new(MyString),
            _ => new()
        };
    }

    public override void InternalSetProperty(ReadOnlySpan<char> propertyName, TextVariant value)
    {
        switch (propertyName)
        {
            case nameof(MyString):
                MyString = value.String;
                break;
            default:
                break;
        }
    }
}
