# Minimap

A round minimap in the corner of the screen during runs, framed like the game's own HUD rings.

## The map

- **A pixel-art picture of the level itself**, taken from straight above as you drop in and colour-matched to the game camera. It is taken once, so it stays still while you play.
- **Only the arena is drawn**, with a bright line along its edge. The ground beyond the walls, where enemies come from, is left out.
- **Rocks and walls you can't walk through are solid black.** Grass, bushes and pebbles are just ground, and dark ground is lifted a little so only obstacles are black (helps on the fire and astral maps).
- **The astral river** is drawn as lavender water with pale banks, and **the ice map's death bushes** are red.

## On the map

- **Enemies** by how dangerous they are, each rank its own shape and colour, a step bigger than the one before: **red circles** for the swarm, **orange triangles** for bigger enemies, **pink squares** for heavy ones (8x the swarm's health or more, such as the Mole and the ShieldCrab), **purple stars** for mini-bosses (elites such as the big snake, the Hydra's four-armed healers, the Hoarder). When the game points at an enemy from the edge of the screen (after a ping), its shape sticks to the minimap's edge when it's out of range.
- **Bosses** (Hydra, Squid, Toad) as a skull badge; off the map it sticks to the edge with an arrow.
- **Objectives** appear once someone in the party has had them on screen, or as soon as the game points at them from the edge of the screen; off the map they stick to the edge with an arrow. The same goes for every piece of an objective on its own (each mushroom, bonfire, llama, object to push, statue and hunt target), for the push zone and herding pen, and for pickups. Discoveries are shared with everyone running the mod (through the host). **Downed teammates and danger pings** always show.
- **Objectives use the game's own icons**, with a dark edge so they read on any ground: blue for most, red for the sacrifice, and the clean objective in its goo's colour.
- **Pickups** (artifacts, magnets, and larger cream-ringed **health potions**) on dark badges, once found.
- **Teammates** in their player colours; a dead teammate is a **ghost** in their colour. While you're a ghost yourself, your arrow follows where you move.
- **Geysers** on the ice map: yellow when ready, orange and shaking while erupting, grey while recharging.
- **Tornadoes** on the fire map, following them as they move.
- **Teleport portals** on the astral map: bright cyan when open, faint when closed.
- **The order objective's bonfires** in the game's colours: blue while waiting, green when lit in the right order, red when wrong.
- **Objective pieces**, each as the game's own icon: the collect objective's mushrooms in blue (following them as they run), the llamas to herd in blue (faded once in the pen), the objects to push in yellow (blue once in the zone), and the statues to break in grey with blue eyes. Pieces that still have to go somewhere get a faint dotted line to their zone. When the game starts pointing at a piece from the edge of the screen, the minimap pins that piece to its own edge too. The push zone (blue) and the herding pen (green) are drawn at their real size with the game's icon in the middle, and a counter at the top shows how many are done ("3/8"), in the game's own font.
- **Hunt targets** get the game's hunt icon in gold that follows them, pinned to the edge when they're off the map.
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
- `[Layers]` `ShowEnemies`, `BigEnemyHealth` (3: how much tougher than the swarm an enemy must be to show as an orange triangle), `HeavyEnemyHealth` (8: the same for a pink square), `ShowObjectives`, `ShowPickups`, `ShowTeammates`, `ShowMoreEvents`, `ShowMapFeatures` (geysers, tornadoes, portals), `HideUndiscoveredObjectives` (true: objectives, their pieces, zones and pickups only once found)
- `[Debug]` `SaveDebugImages` (false): saves the pictures behind the minimap to `BepInEx/minimap-debug`

## Multiplayer

Install it if you want to see a minimap; nobody else needs it. The only thing it sends over the network is which objectives, pieces and pickups have been found, and only to players who also run it. If the host doesn't run it, each player's minimap shows just what they found themselves.

## Notes

- If a level has no navigation mesh, the minimap shows dots without a layout (radar mode) and says so in the BepInEx log.
- The layout is mapped while you drop in (a small slice of work each frame) and shown in flat colours until you land; a second after landing it switches to the level picture.

## Source and bug reports

https://github.com/stefanws0/Spell-Bridge-Minimap

## Changelog

- **1.2.3**: a new icon, and a new package name: **Minimap** (was "SpellBrigadeMinimap"; uninstall the old one if you have it).
- **1.2.2**: an objective now shows on the minimap the moment the game flashes or shows its icon at the edge of the screen. The minimap read the wrong signal for that, so until now objectives only appeared once someone had them on screen.
- **1.2.1**: removes the Treasure Maps layer (its search circle and map pieces) and the `ShowTreasure` setting.
- **1.2.0**: everything the game points at from the edge of the screen is on the minimap too. An objective shows as soon as the game points at it (before, it waited until someone had it on screen), and objectives, mushrooms and pieces the game points at stick to the minimap's edge when they're out of range. Enemies get a shape per rank (circle, triangle, square, star) with a new heavy rank (pink squares; setting `HeavyEnemyHealth`), and the game's hidden "!" danger pointers are no longer drawn on enemies. Every objective and piece is drawn with the game's own icon and a dark edge, coloured per objective (blue for most; the herding pen green; objects to push yellow until they're in the zone; statues grey with blue eyes; the hunt target gold; the sacrifice red; the clean objective in its goo's colour). Each piece, zone, pickup and Treasure Maps map piece now stays hidden until someone has had it on screen or the game points at it, one by one (a whole order objective no longer appears when one bonfire is found). The counters use the game's font.
- **1.1.0**: shows Treasure Maps hunts (search circle and map pieces) when that mod is installed. New setting: `ShowTreasure`.
- **1.0.2**: objective pieces with guide lines and counters, zones at real size, hunt-target crosshair.
- **1.0.1**: moves out of the way of the game's HUD in its corner; dead teammates as ghosts in their colour; your arrow turns while you're a ghost; order-objective mushrooms coloured by state; the fire map's arena edge.
- **1.0.0**: first release.
