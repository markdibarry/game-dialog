# <a id="GameCore_Dialog_DialogStorage"></a> DialogStorage `class`

Namespace: [GameCore.Dialog](GameCore.Dialog.md)

A class for storage and retrieval of dialog variables.

```csharp
public class DialogStorage
```

## Methods

### <a id="GameCore_Dialog_DialogStorage_ClearLocalStorage"></a> ClearLocalStorage\(\)

Clears all the local dialog variables.

```csharp
public void ClearLocalStorage()
```

### <a id="GameCore_Dialog_DialogStorage_ContainsKey_System_String_"></a> ContainsKey\(string\)

Determines whether the storage contains an entry with the specified key.

```csharp
public bool ContainsKey(string key)
```

#### Parameters

[string](https://learn.microsoft.com/dotnet/api/system.string) `key`

The variable key.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

If true, the storage contains an entry with the specified key.

### <a id="GameCore_Dialog_DialogStorage_SetValue_System_ReadOnlySpan_System_Char__System_String_"></a> SetValue\(ReadOnlySpan<char\>, string\)

Adds the specified key and string value to the storage.

```csharp
public void SetValue(ReadOnlySpan<char> key, string value)
```

#### Parameters

[ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\> `key`

The key of the entry to add.

[string](https://learn.microsoft.com/dotnet/api/system.string) `value`

The string value of the entry to add.

### <a id="GameCore_Dialog_DialogStorage_SetValue_System_ReadOnlySpan_System_Char__System_Single_"></a> SetValue\(ReadOnlySpan<char\>, float\)

Adds the specified key and float value to the storage.

```csharp
public void SetValue(ReadOnlySpan<char> key, float value)
```

#### Parameters

[ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\> `key`

The key of the entry to add.

[float](https://learn.microsoft.com/dotnet/api/system.single) `value`

The float value of the entry to add.

### <a id="GameCore_Dialog_DialogStorage_SetValue_System_ReadOnlySpan_System_Char__System_Boolean_"></a> SetValue\(ReadOnlySpan<char\>, bool\)

Adds the specified key and boolean value to the storage.

```csharp
public void SetValue(ReadOnlySpan<char> key, bool value)
```

#### Parameters

[ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\> `key`

The key of the entry to add.

[bool](https://learn.microsoft.com/dotnet/api/system.boolean) `value`

The boolean value of the entry to add.

### <a id="GameCore_Dialog_DialogStorage_TryGetValue__1_System_ReadOnlySpan_System_Char____0__"></a> TryGetValue<T\>\(ReadOnlySpan<char\>, out T?\)

Attempts to get the value associated with the specified key in the storage.

```csharp
public bool TryGetValue<T>(ReadOnlySpan<char> key, out T? value)
```

#### Parameters

[ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\> `key`

The key of the value to get.

T? `value`

When this method returns, contains the value associated with the specified key, if the key is found.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

If true, the storage contains an entry with the specified key.

#### Type Parameters

`T` 

The type of the value to retrieve.

