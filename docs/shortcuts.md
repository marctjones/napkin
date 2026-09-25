# Keyboard shortcuts

Generated from `src/Napkin.Modules.Editing/KeyMaps.cs` — the one table the views, the window's key
bindings and the menus read. `KeyMapTests` fails when this file and the table differ; to regenerate it,
run that test with `NAPKIN_WRITE_SHORTCUTS=1` set.

"Ctrl/Cmd" is Control on Windows and Linux and Command on macOS. A key typed into a text field is
text: no shortcut below except the window's fires while a field has the keyboard.

## Anywhere in the window

| Keys | Does |
| --- | --- |
| `Ctrl/Cmd+N` | New design |
| `Ctrl/Cmd+O` | Open a design |
| `Ctrl/Cmd+S` | Save |
| `Ctrl/Cmd+Shift+S` | Save as |
| `Ctrl/Cmd+Z` | Undo |
| `Ctrl/Cmd+Shift+Z`, `Ctrl/Cmd+Y` | Redo (the platform's own keys: Ctrl+Y first on Windows, Cmd+Shift+Z on macOS) |
| `Ctrl/Cmd+L` | Cut list |
| `Ctrl/Cmd+Shift+L` | Shopping list |
| `Ctrl/Cmd+1` | Open the first sample |
| `Ctrl/Cmd+2` | Open the second sample |
| `Ctrl/Cmd+3` | Open the third sample |
| `Ctrl/Cmd+4` | Open the fourth sample |
| `Ctrl/Cmd+5` | Open the fifth sample |
| `Ctrl/Cmd+6` | Open the sixth sample |
| `Ctrl/Cmd+7` | Open the seventh sample |
| `Ctrl/Cmd+8` | Open the eighth sample |
| `Ctrl/Cmd+9` | Open the ninth sample |

## Editing (the plan or the 3D view has the keyboard)

| Keys | Does |
| --- | --- |
| `S` | Select tool |
| `R` | Rectangle tool; in 3D, a plain board to place; in a standard view, says why not |
| `W` | Wall tool, with the member it last had; brings the plan forward |
| `Shift+W` | Room tool: drag out a room, or click inside four walls for their inside faces; brings the plan forward |
| `C` | Cut the selected part to shape (the shape workshop); not in a standard view |
| `D` | Duplicate the selection |
| `M` | Mirror copy east–west |
| `Shift+M` | Mirror copy north–south |
| `P` | Pin the selection in place |
| `Delete`, `Backspace` | Delete the selected joint, or the selection |
| `J` | Join the two selected parts |
| `Shift+J` | Join every pair of touching parts |
| `Q` | Rough sketching on or off: big round steps, nothing stated, rectangles drawn as planks |
| `F` | Firm up the selection, or every part: the relationships touching parts imply, the nearest stock, the drawn sizes stated |
| `X` | Turn the selection about X (while placing stock in 3D: turn what is held) |
| `Shift+X` | Turn about X the other way |
| `Y` | Turn the selection about Y (while placing: turn what is held) |
| `Shift+Y` | Turn about Y the other way |
| `Z` | Turn the selection about Z (while placing: turn what is held) |
| `Shift+Z` | Turn about Z the other way |
| `V` | 3D from a flat view; back to the last flat view from 3D |
| `G` | Show or hide the grid |
| `H` | Show or hide hidden edges, as light dashes (Bottom, Front, Back, Left, Right) |
| `Escape` | Stop what is under way, put down the tool, or let go of the selection |
| `Enter` | Edit the selected joint |
| `Tab` | Type the selected part's width (plan) |
| `Left` | Nudge the selection one grid step west (plan; with nothing selected the arrow pans) |
| `Right` | Nudge one grid step east |
| `Up` | Nudge one grid step north |
| `Down` | Nudge one grid step south |
| `Shift+Left` | Nudge four grid steps west |
| `Shift+Right` | Nudge four grid steps east |
| `Shift+Up` | Nudge four grid steps north |
| `Shift+Down` | Nudge four grid steps south |

## Moving about the view

| Keys | Does |
| --- | --- |
| `Ctrl/Cmd+0` | Zoom to fit |
| `Home` | Reset the view: the plan fits; 3D goes back to its first angle |
| `+`, `Shift++`, `Ctrl/Cmd++` | Zoom in |
| `-`, `Ctrl/Cmd+-` | Zoom out |
| `Left` | Pan left (plan, standard views); orbit (3D) |
| `Right` | Pan right; orbit |
| `Up` | Pan up; orbit |
| `Down` | Pan down; orbit |
| `Shift+Left` | Pan or orbit further |
| `Shift+Right` | Pan or orbit further |
| `Shift+Up` | Pan or orbit further |
| `Shift+Down` | Pan or orbit further |
| `O` | Orthographic or perspective (3D; a standard view is always orthographic) |
| `1` | Top (the plan) |
| `2` | Bottom |
| `3` | Front |
| `4` | Back |
| `5` | Left |
| `6` | Right |
| `7` | 3D |
