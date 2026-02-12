# <a id="GameDialog_Runner_DialogTextLabel"></a> DialogTextLabel `class`

Namespace: [GameDialog.Runner](GameDialog.Runner.md)

An example RichTextLabel writer.

```csharp
[Tool, GlobalClass]
public partial class DialogTextLabel : RichTextLabel
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[GodotObject](https://docs.godotengine.org/en/stable/classes/class_object.html) ← 
[Node](https://docs.godotengine.org/en/stable/classes/class_node.html) ← 
[CanvasItem](https://docs.godotengine.org/en/stable/classes/class_canvasitem.html) ← 
[Control](https://docs.godotengine.org/en/stable/classes/class_control.html) ← 
[RichTextLabel](https://docs.godotengine.org/en/stable/classes/class_richtextlabel.html) ← 
[DialogTextLabel](GameDialog.Runner.DialogTextLabel.md)

## Properties

### <a id="GameDialog_Runner_DialogTextLabel_AutoProceedEnabled"></a> AutoProceedEnabled

If true, the text will automatically proceed when it reaches the end of the line/page.

```csharp
public bool AutoProceedEnabled { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### <a id="GameDialog_Runner_DialogTextLabel_AutoProceedTimeout"></a> AutoProceedTimeout

If <code>AutoProceedEnabled</code> is true, determines the amount of time after reaching the end 
of the line/page before automatically proceeding.

```csharp
public float AutoProceedTimeout { get; set; }
```

#### Property Value

 [float](https://learn.microsoft.com/dotnet/api/system.single)

### <a id="GameDialog_Runner_DialogTextLabel_CharsPerSecond"></a> CharsPerSecond

The speed, in characters per second, at which characters are written.

```csharp
[Export(PropertyHint.None, "")]
public int CharsPerSecond { get; set; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### <a id="GameDialog_Runner_DialogTextLabel_Dialog"></a> Dialog

The Dialog object. For parsing and handling text events.

```csharp
public Dialog? Dialog { get; set; }
```

#### Property Value

 [Dialog](GameDialog.Runner.Dialog.md)?

### <a id="GameDialog_Runner_DialogTextLabel_IsSpeedUpEnabled"></a> IsSpeedUpEnabled

If true, the base writing speed is multiplied by this value.

```csharp
public bool IsSpeedUpEnabled { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### <a id="GameDialog_Runner_DialogTextLabel_ResetButton"></a> ResetButton

A helper export button to reset the DialogTextLabel.

```csharp
[ExportToolButton("Reset")]
public Callable ResetButton { get; }
```

#### Property Value

 Callable

### <a id="GameDialog_Runner_DialogTextLabel_ScrollSpeed"></a> ScrollSpeed

The speed, in pixels per second, at which the text scrolls.

```csharp
[Export(PropertyHint.None, "")]
public float ScrollSpeed { get; set; }
```

#### Property Value

 [float](https://learn.microsoft.com/dotnet/api/system.single)

### <a id="GameDialog_Runner_DialogTextLabel_ScrollStep"></a> ScrollStep

The interval, in pixels, at which the scroll visually updates.

```csharp
[Export(PropertyHint.None, "")]
public float ScrollStep { get; set; }
```

#### Property Value

 [float](https://learn.microsoft.com/dotnet/api/system.single)

### <a id="GameDialog_Runner_DialogTextLabel_SpeedMultiplier"></a> SpeedMultiplier

The base writing speed is determined by this value multiplied by <code>CharsPerSecond</code>.

```csharp
public double SpeedMultiplier { get; set; }
```

#### Property Value

 [double](https://learn.microsoft.com/dotnet/api/system.double)

### <a id="GameDialog_Runner_DialogTextLabel_Suspended"></a> Suspended

If true, the writing is temporarily suspended until `Resume()` is called.

```csharp
public bool Suspended { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### <a id="GameDialog_Runner_DialogTextLabel_WriteNextLineButton"></a> WriteNextLineButton

A helper export button to write the next dialog line.

```csharp
[ExportToolButton("Write Next Line")]
public Callable WriteNextLineButton { get; }
```

#### Property Value

 Callable

### <a id="GameDialog_Runner_DialogTextLabel_WriteNextPageButton"></a> WriteNextPageButton

A helper export button to write the next dialog page.

```csharp
[ExportToolButton("Write Next Page")]
public Callable WriteNextPageButton { get; }
```

#### Property Value

 Callable

### <a id="GameDialog_Runner_DialogTextLabel_Writing"></a> Writing

If true, the DialogTextLabel is currently writing text.

```csharp
public bool Writing { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

### <a id="GameDialog_Runner_DialogTextLabel_IsComplete"></a> IsComplete\(\)

Determines if the DialogTextLabel has completed writing all available text.

```csharp
public bool IsComplete()
```

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

If true, the DialogTextLabel has completed writing all available text.

### <a id="GameDialog_Runner_DialogTextLabel_IsOnLastPage"></a> IsOnLastPage\(\)

Determines if the displayed text is the last available page.

```csharp
public bool IsOnLastPage()
```

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

If true, the current page is the last available page.

### <a id="GameDialog_Runner_DialogTextLabel_OnTextEventTriggered_GameDialog_Runner_TextEvent_"></a> OnTextEventTriggered\(TextEvent\)

Handles the TextEventTriggered event.

```csharp
public void OnTextEventTriggered(TextEvent textEvent)
```

#### Parameters

[TextEvent](GameDialog.Runner.TextEvent.md) `textEvent`

The triggered TextEvent.

### <a id="GameDialog_Runner_DialogTextLabel_Reset"></a> Reset\(\)

Resets the DialogTextLabel.

```csharp
public void Reset()
```

### <a id="GameDialog_Runner_DialogTextLabel_Resume"></a> Resume\(\)

Resumes writing the text.

```csharp
public void Resume()
```

### <a id="GameDialog_Runner_DialogTextLabel_SetDialogText_System_String_"></a> SetDialogText\(string\)

Parses text for TextEvents and sets the parsed text to <code>RichTextLabel.Text</code>.

```csharp
public void SetDialogText(string text)
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `text`

The unparsed text.

### <a id="GameDialog_Runner_DialogTextLabel_WriteNextLine"></a> WriteNextLine\(\)

Writes the next available line of text.

```csharp
public void WriteNextLine()
```

### <a id="GameDialog_Runner_DialogTextLabel_WriteNextPage"></a> WriteNextPage\(\)

Writes the next available text that can fit within the bounds of the RichTextLabel.

```csharp
public void WriteNextPage()
```

## Events

### <a id="GameDialog_Runner_DialogTextLabel_CharWritten"></a> CharWritten

Occurs when a character is written.

```csharp
public event CharWrittenHandler? CharWritten
```

#### Event Type

[CharWrittenHandler](#GameDialog_Runner_DialogTextLabel_CharWrittenHandler)?

### <a id="GameDialog_Runner_DialogTextLabel_FinishedWriting"></a> FinishedWriting

Occurs when the DialogTextLabel finishes writing all available text.

```csharp
public event Action? FinishedWriting
```

#### Event Type

 [Action](https://learn.microsoft.com/dotnet/api/system.action)?

### <a id="GameDialog_Runner_DialogTextLabel_TextEventTriggered"></a> TextEventTriggered

Occurs when a TextEvent is triggered.

```csharp
public event TextEventTriggeredHandler? TextEventTriggered
```

#### Event Type

[TextEventTriggeredHandler](#GameDialog_Runner_DialogTextLabel_TextEventTriggeredHandler)?

## Delegates

### <a id="GameDialog_Runner_DialogTextLabel_CharWrittenHandler"></a> CharWrittenHandler

Represents the method that will handle the CharWritten event.

```csharp
public delegate void CharacterWrittenHandler(int charIndex, char charWritten)
```

#### Parameters

[int](https://learn.microsoft.com/dotnet/api/system.int32) `charIndex`

The index of the char written.

[char](https://learn.microsoft.com/dotnet/api/system.char) `charWritten`

The char that was written.

### <a id="GameDialog_Runner_DialogTextLabel_TextEventTriggeredHandler"></a> TextEventTriggeredHandler

Represents the method that will handle the TextEventTriggered event.

```csharp
public delegate void DialogTextLabel.TextEventTriggeredHandler(TextEvent textEvent)
```

#### Parameters

[TextEvent](GameDialog.Runner.TextEvent.md) `textEvent`

The triggered TextEvent.

