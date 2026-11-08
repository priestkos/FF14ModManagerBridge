# FF14 Mod Manager Bridge

<img src="images/icon.png" width="96">

A Dalamud plugin that lets the **[FF14 Mod Manager](https://github.com/priestkos/ff14-mod-manager)** Windows app see your character and apply Glamourer designs and Penumbra mods for you.

No commands · no window · Dalamud API 15 · .NET 10

> **No support will be provided. This software is to be used as is, without warranty of any kind.**

## What it does

The app works without it. With the Bridge running, the app can also:

- show who you are and which Penumbra collection is on you ("In game:" at the top right of the app),
- **Apply to my character**: put a Glamourer design on you,
- **Enable / Disable in collection**: switch a Penumbra mod in a collection, then redraw you so it shows straight away,
- save your character, world, collection and last applied design next to each screenshot.

## How it works

It opens a named pipe, `FF14ModManager.Bridge`, with `PipeOptions.CurrentUserOnly`, so only your own Windows user can connect. The app sends one JSON line and gets one back:

| Request | Does | Reply |
| --- | --- | --- |
| `{"cmd":"status"}` | reads you and the collection on you | `{ok, character, world, collection, collectionId}`; just `{ok}` at the title screen |
| `{"cmd":"applyDesign","id":"<design guid>"}` | Glamourer `ApplyDesign` on you: one-off, equipment + customisation | `{ok}` or `{ok:false, error}` |
| `{"cmd":"setMod","collection":"<guid>","dir":"<mod folder>","enabled":true}` | Penumbra `TrySetMod`, then a redraw of you | `{ok}` or `{ok:false, error}` |

Every game and IPC call runs on Dalamud's framework thread. A failing call answers `{ok:false, error}` instead of throwing, and the pipe keeps serving. The protocol is in `FF14ModManagerBridge/Core/Bridge.cs`, and **the same file is in the app's repo** (`App/Core/Bridge.cs`): change both together.

## Commands

None. The plugin has no window, no settings and no slash command; it only answers the app.

## Build

Requires the .NET 10 SDK and the Dalamud dev distribution (`%APPDATA%\XIVLauncher\addon\Hooks\dev`).

```
dotnet build FF14ModManagerBridge/FF14ModManagerBridge.csproj -c Release
```

Output: `FF14ModManagerBridge/bin/Release/FF14ModManagerBridge.dll`, with the plugin zip in `FF14ModManagerBridge/bin/Release/FF14ModManagerBridge/latest.zip`.

## Releases

Each push to `main` that changes the plugin (not docs-only paths) creates a GitHub release automatically; the version is the next four-part number above the latest release tag (or the csproj version when that is higher).

## Install

From [PlogonYes](https://github.com/priestkos/PlogonYes): `/xlsettings` → Experimental → Custom Plugin Repositories → paste `https://raw.githubusercontent.com/priestkos/PlogonYes/main/pluginmaster.json`, tick it, Save. Then `/xlplugins` → **FF14 Mod Manager Bridge** → Install.

As a dev plugin:

1. `/xlsettings` → Experimental → Dev Plugin Locations → add `FF14ModManagerBridge/bin/Debug/FF14ModManagerBridge.dll` (from `dotnet build FF14ModManagerBridge/FF14ModManagerBridge.csproj -c Debug`).
2. `/xlplugins` → Dev Tools → enable **FF14 Mod Manager Bridge**.

## Tests

The pipe protocol is Dalamud-free, so it runs outside the game over a real named pipe:

```
dotnet run --project FF14ModManagerBridge.Tests
# ALL PASS (5 checks)
```

Covers the round trip, a failing handler becoming `{ok:false}`, the server surviving an error, and the client answering "not connected" quickly when no plugin is running.

## Limits

- The Penumbra and Glamourer calls only run inside the game, so CI builds them but cannot exercise them.
- Glamourer has no IPC that says which design is on you; the app remembers the last design it applied.
- Applying a design uses Glamourer's defaults (equipment + customisation, one-off, not locked).

## Layout

```
FF14ModManagerBridge/Plugin.cs        services, pipe server, the three commands
FF14ModManagerBridge/Core/Bridge.cs   pipe protocol, shared with the app
FF14ModManagerBridge.Tests/Program.cs offline pipe harness
images/                               icon.svg (source) and icon.png (manifest icon)
.github/workflows/                    build.yml (build + tests on every push), release.yml (release on main)
```

Built against Penumbra.Api 5.19.2 and Glamourer.Api 2.8.2 (both MIT).
