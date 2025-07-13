using System;
using System.Text;

namespace GameDialog.Runner;

public interface ITextEventHandler
{
    TextEvent ParseTextEvent(ReadOnlySpan<char> tagContent, int renderedIndex, StringBuilder sb);
    void HandleTextEvent(TextEvent textEvent);
}