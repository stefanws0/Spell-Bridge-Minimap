# Minimap

A minimap in the corner of the screen during runs.

- **Level layout** read from the level's navigation mesh when the run starts.
- **Enemies** as red dots; **elites** larger and orange.
- **Objectives, the boss, downed teammates and danger pings**, using the game's own icons. Off-map ones stick to the edge with an arrow.
- **Pickups** (artifacts, health, magnets) the game points at.
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
- `[Layout]` `Corner` (TopRight), `Size` (220), `Margin` (16), `Opacity` (0.85)
- `[View]` `Radius` (40), `MinRadius` (15), `MaxRadius` (120), `RotateWithCamera` (true)
- `[Layers]` `ShowEnemies`, `ShowObjectives`, `ShowPickups`, `ShowTeammates`, `ShowMoreEvents`

## Multiplayer

Client-side only. Install it if you want to see a minimap; nobody else needs it, and it sends nothing over the network.

## Notes

- If a level has no navigation mesh, the minimap shows dots without a layout (radar mode) and says so in the BepInEx log.
- The layout is painted once per level, which can cause a brief hitch as the run starts.
