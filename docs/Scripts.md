# Creating a Script

New dialog scripts are made using the `.dia` extension. The name of the file should be unique to the project.

## Section Title

Dialog files consist of one or more sections. To start a new section of dialog, write the title in PascalCase in between two pairs of dashes:
```gamedialog
--TestSection--
```

## Speakers and Lines

Each section is made up of one or more lines of dialog. Each line begins with the speaking character's ID, a colon, and the text you want them to say:
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

You can add comments using two forward slashes `//`. For multiline comments, use `/*` to open and `*/` to close:

```gamedialog
--Greeting--
// This is a comment.
Stalone: Good morning! // So is this.

/*
This is a
multi-line
comment
*/
```

## `goto`

By default, the reader starts at the first section in the file. If you have more than one section, it's important to connect them otherwise the dialog script will exit at the end of the current section. To connect them, type an open square bracket, the keyword `goto`, the name of the section you want to "go to", and a closing square bracket:

```gamedialog
--Greeting--
Stalone: Hello World!
[goto Goodbye]

--Goodbye--
Stalone: See you later!
```

## Choices

It's common in dialog for characters to ask questions and let the player answer using multiple choices. To do so, start a line with a `?` and a space after it, then write the choice that will be displayed. Any additional dialog should be indented below the choice.

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

You can store and reuse simple values (floats, strings, or bools) using variables. Define or update a variable inside square brackets anywhere in your dialog.

```gamedialog
--Color--
Stalone: What's your favorite color?
? Blue
    [color="blue"]
? Green
    [color="green"]
? Red
    [color="red"]
Twosen: My favorite color is [color]!;
Stalone: That's a nice pick!
```

Keep in mind, once you set a variable’s type, it cannot change.

```
[color="blue"]
[color="plaid"] // This works
[color=25] // This will display an error
```

## Conditional Branching

Variables will help us create branching dialog. Surround some dialog in an "if/else/elseif" condition to conditionally display it.

```gamedialog
WIP
```

We can use Choices and Variables together to conditionally display options to choose from. When the script reaches a choice branch the abstract method `OnChoice(List<Choice> choices)` is called. All of the options are provided to the method. If you have a choice wrapped around a conditional branch and if fails the check, it will have the `Disabled` property set to `true`. Let's see that previous example with a conditional choice!

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
if [age >= 21]
    ? Shirley Temple!
        Twosen: Can I see some ID?
Stalone: Ah, that hits the spot!
```

## Tags

This project supports the same BBCode tags Godot does, with some additional tags provided.

### `speed`

Using the `TextWriter` node, the text will display like a typewriter at a steady pace (30 chars per second by default). You can override this with a `speed` tag. The value is a multiplier of the default, so less than one is faster, and and greater than one is slower. Writing a closing tag resets the speed to the default.
```
Stalone: Hi, how've you been? [speed=0.4]...[/speed] Not much of a talker, huh?
```

Setting the speed to a value of 0 will make the text write as fast as it can. If you need it to change for the rest of the line, just omit the closing tag:
```
Stalone: One of my favorite things about apples is-[speed=0]OW! I JUST STEPPED ON A MOUSE TRAP!
```

### `pause`

The `pause` tag makes the `TextWriter` node stop for a specified amount of time in seconds (`[pause=2]`).

### `auto`

The `auto` tag can be used to have the `TextWriter` node auto-proceed at the end of a page or line after a short pause, determined by the length of the text displayed. The pause time can also be specified in seconds (`[auto=3]`). If used outside of a dialog line, it will be enabled until the tag is closed `[/auto]` or the dialog script finishes.

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

### `page`

The `page` tag works the same as the `prompt` tag, but will also scroll the current line to the top of the `TextWriter` display upon user input.
This is useful for long, multi-line text that needs broken up into distinct pages for effect.

### `await`

Sometimes, a dialog script needs to wait for something to happen before continuing. If you have a predefined method and it's of a `void` return type, you can tell the dialog to suspend execution until a later time by prepending it with the `await` keyword. To resume, call `Resume()` on your `DialogBase` object.

```
Stalone: Hey, look! A plane is flying overhead!
[await RunEvent("PlaneFlying")]
Stalone: Wouldn't it be great to be a pilot?
```

## Properties and Methods

WIP

