# Setup

The GameDialog nuget package is available here. The VSCode/VSCodium extension is available here.

## Example Project

Included in this repo is an example project. Just copy the "example-project" folder and run it in 
your Godot editor.

## VSCode Extension

This dialog system comes with a VSCode extension. The extension provides validation and syntax 
highlighting for .dia files. It also provides a context menu for .dia files to generate .csv and/or 
.pot files for translations. To install, navigate to the directory the .vsix file is in and run:

```
code --install-extension GameDialog_Extension.vsix

// or

codium --install-extension GameDialog_Extension.vsix
```

If custom members are changed in your session, right click on any .dia file and select 
`Update Members` for the extension to pick up the changes.

## Nuget package

Once you have the nuget package installed, you'll have access to the GameDialog class library and 
source generator. You may notice that there is no built in dialog box or choice menu display. 
Dialog systems tend to be just as bespoke as menu systems, so it's intentionally not 
implemented to allow the user more freedom in how this system is used. However, A custom 
`RichTextLabel` node (`DialogTextLabel`) is provided to support some of the built-in tags.

### Starting a dialog

For the included example project, a `DialogBox` node was made that creates a new `Dialog` instance 
in its `_Ready()` method. You'll notice there are events to subscribe to that define what you want to 
happen at certain points in a script. The events are:

* `DialogLineStarted`
* `DialogLineResumed`
* `ChoiceRead`
* `HashRead`
* `ScriptEnded`

These can be read about in depth in the [api section](./api/GameDialog.Runner.md).

### Displaying the dialog

In the example project's `DialogBox` scene, a `DialogTextLabel` displays the text. You can use 
`Dialog.SetTextAndFillEvents()` to set the text parse the text events and 
`Dialog.HandleTextEvent()` to handle text events when they're encountered. Making a text writer can 
be difficult, however, so a custom one (`DialogTextLabel`) is provided. Godot doesn't allow using custom nodes from other C# libraries, but if you make a class with a `[DialogTextLabel]` attribute, 
you should get a warning with a code fix to automatically generate one for your own use.

> For more on creating a script, see the [Creating a Script](./Scripts.md) section.