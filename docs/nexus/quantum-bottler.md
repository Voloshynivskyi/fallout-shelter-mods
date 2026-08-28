# Nexus page copy — Quantum Bottler

Everything below is ready to paste. The description block is BBCode, which is what the Nexus
editor uses.

## Mod name

    Quantum Bottler - the Nuka-Cola Bottler produces Quantum

## Summary (the one-liner under the title)

    The Nuka-Cola Bottler stops making food and water and starts making Nuka-Cola Quantum, at a slow, deliberately balanced rate. Quantum becomes something you build toward instead of something you buy or cheat in.

## Category

Gameplay (secondary: Rooms, if the game has that category)

## Tags

    quantum, nuka cola, production, economy, bepinex, gameplay, balance

## Description

[size=4][b]Quantum you can actually produce[/b][/size]

In the base game Nuka-Cola Quantum is a premium currency. Lunchboxes, quests, events — that is the whole list. No room makes it, so the only reliable way to get more is to pay.

This mod turns the Nuka-Cola Bottler into a real Quantum production room. It stops producing food and water entirely and produces Quantum instead, slowly enough that Quantum stays worth something.

The Luck-based caps bonus the Bottler gives is left exactly as it is in vanilla.

[size=4][b]How slow is slow[/b][/size]

Time to produce one bottle, at full worker efficiency:

[code]
Room level    1 wide    2 wide    3 wide
Level 1       4h        2h        1h 20m
Level 2       3h        1h 30m    1h
Level 3       2h        1h        40m
[/code]

A fresh one-segment Bottler takes four hours per bottle. A fully upgraded three-wide one manages a bottle every forty minutes. Rushing works normally, and the room accumulates a whole bottle before it can be collected, like any other production room — you never collect a fraction.

Every number above is configurable if you disagree with the balance.

[size=4][b]Requirements[/b][/size]

[list]
[*]Fallout Shelter for PC (Steam, Bethesda or Windows Store) — the Unity [b]Mono[/b] build
[*][url=https://github.com/BepInEx/BepInEx/releases]BepInEx 5.x[/url] — the [b]x64, Unity Mono[/b] variant
[*]Tested against Fallout Shelter 2.5.1
[/list]

[size=4][b]Installation[/b][/size]

[list=1]
[*]If you do not have BepInEx: download [b]BepInEx_win_x64_5.4.x.x.zip[/b] and extract it into your Fallout Shelter game folder — the one containing [b]FalloutShelter.exe[/b]. You should end up with [b]winhttp.dll[/b], [b]doorstop_config.ini[/b] and a [b]BepInEx[/b] folder next to the exe. Launch the game once and close it so BepInEx creates its folders.
[*]Extract this mod into the same game folder, so the DLL lands at [b]BepInEx\plugins\NukaColaQuantumProduction.dll[/b].
[*]Launch the game.
[/list]

To find your game folder: Steam, right-click Fallout Shelter, Manage, Browse local files.

To confirm it loaded, open [b]BepInEx\LogOutput.log[/b] and look for:

[code]
[Info   :Nuka-Cola Quantum Production] Nuka-Cola Quantum Production 1.12.2 ready (11 patches). Hours per bottle at size 1: L1=4 L2=3 L3=2.
[/code]

[size=4][b]Uninstalling[/b][/size]

Delete [b]BepInEx\plugins\NukaColaQuantumProduction.dll[/b].

Your Bottler goes back to food and water on the next launch. Nothing this mod does is written into your save — it only changes behaviour while the game is running, so removing it is completely safe and reversible. Quantum you already collected stays yours.

[size=4][b]Configuration[/b][/size]

Settings are in [b]BepInEx\config\ovolo.falloutshelter.nukaquantum.cfg[/b], created on first launch. Edit it and restart the game.

You can set the hours per bottle at each room level, and choose whether to keep or remove the vanilla Luck caps bonus.

If you are filing a bug report, set [b]VerboseLogging = true[/b] and attach [b]BepInEx\LogOutput.log[/b]. Left off, the mod writes two lines a session.

[size=4][b]Compatibility[/b][/size]

Works alongside other BepInEx mods. It changes one room's output and nothing else. Each patch is applied separately, so if a game update breaks one part the rest keeps working and the failure is named in the log.

[size=4][b]Credits[/b][/size]

Made by ovolo. Source and issue tracker: [url=https://github.com/Voloshynivskyi/fallout-shelter-mods]github.com/Voloshynivskyi/fallout-shelter-mods[/url]

MIT licensed — do what you like with it, including bundling it, as long as the licence comes along.

## File to upload

    D:\FalloutShelter-Mods\QuantumBottler\dist\NukaColaQuantumProduction-1.12.2.zip

File name on Nexus:  Quantum Bottler 1.12.2
Version:             1.12.2
