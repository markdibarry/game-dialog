# Game Dialog

A free game dialog language I'm making for my personal game framework.

## Section Title

Dialog files use the `.dia` extension and consist of one or more sections. To start a new section of dialog, write the title in PascalCase in between two pairs of dashes:
```gamedialog
--TestSection--
```

## Speakers and Lines
Each section is made up of one or more lines of dialog. Each line begins with the speaking character's ID, a colon, and the text you want them to say:
```gamedialog
--Greeting--
Stalone: Hello World!
```

Internally, the dialog system will call `GetName(string id)` to get the speaker's name for display. If none is defined, it will display the ID.

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

## goto
By default, the reader starts at the first section in the file. If you have more than one section, it's important to connect them otherwise the dialog will exit at the end of the current section. To connect them, type an open square bracket, the keyword `goto`, the neme of the section you want to "go to", and a closing square bracket:

```gamedialog
--Greeting--
Stalone: Hello World!
[goto Goodbye]

--Goodbye--
Stalone: See you later!
```

## Choices
It's common for dialog for characters to ask questions and let the player answer using multiple choices. To do so, start a line with a `?` and a space after it.

```gamedialog
--Color--
Stalone: What's your favorite color?
? Blue
? Green
? Red
```

## Variables
You can store and reuse simple values (floats, strings, or bools) using variables. Define or update a variable inside square brackets anywhere in your dialog. Keep in mind, once you set a variable’s type, it cannot change.

WIP