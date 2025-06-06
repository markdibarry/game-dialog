# Game Dialog

A free game dialog language I'm making to work with my personal game framework.

# Keywords

Each `.dia` file is made up of one or more sections. To start a new section of dialog, write the title in PascalCase in between pairs of dashes:
```
--TestSection--
```

## Speakers
Each section is made up of one or more lines of dialog. To define one, type the speaking character's name with a colon, and the text they'll say after. Here's what an example of a basic section:
```
--Greeting--
Stalone: Hello World!
```

The first section defined will always be the first used. If you have two sections, it's important to connect them or the dialog will exit at the end of the current section.

```
--Greeting--
Stalone: Hello Wrold!
[goto Goodbye]

--Goodbye--
Stalone: See you later!
```

## Comments
You can add comments using two forward slashes `//`. Anything after

## Speed
You can change the speed of the text being written by using the `speed` keyword:
```
Twosen: I'm talking normally...[speed = 3] Now I'm talking much faster!
```
It's defined as a multiplier, so less than 1 will make it slower, and greater than 1 will make it faster.

If speed is set during a line of dialog, the speed will only be changed for that line. If you want to change the speed globally for all text in the script, call as a separate expression:
```
--SpeedTest--
[speed = 0.5]
Twosen: I am talking at half speed... [speed = 3] Now I'm talking faster!
Threena: But I'm still talking at half speed.
[speed = 1]
Threena: Now I'm back to normal!
```

## Pause
Pause is only valid in a dialog line. It sets a pause for the amount of time specified in seconds:
```
Stalone: I'm about to pause for dramatic effect...[pause = 3] How was that?
```

## Multiple Lines
If you have a long line of dialog, but don't want it all on the same line, surround it in a pair of two carats:
```
Threena: ^^I have a lot to say but I don't want to do it all on one line. 
    Feels like a waste, you know?^^
```

## Variables and Functions

To store a variable and use it again later, define it within square brackets like so:
```
// Valid
[myVariable = 5]

// Also valid
[myOtherVariable="tuna"]
```

Once defined, a variable can be reassigned, but only to the same type:
```
// Valid
[myVariable = "happy"]
[myVariable = "sad"]

// Not valid
[myVariable = "happy"]
[myVariable = 7]
```

## Persistant state
These variables, of course, won't persist outside of the script. If you'd like to assign and pull from outside state, however, it's easy to get set up.
When making a new project, you should add a new file/class called `DialogBridge.cs` somewhere in your project. Here you can place properties and methods to reference in your dialog. In order to register it, when the game starts, you should put the following logic: 
```
DialogBridge dialogBridge = new();
DialogBridgeRegister.SetDialogBridge(dialogBridge);
```

With this done, you can, for example, write some custom logic to get your party's current gold amount:
```
// DialogBridge.cs

public float Gold => MainParty.Gold;

// Test.dia

Stalone: I have [Gold] pieces of gold to spend!
```
The dialog language parser will automatically pick up properties and methods defined in the `DialogBridge.cs` and will show an error if it's spelled wrong or not defined.


The dialog language has a built in method for accessing a character's name `GetName()`, which takes one parameter: the ID of the actor whos name you're trying to access. By default, it will display the ID as written. In the `DialogBridge.cs` file, you can override the method and have it return a name based on your game's logic.


# Internals
## Variables



## Functions
Functions are passed in the following format:

0. The Opcode Id
1. The function name's string index
2. The number of arguments
3. The list of expressions for each argument...

Following this pattern, `GetName("Stalone")` would be sent as `[5, 2, 1, 2, 3]`

* 5 - Function OpCode
* 2 - "GetName" has a string index of 2
* 1 - Argument length of 1
* 2 - Type of the first argument is a string (Opcode 2)
* 3 - "Stalone" has a string index of 3