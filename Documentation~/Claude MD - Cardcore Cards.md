## Glossary

**Currency:** These are used in card and board games to hold state. They come as a pair, an amount and an integer. When validating format, give a warning if they are not paired. Often currency types are communicated to the player as only icons.  
**Ruleset**: A specific instance of card and board game rules using Cardcore. Ideally headless, but can be written into the game engine as we are learning how to generalize board game construction.   
**Card**: A digital representation of a physical card. To be clear, we will use mechanics that are impossible to replicate in the physical world. But in general, a card is very much some data and pictures printed on some paper that can be replicated easily

## 

## Cardcore cards

* These cards are stored in JSON  
* They are the data that is used both by the Cardcore ruleset and by the client game to display the cards.   
* They are the single source of truth  
* Simple cards are just data  
* Complicated cards will have a script file to accompany them  
* Cards can be modified during gameplay. This changes the instance, not the definition

## Cardcore Markdown format:

| \* | indicates any string, including empty strings. Stops at any white space or other markdown character |
| :---- | :---- |
| some\_string | An identifier. Make sure they are lower case and unbroken by whitespace. Underscores are used to show whitespace.  |
| \[some\_string\] | strings in brackets are identifiers for icons. When we build a card in the engine of our choice, it needs to know how to look up the icon and replace it. They are unique identifiers |
| \#some\_string | This indicates a key word, it should match to  |
| some\_string:some\_other\_string | This indicates a list of identifiers. Example: in the types field, you can have many types.  |
| \#some\_string | Keyword. This is a player facing rule that is understood across cards.  |
| $ {some\_string\] | C\# style variable replacement, example:  $"Hello, {name}\!" |

### Types

This is a list of types that the card contains. These might be related to each other or not, only a ruleset needs to care about that. Cardcore only cares that they are a list of string variables

### Reward 1 \#

This is the amount of a currency to add to the player’s state

### Reward 1 Type

This is the type of currency to reward. 

### Cost 1 \#

This currency is deducted from the player state when played. Ruleset determines whether or not the card is playable without the cost and how to handle that

### Cost 1 Type

Type of currency to charge

### Threshold 1 \#

Thresholds a catch all for attributes that aren’t quite currencies or types. For example, if a sword requires a strength of 3 to wield, it would have a threshold value of 3 \[str\]  
Rulesets have a lot of leeway on how to use thresholds. But one thing is held true. Thresholds have an amount and a type.

### Threshold 1 Type

The type of the threshold

### Action 1 … N

Actions are the bulk of what makes a card unique. The important things about actions is that they are evaluated from the top to the bottom order. There isn’t a hard limit to the amount of actions on a card, but for UI and understandability purposes, they are probably never going to get higher than 10\.   
Some rulesets may remove, append, or insert actions in a cards list. This can either be on the instance or the card definition level. However, serializing back to permanently change a cards data isn’t supported yet. (this is how we could make a “legacy” style boardgame)

### Targets

These are used by the ruleset as guidelines on what a valid place to play the card is. I believe we will discover much more about how to work with targets as we implement rulesets. Expect change here

### Back

This is the back of the card. Unless specified, cards are displayed with the front face (the card) to the player. There are reasons to show a back, and in some rulesets, we might have several backs to choose from. (once again, we will expand beyond physical limits, so some cards might have more than 1 back\!)

### Rarity

This signals to the player how valuable a card is, or how likely they are to see it. Think of it as a very specific type. Some rules sets may have special filter rules based on type

### Flavor

Text to make the game more fun and thematic.   
