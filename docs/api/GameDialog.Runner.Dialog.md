# <a id="GameDialog_Runner_Dialog"></a> Dialog `class`

Namespace: [GameDialog.Runner](GameDialog.Runner.md)

Used to manage dialog scripts.

```csharp
public sealed class Dialog
```

## Constructors

### <a id="GameDialog_Runner_Dialog__ctor_Godot_Node_"></a> Dialog\(Node\)

```csharp
public Dialog(Node context)
```

#### Parameters

[Node](https://docs.godotengine.org/en/stable/classes/class_node.html) `context`

The Godot Node context

## Properties

### <a id="GameDialog_Runner_Dialog_Context"></a> Context

The Godot Node this Dialog is used by.

```csharp
public Node Context { get; }
```

#### Property Value

 [Node](https://docs.godotengine.org/en/stable/classes/class_node.html)

### <a id="GameDialog_Runner_Dialog_DialogStorage"></a> DialogStorage

Stores variables for the current dialog script.

```csharp
public DialogStorage DialogStorage { get; }
```

#### Property Value

 [DialogStorage](GameDialog.Runner.DialogStorage.md)

### <a id="GameDialog_Runner_Dialog_GlobalAutoProceedEnabled"></a> GlobalAutoProceedEnabled

The global auto-proceed value.
Updated when an [auto] tag is used outside of a dialog line.

```csharp
public bool GlobalAutoProceedEnabled { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### <a id="GameDialog_Runner_Dialog_GlobalAutoProceedTimeout"></a> GlobalAutoProceedTimeout

The global auto-proceed timeout value.
Updated when an [auto] tag is used outside of a dialog line.

```csharp
public float GlobalAutoProceedTimeout { get; }
```

#### Property Value

 [float](https://learn.microsoft.com/dotnet/api/system.single)

### <a id="GameDialog_Runner_Dialog_GlobalSpeedMultiplier"></a> GlobalSpeedMultiplier

The global speed multiplier value.
Updated when a [speed] tag is used outside of a dialog line.

```csharp
public double GlobalSpeedMultiplier { get; }
```

#### Property Value

 [double](https://learn.microsoft.com/dotnet/api/system.double)

### <a id="GameDialog_Runner_Dialog_TranslationFileType"></a> TranslationFileType

Updates the translation file type to check for when parsing dialog lines.

```csharp
public static TranslationFileType TranslationFileType { get; set; }
```

#### Property Value

 [TranslationFileType](GameDialog.Runner.TranslationFileType.md)

## Methods

### <a id="GameDialog_Runner_Dialog_AdjustEventIndices_System_String_System_String_System_Collections_Generic_List_GameDialog_Runner_TextEvent__"></a> AdjustEventIndices\(string, string, List<TextEvent\>\)

Adjusts TextEvent indices based on comparing the text before and after setting the RichTextLabel.
An alternative to setting the RichTextLabel.Text twice.

```csharp
public static void AdjustEventIndices(string eventParsedText, string displayedText, List<TextEvent> events)
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `eventParsedText`

[string](https://learn.microsoft.com/dotnet/api/system.string) `displayedText`

[List](https://learn.microsoft.com/dotnet/api/system.collections.generic.list\-1)<[TextEvent](GameDialog.Runner.TextEvent.md)\> `events`

### <a id="GameDialog_Runner_Dialog_Clear"></a> Clear\(\)

Clears and resets the Dialog script.

```csharp
public void Clear()
```

### <a id="GameDialog_Runner_Dialog_EndDialogLine"></a> EndDialogLine\(\)

Should be called when a Dialog line has ended.

```csharp
public void EndDialogLine()
```

### <a id="GameDialog_Runner_Dialog_Load_System_String_"></a> Load\(string\)

Loads a script from a path.

```csharp
public void Load(string path)
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `path`

### <a id="GameDialog_Runner_Dialog_LoadFromText_System_String_"></a> LoadFromText\(string\)

Loads a script from a string.

```csharp
public void LoadFromText(string text)
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `text`

The text string.

### <a id="GameDialog_Runner_Dialog_LoadSingleLine_System_String_"></a> LoadSingleLine\(string\)

Loads a script from a single dialog string. Must contain the speaker.

```csharp
public void LoadSingleLine(string text)
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `text`

The single dialog string.

### <a id="GameDialog_Runner_Dialog_ParseEventsFromText_System_String_System_Collections_Generic_List_GameDialog_Runner_TextEvent__"></a> ParseEventsFromText\(string, List<TextEvent\>\)

Removes TextEvents from text and inserts them into the provided List.

```csharp
public string ParseEventsFromText(string unparsedText, List<TextEvent> textEvents)
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `unparsedText`

The text with events.

[List](https://learn.microsoft.com/dotnet/api/system.collections.generic.list\-1)<[TextEvent](GameDialog.Runner.TextEvent.md)\> `textEvents`

The list of text events to fill.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The text with the events removed.

### <a id="GameDialog_Runner_Dialog_Resume"></a> Resume\(\)

Resumes the dialog to the next line.

```csharp
public void Resume()
```

### <a id="GameDialog_Runner_Dialog_Resume_System_Int32_"></a> Resume\(int\)

Resumes the dialog to the provided line index.

```csharp
public void Resume(int nextIndex)
```

#### Parameters

[int](https://learn.microsoft.com/dotnet/api/system.int32) `nextIndex`

The next index to read.

### <a id="GameDialog_Runner_Dialog_Start_System_String_"></a> Start\(string\)

Begins a loaded dialog script.

```csharp
public void Start(string sectionId = "")
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `sectionId`

Optional starting section id

### <a id="GameDialog_Runner_Dialog_TryEvaluateExpression_System_ReadOnlyMemory_System_Char__"></a> TryEvaluateExpression\(ReadOnlyMemory<char\>\)

Attepts to evaluate a string of text as an expression.

```csharp
public bool TryEvaluateExpression(ReadOnlyMemory<char> text)
```

#### Parameters

[ReadOnlyMemory](https://learn.microsoft.com/dotnet/api/system.readonlymemory\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\> `text`

The text to evaluate.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

If true, the expression was evaluated successfully.

### <a id="GameDialog_Runner_Dialog_TryParseBuiltInEvent_GameDialog_Runner_TextEvent_GameDialog_Runner_EventType__System_Single__"></a> TryParseBuiltInEvent\(TextEvent, out EventType, out float\)

Attempts to get the EventType and any parameter for a built in text event.

```csharp
public bool TryParseBuiltInEvent(TextEvent textEvent, out EventType eventType, out float parameter)
```

#### Parameters

[TextEvent](GameDialog.Runner.TextEvent.md) `textEvent`

The triggered text event

[EventType](GameDialog.Runner.EventType.md) `eventType`

The resulting event type

[float](https://learn.microsoft.com/dotnet/api/system.single) `parameter`

The resulting parameter for the event

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

If true, the TextEvent is a built in event.

### <a id="GameDialog_Runner_Dialog_ValidateScript_System_Collections_Generic_List_GameDialog_Runner_Error__System_Text_StringBuilder_System_IO_StreamWriter_"></a> ValidateScript\(List<Error\>, StringBuilder?, StreamWriter?\)

Validates the loaded script for errors.

```csharp
public void ValidateScript(List<Error> errors, StringBuilder? chart = null, StreamWriter? sw = null)
```

#### Parameters

[List](https://learn.microsoft.com/dotnet/api/system.collections.generic.list\-1)<[Error](GameDialog.Runner.Error.md)\> `errors`

The error list to populate

[StringBuilder](https://learn.microsoft.com/dotnet/api/system.text.stringbuilder)? `chart`

The chart StringBuilder to append to

[StreamWriter](https://learn.microsoft.com/dotnet/api/system.io.streamwriter)? `sw`

The StreamWriter object for generating translations

## Events

### <a id="GameDialog_Runner_Dialog_ChoiceRead"></a> ChoiceRead

Occurs when a set of choices is encountered.

```csharp
public event Action<IReadOnlyList<Choice>>? ChoiceRead
```

#### Event Type

 [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist\-1)<[Choice](GameDialog.Runner.Choice.md)\>\>?

### <a id="GameDialog_Runner_Dialog_DialogLineResumed"></a> DialogLineResumed

If an async method is called in the middle of a line of dialog,
this will be invoked when it finishes.
Used to notify the text writer to continue.

```csharp
public event Action? DialogLineResumed
```

#### Event Type

 [Action](https://learn.microsoft.com/dotnet/api/system.action)?

### <a id="GameDialog_Runner_Dialog_DialogLineStarted"></a> DialogLineStarted

Occurs when the script reaches a dialog line.
Used to pass the provided text to the text writer and handle the provided speaker IDs.

```csharp
public event Action<string, IReadOnlyList<string>>? DialogLineStarted
```

#### Event Type

 [Action](https://learn.microsoft.com/dotnet/api/system.action\-2)<[string](https://learn.microsoft.com/dotnet/api/system.string), [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist\-1)<[string](https://learn.microsoft.com/dotnet/api/system.string)\>\>?

### <a id="GameDialog_Runner_Dialog_HashRead"></a> HashRead

Occurs when a hash tag is encountered. Provides the hash's key value pairs.
If no value is defined, the value will be an empty string.

```csharp
public event Action<IReadOnlyDictionary<string, string>>? HashRead
```

#### Event Type

 [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary\-2)<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)\>\>?

### <a id="GameDialog_Runner_Dialog_ScriptEnded"></a> ScriptEnded

Occurs when the end of the script has been reached.

```csharp
public event Action<Dialog>? ScriptEnded
```

#### Event Type

 [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[Dialog](GameDialog.Runner.Dialog.md)\>?

