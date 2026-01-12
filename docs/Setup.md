# Setup

## VSCode Extension

The extension is available in the github repo under "Actions", clicking the most recent workflow 
run, and downloading the .zip files under "Artifacts". There is one containing the addon, and 
another containing the vscode/vscodium extension. To install, navigate to the directory the .vsix 
file is in and run:

```
code --install-extension (filename).vsix

// or

codium --install-extension (filename).vsix
```

The extension provides syntax highlighting for `.dia` files and a context menu to export all 
strings as a CSV file.

## Addon

While this is a WIP, to use the dialog system in your project, download the addon and add it 
somewhere in your project.

In order to use this system, you need to create your own custom class inheriting from the supplied 
`GameDialog.Runner.DialogBase` class. You may notice that there is no built in dialog box or choice 
menu display. Dialog systems tend to be just as bespoke as menu systems, so it's intentionally not 
implemented to allow the user more freedom in how this system is used. However, A custom 
`RichTextLabel` node (`TextWriter`) is provided to support some of the built-in tags. A current 
goal of this project is to make it easier to plug into in the future.

With a custom class in place you can run a script with the following code:

```csharp
Dialog dialog = new Dialog();
dialog.Load("HelloWorld.dia");
dialog.Start();
```

> For more on creating a script, see the [Creating a Script](./Scripts.md) section.

You can also run a single-line dialog via the following:

```csharp
Dialog dialog = new Dialog();
dialog.LoadSingleLine("Hello world! I'm writing dialog.");
dialog.Start();
```

By default, you won't see anything happen, of course. When a dialog line is served, you need to 
decide what to do with it! For this, there are a few methods to override in your custom dialog 
class allowing you to hook into certain parts of the system.

## Example Project

I've added an example project in the repo. Just copy the "example-project" folder and add the addon 
to the `Addons` folder.