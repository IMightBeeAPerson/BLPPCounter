# Beatleader PP Counter
### Description
It's a counters+ counter. Mostly self-explainatory, just drag the file into your plugins folder and put it on with counters+.
## Customizing format
### Overview
In the config for this counter, you will find the text format strings. These are what format the counter to look the way it does in game. Here's some examples of these strings:
```json
"DefaultTextFormat": "&x<1 / &y> &l<2[e\n*c,red*$* mistake&s(e)]>"
"ClanTextFormat": "[p$ ]&[[c&x]&]<1 / [o$ ]&[[f&y]&] >&l<2\n&m[t\n$]>"
"WeightedTextFormat": "&x[p ($)]<1 / &y[o ($)]><3 [c#&r]> &l<2[e\n*c,red*$* mistake&s(e)][m\n$]>"
"RelativeTextFormat": "[c&x][p ($)]<1 || [f&y][o ($)]> &l<2\n[c&a]% to beat[t\n$]>"
```
Lemme break it down and explain it so that you can understand it and customize it yourself. This works by displaying everything as normal text except for the special characters, which will be replaced with values from the counter. 
Here's a quick explaination of important characters.
- The '&' is the escape character, meaning special things happen to the character(s) after it. If you want to type it literally, or any special character, without anything special happening, simply type 2 in a row like this: '&&'.
- If there is a letter (a-z) after the escape character, then this represents a value that will be replaced when the counter is active. What specifically will be explained below in the next section.
- The '[' and ']' characters will create a group. Each group has a value (aka letter) it is assigned to, and will be stored with this value. 
  What this does is tie a string of character to a value, and everything inside will be disabled and enabled with the character. To input the value inside the group, simply put a '$', which will be replaced with the value.
- To create a capture, you use a very similar syntax to a group, just with different kind of brackets, '<' and '>'. A capture doesn't have any value attached to it, so instead of using a letter as an identifier, you use a number.
  The main use of this is for counters to enable and disable parts of the counter dynamically. For example, when you miss, the display need to show a new section, and it will use captures to do so.
- There is also parameters, which you can see in the WeightedTextFormat towards the end "&s(e)". This is denoted by putting parentheses, '(' and ')', around parameters directly after an escape character. What parameters will be denoted below and is specific to each letter and sometimes each counter. *Note: you do not have to escape parentheses to use them normally. You only need to escape them if it is directly after a escape character.* 

**Note:** The special characters used in these explainations are the default characters, however they can be changed in settings if you don't like how they feel or look. Everything, including the brackets, can be changed to whatever you want.
### Global escape letters
These are letters that share the same functionality through all formatted messages and counters.
| Letter | Parameters | Description |
| ------ | ---------- | ----------- |
| s | \<number variable\> | This takes one parameter, a number variable, and will dynamically output either a 's' or nothing ('') depending on if the number is exactly 1 or not. This is used to be grammatically correct | 
### Counter Specific Syntax 
The different counter types have different letters to mean different things, instead of making you sift through the code or figure it out, here's a list :)
#### Default Counter (Progressive and Normal)
This includes any counter that is too simple to have their own display method, and therefore just uses the one in the main counter class.
| Letter | Description |
| ------ | ----------- |
| x | The unmodified PP number |
| y | The unmodified PP number if the map was FC'ed |
| e | The amount of mistakes made in the map. This includes bomb and wall hits |
| l | The label (ex: PP, Tech PP, etc) |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is linked to the 'enable messages' option and should be used for messages and other info |

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
| e | The amount of mistakes made in the map. This includes bomb and wall hits |
| t | This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target |
| m | This will show a message if the counter is used on a map that isn't perfectly ideal for clan counter or that clan counter can't be used on. It will say the reason for why this isn't ideal |

**Note:** Only clan counter has messages (m), they do not exist on relative counter and it will throw an error if you try to use it on relative counter.

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is linked to the 'enable messages' option and should be used for messages and other info |

#### Weighted Counter
This is a special counter that will appear when the clan counter fails because the map is already captured, the map is too hard to capture, or the API requests return bad information that causes an error.
| Letter | Description |
| ------ | ----------- |
| p | The unmodified PP number |
| x | The modified PP number (plus/minus value) |
| o | The unmodified PP number if the map was FC'ed |
| y | The modified PP number if the map was FC'ed |
| l | The label (ex: PP, Tech PP, etc) |
| e | The amount of mistakes made in the map. This includes bomb and wall hits |
| m | This will show a message if the counter is used on a map that isn't perfectly ideal for clan counter or that clan counter can't be used on. It will say the reason for why this isn't ideal |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is linked to the 'enable messages' option and should be used for messages and other info |
| 3 | This capture is will enable/disable based off if you enable showing your placement in settings |

### Message Specific Syntax
Messages are things that appear in the counter when something to change the counter from normal happens. Sometimes these take in values, sometimes they do not.

#### Normal Messages
These are messages with no values, and therefore will not be parsed and are completely normal rich text.
- Map Captured Message
- Map Uncapturable Message
- Map Unranked Message
- Map Load Failed Message

#### Clan Message
This message is supposed to inform the player what percent or accuracy is needed to capture the map.
| Letter | Description |
| ------ | ----------- |
| a | The accuracy needed to capture the map |
| p | The total PP number needed to capture the map |
| x | The tech PP needed |
| y | The accuracy PP needed |
| z | The pass PP needed |
| t | This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target |

#### Targeting Message
This message is for whenever you are targeting someone. Currently it can show a few stats of the person you are targeting, but I'll most likely add more at a later date.
| Letter | Description |
| ------ | ----------- |
| t | The name of the person being targeted |
| m | The mods used by the person you are targeting |
