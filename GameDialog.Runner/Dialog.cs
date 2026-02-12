using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("GameDialog.Server")]

namespace GameDialog.Runner;

/// <summary>
/// Used to manage dialog scripts.
/// </summary>
public sealed partial class Dialog
{
    /// <summary>
    /// Constructs a new Dialog object.
    /// </summary>
    /// <param name="context">The Godot Node context</param>
    public Dialog(Node context)
    {
        Context = context;
        GlobalSpeedMultiplier = 1;
        DialogStorage = new(DialogBridge.Create(this));
        _dialogReader = new(this);
    }

    /// <summary>
    /// Updates the translation file type to check for when parsing dialog lines.
    /// </summary>
    public static TranslationFileType TranslationFileType { get; set; }

    /// <summary>
    /// The Godot Node this Dialog is used by.
    /// </summary>
    public Node Context { get; }
    /// <summary>
    /// Stores variables for the current dialog script.
    /// </summary>
    public DialogStorage DialogStorage { get; }
    /// <summary>
    /// The global speed multiplier value.
    /// Updated when a [speed] tag is used outside of a dialog line.
    /// </summary>
    public double GlobalSpeedMultiplier { get; internal set; }
    /// <summary>
    /// The global auto-proceed value.
    /// Updated when an [auto] tag is used outside of a dialog line.
    /// </summary>
    public bool GlobalAutoProceedEnabled { get; internal set; }
    /// <summary>
    /// The global auto-proceed timeout value.
    /// Updated when an [auto] tag is used outside of a dialog line.
    /// </summary>
    public float GlobalAutoProceedTimeout { get; internal set; }

    private readonly DialogReader _dialogReader;

    /// <summary>
    /// Occurs when the end of the script has been reached.
    /// </summary>
    public event Action<Dialog>? ScriptEnded;
    /// <summary>
    /// Occurs when the script reaches a dialog line.
    /// Used to pass the provided text to the text writer and handle the provided speaker IDs.
    /// </summary>
    public event Action<string, IReadOnlyList<string>>? DialogLineStarted;
    /// <summary>
    /// If an async method is called in the middle of a line of dialog,
    /// this will be invoked when it finishes.
    /// Used to notify the text writer to continue.
    /// </summary>
    public event Action? DialogLineResumed;
    /// <summary>
    /// Occurs when a set of choices is encountered.
    /// </summary>
    public event Action<IReadOnlyList<Choice>>? ChoiceRead;
    /// <summary>
    /// Provides key value pairs when a hash tag is encountered.
    /// If no value is defined, the value will be an empty string.
    /// </summary>
    public event Action<IReadOnlyDictionary<string, string>>? HashRead;

    /// <summary>
    /// Clears and resets the Dialog script.
    /// </summary>
    public void Clear() => _dialogReader.Clear();

    /// <summary>
    /// Loads a script from a path.
    /// </summary>
    /// <param name="path"></param>
    public void Load(string path) => _dialogReader.Load(path);

    /// <summary>
    /// Loads a script from a string.
    /// </summary>
    /// <param name="text">The text string.</param>
    public void LoadFromText(string text) => _dialogReader.LoadFromText(text);

    /// <summary>
    /// Loads a script from a path using System.IO
    /// Faster, but not able to run in exports.
    /// </summary>
    /// <param name="filePath">The filepath to read from</param>
    /// <param name="rootPath">The project's root path</param>
    internal void LoadFromFile(string filePath, string rootPath) => _dialogReader.LoadFromFile(filePath, rootPath);

    /// <summary>
    /// Loads a script from a single dialog string. Must contain the speaker.
    /// </summary>
    /// <param name="text">The single dialog string.</param>
    public void LoadSingleLine(string text) => _dialogReader.LoadSingleLine(text);

    /// <summary>
    /// Validates the loaded script for errors.
    /// </summary>
    /// <param name="errors">The error list to populate</param>
    /// <param name="chart">The chart StringBuilder to append to</param>
    /// <param name="sw">The StreamWriter object for generating translations</param>
    public void ValidateScript(List<Error> errors, StringBuilder? chart = null, StreamWriter? sw = null)
    {
        _dialogReader.ValidateScript(errors, chart, sw);
    }

    /// <summary>
    /// Begins a loaded dialog script.
    /// </summary>
    /// <param name="sectionId">Optional starting section id</param>
    public void Start(string sectionId = "") => _dialogReader.Start(sectionId);

    /// <summary>
    /// Resumes the dialog to the next line.
    /// </summary>
    public void Resume() => _dialogReader.Resume();

    /// <summary>
    /// Resumes the dialog.
    /// </summary>
    /// <param name="nextIndex">The next index to read.</param>
    public void Resume(int nextIndex) => _dialogReader.Resume(nextIndex);

    /// <summary>
    /// Should be called when a Dialog line has ended.
    /// </summary>
    public void EndDialogLine() => _dialogReader.EndDialogLine();

    /// <summary>
    /// Removes TextEvents from text and inserts them into the provided List.
    /// </summary>
    /// <param name="unparsedText">The text with events.</param>
    /// <param name="textEvents">The list of text events to fill.</param>
    /// <returns>The text with the events removed.</returns>
    public string ParseEventsFromText(string unparsedText, List<TextEvent> textEvents)
    {
        return _dialogReader.ParseEventsFromText(unparsedText, textEvents);
    }

    /// <summary>
    /// Adjusts TextEvent indices based on comparing the text before and after setting the RichTextLabel.
    /// An alternative to setting the RichTextLabel.Text twice.
    /// </summary>
    /// <param name="eventParsedText"></param>
    /// <param name="displayedText"></param>
    /// <param name="events"></param>
    public static void AdjustEventIndices(string eventParsedText, string displayedText, List<TextEvent> events)
    {
        DialogReader.AdjustEventIndices(eventParsedText, displayedText, events);
    }

    /// <summary>
    /// Attempts to get the EventType and any parameter for a built in text event.
    /// </summary>
    /// <param name="textEvent">The triggered text event</param>
    /// <param name="eventType">The resulting event type</param>
    /// <param name="parameter">The resulting parameter for the event</param>
    /// <returns>If true, the TextEvent is a built in event.</returns>
    public bool TryParseBuiltInEvent(TextEvent textEvent, out EventType eventType, out float parameter)
    {
        return _dialogReader.TryParseBuiltInEvent(textEvent, out eventType, out parameter);
    }

    /// <summary>
    /// Attepts to evaluate a string of text as an expression.
    /// </summary>
    /// <param name="text">The text to evaluate.</param>
    /// <returns>If true, the expression was evaluated successfully.</returns>
    public bool TryEvaluateExpression(ReadOnlyMemory<char> text)
    {
        return _dialogReader.TryEvaluateExpression(text);
    }

    internal void InvokeScriptEnded() => ScriptEnded?.Invoke(this);
    internal void InvokeDialogLineStarted(string text, IReadOnlyList<string> speakerIds)
    {
        DialogLineStarted?.Invoke(text, speakerIds);
    }
    internal void InvokeDialogLineResumed() => DialogLineResumed?.Invoke();
    internal void InvokeChoiceRead(IReadOnlyList<Choice> choices) => ChoiceRead?.Invoke(choices);
    internal void InvokeHashRead(IReadOnlyDictionary<string, string> hashValues)
    {
        HashRead?.Invoke(hashValues);
    }
}
