# <a id="GameCore_Dialog_TextEvent"></a> TextEvent `struct`

Namespace: [GameCore.Dialog](GameCore.Dialog.md)

Represents a dialog text event.

```csharp
public struct TextEvent
```

## Fields

### <a id="GameCore_Dialog_TextEvent_Tag"></a> Tag

The event text content.

```csharp
public ReadOnlyMemory<char> Tag
```

#### Field Value

 [ReadOnlyMemory](https://learn.microsoft.com/dotnet/api/system.readonlymemory\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

### <a id="GameCore_Dialog_TextEvent_TextIndex"></a> TextIndex

The char index in the rendered text when the event triggers.

```csharp
public int TextIndex
```

#### Field Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

