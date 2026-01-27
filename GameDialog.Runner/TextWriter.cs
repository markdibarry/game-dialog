using System;
using System.Collections.Generic;
using Godot;

namespace GameDialog.Runner;

[Tool, GlobalClass]
public partial class TextWriter : RichTextLabel
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
    private readonly VScrollBar _scrollBar;
    private bool _isScrolling;
    private double _targetScrollValue;
    // Although ScrollBar.Value is a double, it rounds on Set,
    // so an intermediate is necessary.
    private double _movingScrollValue;
    private readonly List<TextEvent> _textEvents = [];
    private int _textEventIndex;

    [Export]
    public int CharsPerSecond { get; set; }

    /// <summary>
    /// Scroll speed in pixels per second
    /// </summary>
    [Export]
    public float ScrollSpeed
    {
        get;
        set => field = Math.Max(value, 0);
    }
    /// <summary>
    /// Pixels at which the scroll updates
    /// </summary>
    [Export]
    public float ScrollStep
    {
        get;
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
        set => SetDialogText(value);
    }

    private double PauseTimer
    {
        get;
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
        set;
    }

    public event Action? FinishedWriting;

    public override bool _Set(StringName property, Variant value)
    {
        if (property == RichTextLabel.PropertyName.Text)
        {
            if (value.Obj is string text)
                SetDialogText(text);

            return true;
        }

        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Suspended)
            return;

        if (PauseTimer > 0)
            PauseTimer = Math.Max(0, PauseTimer - delta);
        else if (_isScrolling)
            ScrollProcess(delta);
        else if (Writing)
            Write(delta);
    }

    public void WriteNextPage() => WriteNext(false);

    public void WriteNextLine() => WriteNext(true);

    public bool IsComplete() => VisibleCharacters == -1 || VisibleCharacters == _totalCharacters;

    public void Resume() => Suspended = false;

    public void SetDialogText(string text)
    {
        _textEventIndex = 0;
        VisibleCharacters = 0;
        _textEvents.Clear();
        string textWithoutEvents = Dialog?.ParseEventsFromText(text, _textEvents) ?? text;
        base.Text = textWithoutEvents;
        DialogBase.AdjustEventIndices(textWithoutEvents, GetParsedText(), _textEvents);
        _totalCharacters = GetTotalCharacterCount();
        _scrollBar.Value = 0;
        _targetScrollValue = 0;
        _movingScrollValue = 0;
        _targetWriteRange = new(0, GetLastFittingCharacter(0));

        AutoProceedEnabled = Dialog?.AutoProceedGlobalEnabled ?? false;
        AutoProceedTimeout = Dialog?.AutoProceedGlobalTimeout ?? 0;
        SpeedMultiplier = Dialog?.SpeedMultiplier ?? 1;
    }

    public bool IsOnLastPage() => _scrollBar.Value >= _scrollBar.MaxValue - Size.Y;

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
        {
            FinishedWriting?.Invoke();
            return;
        }

        _writeCounter = 1;
        // Is this screen fully written?
        int lastFittingLine = GetLastFittingLine(_targetScrollValue);
        int lastFittingChar = GetLineRange(lastFittingLine).Y;

        if (currentChar < lastFittingChar)
        {
            int firstFittingLine = GetFirstFittingLine(_targetScrollValue);
            int firstChar = GetLineRange(firstFittingLine).X;
            _targetWriteRange = new(firstChar, lastFittingChar);
            _isWriting = true;
            return;
        }

        int nextLine;

        if (isLine)
            nextLine = GetNextFittingLine(lastFittingLine);
        else
            nextLine = lastFittingLine + 1;

        SetTargetScrollLine(nextLine);
        int charStart = GetLineRange(nextLine).X;
        int charEnd = GetLastFittingCharacter(_targetScrollValue);
        _targetWriteRange = new(charStart, charEnd);
        _isWriting = true;
    }

    private void SetTargetScrollLine(int line)
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

    private int GetFirstFittingLine(double startingOffset)
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

    private int GetLastFittingCharacter(double startingOffset)
    {
        int line = GetLastFittingLine(startingOffset);
        Vector2I range = GetLineRange(line);
        return range.Y;
    }

    /// <summary>
    /// Finds the last line that can fit on the screen.
    /// </summary>
    /// <param name="startingOffset">
    /// The vertical scroll offset from which to begin searching.
    /// </param>
    /// <returns></returns>
    private int GetLastFittingLine(double startingOffset)
    {
        int totalLines = GetLineCount();
        float controlHeight = Size.Y;
        float contentHeight = GetContentHeight();
        int lastLine = 0;
        float lineTop;
        float lineBottom = 0;

        for (int i = 0; i < totalLines; i++)
        {
            lineTop = lineBottom;
            lineBottom = (i + 1 < totalLines) ? GetLineOffset(i + 1) : contentHeight;

            if (lineTop <= startingOffset || lineBottom <= startingOffset + controlHeight)
                lastLine = i;

            if (lineTop > startingOffset + controlHeight)
                break;
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
    private int GetNextFittingLine(double startingOffset)
    {
        int lastVisibleLine = GetLastFittingLine(startingOffset);
        return GetNextFittingLine(lastVisibleLine);
    }

    /// <summary>
    /// Finds the first line index that, when scrolled to the top of the RichTextLabel,
    /// still leaves the following line fully visible in the viewport.
    /// </summary>
    /// <param name="lastFittingLine">
    /// The last visible line to start from.
    /// </param>
    /// <returns>
    /// The index of the line that can be aligned to the top
    /// while keeping its next line fully in view.
    /// </returns>
    private int GetNextFittingLine(int lastFittingLine)
    {
        int totalLines = GetLineCount();
        float controlHeight = Size.Y;

        if (lastFittingLine >= totalLines - 1)
            return totalLines - 1;

        int line = lastFittingLine + 1;
        int totalSize = GetLineHeight(line);

        if (totalSize >= controlHeight)
            return line;

        while (line > 0)
        {
            int prevLineHeight = GetLineHeight(line - 1);

            if (totalSize + prevLineHeight > controlHeight)
                break;

            totalSize += prevLineHeight;
            line--;

            if (totalSize == controlHeight)
                break;
        }

        return line;
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

        if (_movingScrollValue == _targetScrollValue)
        {
            _scrollBar.Value = _targetScrollValue;
            _isScrolling = false;
        }
        else
        {
            if (ScrollStep > 0)
                _scrollBar.Value = Math.Floor(_movingScrollValue / ScrollStep) * ScrollStep;
            else
                _scrollBar.Value = _movingScrollValue;
        }
    }

    private bool HandleTextEvent(int textIndex)
    {
        if (Dialog == null || _textEventIndex >= _textEvents.Count)
            return false;

        TextEvent textEvent = _textEvents[_textEventIndex];

        if (textIndex < textEvent.TextIndex)
            return false;

        if (textEvent.Tag.Span.StartsWith("await "))
            Suspended = true;

        _textEventIndex++;
        (EventType EventType, float Param1) result;
        int currentChar = VisibleCharacters == -1 ? _totalCharacters : VisibleCharacters;

        try
        {
            result = Dialog.HandleTextEvent(textEvent);
        }
        catch (Exception)
        {
            return true;
        }

        switch (result.EventType)
        {
            case EventType.Pause:
                float timeValue = result.Param1;
                PauseTimer += timeValue;
                break;
            case EventType.Speed:
                SpeedMultiplier = result.Param1;
                break;
            case EventType.Auto:
                float autoValue = result.Param1;
                AutoProceedEnabled = autoValue != -2;
                AutoProceedTimeout = autoValue;
                bool isComplete = currentChar == _totalCharacters;

                if (isComplete)
                    PauseTimer += AutoProceedTimeout;

                break;
            case EventType.Prompt:
                _targetWriteRange.Y = currentChar + 1;
                break;
            case EventType.Scroll:
                int currentLine = GetCharacterLine(currentChar);
                SetTargetScrollLine(currentLine);

                // Is this screen fully written?
                int lastFittingLine = GetLastFittingLine(_targetScrollValue);
                int lastFittingChar = GetLineRange(lastFittingLine).Y;

                if (currentChar != lastFittingChar)
                {
                    int firstFittingLine = GetFirstFittingLine(_targetScrollValue);
                    int firstChar = GetLineRange(firstFittingLine).X;
                    _targetWriteRange = new(firstChar, lastFittingChar);
                    _isWriting = true;
                }

                break;
        }

        return true;
    }

    public void Reset(bool clearText)
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
        //VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
        _scrollBar.AllowGreater = true;
        _scrollBar.Scale = Vector2.Zero;
        _scrollBar.Value = 0;
        _targetScrollValue = 0;
        _movingScrollValue = 0;
        _targetWriteRange = new(0, GetLastFittingCharacter(0));
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

        if (_isScrolling)
            ScrollProcess(delta);

        if (currentChar < _targetWriteRange.Y)
            return;

        _writeCounter = 0;

        if (AutoProceedEnabled)
            PauseTimer += AutoProceedTimeout;
    }
}
