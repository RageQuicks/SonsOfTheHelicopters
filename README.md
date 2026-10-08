# Rust Helicopter: a Sons of the Forest mod

A [RedLoader](https://github.com/ToniMacaroni/RedLoader) mod that adds a flyable **Rust-style minicopter** to Sons of
the Forest. Type `/helicopter` and one is placed on the ground in front of you with a **full fuel tank**, ready to fly.

The minicopter is built in-game from simple shapes, styled after Rust's: an open scrap-metal cockpit with a roll cage,
skids, an engine and red fuel tank behind the seat, a two-blade main rotor and a tail rotor. It flies like Rust's,
burns fuel, and can be wrecked in a crash.

> **Status:** written against the RedLoader SDK source and APIs used by other published mods, and compile-checked,
> but **not yet tested in the game**. Please report anything that misbehaves (see [Troubleshooting](#troubleshooting)).

## Spawning

| Where | Type | Notes |
|---|---|---|
| Multiplayer chat (you are the host) | `/helicopter` | Press Enter to open chat. RedLoader runs chat commands on the host only. |
| Game console (press **F1**) | `helicopter` | Works in single player and as host. `/helicopter` is also registered in the console. |

Single player has no chat, so use the F1 console there.

Options (same in chat and console):

| Command | What it does |
|---|---|
| `helicopter` | Spawns a minicopter on open, flat ground in front of you, with a full tank. |
| `helicopter refuel` | Fills the tank of the helicopter you're flying, or the nearest one. |
| `helicopter clear` | Removes every spawned helicopter. |
| `helicopter help` | Lists the options. |

The spawner looks for clear ground about 7 m ahead (configurable). If something is in the way, it tries other
directions, then gives up with a message telling you to move to a clearing.

## Flying (Rust controls)

Walk up to the minicopter and press **E** to get in. Press **E** again to get out.

| Input | Action |
|---|---|
| **W** / **S** | Throttle up (climb) / down (descend). With neither held, it holds altitude. |
| **A** / **D** | Turn left / right |
| **Mouse forward / back** | Pitch the nose down / up (fly forward / back) |
| **Mouse left / right** | Roll left / right (strafe) |
| **Arrow keys** | Pitch and roll from the keyboard, if you prefer |
| **Left Alt** (hold) | Free look: look around without steering |
| **V** | Switch between the cockpit camera and the chase camera |
| **E** | Get in / get out |

- **Engine:** like in Rust, it starts when you throttle up and the rotor needs about 3 seconds to spin up before
  you lift off. It shuts off when you get out. In the air, the helicopter then sinks while the rotor winds down.
- **Fuel:** every helicopter spawns with a full 100 L tank. It burns 0.5 L/min idling and up to 2 L/min at full
  climb (about an hour of flying). When the tank runs dry the engine cuts out; refuel with `helicopter refuel`.
- **Damage:** hard impacts damage the hull. At 0% it's wrecked: the engine dies, you're thrown out, and the wreck
  disappears after a minute. Normal landings, even descending at full S, don't cause damage.
- **HUD:** while flying, the bottom-right panel shows fuel, hull, engine state, rotor speed, height above the ground,
  speed and climb rate. Next to a helicopter, a prompt shows its fuel and hull.

## Installing

### 1. RedLoader

Install RedLoader into the game first and start the game once, so it can generate its files.

- Current game version (2.x): RedLoader for .NET 10, **0.9.2 or later**, from
  [enderfish/RedLoader](https://github.com/enderfish/RedLoader/releases).
- Older setups: RedLoader 0.8.x (.NET 6) from [ToniMacaroni/RedLoader](https://github.com/ToniMacaroni/RedLoader/releases)
  also works. The build picks whichever one is installed.

### 2. Build the mod

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The project compiles against the game code
RedLoader generated in your game folder, so build it on the PC where the game is installed:

```
cd SonsOfTheHelicopters
dotnet build -c Release -t:Deploy
```

`-t:Deploy` copies the mod into the game's `Mods` folder:

```
Sons Of The Forest\Mods\RustHelicopter.dll
Sons Of The Forest\Mods\RustHelicopter\manifest.json
```

If the game isn't in Steam's default folder (`C:\Program Files (x86)\Steam\steamapps\common\Sons Of The Forest`),
add `-p:GamePath="D:\SteamLibrary\steamapps\common\Sons Of The Forest"`, or create `GamePath.user.props` next to
`RustHelicopter.csproj` (git-ignored):

```xml
<Project><PropertyGroup><GamePath>D:\SteamLibrary\steamapps\common\Sons Of The Forest</GamePath></PropertyGroup></Project>
```

`dotnet build -c Release -t:Package` instead makes a zip, laid out like the game folder, in `dist\` for sharing.

To uninstall, delete `Mods\RustHelicopter.dll` and the `Mods\RustHelicopter` folder.

## Settings

Change settings in the game's mod settings menu (RedLoader's mod list), or edit `UserData\RustHelicopter.cfg`.

| Setting | Default | |
|---|---|---|
| Fuel capacity | 100 L | New helicopters spawn full. |
| Fuel use at idle / full throttle | 0.5 / 2 L per min | |
| Climb power | 0.65 | Extra lift at full throttle, as a fraction of hover thrust. |
| Max tilt | 35° | More tilt means more speed (about 100 km/h at 35°). |
| Turn speed | 70°/s | |
| Auto level | 10°/s | How fast it levels out when you stop moving the mouse. 0 keeps the tilt you set. |
| Mouse flight sensitivity / invert pitch | 1.5 / off | |
| Crash damage | on | |
| Start in third person / chase camera distance | off / 9 m | |
| Free look sensitivity | 2 | |
| Hide player body while flying | on | Keeps your character from clipping through the cockpit. |
| Spawn distance | 7 m | |
| Keys: enter/exit, camera, free look | E, V, LeftAlt | Unity `KeyCode` names. |

## Multiplayer

Helicopters are **not networked**: each one exists only on the PC that spawned it. Other players can't see it, and
will most likely see you moving through the air without it. That's fine for single player and for fooling around
as host, but it's not a shared vehicle. Syncing it would need a custom Bolt network entity, a possible follow-up.

- `/helicopter` in chat only works for the **host**. RedLoader handles chat commands on the host, and the mod only
  spawns for the person sitting at that PC, so other players get "no permission".
- Clients can use the F1 console if the host allows cheats.
- The mod is client-side (`"platform": "Client"`); it does nothing on a dedicated server.

Helicopters are not saved; they disappear when you quit or load another save.

## Troubleshooting

RedLoader's console window and `_Redloader\Latest.log` show the mod's messages, all prefixed `[RustHelicopter]`.

- **"Registered command 'helicopter'" missing from the log:** the mod didn't load. Check that `RustHelicopter.dll` is
  in `Mods` and `manifest.json` is in `Mods\RustHelicopter`, and that it was built for your RedLoader version.
- **Mouse still turns the camera while flying, or the view jitters:** the game may have changed how mouse look
  works. Report it with your game and RedLoader versions.
- **Can't type `/helicopter` in single player:** single player has no chat; press F1 and type `helicopter`.
- **Spawns somewhere odd:** it needs clear, fairly flat ground (under 30° slope) within about 11 m. Try again from a
  clearing.

## How it works

| File | |
|---|---|
| `src/RustHelicopterMod.cs` | Mod entry point: settings, the `helicopter` console command, enter/exit key handling. |
| `src/ChatCommand.cs` | `/helicopter` in multiplayer chat, through RedLoader's `ServerCommands`. |
| `src/HelicopterSpawner.cs` | Finds clear, flat ground near the player and spawns a helicopter there. |
| `src/MinicopterModel.cs` | Builds the minicopter model from Unity primitives with HDRP materials. |
| `src/HelicopterController.cs` | Rigidbody flight model, engine and rotor spool, fuel, crash damage. |
| `src/Pilot.cs` | Seats the local player: kinematic body, mouse look off, cockpit/chase camera, getting out. |
| `src/Hud.cs` | Flight panel and the "get in" prompt (IMGUI). |
| `src/WorldCast.cs` | Raycasts that ignore the player and the helicopters. |
| `src/Config.cs` | Settings. |

Flight model: the rotor pushes along the helicopter's up axis with enough thrust to hover, plus or minus the throttle,
and a little extra when tilted so that tilting turns into speed rather than lost height. Mouse and arrow keys set a
target pitch and roll that a rate controller steers towards. A/D set a turn rate. Vertical damping gives Rust's
"let go and it hovers" feel. Everything scales with rotor speed, so lift builds as the rotor spins up and fades
when the engine stops.
