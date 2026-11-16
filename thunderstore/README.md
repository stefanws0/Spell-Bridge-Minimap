# Minimap

A round minimap in the corner of the screen during runs, framed like the game's own HUD rings.

- **A pixel-art picture of the level itself**, taken from straight above as you drop into a run and colour-matched to what the game camera shows. The arena's edge is a bright line and **obstacles** (rocks, trees, walls inside the arena) get a dark outline. Only the arena is drawn.
- **Enemies** as red dots; **elites** larger and orange.
- **Objectives** appear once someone in the party has had them on screen; discoveries are shared with everyone running the mod (through the host). **The boss, downed teammates and danger pings** always show. All use the game's own icons, and off-map ones stick to the edge with an arrow.
- **Pickups** (artifacts, magnets, and larger cream-ringed **health potions**) on dark badges.
- **Teammates** in their player colors; ghosts are faded.
- **MoreEvents** targets and zones, if that mod is installed.

The map turns with the camera, so up on the minimap is up on screen.

## Keys

| Key | Action |
|---|---|
| M | Hide / show |
| = | Zoom in |
| - | Zoom out |

## Config (in your mod manager's config editor)

- `[General]` `Enabled`, `ToggleKey`, `ZoomInKey`, `ZoomOutKey`
- `[Layout]` `Corner` (BottomRight), `Shape` (Round / Square), `Size` (220), `Margin` (16), `BottomOffset` (30, room for the text bar), `Opacity` (0.85)
- `[Look]` `Style` (Pixelated / Flat), `HighlightObstacles` (true)
- `[View]` `Radius` (40), `MinRadius` (15), `MaxRadius` (120), `RotateWithCamera` (true)
- `[Layers]` `ShowEnemies`, `ShowObjectives`, `ShowPickups`, `ShowTeammates`, `ShowMoreEvents`, `HideUndiscoveredObjectives` (true)
- `[Debug]` `SaveDebugImages` (false): saves the level picture, colour sample and finished minimap to `BepInEx/minimap-debug`

## Multiplayer

Install it if you want to see a minimap; nobody else needs it. The only thing it sends over the network is which objectives have been found, and only to players who also run it. If the host doesn't run it, each player's minimap shows just the objectives they found themselves.

## Notes

- If a level has no navigation mesh, the minimap shows dots without a layout (radar mode) and says so in the BepInEx log.
- The layout is mapped while you drop in (a small slice of work each frame) and shown in flat colours until you land; a second after landing it switches to the colour-matched level picture.
