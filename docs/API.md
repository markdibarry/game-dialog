# API

## DialogBase

### `ScriptEnded`

```csharp
public event Action<DialogBase>? ScriptEnded
```

This event is invoked when the script has ended. Intended to handle cleanup and removal of the `DialogBase` object.

### `Next`

```csharp
public int? Next { get; set; }
```

The `Next` property sets the next instruction index to be read when `Resume()` is called. Intended to be set manually using a `Choice` via `Choice.Next`. If an invalid value is provided, the dialog will end.

### `OnDialogLineStarted()`

```csharp
protected abstract void OnDialogLineStarted(DialogLine line)
```

`OnDialogLineStarted()` is called when the script encounters a dialog line, intended for opening a dialog display. In the example below, we have added the built-in `TextWriter` node as a child of a custom dialog box and exposed it as a property.

```csharp
public DialogBox? DialogBox { get; set; }

protected override void OnDialogLineStarted(DialogLine line)
{
    DialogBox ??= CreateDialogBox();
    // Parses the TextEvents from the text.
    DialogBox.TextWriter.SetParsedText(line.Text);
    // Begins writing until the display runs out of space.
    DialogBox.TextWriter.WriteNextPage();
}

private DialogBox CreateDialogBox()
{
    PackedScene packedScene = GD.Load<PackedScene>(DialogBox.ScenePath);
    DialogBox newBox = packedScene.Instantiate<DialogBox>();
    // Lets the dialog know when the line has ended.
    newBox.LineEnded += OnDialogLineEnded;
    // Sets the DialogBase object as the TextWriter's TextEvent parser.
    newBox.TextWriter.TextEventHandler = this;
    AddChild(newBox);
    return newBox;
}
```

### `OnDialogLineResumed()`

```csharp
protected abstract void OnDialogLineResumed()
```

In this system, when a predefined method is awaited in the middle of a dialog line, the dialog and writer is suspended until `DialogBase.Resume()` is called. `OnDialogLineResumed()` is called when a currently active dialog line is resumed so you can know to resume the writer.

```csharp
protected override void OnDialogLineResumed()
{
    DialogBox?.TextWriter.Resume();
}
```

### `OnDialogLineEnded()`

```csharp
protected virtual void OnDialogLineEnded(DialogLine line)
```

`OnDialogLineEnded()` is called when a line ends. This is a `virtual` method rather than `abstract` because its default behavior will cover most cases. The default behavior is to set the `Next` as the next instruction, return the `DialogLine` to the pool, and calls `Resume()`.

### `OnChoice()` 

```csharp
protected abstract void OnChoice(List<Choice> choices)
```

When the script encounters a choice set, the dialog is suspended and this method is called. When you're finished handling the choice logic, set the `Next` property to the `Choice.Next` that was selected, and then call `DialogBase.Resume()`.

### `OnHash()`

```csharp
protected virtual void OnHash(Dictionary<string, string> hashData)
```

This method is called when your dialog script encounters as hash tag set and passes along the key/value pairs. If the hash tag has not assigned a value, it will be passed as an empty string.

WIP