# Minimap

A round minimap for The Spell Brigade, in the corner of the screen and styled like the game's own HUD.

It shows the arena with its walls and rocks, enemies (a different shape per tier), bosses, objectives, pickups,
teammates, and things like geysers, tornadoes and portals. Objectives show up once someone in your party has seen them.

Install it from Thunderstore with r2modman or Gale:
[StrayingFox/Minimap](https://thunderstore.io/c/the-spell-brigade/p/StrayingFox/Minimap/). Keys and settings are in
[thunderstore/README.md](thunderstore/README.md).

## Building

You need the .NET SDK and the game with BepInExPack IL2CPP, started once so BepInEx has made its `interop` folder.
The build uses r2modman's Default profile; for another one, add `-p:BepInExDir=<your BepInEx folder>`.

```bash
dotnet build src/Plugin -c Release
```

Tests: `dotnet run --project tests/Core.Tests -c Release`. Thunderstore zip: `powershell -ExecutionPolicy Bypass -File package.ps1`.

## My other mods

- [Dash](https://github.com/stefanws0/Spell-brigade-dash)
- [Mouse Ping](https://github.com/stefanws0/Spell-brigade-mouse-ping)

MIT licensed.
