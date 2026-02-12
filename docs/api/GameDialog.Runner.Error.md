# <a id="GameDialog_Runner_Error"></a> Error `class`

Namespace: [GameDialog.Runner](GameDialog.Runner.md)

Represents a dialog validation error.

```csharp
public class Error
```

## Constructors

### <a id="GameDialog_Runner_Error__ctor_System_Int32_System_Int32_System_Int32_System_String_"></a> Error\(int, int, int, string\)

```csharp
public Error(int line, int start, int end, string message)
```

#### Parameters

[int](https://learn.microsoft.com/dotnet/api/system.int32) `line`

The line number where the error occurred.

[int](https://learn.microsoft.com/dotnet/api/system.int32) `start`

The starting char index where the error occurred.

[int](https://learn.microsoft.com/dotnet/api/system.int32) `end`

The ending char index where the error occurred.

[string](https://learn.microsoft.com/dotnet/api/system.string) `message`

The description of the error.

## Properties

### <a id="GameDialog_Runner_Error_End"></a> End

The ending char index where the error occurred.

```csharp
public int End { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### <a id="GameDialog_Runner_Error_Line"></a> Line

The line number where the error occurred.

```csharp
public int Line { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### <a id="GameDialog_Runner_Error_Message"></a> Message

The description of the error.

```csharp
public string Message { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### <a id="GameDialog_Runner_Error_Start"></a> Start

The starting char index where the error occurred.

```csharp
public int Start { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

