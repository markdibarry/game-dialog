# Creating a Script

New dialog scripts are made using the `.dia` extension. The name of the file should be unique to 
the project.

## Section Title

Dialog files consist of one or more sections. To start a new section of dialog, write the title in 
PascalCase in between two pairs of dashes:
```gamedialog
--TestSection--
```

## Speakers and Lines

Each section is made up of one or more lines of dialog. Each line begins with the speaking 
character's ID, a colon, and the text you want them to say:
```gamedialog
--Greeting--
Stalone: Hello World!
```

Add multiple speakers by separating names with a comma:
```gamedialog
--PizzaFans--
Stalone: Who likes pizza?
Twosen, Threena: Me!
```

For longer dialog lines you can wrap your text in `^^` and indent from the second line on:
```gamedialog
--MoodyExposition--
Stalone: ^^It all started on a Fall night a couple 
    years back. There was a chill in the air and 
    everyone seemed tense.^^
```

## Comments

You can add comments using two forward slashes `//`:

```gamedialog
--Greeting--
// This is a comment.
Stalone: Good morning! // So is this.
```

## `goto`

By default, the reader starts at the first section in the file. If you have more than one section, 
it's important to connect them otherwise the dialog script will exit at the end of the current 
section. To connect them, type an open square bracket, the keyword `goto`, the name of the section 
you want to "go to", and a closing square bracket:

```gamedialog
--Greeting--
Stalone: Hello World!
[goto Goodbye]

--Goodbye--
Stalone: See you later!
```

## Choices

It's common in dialog for characters to ask questions and let the player answer using multiple 
choices. To do so, start a line with a `?` and a space after it, then write the choice that will be 
displayed. Any additional dialog should be indented below the choice.

```gamedialog
--Drinks--
Stalone: I'm thirsty!
Twosen: What'll you have?
? Something strong.
    Twosen: Careful, this soda is pretty fizzy!
? Something interesting.
    Twosen: This is called a Capri-Sun!
? Milk, please!
    Twosen: Is 2% okay?
Stalone: Ah, that hits the spot!
```

## Variables

You can store and reuse simple values (floats, strings, or bools) using variables. Define or update 
a variable inside square brackets anywhere in your dialog.

```gamedialog
--Color--
Stalone: What's your favorite color?
? Blue
    [myColor="blue"]
? Green
    [myColor="green"]
? Red
    [myColor="red"]
Twosen: My favorite color is [myColor]!;
Stalone: That's a nice pick!
```

Keep in mind, once you set a variable’s type, it cannot change.

```
[myColor="blue"]
[myColor="plaid"] // This works
[myColor=25] // This will display an error
```

## Conditional Branching

Variables will help us create branching dialog. Surround some dialog in an "if/else/elseif" condition 
to conditionally display it.

```gamedialog
[timesTalked = GetTimesTalked("Threena")]
if [timesTalked == 1]
    Threena: Hello there! You need help finding your way? Turn left to get to the town square!
else if [timesTalked == 2]
    Threena: Did you not hear me? Turn left around that building!
else if [timesTalked == 3]
    Threena: Is there something on my face? GO. LEFT.
else
    Threena: I've called the guards.
```

> For more info on using methods see [Properties and Methods](#properties-and-methods)

We can use Choices and Variables together to conditionally display options to choose from. When the 
script reaches a choice branch the abstract method `OnChoice(List<Choice> choices)` is called. All 
of the options are provided to the method. If you have a choice wrapped around a conditional branch 
and if fails the check, it will have the `Disabled` property set to `true`. Let's see that previous 
example with a conditional choice!

```gamedialog
--Drinks--
[age = 15]
Stalone: I'm thirsty!
Twosen: What'll you have?
? Something strong.
    Twosen: Careful, this soda is pretty fizzy!
? Something interesting.
    Twosen: This is called a Capri-Sun!
? Milk, please!
    Twosen: Is 2% okay?
? if [age >= 21] Shirley Temple!
    Twosen: Can I see some ID?
Stalone: Ah, that hits the spot!
```

## Tags

This project supports the same BBCode tags Godot does, with some additional tags provided.

### `speed`

Using the `TextWriter` node, the text will display like a typewriter at a steady pace (30 chars per 
second by default). You can override this with a `speed` tag. The value is a multiplier of the 
default, so less than one is faster, and and greater than one is slower. Writing a closing tag 
resets the speed to the default.

```
Stalone: Hi, how've you been? [speed=0.4]...[/speed] Not much of a talker, huh?
```

Setting the speed to a value of 0 will make the text write as fast as it can. If you need it to 
change for the rest of the line, just omit the closing tag:
```
Stalone: One of my favorite things about apples is-[speed=0]OW! I JUST STEPPED ON A MOUSE TRAP!
```

### `pause`

The `pause` tag makes the `TextWriter` node stop for a specified amount of time in seconds 
(`[pause=2]`).

### `auto`

The `auto` tag can be used to have the `TextWriter` node auto-proceed at the end of a page or 
line after a short pause, determined by the length of the text displayed. The pause time can also 
be specified in seconds (`[auto=3]`). If used outside of a dialog line, it will be enabled until 
the tag is closed `[/auto]` or the dialog script finishes.

### `end`

The `end` tag will automatically end the script.

```
--Greeting--
Stalone: Hello! Do you want to chat?
? No.
    Stalone: Oh, ok!
    [end]
? Yes.
Stalone: Great! What's your favorite breakfast food?
...
```

### `goto`

The `goto` tag will make the dialog script "go to" a specified section.

```
--Greeting--
Stalone: What's your favorite breakfast food?
? Waffles.
    [goto Waffles]
? Pancakes.
    [goto Pancakes]

--Waffles--
Stalone: Waffles are my favorite too!
[end]

--Pancakes--
Stalone: Pancakes are good, but have you tried waffles?
```

### `prompt`

The `prompt` tag will stop the text at this position and wait for the user's input.

### `scroll`

The `scroll` tag works the same as the `prompt` tag, but will also scroll the current line to the top 
of the `TextWriter` display upon user input. This is useful for long, multi-line text that needs 
broken up into distinct pages for effect.

## Properties and Methods

### Adding members

By default, if you try to use a method or property name in your script that you haven't 
pre-defined, the extension will show an error. To define a method or property for use in your 
scripts, you'll need to make a partial class named `DialogBridge` inheriting from `DialogBridgeBase`.
The extension will scan your workspace for a file named `DialogBridge.cs`, and then grab all 
compatible methods and properties inside and create a file ending in `DialogBridge.g.cs`.
The runtime will use this file to handle your methods and properties.

Compatible types for properties and method parameters include `string`, `bool`, and `float`. Method 
return types include `string`, `bool`, `float`, `void`, `Task`, and `ValueTask`.

> For information on async methods, see [await](#await)

### Using members

If you've added a compatible method or property, you can use them just like tags by surrounding them 
in square brackets.

```
[PlaySound("applause")]
Stalone: Welcome to the show! What a great crowd!
```

If the method returns a `string` or `float` it'll be appended to your dialog line. You can also 
assign the result of a method to a variable for use later.

```
[favoriteFood = GetFavoriteFood()]
Stalone: My favorite food is [favoriteFood]!
```

### Passing members

If you have some data you want available inside your script, but don't want to define it as a 
global member in the `DialogBridge.cs`, you can pass it in by setting the value on the dialog's
`TextStorage` property.

```cs
dialog.TextStorage.SetValue("songName", "Bulls On Parade");
dialog.StartScript();
```

Then in your dialog, declare it as a passed-in value by providing the type and the variable name:

```
--CoolMusic--
[@string songName]
Twosen: What're you listening to?
Stalone: My favorite song, [songName]!
```

Supported types are `@string`, `@float` and `@bool`. Remember, you need to pass them in for the 
script to use them!

### `await`

Sometimes, a dialog script needs to wait for something to happen before continuing. If your method 
is of a `Task` or `ValueTask` return type, you can tell the dialog to suspend execution until a later time 
by prepending it with the `await` keyword.

```
Stalone: Hey, what's that over there?
[await WalkTowards("SuspiciousDresser")]
Stalone: It's a clue!
```