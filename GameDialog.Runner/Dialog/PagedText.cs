using System;
using System.Collections.Generic;
using GameDialog.Common;
using GameDialog.Pooling;
using Godot;

namespace GameDialog.Runner;

[Tool, GlobalClass]
public partial class PagedText : RichTextLabel, IPoolable
{
    public PagedText()
    {
        ClipContents = true;
        BbcodeEnabled = true;
        _scrollBar = GetVScrollBar();
        Reset(false);
    }

    private const int DefaultCharsPerSecond = 30;
    private const int SpeedUpMultiplier = 3;
    private const float AutoTimeoutMultiplier = 0.2f;

    private bool _isWriting;
    private double _writeCounter;
    private int _totalCharacters;
    private Vector2I _targetWriteRange;
    private VScrollBar _scrollBar;
    private bool _isScrolling;
    private double _targetScrollValue;
    // Although ScrollBar.Value is a double, it rounds on Set,
    // so an intermediate is necessary.
    private double _movingScrollValue;
    private readonly List<TextEvent> _textEvents = [];
    private int _textEventIndex;

    [Export]
    public int CharsPerSecond { get; set; }
    [Export]
    public float ScrollSpeed
    {
        get => field;
        set => field = Math.Max(value, 0);
    }
    [ExportToolButton("Write Next Line")]
    public Callable WriteNextLineButton => Callable.From(WriteNextLine);
    [ExportToolButton("Write Next Page")]
    public Callable WriteNextPageButton => Callable.From(WriteNextPage);
    [ExportToolButton("Reset")]
    public Callable ResetButton => Callable.From(() => Reset(false));

    public bool Writing => _isWriting;
    public double SpeedMultiplier
    {
        get => field;
        set => field = value > 0 ? value : field;
    }
    public bool IsSpeedUpEnabled { get; set; }
    private double PauseTimer
    {
        get => field;
        set => field = value >= 0 ? value : field;
    }
    public bool AutoProceedEnabled { get; set; }
    private float AutoProceedTimeout
    {
        get
        {
            double time = field;

            if (time == -1)
                time = (_targetWriteRange.Y - _targetWriteRange.X) * AutoTimeoutMultiplier;

            return (float)Math.Max(0, time);
        }
        set => field = value;
    }
    /// <summary>
    /// Parses and handles text events, usually a DialogBase object.
    /// </summary>
    public ITextEventHandler? TextEventHandler { get; set; }
    /// <summary>
    /// A replacement for the base Text property to set parsed text properly.
    /// If set via the editor, this code is not called, even with an [Export] attribute.
    /// </summary>
    public new string Text
    {
        get => base.Text;
        set => SetParsedText(value);
    }

    public event Action? FinishedWriting;

    public override bool _Set(StringName property, Variant value)
    {
        if (property == RichTextLabel.PropertyName.Text)
        {
            if (value.Obj is string text)
                SetParsedText(text);

            return true;
        }

        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (PauseTimer > 0)
        {
            if (IsSpeedUpEnabled)
                delta *= SpeedUpMultiplier;

            PauseTimer = Math.Max(0, PauseTimer - delta);
        }
        else if (_isScrolling)
        {
            ScrollProcess(delta);
        }
        else if (Writing)
        {
            Write(delta);
        }
    }

    public void WriteNextPage() => WriteNext(false);

    public void WriteNextLine() => WriteNext(true);

    public bool IsComplete() => VisibleCharacters == -1 || VisibleCharacters == _totalCharacters;

    public void SetParsedText(string text)
    {
        _textEvents.Clear();
        _textEventIndex = 0;
        VisibleCharacters = 0;
        base.Text = text;
        base.Text = TextParser.GetEventParsedText(text, GetParsedText(), _textEvents, TextEventHandler);
        _totalCharacters = GetTotalCharacterCount();
        _scrollBar.Value = 0;
        _targetWriteRange = new(0, GetLastVisibleCharacter(0));
    }

    public void ClearObject()
    {
        Reset(true);
    }

    /// <summary>
    /// Sets up writing the next line or page.
    /// </summary>
    /// <param name="isLine">If false, writes a new page</param>
    private void WriteNext(bool isLine)
    {
        if (Writing || _isScrolling)
            return;

        int currentChar = VisibleCharacters == -1 ? _totalCharacters : VisibleCharacters;

        if (currentChar == _totalCharacters)
            return;

        // Is this screen fully written?
        int lastLine = GetLastVisibleLine(_scrollBar.Value);
        int lastChar = GetLineRange(lastLine).Y;

        if (currentChar != lastChar)
        {
            int firstLine = GetFirstVisibleLine(_scrollBar.Value);
            int firstChar = GetLineRange(firstLine).X;
            _targetWriteRange = new(firstChar, lastChar);
            _isWriting = true;
            return;
        }

        int nextLine;

        if (isLine)
            nextLine = GetFirstLineWithNextVisible(_scrollBar.Value);
        else
            nextLine = lastLine + 1;

        _movingScrollValue = _scrollBar.Value;
        _targetScrollValue = GetLineOffset(nextLine);
        _isScrolling = true;
        int charStart = GetLineRange(nextLine).X;
        int charEnd = GetLastVisibleCharacter(_targetScrollValue);
        _targetWriteRange = new(charStart, charEnd);
        _isWriting = true;
    }

    private int GetFirstVisibleLine(double startingOffset)
    {
        int totalLines = GetLineCount();

        for (int i = 0; i < totalLines; i++)
        {
            float lineTop = GetLineOffset(i);

            if (lineTop >= startingOffset)
                return i;
        }

        return 0;
    }

    private int GetLastVisibleCharacter(double startingOffset)
    {
        int line = GetLastVisibleLine(startingOffset);
        Vector2I range = GetLineRange(line);
        return range.Y;
    }

    /// <summary>
    /// Finds the last fully visible line on screen.
    /// </summary>
    /// <param name="startingOffset">
    /// The vertical scroll offset from which to begin searching.
    /// </param>
    /// <returns></returns>
    private int GetLastVisibleLine(double startingOffset)
    {
        int totalLines = GetLineCount();
        float controlHeight = Size.Y;
        float contentHeight = GetContentHeight();
        int lastLine = 0;
        float lineTop = 0;

        for (int i = 0; i < totalLines; i++)
        {
            float lineBottom = (i + 1 < totalLines) ? GetLineOffset(i + 1) : contentHeight;

            if (lineTop >= startingOffset && lineBottom <= startingOffset + controlHeight)
                lastLine = i;
            else if (lineTop > startingOffset + controlHeight)
                break;

            lineTop = lineBottom;
        }

        return lastLine;
    }

    /// <summary>
    /// Finds the first line index that, when scrolled to the top of the RichTextLabel,
    /// still leaves the following line fully visible in the viewport.
    /// </summary>
    /// <param name="startingOffset">
    /// The vertical scroll offset from which to begin searching.
    /// </param>
    /// <returns>
    /// The index of the line that can be aligned to the top
    /// while keeping its next line fully in view.
    /// </returns>
    private int GetFirstLineWithNextVisible(double startingOffset)
    {
        int totalLines = GetLineCount();
        int lastVisibleLine = GetLastVisibleLine(startingOffset);
        float controlHeight = Size.Y;

        if (lastVisibleLine >= totalLines - 1)
            return lastVisibleLine;

        int firstLine = lastVisibleLine + 1;
        int totalOffset = GetLineHeight(firstLine);

        while (firstLine > 0)
        {
            totalOffset += GetLineHeight(firstLine - 1);

            if (totalOffset > controlHeight)
                break;

            firstLine--;
        }

        return firstLine;
    }

    private void ScrollProcess(double delta)
    {
        if (ScrollSpeed == 0)
        {
            _scrollBar.Value = _targetScrollValue;
            _isScrolling = false;
            return;
        }

        double speed = ScrollSpeed * delta;
        _movingScrollValue = Mathf.MoveToward(_movingScrollValue, _targetScrollValue, speed);
        _scrollBar.Value = _movingScrollValue;

        if (_movingScrollValue == _targetScrollValue)
            _isScrolling = false;
    }

    private void RaiseTextEvents()
    {
        TextEvent te = GetEvent(_textEvents, _textEventIndex);

        while (te.EventType != EventType.Undefined && te.TextIndex <= VisibleCharacters)
        {
            // TODO: Handle Seen
            te.Seen = true;
            HandleTextEvent(te);
            _textEventIndex++;
            te = GetEvent(_textEvents, _textEventIndex);
        }

        static TextEvent GetEvent(List<TextEvent> events, int index)
        {
            if (index >= events.Count)
                return TextEvent.Undefined;

            return events[index];
        }
    }

    private void HandleTextEvent(TextEvent textEvent)
    {
        switch (textEvent.EventType)
        {
            case EventType.Pause:
                float timeValue = (float)textEvent.Value;
                PauseTimer += timeValue;
                break;
            case EventType.Speed:
                float speedValue = (float)textEvent.Value;
                SpeedMultiplier = speedValue;
                break;
            case EventType.Auto:
                float autoValue = (float)textEvent.Value;
                AutoProceedEnabled = autoValue != -2;
                AutoProceedTimeout = autoValue;
                break;
            default:
                TextEventHandler?.HandleTextEvent(textEvent);
                break;
        }
    }

    private void Reset(bool clearText)
    {
        if (clearText)
            Text = string.Empty;

        _textEvents.Clear();
        _textEventIndex = 0;
        VisibleCharacters = 0;
        SpeedMultiplier = 1;
        CharsPerSecond = DefaultCharsPerSecond;
        AutoProceedEnabled = false;
        AutoProceedTimeout = 0;
        PauseTimer = 0;
        VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
        _scrollBar.AllowGreater = true;
        _scrollBar.Scale = Vector2.Zero;
        _scrollBar.Value = 0;
        _targetWriteRange = new(0, GetLastVisibleCharacter(0));
    }

    private void Write(double delta)
    {
        int currentChar = VisibleCharacters;
        bool isComplete = currentChar == -1 || currentChar == _totalCharacters;

        if (isComplete || currentChar >= _targetWriteRange.Y)
        {
            _isWriting = false;
            FinishedWriting?.Invoke();

            if (AutoProceedEnabled && !isComplete)
                WriteNextPage();

            return;
        }

        int charsToAdd = GetCharsToAdd(currentChar, delta, _targetWriteRange);

        if (charsToAdd <= 0)
            return;

        VisibleCharacters = currentChar + charsToAdd;
        RaiseTextEvents();

        if (VisibleCharacters < _targetWriteRange.Y)
            return;

        _writeCounter = 0;

        if (AutoProceedEnabled)
            PauseTimer += AutoProceedTimeout;
    }

    private int GetCharsToAdd(int currentChar, double delta, Vector2I charRange)
    {
        double totalSpeed = CharsPerSecond * SpeedMultiplier;

        if (IsSpeedUpEnabled)
            totalSpeed *= SpeedUpMultiplier;

        _writeCounter += delta * totalSpeed;
        int charsToAdd = (int)_writeCounter;

        if (charsToAdd < 1)
            return 0;

        _writeCounter -= charsToAdd;

        // Jump to first char if below
        if (currentChar < charRange.X)
            charsToAdd = charRange.X - currentChar;
        else if (currentChar + charsToAdd >= charRange.Y)
            charsToAdd = charRange.Y - currentChar;

        return charsToAdd;
    }
}
