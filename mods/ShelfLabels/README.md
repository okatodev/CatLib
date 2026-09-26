# Shelf Labels

Adds up to three extra labels next to every shelf label, so one shelf can say both "Port Windy" and "Fragile".
The extra labels use the game's own pictures and are changed the same way: left click for the next picture, right click for the previous one.
The game's own label is never touched.

## Settings

| Setting | Scope | Meaning |
|---|---|---|
| Extra labels | session | 0 to 3 extra labels per shelf label, the host decides for everyone |
| Placement | session | where extra labels go on shelves without a placement of their own |
| Hide stands | session | extra labels on shelves without a choice of their own come without the wooden stand |
| Change placement | local | hotkey, Ctrl+L by default |
| Stand on or off | local | hotkey, Ctrl+K by default |
| Gap | local | space between labels in meters |

## Placement per shelf

Shelves differ: beside one label the extra labels block a passage, beside another they go into a wall.
Look at a shelf label or one of its extra labels and press Ctrl+L to switch its placement:
freer side, right, left, below, no extra labels, and around again. A notification shows the new placement.
Every player can do it; like pictures, the request goes to the host, the host keeps the placement in its save and everyone sees the same.

## Stands

The wooden stand under a label fits some shelves and not others. Look at a shelf label or one of its extra labels and press Ctrl+K
to hide or show the stand of its extra labels. The game's own label always keeps its stand. Like placements, the choice is the host's and is kept in its save.

The stand is removed only from the copies: a part that lies entirely below the picture frame is turned off,
and a mesh that reaches below the frame gets a trimmed copy cut along a plane just under the frame.
The game's label mesh is not readable from scripts, so the mod copies its vertex and index data from the video card
and lifts every vertex below the cut up to the cut line: the stand collapses into the frame's lower edge while normals, texture coordinates and colors stay untouched.
If that data cannot be read either, the label keeps its stand and the log explains why.

## Saves

Pictures and placements are stored with CatLib per game save, in `CatLibSaves/<save>/catlib.shelflabels.json` next to the game's save folder.
Each picture is stored under the game's permanent id of the shelf label and the slot number, for example `label/390/2`,
each placement under the label id, for example `place/390`, and each stand choice as `stand/390`.

- They are written only when the game saves, and only by the host.
- Lowering the number of extra labels or choosing "no extra labels" hides them but keeps their pictures; showing them again brings the pictures back.
- Removing the mod leaves the game save as it was. Its data stays in its own file and returns if the mod is installed again.

## Multiplayer

Every player needs the mod. A player's click goes to the host, the host applies it and sends the result to everyone.
A player who joins gets all pictures and placements from the host. Without the mod on the host the extra labels are hidden for the player.

## How it works

The extra labels are copies of the game's label holder without its network and save components and with the game's label script turned off.
Clicks on the copies are caught before the game handles them, so they never reach the game's network code.
