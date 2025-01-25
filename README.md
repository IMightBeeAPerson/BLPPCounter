# Beatleader PP Counter
### Description
#### How do I install this?
It's a counters+ counter. Mostly self-explainatory, just drag the file into your plugins folder and put it on with counters+.
#### What is this?
This is a counter for beatleader PP. It has various options for how to do this, from just the standard counter to competing against your own scores or sniping other people's scores. It even has functionality for clan wars, where it can tell you what percent you'd need to get on a map to capture it. It does account for all modifiers, and below is 2 methods on how to completely customize the data shown. There is an in game way and config way, with both being a bit of a mess. The counter works fine if you never want to mess with it.

**Side note:** This is just a project done for fun, and is my first time coding a mod and in general stuff that relies on APIs and other libraries. If you decide to use this, remember that it may not work perfectly. However, feel free to inform me of any bugs you find.
## In game settings
### Overview
Inside of the game, there are a lot of settings that aren't just directly for the counter. If you go to mod settings for this mod, there are a lot of submenus and options, and therefore need a lot of explanations. This will go in order, from top to bottom. Note that the ui works fine in FPFC mode if you would prefer to edit it outside of VR.
### Simple menu
This refers to the first checkbox setting and the submenu below it. The checkbox setting will toggle on/off whether in the information from the submenu will be used in the tab setting menu on the song select screen. If it is toggled off, the menu in the song select screen will show almost all options for the counter that can be found in the counters+ menu. If it is toggled on, then within the submenu you can choose which settings to show in the song select menu. That way, if you would like on certain settings to be able to be changed on short notice, you can do so.
### Format Editor
This is by far the most complex part of this menu. This is about completely customizing any part of this counter to look however you want. It also is optional, so feel free to skip over this. 
#### Auto Update Preview
This checkbox setting will toggle on/off if preview shown should be updated every time a change is made to the format. (This is outside the submenu, but everything else stated is inside the "edit format settings" menu)
#### Dropdown Choices
There are 2 dropdown boxes that allow you to find which specific format you want to edit. The first is which counter to edit, and the second one is which format inside this counter to edit (Note that not all counters are shown here because not all counters have editable formats).
#### Format Displays
Below the dropdown choices, there are 2 submenus, which will be explained below this. Below the submenus, there are 2 displays, one shown the raw code (color coded for your convenience), while the other showing what the format looks like with some dummy values in it. To understand how the code works, see [This section](#customizing-format-in-config).
#### Format Editor Menu
This is the first of 2 submenus, and is where the actual editing of the format takes place. There are six buttons, with all of them saying exactly what they do. There is a list in the middle what breaks up the format into several tokens, with the arrows on each block moving that block up or down. If you want to know where that block is located on the code and format to the right, clicking on it will highlight where it is. If is a group or capture, then it will use the secondary highlight color to highlight what is inside the group/capture. If you look to your left, there is a table showing all the aliases and descriptions for this format. If you have [auto update preview](#auto-update-preview) disabled, then you will need to click the update preview button in the top right to see changes made.
#### Value Editor Menu
This is the second submenu, and is used to edit the dummy values used in the format displays. These changes cannot be saved to config or anything, but will persist until you close the game. The save button is needed to be hit for changes to been seen outside of the menu.
### Color Editor
This is a simple menu to edit the colors used inside of the format editor. Inside there is a list of color settings with an alpha slider below to fully customize the coloring in the format editor. 
### Alias Editor
This is a way to edit the names used in formats to whatever you want. To understand more about how aliases work, see [this](#making-custom-aliases).
#### Update References
This is a checkbox setting for whether or not to update format references when adding aliases. If aliases are added directly to the config, you must do it manually, but if you add them through the menu and have this enabled, then the formats will have their references updated automatically.
#### Alias Editor Menu
This menu is pretty simple. There is the double dropdown menu to specify which counter and format you would like to add an alias for, and when you hit the 'Add New Alias' button, a popup appears. In this popup, you specify which alias you would like to replace, and what to replace it with. Then hit save, and it will be added to the list. If you want to get rid if it, you just simply hit the delete button.
## Customizing format in config
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

There is also a special escape character, the apostrophe. This allows for the use of an alias instead of just putting a one letter character to make the format string a bit more readable. Each letter also has a name assigned to it that will be shown below in the tables. These names are changeable in the config if you don't like the ones I gave. Here's an example of a format with and without the use of aliases:

*Without:*
```json
"DefaultTextFormat": "&x<1 / &y> &l<2[e\n*c,red*$* mistake&s(e)]>"
```
*With:*
```json
"DefaultTextFormat": "&'PP'<1 / &'FCPP'> &'Label'<2['Mistakes'\n*c,red*$* mistake&'Dynamic s'('Mistakes')]>"
```
As you can see, it is a lot easier to tell what each value is with the use of aliases. The format for it is &'\<Alias name\>'. It is important to follow the format or the counter will throw an error and not work.

### Making custom aliases
This is done by going to the bottom of "TokenSettings" and inserting information into the "TokenAliases" array. Here is an example of an added custom alias:
```json
    "TokenAliases": [
      {
        "CounterName": "Relative",
        "FormatName": "Main Format",
        "AliasCharacter": "d",
        "AliasName": "hi",
        "OldAlias": "Acc Difference"
      }
    ]
```
What the above example does is for specifically the normal counter, replace the "d" character's alias, "Acc Difference", with the new alias "hi". There are 5 parameters for custom aliases, each of which serve to do something. Below is a table of each with a description:
| Parameter | Description |
| --------- | ----------- |
| CounterName | The counter you wish to change the character on. If you want to change the character on all counters (that have that character), just leave it blank by putting "" with nothing inside of it. |
| FormatName | This refers to what specific format inside of the counter to use. "Main Format" will always refer to the main format usually shown in a counter, but there is a table below showing what each format name is |
| AliasCharacter | This is the character you wish to change, you must put the character, which is provided below for all counters (this was changed from previous versions to better work with the in game ui). |
| AliasName | The new alias name for the given AliasCharacter. Any character is allowed except for the ones used in token settings. If you do use one of those characters, it may or may not work, depending on how the parser handles it. Best practice would be to avoid using them at all. |
| OldAlias | For parsing reasons, the old alias that this is to replace is required to be provided. Just copy and paste it from below |

Every character has a default alias that works perfectly fine without any changes, so if this feels too complicated and you just want to use what is given, feel free to do so. 
### Counter & Format Names
| Counter | Format Name | Explanation |
| ------- | ----------- | ----------- |
| Default | Main Format | This is the generic counter that default counter and progressive counter use as their counter. |
| Default | Target Format | This is the format for the target message shown in relative counter |
| Default | Percent Needed Format | This is a backup format for clan counter and relative counter that will display the percent needed to capture a map or beat a score |
| Clan Counter | Main Format | This is the main format displayed when trying to capture a map |
| Clan Counter | Weighted Format | This is the format for the weighted counter, which will display if a map cannot be captured for some reason |
| Clan Counter | Custom Message Format | This is a custom message to replace the percent needed format specifically for clan counter |
| Relative Counter | Main Format | The main format displayed for relative counter |
### Global escape letters
These are letters that share the same functionality through all formatted messages and counters.
| Letter | Alias | Parameters | Description |
| ------ | ----- | ---------- | ----------- |
| s | Dynamic s | \<number variable\> | This takes one parameter, a number variable, and will dynamically output either a 's' or nothing ('') depending on if the number is exactly 1 or not. This is used to be grammatically correct |
| h | Hide | \<number variable, *(optional)* number\> | This takes either one or two parameters. The first one is a number variable and the second one is a number (not a variable). This letter acts as a flag, and based off of what the number variable is, will hide the variable completely. If no number is given in the second parameter, then it will only show the number variable when it is greater than 0. Otherwise it will show if the variable is greater than the number given. | 
### Counter Specific Syntax 
The different counter types have different letters to mean different things, instead of making you sift through the code or figure it out, here's a list :)
#### Default Counter (Progressive and Normal)
This includes any counter that is too simple to have their own display method, and therefore just uses the one in the main counter class.
| Letter | Alias | Description |
| ------ | ----- | ----------- |
| x | PP | The unmodified PP number |
| y | FCPP | The unmodified PP number if the map was FC'ed |
| e | Mistakes | The amount of mistakes made in the map. This includes bomb and wall hits |
| l | Label | The label (ex: PP, Tech PP, etc) |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is linked to the 'enable messages' option and should be used for messages and other info |

#### Clan and Relative Counter
These both have mostly the same syntax, and therefore will share the same table. Exceptions will be below.
| Letter | Alias | Description |
| ------ | ----- | ----------- |
| p | PP | The unmodified PP number |
| x | PP Difference | The modified PP number (plus/minus value) |
| c | Color | Must use as a group value, and will color everything inside group |
| o | FCPP | The unmodified PP number if the map was FC'ed |
| y | FCPP Difference | The modified PP number if the map was FC'ed |
| f | FC Color | Must use as a group value, and will color everything inside group |
| l | Label | The label (ex: PP, Tech PP, etc) |
| e | Mistakes | The amount of mistakes made in the map. This includes bomb and wall hits |
| t | Target | This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target |

Below is a table with the letters that only apply to one counter or the other.

| Letter | Alias | Counter | Description |
| ------ | ----- | ------- | ----------- |
| m | Message | Clan Counter | This will show a message if the counter is used on a map that isn't perfectly ideal for clan counter or that clan counter can't be used on. It will say the reason for why this isn't ideal |
| d | Acc Difference | Relative Counter | This will show the difference in percentage at the current moment between you and the replay you're comparing against |
| a | Accuracy | Relative Counter | This is the accuracy needed to beat your or your target's previous score |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is linked to the 'enable messages' option and should be used for messages and other info |

#### Weighted Counter
This is a special counter that will appear when the clan counter fails because the map is already captured, the map is too hard to capture, or the API requests return bad information that causes an error.
| Letter | Alias | Description |
| ------ | ----- | ----------- |
| p | PP | The unmodified PP number |
| x | PP Difference | The modified PP number (plus/minus value) |
| o | FCPP | The unmodified PP number if the map was FC'ed |
| y | FCPP Difference | The modified PP number if the map was FC'ed |
| l | Label | The label (ex: PP, Tech PP, etc) |
| e | Mistakes | The amount of mistakes made in the map. This includes bomb and wall hits |
| m | Message | This will show a message if the counter is used on a map that isn't perfectly ideal for clan counter or that clan counter can't be used on. It will say the reason for why this isn't ideal |
| c | Rank Color | The color based off of what rank you are out of your clan, must be used inside of a group and will color everything in the group |
| r | Rank | The rank you are in the clan at that current moment |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is linked to the 'enable messages' option and should be used for messages and other info |
| 3 | This capture is will enable/disable based off if you enable showing your placement in settings |

### Rank Counter
This counter will show what rank you are on the leaderboard if the map were to end at that moment.
| Letter | Alias | Description |
| ------ | ----- | ----------- |
| x | PP | The unmodified PP number |
| y | FCPP | The unmodified PP number if the map was FC'ed |
| r | Rank | The rank you would be on the leaderboard if the map ended right then |
| d | PP Difference | The modified PP number, shows how much pp to go up one rank on the leaderboard |
| l | Label | The label (ex: PP, Tech PP, etc) |
| c | Rank Color | The color of the rank (set in settings) |

| Number | Description |
| ------ | ----------- |
| 1 | This capture will enable and disable based off if the player is FC'ing the map or not |
| 2 | This capture is linked to the 'enable messages' option and should be used for messages and other info |
| 3 | This will be shown if the rank is not 1 |
| 4 | This will be shown if the rank is 1 |

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
| Letter | Alias | Description |
| ------ | ----- | ----------- |
| c | Color | Must use as a group value, and will color everything inside group |
| a | Accuracy | The accuracy needed to capture the map |
| p | PP | The total PP number needed to capture the map |
| x | Tech PP | The tech PP needed |
| y | Acc PP | The accuracy PP needed |
| z | Pass PP | The pass PP needed |
| t | Target | This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target |

#### Targeting Message
This message is for whenever you are targeting someone. Currently it can show a few stats of the person you are targeting, but I'll most likely add more at a later date.
| Letter | Alias | Description |
| ------ | ----- | ----------- |
| t | Target | The name of the person being targeted |
| m | Mods | The mods used by the person you are targeting |
