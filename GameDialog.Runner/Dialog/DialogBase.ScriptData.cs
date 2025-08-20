using System;
using System.Buffers;
using System.IO;
using System.Text.Json;
using GameDialog.Common;

namespace GameDialog.Runner;

public partial class DialogBase
{
    private void ClearStorage()
    {
        _textStorage.Clear();
        SpeakerIds.Clear();
        Strings.Clear();
        Floats.Clear();

        foreach (var inst in Instructions)
            ArrayPool<ushort>.Shared.Return(inst, true);

        Instructions.Clear();
    }

    /// <summary>
    /// Loads a signle line of text.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="speakerId"></param>
    public void LoadSingleLine(string text, string? speakerId = null)
    {
        ClearStorage();
        Strings.Add(text);
        var arr = ArrayPool<ushort>.Shared.Rent(5);
        int i = 0;
        arr[i++] = InstructionType.Line;
        arr[i++] = 1;

        if (speakerId != null)
        {
            SpeakerIds.Add(speakerId);
            arr[i++] = 1;
            arr[i++] = 0;
        }

        arr[i++] = 0;
        Instructions.Add(arr);
    }

    /// <summary>
    /// Loads a .dia script file.
    /// </summary>
    /// <param name="path"></param>
    /// <exception cref="DialogException"></exception>
    public void LoadScript(string path)
    {
        ClearStorage();
        string globalizedPath = Godot.ProjectSettings.GlobalizePath(path);

        if (!File.Exists(globalizedPath))
            throw new DialogException("No path provided");

        using FileStream fileStream = new(globalizedPath, FileMode.Open, FileAccess.Read);

        if (fileStream.Length > int.MaxValue)
            throw new DialogException("File length exceeds max value.");

        byte[]? rented = null;

        try
        {
            int fileLength = (int)fileStream.Length;

            if (fileStream.Length <= 64 * 1024)
            {
                Span<byte> stackBuffer = stackalloc byte[fileLength];
                ParseScript(fileStream, ref stackBuffer);
            }
            else
            {
                rented = ArrayPool<byte>.Shared.Rent(fileLength);
                Span<byte> rentedSpan = rented.AsSpan(0, fileLength);
                ParseScript(fileStream, ref rentedSpan);
            }
        }
        finally
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void ParseScript(FileStream fileStream, ref Span<byte> buffer)
    {
        int pos = 0;

        while (pos < buffer.Length)
        {
            int bytesRead = fileStream.Read(buffer[pos..]);

            if (bytesRead == 0)
                throw new DialogException("Unexpected end of file.");

            pos += bytesRead;
        }

        var reader = new Utf8JsonReader(buffer);

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isSpeakerIds = reader.ValueTextEquals(nameof(SpeakerIds));

                if (isSpeakerIds || reader.ValueTextEquals(nameof(Strings)))
                {
                    reader.Read();
                    reader.Read();
                    int stringCount = GetArrayCount(reader);

                    for (int i = 0; i < stringCount; i++)
                    {
                        if (isSpeakerIds)
                            SpeakerIds.Add(reader.GetString() ?? string.Empty);
                        else
                            Strings.Add(reader.GetString() ?? string.Empty);

                        reader.Read();
                    }
                }
                else if (reader.ValueTextEquals(nameof(Floats)))
                {
                    reader.Read();
                    reader.Read();
                    int floatCount = GetArrayCount(reader);

                    for (int i = 0; i < floatCount; i++)
                    {
                        Floats.Add(reader.GetSingle());
                        reader.Read();
                    }
                }
                else if (reader.ValueTextEquals(nameof(Instructions)))
                {
                    reader.Read();
                    reader.Read();
                    int arrCount = GetNestedArrayCount(reader);
                    reader.Read();

                    for (int i = 0; i < arrCount; i++)
                    {
                        int intCount = GetArrayCount(reader);
                        ushort[] arr = ArrayPool<ushort>.Shared.Rent(intCount);
                        Instructions.Add(arr);

                        for (int j = 0; j < intCount; j++)
                        {
                            arr[j] = reader.GetUInt16();
                            reader.Read();
                        }

                        reader.Read();
                        reader.Read();
                    }
                }
            }
            reader.Read();
        }
        
        static int GetNestedArrayCount(Utf8JsonReader reader)
        {
            int count = 0;
            JsonTokenType prevType = JsonTokenType.None;

            while (prevType != JsonTokenType.EndArray || reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.StartArray)
                    count++;

                prevType = reader.TokenType;
                reader.Read();
            }

            return count;
        }

        static int GetArrayCount(Utf8JsonReader reader)
        {
            int count = 0;

            while (reader.TokenType != JsonTokenType.EndArray)
            {
                count++;
                reader.Read();
            }

            return count;
        }
    }
}

public class DialogException : Exception
{
    public DialogException(string message) : base(message)
    {
    }
}