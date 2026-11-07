# FF14 Mod Manager Bridge

A Dalamud plugin that runs in the game for the **FF14 Mod Manager** Windows app ([priestkos/ff14-mod-manager](https://github.com/priestkos/ff14-mod-manager)). The app works without it. With it, the app can also:

- show which character you're on and which Penumbra collection is on you ("In game:" at the top right of the app),
- apply a Glamourer design to your character (**Apply to my character**),
- enable or disable a Penumbra mod in a collection (**Enable/Disable in collection**); you are redrawn so the change shows straight away,
- record character, world, collection and last applied design next to each screenshot.

It has no window and no settings.

## How it talks to the app

A named pipe, `FF14ModManager.Bridge`, opened with `PipeOptions.CurrentUserOnly` on both ends, so only your Windows user can connect. Each connection carries one JSON request line and one JSON reply line:

| Request | Does | Reply |
|---|---|---|
| `{"cmd":"status"}` | reads you and your collection | `{ok, character, world, collection, collectionId}` (`ok` only, at the title screen) |
| `{"cmd":"applyDesign","id":"<design guid>"}` | Glamourer `ApplyDesign` on you: one-off, equipment + customisation | `{ok}` or `{ok:false, error}` |
| `{"cmd":"setMod","collection":"<guid>","dir":"<mod folder>","enabled":true}` | Penumbra `TrySetMod`, then a redraw of you | `{ok}` or `{ok:false, error}` |

Every game and IPC call runs on Dalamud's framework thread. The protocol lives in `FF14ModManagerBridge/Core/Bridge.cs`, and **the same file is in the app's repo** (`App/Core/Bridge.cs`). Change both together.

## Install (dev plugin)

1. `dotnet build FF14ModManagerBridge/FF14ModManagerBridge.csproj -c Release`, or download the `plugin` artifact (`latest.zip`) from a green Actions run.
2. `/xlsettings` → Experimental → Dev Plugin Locations → add `<repo>\FF14ModManagerBridge\bin\Release\FF14ModManagerBridge.dll` → Save.
3. `/xlplugins` → Dev Tools → enable **FF14 Mod Manager Bridge**.

Built against Dalamud API 15 with Penumbra.Api 5.19.2 and Glamourer.Api 2.8.2 (both MIT).

## Tests

```bash
dotnet run --project FF14ModManagerBridge.Tests   # pipe protocol over a real named pipe; exit code = failures
```

CI (`.github/workflows/build.yml`) installs the Dalamud distribution on the runner, builds the plugin, uploads `latest.zip`, and runs the protocol tests. CI cannot run the game, so the Penumbra and Glamourer calls are only exercised in game.
