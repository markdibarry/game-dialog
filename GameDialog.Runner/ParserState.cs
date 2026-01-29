using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GameDialog.Runner;

public class ParserState
{
    public List<ReadOnlyMemory<char>> Script { get; } = [];
    public ReadOnlySpan<char> SpanLine => MemLine.Span;
    public ReadOnlyMemory<char> MemLine
    {
        get
        {
            if (LineIdx >= Script.Count)
                return default;

            var mem = Script[LineIdx];

            if (_commentStart != -1)
                mem = mem[.._commentStart];

            return mem;
        }
    }
    public int CurrentIndentLevel => _indents.Count > 0 ? _indents.Peek() : 0;
    public int CurrentIndentCount => _indents.Count;
    public int IndentChange => CurrentIndentLevel - _prevIndentLevel;
    public int LineIdx { get; private set; }
    public int Dedents { get; private set; }
    public ReadOnlyMemory<char> RowPrefix { get; private set; }

    private IndentStyle _indentStyle;
    private readonly Stack<int> _indents = [];
    private int _prevIndentLevel;
    private int _commentStart;

    /// <summary>
    /// Reads a file at the specified path to the script. Uses System.IO.
    /// </summary>
    /// <param name="globalPath"></param>
    public void ReadFileToScript(string globalPath, string rootPath)
    {
        if (RowPrefix.Length > 0 && MemoryMarshal.TryGetArray(RowPrefix, out var seg) && seg.Array != null)
            ArrayPool<char>.Shared.Return(seg.Array);

        ReadOnlySpan<char> localPath = globalPath;

        if (localPath.StartsWith(rootPath))
            localPath = localPath[rootPath.Length..];

        if (localPath.StartsWith('/'))
            localPath = localPath[1..];

        RowPrefix = GeneratePrefix(localPath);
        ReadFileToList(globalPath, Script);
    }

    public void ReadStringToScript(string text, ReadOnlySpan<char> path)
    {
        if (RowPrefix.Length > 0 && MemoryMarshal.TryGetArray(RowPrefix, out var seg) && seg.Array != null)
            ArrayPool<char>.Shared.Return(seg.Array);

        RowPrefix = default;

        if (path.StartsWith("res://"))
            path = path["res://".Length..];

        RowPrefix = GeneratePrefix(path);
        ReadStringToList(text, Script);
    }

    public void Reset()
    {
        _commentStart = -1;
        _indents.Clear();
        _indentStyle = IndentStyle.Unset;
        _prevIndentLevel = 0;
        Dedents = 0;
        MoveLine(0);
    }

    public bool Dedent()
    {
        if (Dedents <= 0)
            return false;

        Dedents--;
        return true;
    }

    public void MoveNextLine(List<Error>? errors = null)
    {
        MoveLine(LineIdx + 1, errors);
    }

    public void MoveLine(int lineIdx, List<Error>? errors = null)
    {
        _commentStart = -1;

        if (Script == null || lineIdx < 0)
        {
            LineIdx = 0;
            UpdateIndentation(errors);
            return;
        }

        LineIdx = Script.GetNextNonEmptyLine(lineIdx);

        if (LineIdx < Script.Count)
        {
            ReadOnlySpan<char> span = Script[LineIdx].Span;
            ReadOnlySpan<char> stripped = span.StripLineComment();

            if (span.Length != stripped.Length)
                _commentStart = stripped.Length;
        }

        UpdateIndentation(errors);
    }

    private void UpdateIndentation(List<Error>? errors)
    {
        ReadOnlySpan<char> line = SpanLine;
        Dedents = 0;

        if (_indentStyle == IndentStyle.Unset)
        {
            if (line.StartsWith(' '))
                _indentStyle = IndentStyle.Spaces;
            else if (line.StartsWith('\t'))
                _indentStyle = IndentStyle.Tabs;
            else
                return;
        }

        if (_indentStyle == IndentStyle.Spaces)
            HandleIndent(line, ' ', '\t');
        else if (_indentStyle == IndentStyle.Tabs)
            HandleIndent(line, '\t', ' ');

        void HandleIndent(ReadOnlySpan<char> line, char correct, char incorrect)
        {
            int newIndent = 0;

            while (newIndent < line.Length)
            {
                char c = line[newIndent];

                if (c == incorrect)
                {
                    errors?.AddError(LineIdx, newIndent, newIndent + 1, "Mixed indentation detected");
                    return;
                }

                if (c != correct)
                    break;

                newIndent++;
            }

            _prevIndentLevel = CurrentIndentLevel;

            if (newIndent > CurrentIndentLevel)
            {
                _indents.Push(newIndent);
                return;
            }

            while (newIndent < CurrentIndentLevel)
            {
                Dedents++;
                _prevIndentLevel = _indents.Pop();
            }
        }
    }

    private enum IndentStyle
    {
        Unset,
        Spaces,
        Tabs
    }

    private static ReadOnlyMemory<char> GeneratePrefix(ReadOnlySpan<char> path)
    {
        ReadOnlySpan<char> nameNoExt = Path.GetFileNameWithoutExtension(path);
        ReadOnlySpan<char> pathOnly = Path.GetDirectoryName(path);
        int hashSize = 6;
        int arrLen = hashSize + nameNoExt.Length + 1;
        char[] chars = ArrayPool<char>.Shared.Rent(arrLen);
        FillPathHash(pathOnly, chars, hashSize);
        chars[hashSize] = '_';

        for (int i = hashSize + 1; i < arrLen; i++)
            chars[i] = nameNoExt[i - (hashSize + 1)];

        return chars.AsMemory(0, arrLen);
    }

    private static void FillPathHash(ReadOnlySpan<char> path, char[] buf, int length)
    {
        const uint FNV_OFFSET = 2166136261u;
        const uint FNV_PRIME = 16777619u;
        uint offset = FNV_OFFSET;

        foreach (char c in path)
        {
            offset ^= (uint)(c & 0xFF);
            offset *= FNV_PRIME;
            offset ^= (uint)(c >> 8);
            offset *= FNV_PRIME;
        }

        int bytesNeeded = (length + 1) / 2;
        uint v = offset;
        int writePos = 0;

        for (int i = bytesNeeded - 1; i >= 0; i--)
        {
            byte b = (byte)(v >> (i * 8));
            int hi = (b >> 4) & 0xF;
            int lo = b & 0xF;

            if (writePos < length)
                buf[writePos++] = (char)(hi < 10 ? ('0' + hi) : ('a' + hi - 10));

            if (writePos < length)
                buf[writePos++] = (char)(lo < 10 ? ('0' + lo) : ('a' + lo - 10));
        }
    }

    private static void ReadStringToList(string text, List<ReadOnlyMemory<char>> lines)
    {
        if (lines.Count > 0 && MemoryMarshal.TryGetArray(lines[0], out var seg) && seg.Array != null)
            ArrayPool<char>.Shared.Return(seg.Array);

        lines.Clear();
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '\r')
                continue;

            if (c == '\n')
            {
                int len = i - start;
                lines.Add(text.AsMemory(start, len));
                start = i + 1;
            }
        }

        if (start <= text.Length - 1)
            lines.Add(text.AsMemory(start, text.Length - start));
    }

    private static void ReadFileToList(string path, List<ReadOnlyMemory<char>> lines)
    {
        const int ByteBufferSize = 32 * 1024;

        if (lines.Count > 0 && MemoryMarshal.TryGetArray(lines[0], out var seg) && seg.Array != null)
            ArrayPool<char>.Shared.Return(seg.Array);

        lines.Clear();
        var bytePool = ArrayPool<byte>.Shared;
        var charPool = ArrayPool<char>.Shared;
        byte[] byteBuf = bytePool.Rent(ByteBufferSize);
        char[]? charBuf = null;
        int charPos = 0;
        Decoder decoder = Encoding.UTF8.GetDecoder();

        try
        {
            using var fs = File.OpenRead(path);

            if (fs.Length > int.MaxValue)
                throw new IOException("File too large to decode into a single buffer.");

            int maxChars = Encoding.UTF8.GetMaxCharCount((int)fs.Length);
            charBuf = charPool.Rent(maxChars);
            int bytesRead;

            while ((bytesRead = fs.Read(byteBuf, 0, ByteBufferSize)) > 0)
            {
                int charsDecoded = decoder.GetChars(byteBuf, 0, bytesRead, charBuf, charPos, flush: false);
                charPos += charsDecoded;
            }

            charPos += decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuf, charPos, flush: true);
            int start = 0;

            for (int i = 0; i < charPos; i++)
            {
                char c = charBuf[i];

                if (c == '\r')
                    continue;

                if (c == '\n')
                {
                    int len = i - start;
                    lines.Add(new(charBuf, start, len));
                    start = i + 1;
                }
            }

            if (start < charPos)
                lines.Add(new(charBuf, start, charPos - start));
            else
                charPool.Return(charBuf);
        }
        finally
        {
            bytePool.Return(byteBuf);

            if (charBuf != null && lines.Count == 0)
                charPool.Return(charBuf);
        }
    }
}
