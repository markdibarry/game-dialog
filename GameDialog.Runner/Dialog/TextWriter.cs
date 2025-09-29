using System;
using System.Collections.Generic;
using GameDialog.Pooling;
using Godot;

namespace GameDialog.Runner;

[Tool, GlobalClass]
public partial class TextWriter : RichTextLabel, IPoolable
{
    public TextWriter()
    {
        ClipContents = true;
        BbcodeEnabled = true;
        _scrollBar = GetVScrollBar();
        Reset(false);

        if (_theme == null)
        {
            _theme = new();
            _theme.SetStylebox("scroll", "VScrollBar", new StyleBoxEmpty());
        }

        Theme = _theme;
    }

    private static Theme _theme = default!;

    private const int DefaultCharsPerSecond = 30;
    private const int SpeedUpMultiplier = 3;
    private const float AutoTimeoutMultiplier = 0.05f;

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
    private bool _scrollPageOverride;

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
    public double SpeedMultiplier { get; set; }
    public bool IsSpeedUpEnabled { get; set; }
    public bool AutoProceedEnabled { get; set; }
    public bool Suspended { get; private set; }
    /// <summary>
    /// The DialogBase object. For parsing and handling text events.
    /// </summary>
    public DialogBase? Dialog { get; set; }
    /// <summary>
    /// A replacement for the base Text property to set parsed text properly.
    /// If set via the editor, this code is not called, even with an [Export] attribute.
    /// </summary>
    public new string Text
    {
        get => base.Text;
        set => SetParsedText(value);
    }

    private double PauseTimer
    {
        get => field;
        set => field = value >= 0 ? value : field;
    }
    public float AutoProceedTimeout
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
        if (Suspended)
            return;

        if (PauseTimer > 0)
        {
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

    public void Resume() => Suspended = false;

    public void SetParsedText(string text)
    {
        _textEvents.Clear();
        _textEventIndex = 0;
        VisibleCharacters = 0;
        base.Text = text;
        base.Text = DialogBase.GetEventParsedText(text, GetParsedText(), _textEvents, Dialog);
        _totalCharacters = GetTotalCharacterCount();
        _scrollBar.Value = 0;
        _targetScrollValue = 0;
        _movingScrollValue = 0;
        _targetWriteRange = new(0, GetLastVisibleCharacter(0));

        AutoProceedEnabled = Dialog?.AutoProceedGlobalEnabled ?? false;
        AutoProceedTimeout = Dialog?.AutoProceedGlobalTimeout ?? 0;
        SpeedMultiplier = Dialog?.SpeedMultiplier ?? 1;
    }

    public bool IsOnLastPage() => _scrollBar.Value >= _scrollBar.MaxValue - Size.Y;

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

        if (_scrollPageOverride)
        {
            _scrollPageOverride = false;
            int currentLine = GetCharacterLine(currentChar);
            BeginScrollToLine(currentLine);
        }

        // Is this screen fully written?
        int lastLine = GetLastVisibleLine(_targetScrollValue);
        int lastChar = GetLineRange(lastLine).Y;

        if (currentChar != lastChar)
        {
            int firstLine = GetFirstVisibleLine(_targetScrollValue);
            int firstChar = GetLineRange(firstLine).X;
            _targetWriteRange = new(firstChar, lastChar);
            _isWriting = true;
            return;
        }

        int nextLine;

        if (isLine)
            nextLine = GetFirstLineWithNextVisible(_targetScrollValue);
        else
            nextLine = lastLine + 1;

        BeginScrollToLine(nextLine);
        int charStart = GetLineRange(nextLine).X;
        int charEnd = GetLastVisibleCharacter(_targetScrollValue);
        _targetWriteRange = new(charStart, charEnd);
        _isWriting = true;

        void BeginScrollToLine(int line)
        {
            if (ScrollSpeed == 0)
            {
                _targetScrollValue = GetLineOffset(line);
                _scrollBar.Value = _targetScrollValue;
                _isScrolling = false;
                return;
            }

            _movingScrollValue = _scrollBar.Value;
            _targetScrollValue = GetLineOffset(line);

            if (_targetScrollValue != _movingScrollValue)
                _isScrolling = true;
        }
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

    private bool HandleTextEvent(int textIndex)
    {
        if (_textEventIndex >= _textEvents.Count)
            return false;

        TextEvent textEvent = _textEvents[_textEventIndex];

        if (textIndex < textEvent.TextIndex)
            return false;

        if (textEvent.IsAwait)
            Suspended = true;

        _textEventIndex++;

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
                int currentChar = VisibleCharacters;
                bool isComplete = currentChar == -1 || currentChar == _totalCharacters;

                if (isComplete)
                    PauseTimer += AutoProceedTimeout;

                break;
            case EventType.Prompt:
            case EventType.Page:
                int endChar = VisibleCharacters == -1 ? _totalCharacters : VisibleCharacters;
                _targetWriteRange.Y = endChar + 1;

                if (textEvent.EventType == EventType.Page)
                    _scrollPageOverride = true;

                break;
            default:
                Dialog?.HandleTextEvent(textEvent);
                break;
        }

        return true;
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
        _targetScrollValue = 0;
        _movingScrollValue = 0;
        _scrollPageOverride = false;
        _targetWriteRange = new(0, GetLastVisibleCharacter(0));
    }

    private void Write(double delta)
    {
        int currentChar = VisibleCharacters;
        bool isComplete = currentChar == -1 || currentChar == _totalCharacters;

        if (isComplete && HandleTextEvent(currentChar))
            return;

        if (isComplete || currentChar >= _targetWriteRange.Y)
        {
            _isWriting = false;
            FinishedWriting?.Invoke();

            if (AutoProceedEnabled && !isComplete)
                WriteNextPage();

            return;
        }

        double totalSpeed = CharsPerSecond * SpeedMultiplier;

        if (totalSpeed <= 0) // Instant write
        {
            _writeCounter = _targetWriteRange.Y - currentChar;
        }
        else
        {
            if (IsSpeedUpEnabled)
                totalSpeed *= SpeedUpMultiplier;

            _writeCounter += delta * totalSpeed;

            // catch up to target range start
            if (currentChar < _targetWriteRange.X)
                _writeCounter = _targetWriteRange.X - currentChar;
        }

        while (_writeCounter >= 1 && currentChar < _targetWriteRange.Y)
        {
            if (HandleTextEvent(currentChar))
                break;

            _writeCounter--;
            currentChar++;
            VisibleCharacters = currentChar;
        }

        if (currentChar < _targetWriteRange.Y)
            return;

        _writeCounter = 0;

        if (AutoProceedEnabled)
            PauseTimer += AutoProceedTimeout;
    }
}
