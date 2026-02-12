# <a id="GameDialog_Runner_Choice"></a> Choice `struct`

Namespace: [GameDialog.Runner](GameDialog.Runner.md)

Represents a dialog choice option.

```csharp
public readonly struct Choice
```

## Constructors

### <a id="GameDialog_Runner_Choice__ctor_System_Int32_System_String_System_Boolean_"></a> Choice\(int, string, bool\)

```csharp
public Choice(int next, string text, bool disabled)
```

#### Parameters

[int](https://learn.microsoft.com/dotnet/api/system.int32) `next`

The next line index to be read.

[string](https://learn.microsoft.com/dotnet/api/system.string) `text`

The displayed text for this choice.

[bool](https://learn.microsoft.com/dotnet/api/system.boolean) `disabled`

If true, this choice is not enabled.

## Properties

### <a id="GameDialog_Runner_Choice_Disabled"></a> Disabled

If true, this choice is not enabled.

```csharp
public bool Disabled { get; init; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### <a id="GameDialog_Runner_Choice_Next"></a> Next

The next line index to be read.

```csharp
public int Next { get; init; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### <a id="GameDialog_Runner_Choice_Text"></a> Text

The displayed text for this choice.

```csharp
public string Text { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

