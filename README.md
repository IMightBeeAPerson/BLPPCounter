# Beatleader PP Counter
### Description
It's a counters+ counter. Mostly self-explainatory, just drag the file into your plugins folder and put it on with counters+.
## Customizing format
### Overview
In the config for this counter, you will find the text format strings. These are what format the counter to look the way it does in game. Here's some examples of these strings:
```json
"DefaultTextFormat": "&x&1 / &y&1 &l",
"ClanTextFormat": "[p$ ]&[[c&x]&]&1 / [o$ ]&[[f&y]&] &1&l",
"WeightedTextFormat": "&x[p ($)]&1 / &y[o ($)]&1 &l",
"RelativeTextFormat": "[c&x][p ($)]&1 || [f&y][o ($)]&1 &l"
```
Lemme break it down and explain it so that you can understand it and customize it yourself. This works by displaying everything as normal text except for the special characters, which will be replaced with values from the counter. 
Here's a quick explaination of important characters.
- The '&' is the escape character, meaning special things happen to the character(s) after it. If you want to type it literally, or any special character, without anything special happening, simply type 2 in a row like this: '&&'.
- If there is a letter (a-z) after the escape character, then this represents a value that will be replaced when the counter is active. What specifically will be explained below in the next section.
- The '[' and ']' characters will create a group. Each group has a value (aka letter) it is assigned to, and will be stored with this value. 
  What this does is tie a string of character to a value, and everything inside will be disabled and enabled with the character. To input the value inside the group, simply put a '$', which will be replaced with the value.
- To create a capture, you use a very similar syntax to a group, just with different kind of brackets, '<' and '>'. A capture doesn't have any value attached to it, so instead of using a letter as an identifier, you use a number.
  The main use of this is for counters to enable and disable parts of the counter dynamically. For example, when you miss, the display need to show a new section, and it will use captures to do so.

**Note:** The special characters used in these explainations are the default characters, however they can be changed in settings if you don't like how they feel or look.
### Counter Specific Syntax 
The different counter types have different letters to mean different things, instead of making you sift through the code or figure it out, here's a list :)
#### Default Counter (Progressive and Normal)
This includes any counter that is too simple to have their own display method, and therefore just uses the one in the main counter class.
| Letter | Description |
| ------ | ----------- |
| x | The unmodified PP number |
| y | The unmodified PP number if the map was FC'ed |
| l | The label (ex: PP, Tech PP, etc) |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |

#### Clan and Relative Counter
These both have the same syntax, and therefore will share the same table.
| Letter | Description |
| ------ | ----------- |
| p | The unmodified PP number |
| x | The modified PP number (plus/minus value) |
| c | Must use as a group value, and will color everything inside group |
| o | The unmodified PP number if the map was FC'ed |
| y | The modified PP number if the map was FC'ed |
| f | Must use as a group value, and will color everything inside group |
| l | The label (ex: PP, Tech PP, etc) |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |

#### Weighted Counter
This is a special counter that will appear when the clan counter fails because the map is already captured, the map is too hard to capture, or the API requests return bad information that causes an error.
| Letter | Description |
| ------ | ----------- |
| p | The unmodified PP number |
| x | The modified PP number (plus/minus value) |
| o | The unmodified PP number if the map was FC'ed |
| y | The modified PP number if the map was FC'ed |
| l | The label (ex: PP, Tech PP, etc) |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is will enable/disable based off if you enable showing your placement in settings |

### Message Specific Syntax
Messages are things that appear in the counter when something to change the counter from normal happens. Sometimes these take in values, sometimes they do not.

#### Normal Messages
These are messages with no values, and therefore will not be parsed and are completely normal text.
- Map Captured Message
- Map Uncapturable Message

#### Clan Message
This message is supposed to inform the player what percent or accuracy is needed to capture the map.
| Letter | Description |
| ------ | ----------- |
| a | The accuracy needed to capture the map |
| p | The total PP number needed to capture the map |
| x | The tech PP needed |
| y | The accuracy PP needed |
| z | The pass PP needed |

#### Targeting Message
This message is for whenever you are targeting someone. Currently it can show a few stats of the person you are targeting, but I'll most likely add more at a later date.
| Letter | Description |
| ------ | ----------- |
| t | The name of the person being targeted |
| m | The mods used by the person you are targeting |
