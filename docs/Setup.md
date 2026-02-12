# Setup

## Nuget package

The GameDialog nuget package is [available here](https://www.nuget.org/packages/GameDialog/), 
by looking in the normal nuget.org search, or downloading from the GitHub release.

Once you have the nuget package installed, you'll have access to the GameDialog class library and 
source generator. You may notice that there is no built in dialog box or choice menu display. 
Dialog systems tend to be just as bespoke as menu systems, so it's intentionally not 
implemented to allow the user more freedom in how this system is used. However, A custom 
`RichTextLabel` node (`DialogTextLabel`) is provided to support some of the built-in tags.

## VSCode Extension

This dialog system comes with a VSCode extension. The extension provides validation and syntax 
highlighting for .dia files. It also provides a context menu for .dia files to generate .csv and/or 
.pot files for translations. If custom members are changed in your session, right click on any .dia file and select 
`Update Members` for the extension to pick up the changes.

### How to install

The VSCode/VSCodium extension is available in the GitHub release (GameDialog_Extension.vsix). To 
install, navigate to the directory the .vsix file is in and run:

```
code --install-extension GameDialog_Extension.vsix

// or

codium --install-extension GameDialog_Extension.vsix
```

### Why do I have to download it?

In order to publish a Nuget package, you must have a Microsoft account.

In order to publish a VS Code extension, you must:
* Have an MS account
* Have an Azure account
* Maintain an Azure subscription
* Create and maintain an organization
* Create and maintain a publisher

And you must do the same with different orgs for VSCodium. The alternative is I just provide it 
here, you download it and install via the above.

## Example Project

Included in this repo is an example project. Just copy the "example-project" folder and run it in 
your Godot editor.

### Starting a dialog

For the included example project, a `DialogBox` node was made that creates a new `Dialog` instance 
in its `_Ready()` method. There are events to subscribe to that define what you want to happen at 
certain points in a script. The events are:

* `DialogLineStarted`
* `DialogLineResumed`
* `ChoiceRead`
* `HashRead`
* `ScriptEnded`

These can be read about in depth in the [API section](./api/GameDialog.Runner.Dialog.md#events).

### Displaying the dialog

In the example project's `DialogBox` scene, a `DialogTextLabel` displays the text. In its. Making a 
text writer can be difficult, however, so a custom one (`DialogTextLabel`) is provided. Godot 
doesn't allow using custom nodes from other C# libraries, but if you make a class with a 
`[DialogTextLabel]` attribute, you should get a warning with a code fix to automatically generate 
one for your own use. Alternatively, just copy the 
[DialogTextLabel.cs](../GameDialog.Runner/DialogTextLabel.cs) file into your project.

>For info on the methods/properties/events provided in the example writer, check the 
[API](./api/GameDialog.Runner.DialogTextLabel.md).

> For more on creating a script, see the [Creating a Script](./Scripts.md) section.