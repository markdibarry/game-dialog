# Creating a Script

New dialog scripts are made using the `.dia` extension.

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

Variables help create branching dialog. Surround some dialog in an "if/else/elseif" condition 
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

Choices and Variables can be used together to conditionally display options to choose from. When 
the script reaches a choice branch the event `ChoiceRead` is invoked. All of the options are 
provided to the subscribed method(s). If you have a choice wrapped around a conditional branch and if 
fails the check, it will have the `Disabled` property set to `true`.

Let's see that previous example with a conditional choice!

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

Using the `DialogTextLabel` node, the text will display like a typewriter at a steady pace (30 chars per 
second by default). You can override this with a `speed` tag. The value is a multiplier of the 
default, so less than one is faster, and and greater than one is slower. Writing a closing tag 
resets the speed to the default.

```
Stalone: Hi, how've you been? [speed=0.4]...[/speed] Not much of a talker, huh?
```

Setting the speed to a value of 0 makes the text write as fast as possible. If you need it to 
change for the rest of the line, just omit the closing tag:
```
Stalone: One of my favorite things about apples is-[speed=0]OW! I JUST STEPPED ON A MOUSE TRAP!
```

### `pause`

The `pause` tag makes the `DialogTextLabel` node stop for a specified amount of time in seconds 
(`[pause=2]`).

### `auto`

The `auto` tag can be used to have the `DialogTextLabel` node auto-proceed at the end of a page or 
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

By default, the reader starts at the first section in the file. If you have more than one section, 
it's important to connect them otherwise the dialog script will exit at the end of the current 
section. To connect them, type an open square bracket, the keyword `goto`, the name of the section 
you want to "go to", and a closing square bracket:

```gamedialog
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

The `scroll` tag will scroll the current line to the top of the `DialogTextLabel` display.

### `page`

The `page` tag is a combination of the `prompt` tag, a linebreak, and `scroll` tag all in one!
It stops the text at the tag's position, then will move to a new line and scroll the current line 
to the top of the `DialogTextLabel` display upon user input. This is useful for long, multi-line 
text that needs broken up into distinct pages for effect.

## Properties and Methods

### Adding members

If you try to use a method or property name in your script that you haven't pre-defined, the 
extension will show an error. To define a method or property for use in your scripts, you'll need 
to make a partial class with the `[DialogBridge]` attribute. The source generator will scan the 
workspace for any matching class, and then grab all compatible methods and properties inside, 
generating logic so that they can be referenced at runtime.

All methods and properties must be public and unique across all classes the `DialogBridge` 
attribute. Compatible types for properties and method parameters include `string`, `bool`, and 
`float`. Method return types include `string`, `bool`, `float`, `void`, `Task`, and `ValueTask`.

> For information on async methods, see [await](#await)

### Using members

If you've added a compatible method or property, you can use them just like tags by surrounding them 
in square brackets.

```
[PlaySound("applause")]
Stalone: Welcome to the show! What a great crowd!
```

Methods and properties can also be used in lines of dialog. If the property's type or method's 
return type is `string` or `float` it'll be appended to your dialog line, otherwise it'll just be 
evaluated and discarded.

The result of a method can also be assigned to a variable for later use.

```
[favoriteFood = GetFavoriteFood()]
Stalone: My favorite food is [favoriteFood]!
```

### Passing members

If you have some data you want available inside your script, but don't want to define it as a 
global member, you can pass it in by setting the value on the dialog's `DialogStorage` object.

```cs
dialog.DialogStorage.SetValue("songName", "Bulls On Parade");
dialog.Start();
```

Then in your dialog script, declare it as a passed-in value by providing the type and the variable 
name:

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
is of a `Task` or `ValueTask` return type, you can tell the dialog to suspend execution until a 
later time by prepending it with the `await` keyword.

```
Stalone: Hey, what's that over there?
[await WalkTowards("SuspiciousCabinet")]
Stalone: It's a clue!
```