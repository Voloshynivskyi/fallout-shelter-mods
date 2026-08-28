# Nexus page copy — Caps Foundry

Everything below is ready to paste. The description block is BBCode, which is what the Nexus
editor uses.

## Mod name

    Caps Foundry - a new room that produces caps

## Summary (the one-liner under the title)

    A genuinely new buildable room, not a reskin. It appears in the build menu, is staffed like any other room, upgrades through three levels, merges up to three wide, and pays in caps. Luck decides how fast it runs.

## Category

Gameplay (secondary: Rooms, if the game has that category)

## Tags

    room, caps, production, economy, bepinex, new content, gameplay

## Description

[size=4][b]A new room, not a reskin[/b][/size]

Fallout Shelter never lets you produce caps. Every other resource has a room; caps come from quests, rushing and selling. This mod adds a room that makes them.

The Caps Foundry is a full production room. It sits in the build menu with its own name, price and icon, is staffed by dwellers like any other room, upgrades through three levels, and merges up to three segments wide. [b]Luck[/b] is its stat, so the dwellers nobody else wants finally have a job.

It is built on the Nuclear Reactor's model, recoloured so it does not read as a reactor standing in the wrong place.

[size=4][b]What it produces[/b][/size]

[code]
Room level    Caps per cycle (1 / 2 / 3 wide)    Cycle
Level 1       200 / 400 / 600                    4 hours
Level 2       200 / 400 / 600                    3 hours
Level 3       200 / 400 / 600                    2 hours
[/code]

Cycle length is divided by your workers' efficiency, exactly like a vanilla room. A wider room produces proportionally more per cycle rather than cycling faster.

At full efficiency a maxed three-wide Foundry yields 600 caps every two hours. That is deliberate: enough to matter, slow enough that it does not replace playing the game.

The room costs the same as a Nuka-Cola Bottler to build and unlocks alongside it, so it arrives at the point in a vault's life where caps start to bite.

[size=4][b]Requirements[/b][/size]

[list]
[*]Fallout Shelter for PC (Steam, Bethesda or Windows Store) — the Unity [b]Mono[/b] build
[*][url=https://github.com/BepInEx/BepInEx/releases]BepInEx 5.x[/url] — the [b]x64, Unity Mono[/b] variant
[*]Tested against Fallout Shelter 2.5.1
[/list]

[size=4][b]Installation[/b][/size]

[list=1]
[*]If you do not have BepInEx: download [b]BepInEx_win_x64_5.4.x.x.zip[/b] and extract it into your Fallout Shelter game folder — the one containing [b]FalloutShelter.exe[/b]. You should end up with [b]winhttp.dll[/b], [b]doorstop_config.ini[/b] and a [b]BepInEx[/b] folder next to the exe. Launch the game once and close it so BepInEx creates its folders.
[*]Extract this mod into the same game folder, so the DLL lands at [b]BepInEx\plugins\CapsFoundry.dll[/b].
[*]Launch the game. The Foundry appears in the build menu once the Nuka-Cola Bottler is unlocked.
[/list]

To find your game folder: Steam, right-click Fallout Shelter, Manage, Browse local files.

To confirm it loaded, open [b]BepInEx\LogOutput.log[/b] and look for:

[code]
[Info   :Caps Foundry] Caps Foundry 1.3.2 ready (21 patches).
[Info   :Caps Foundry] Registered 'Caps Foundry' as ProteinBar (cloned from Geothermal); registry 29 -> 30 entries.
[/code]

[size=4][b]Uninstalling — please read this[/b][/size]

[b]A vault containing a Caps Foundry will not load without this mod.[/b] The room is saved under a room type the base game has no assets for, so removing the DLL makes that vault fail to open.

To remove the mod safely:
[list=1]
[*]Sell every Caps Foundry, in every vault.
[*]Save and quit.
[*]Delete [b]BepInEx\plugins\CapsFoundry.dll[/b].
[/list]

If you already deleted it and a vault will not open, [b]nothing is lost[/b] — put the DLL back and the vault opens again.

The mod never writes to your save files and keeps no backups of its own. Your saves live in [b]%LocalAppData%\FalloutShelter\[/b] if you want to copy them somewhere before experimenting.

[size=4][b]Configuration[/b][/size]

Settings are in [b]BepInEx\config\ovolo.falloutshelter.capsfoundry.cfg[/b], created on first launch. Edit it and restart the game.

You can change the caps per cycle, the cycle length at each level, the build price, which room it unlocks alongside, its name, and its colour. You can also point it at a different room's 3D model if you would rather it looked like something else.

If you are filing a bug report, set [b]VerboseLogging = true[/b] and attach [b]BepInEx\LogOutput.log[/b]. Left off, the mod writes four lines a session.

[size=4][b]Compatibility[/b][/size]

Works alongside other BepInEx mods. It adds a room rather than changing existing ones, so it does not conflict with balance or UI mods. Each patch is applied separately, so if a game update breaks one part the rest keeps working and the failure is named in the log.

[size=4][b]Credits[/b][/size]

Made by ovolo. Source and issue tracker: [url=https://github.com/Voloshynivskyi/fallout-shelter-mods]github.com/Voloshynivskyi/fallout-shelter-mods[/url]

MIT licensed — do what you like with it, including bundling it, as long as the licence comes along.

## File to upload

    D:\FalloutShelter-Mods\CapsFoundry\dist\CapsFoundry-1.3.2.zip

File name on Nexus:  Caps Foundry 1.3.2
Version:             1.3.2
