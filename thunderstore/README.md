# Minimap

A round minimap in the corner of the screen during runs, framed like the game's own HUD rings.

## The map

- **A pixel-art picture of the level itself**, taken from straight above as you drop in and colour-matched to the game camera. It is taken once, so it stays still while you play.
- **Only the arena is drawn**, with a bright line along its edge. The ground beyond the walls, where enemies come from, is left out.
- **Rocks and walls you can't walk through are solid black.** Grass, bushes and pebbles are just ground, and dark ground is lifted a little so only obstacles are black (helps on the fire and astral maps).
- **The astral river** is drawn as lavender water with pale banks, and **the ice map's death bushes** are red.

## On the map

- **Enemies** as dots that grow with how dangerous they are: **red** for the swarm, **orange** for bigger enemies, **purple** for mini-bosses (elites such as the big snake, the Hydra's four-armed healers, the Hoarder).
- **Bosses** (Hydra, Squid, Toad) as a skull badge; off the map it sticks to the edge with an arrow.
- **Objectives** appear once someone in the party has had them on screen; discoveries are shared with everyone running the mod (through the host). **Downed teammates and danger pings** always show.
- **Pickups** (artifacts, magnets, and larger cream-ringed **health potions**) on dark badges.
- **Teammates** in their player colours; a dead teammate is a **ghost** in their colour. While you're a ghost yourself, your arrow follows where you move.
- **Geysers** on the ice map: yellow when ready, orange and shaking while erupting, grey while recharging.
- **Tornadoes** on the fire map, following them as they move.
- **Teleport portals** on the astral map: bright cyan when open, faint when closed.
- **The order objective's mushrooms** in the game's colours: blue while waiting, green when hit in the right order, red when wrong.
- **MoreEvents** targets and zones, if that mod is installed.

The map turns with the camera, so up on the minimap is up on screen. It moves up out of the way when the game's HUD (collected artifacts, the revive counter) or another mod's panel sits in its corner, and back down when that's gone.

## Keys

| Key | Action |
|---|---|
| M | Hide / show |
| = | Zoom in |
| - | Zoom out |

## Config (in your mod manager's config editor)

- `[General]` `Enabled`, `ToggleKey`, `ZoomInKey`, `ZoomOutKey`
- `[Layout]` `Corner` (BottomRight), `Shape` (Round / Square), `Size` (220), `Margin` (16), `BottomOffset` (30, room for the text bar), `AvoidGameHud` (true), `Opacity` (0.85)
- `[Look]` `Style` (Pixelated / Flat), `HighlightObstacles`, `TrimOutsideArena`, `LiftDarkGround`, `ObstacleKeywords`, `TintKeywords`, `PaintFromGameView` (off; experimental)
- `[View]` `Radius` (40), `MinRadius` (15), `MaxRadius` (120), `RotateWithCamera` (true)
- `[Layers]` `ShowEnemies`, `BigEnemyHealth` (3: how much tougher than the swarm an enemy must be to show orange), `ShowObjectives`, `ShowPickups`, `ShowTeammates`, `ShowMoreEvents`, `ShowMapFeatures` (geysers, tornadoes, portals), `HideUndiscoveredObjectives` (true)
- `[Debug]` `SaveDebugImages` (false): saves the pictures behind the minimap to `BepInEx/minimap-debug`

## Multiplayer

Install it if you want to see a minimap; nobody else needs it. The only thing it sends over the network is which objectives have been found, and only to players who also run it. If the host doesn't run it, each player's minimap shows just the objectives they found themselves.

## Notes

- If a level has no navigation mesh, the minimap shows dots without a layout (radar mode) and says so in the BepInEx log.
- The layout is mapped while you drop in (a small slice of work each frame) and shown in flat colours until you land; a second after landing it switches to the level picture.

## Changelog

- **1.0.1**: moves out of the way of the game's HUD in its corner; dead teammates as ghosts in their colour; your arrow turns while you're a ghost; order-objective mushrooms coloured by state; the fire map's arena edge.
- **1.0.0**: first release.
