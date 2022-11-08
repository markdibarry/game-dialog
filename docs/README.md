# Game Dialog

A free game dialog language I'm making to work with my personal game framework.

## Concepts

### Sections

Each dialog script is separated into sections. To have a valid dialog script, you need at least one section. 

A section is declared using the following syntax:
```
--MyConversation--
```
You can add as many sections as you like, but the first section's name **must not contain spaces**, **match the file's name**, and be **unique to the game project**. All subsequent sections only need to be unique to the current script.

### Speakers

One of the biggest parts of this system involves Speakers: characters that are speaking in the dialog.

Each speaker has an **identifier**, **display name**, **portrait**, and **mood**. This way, when a character
is speaking, you have full customizability of the display while still referring to the same character.

#### Identifier

To write a dialog line, you start by referencing a speaker's identifier. If nothing else is set, it will try to infer as much as it can from this ID. As an example, if you wrote the following dialog line:
```
Bartender: Rainy out this evening!
```
The system will look in the persistent storage for an actor matching the ID "Bartender" for a defined custom name. If, after the lookup, it finds "Denny", it will set the Speaker's display name as "Denny" for this dialog session. If it finds nothing, it will simply display "Bartender".

To access different customizable properties for a Speaker, you can reference it using the following syntax:

```
ID_Property
```

#### Display Name

Just because the system may set the Speaker's name automatically, doesn't mean it can't be changed. You may want to reveal a character's name within a conversation. You can override the Speaker's display name for the session in between dialog lines, or even while speaking!

Here's an example of updating the name after speaking:
```
Bartender: You can call me Denny! I'm the bartender here.
Bartender_Name = "Denny"
```
After the name is set, any lines referencing the ID "Bartender" in the same dialog session will display the name "Denny".

To make changes in the middle of the Speaker's text, simply surround the call in brackets. Here's the same example but updating the name while speaking:
```
Bartender:  You can call me Denny! [Bartender_Name = "Denny"] I'm the bartender here.
```
This will update the display name while the character is still speaking!

#### Portrait

Similar to the Display Name, the system will attempt to infer which portrait to display from the Speaker's ID. For this to work properly, the system requires the following folder structure:
```
ID
|--mood.file
|--etc.
```
*For information on moods, see the [Mood section](#mood).*

Using the previous example:
```
Bartender
|--neutral.png
|--etc.
```
For the previous examples, the system would look in the portraits folder for "Bartender" and if found, will display the default "neutral" mood portrait. If no folder and file is found, no portrait will be displayed. 

For a character reveal, you may want to hide the Speaker's portrait, or show a cloaked figure until they choose to make themselves known. The following example shows a dialog script that depicts just such a situation:

```
--BigReveal--

Bartender_Name = "???"
Bartender_Portrait = "MysteriousFigure"
Bartender: I don't know how you found me... I was trying to remain hidden. It was me all along!

Bartender_Name = "Denny"
Bartender_Portrait = "Denny"
Bartender: Would you like to see a menu?
```

You could even override the bartender's portrait with the end boss's (if you wanted)!

#### Mood

In conjunction with Portraits, dialog is most expressive when Speakers can show different emotions while talking. To specify alternate portraits, you'll need to add other named moods to the portrait's folder.

```
Bartender
|--neutral.png
|--happy.png
|--annoyed.png
|--etc.
```

Here's a dialog example where the bartender gets some good news:

```
Bartender: It was a rough walk to work today. [Bartender_Mood="happy"] But my customers always cheer me up!
```

### Choices

Dialog can be much more immersive when conversation branches. These splits are called **Choices**.

To add some choices, start a line with a `?`:

```
Patron: I'm thirsty!
Bartender: What'll you have?
    ? Something strong.
    ? Something interesting.
    ? Milk, please!
Bartender: Right away!
```

Of course, choices aren't very interesting if it doesn't have any impact on the conversation. You can use the `next` tag to tell it to go to a different section based on your selection:

*For more information on tags, see the [BBCode section](#bbcode)*

```
Patron: I'm thirsty!
Bartender: What'll you have?
    ? Something strong. [next=Strong]
    ? Something interesting. [next=Interesting]
    ? Milk, please! [next=Milk]
    
--Strong--
Bartender: Careful, this soda is pretty fizzy! [next=End]

--Interesting--
Bartender: Wild-card, eh? This is called a Capri-Sun! [next=End]

--Milk--
Bartender: 2% okay? [next=End]

--End--
Bartender: Have a great day!
```

This can come in handy for bigger conversations, where you need to organize the branches into separate sections, but for short interactions like this, you can just nest the lines directly in the choice! When the lines for that choice finishes, it'll move on to the next main line. Lets refactor a bit:

```
Patron: I'm thirsty!
Bartender: What'll you have?
    ? Something strong.
        Bartender: Careful, this soda is pretty fizzy!
    ? Something interesting.
        Bartender: Wild-card, eh? This is called a Capri-Sun!
    ? Milk, please!
        Bartender: 2% okay?
Bartender: Have a great day!
```

You can even nest choices in the nested lines and the indentation is optional, but what is more readable is up to you:

```
Patron: I'm thirsty!
Bartender: What'll you have?
    ? Something strong.
        Bartender: Careful, this soda is pretty fizzy!
    ? Something interesting.
        Bartender: Wild-card, eh? This is called a Capri-Sun!
    ? Milk, please!
        Bartender: 2% or whole?
            ? 2%
                Patron: I had a big lunch, better make it 2%.
                Bartender: I'm trying to watch my weight too.
            ? Whole
                Bartender: You're right! Life is short!
Bartender: Have a great day!
```

### BBCode

This project supports BBCode by default, with some additional tags provided. Here's an example using the built-in "color" tag to change the text's color:

```
Bartender: My favorite food is [color=green]broccoli[/color]!
```

#### New Line

By default, the text will wrap, and will pause for user input when the text overflows to another page. Sometimes, you'll want to intentionally make some text appear on a new line. The `nl` tag will tell the proceeding text to appear on the next line.
```
Bartender: Hi![nl]How's the weather?
```

#### Speed

The text will display like a typewriter at a steady pace by default. Speed is defined as how long in seconds between each character typed, however, you may want to override this value. If you want it to only change for a short part, you can set it like so:
```
Bartender: Welcome to my bar! [speed=0.4]...[/speed] Not much of a talker, huh?
```
Setting the speed to a value of 0 will make the text write as fast as it can. If you need it to change for the rest of the line, just omit the closing tag:
```
Bartender: I've lived here for about 20 ye-[speed=0]OW! I JUST STEPPED ON A MOUSE TRAP!
```

#### Next

A script can have multiple sections. By default, the lines will be read in order, but you can skip to a different section by writing a `next` tag:
```
--Main--
Bartender: I'm going to the store to get some fruit. [next=Store]

--Driving--
Bartender: (listening to the radio) I love this song.

--Store--
Bartender: Chilly in here.
```

#### End

The `end` tag will end the conversation after the current line.

#### Auto Proceed

When a line finishes displaying it'll wait for user prompt. If you want to bypass this, you can use the `auto` tag:

```
Bartender: People say I talk a lot...
Patron: I don't think that's tr-[auto]
Bartender: They also say I interrupt people. 
```

### Variables

One of the core needs of a good dialog system is being able to store and retrieve data to display. This usually falls into two scopes, persistent and temporary storage. Persistent refers to data that persists outside the current dialog session, and temporary is data just within the session.

#### Temporary

By default, a temporary lookup is created every time a dialog session is started, and is cleared along with the session when it ends.

You can create and set a variable using the following syntax:

```
myNumVariable = 5.1
myStrVariable = "Stuff"
myboolVariable = true
```

As you may have noticed above, this system supports three data types: **float**, **boolean**, and **string**. Once you have set a variable, you can only reassign it to a value of the same type.

You're able to use variables to display a value in the dialog line like so:

```
--Hobbies--

hobby = "boating"
Bartender: I'm really into [hobby]!
```

If you try to access a variable that doesn't exist, the default value for the expected type will be provided. In the previous example, if `hobby` was never defined, but the variable was still attempted to be accessed, the line would be displayed as `I'm really into !`, so be careful!

#### Persistent

Depending on the scenario, you may want to access and store data that persists through a game session, or written to save data. The method of accessing this data is the same as temporary variables, however, the system checks to see if the variable key exists in persistent storage first, and only uses the temporary storage as a fallback. Persistent data is a little more delicate, and can have more far-reaching effects, so it's up to the user to determine the logic for how this is handled.

As an example, say you want to set the player's gold count. You would set up the peristent storage to accept a key like `gold` and handle it in a custom way. Then you'd set it like so:

```
Bartender: Thank you for the tip!
gold += 20
Bartender: Now I have [gold] total.
```

You may also have variables that should be read-only. If you wanted to get the game's current time, you would architect your persistent lookup to retrieve the value via a custom variable key like `game_time_hrs`, while ignoring any attempt to set it.

```
game_time_hrs = "hamburger"
Patron: I've been waiting here for [game_time_hrs] hours!
```

This will still render the hours as stored in the persistent storage. The system will not fail if attempting to set read-only variables, and will simply ignore it, so please keep this in mind.

### Conditionals

Variables can make these conversations even more interesting by using them to drive what choices and lines you encounter. Perhaps you only want the player to see a choice if a specific condition is met:

```
Bartender: I can only serve you drinks if you have a library card.
    if has_library_card
    ? I'm a long-time patron.
        Bartender: OH. Excuse me! How embarrassing for me.
    ? Let me speak to your manager.
        Bartender: I AM the manager! [end]
Bartender: What'll you have?
\\ ...
```

Here's an example controlling the lines that are seen based on a variable:

```
Bartender: Haven't I seen you somewhere before?
if times_visited > 2
    Patron: How do you NOT remember me??
    Bartender: Oh, geeze. My bad!
else if times_visited > 0
    Patron: I've been in once or twice.
else
    Patron: Never seen you in my life.
Bartender: Well, happy to have you either way.
```

Similar to choices, the indentation isn't strict. It's up to you to decide what is more readable.

### Seen

TODO