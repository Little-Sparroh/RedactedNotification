# RedactedNotification

A BepInEx mod for Mycopunk that alerts you when the rare **ERROR REDACTED** mission modifier appears on the mission
board — including while you are mid-mission and the board refreshes.

## Features

- **Board watch**: Tracks the current mission board seed and detects ERROR REDACTED on any listed mission
- **Mid-mission alerts**: Lives on the player HUD so you still get notified when the board rotates during a run
- **Hidden by default**: Only shows when ERROR REDACTED is present
- **Alert blink**: Soft alpha pulse on text + icon when the modifier is on the board
- **Modifier icon**: Uses the real ERROR REDACTED icon and color when available
- **Optional idle mode**: Persistent grey indicator with “no ERROR detected.” when the modifier is absent
- **Repositionable**: Soft integration with ModSettingsMenu HUD repositioning when available

## Getting Started

### Dependencies

- Mycopunk (base game)
- [BepInEx](https://github.com/BepInEx/BepInEx) - Version 5.4.2403 or compatible
- [SparrohUILib](https://thunderstore.io/c/mycopunk/p/Sparroh/SparrohUILib/) (`Sparroh-SparrohUILib`)

### Building/Compiling

```bash
dotnet build --configuration Release
```

### Installing

**Via Thunderstore (Recommended)**:

1. Install via Thunderstore / r2modman
2. Ensure **SparrohUILib** is installed as a dependency

**Manual Installation**:

1. Place `RedactedNotification.dll` in `BepInEx/plugins/`
2. Place `SparrohUILib.dll` in `BepInEx/plugins/` (or install the SparrohUILib package)

## Configuration

Config file: `BepInEx/config/sparroh.redactednotification.cfg`

### General

| Option                   | Default                        | Description                                                           |
|--------------------------|--------------------------------|-----------------------------------------------------------------------|
| `Enable Notification`    | `true`                         | Master toggle for the notification HUD                                |
| `Show When Not Detected` | `false`                        | Always show a grey idle state when ERROR REDACTED is not on the board |
| `Alert Text`             | `ERROR REDACTED has appeared!` | Text when the modifier is on the board                                |
| `Idle Text`              | `no ERROR detected.`           | Text for idle mode                                                    |
| `Enable Blink`           | `true`                         | Soft alpha pulse on the alert so it is easier to notice               |
| `Blink Speed`            | `2.0`                          | Blink cycles per second (try 1–3)                                     |
| `Blink Min Alpha`        | `0.35`                         | Dim-phase alpha (higher = subtler)                                    |

### HUD Positioning

| Option           | Default | Description                     |
|------------------|---------|---------------------------------|
| `Notification X` | `0.50`  | Normalized HUD X position (0–1) |
| `Notification Y` | `0.12`  | Normalized HUD Y position (0–1) |

Config hot-reloads when the file changes.

## How it works

The game regenerates the mission board from `Global.MissionSelectSeed` (about every 10 minutes). This mod:

1. Resolves the ERROR REDACTED `MissionModifier` from `Global.Instance.MissionModifiers`
2. Reconstructs the current board from that seed (same logic as mission select)
3. Shows a reticle HUD alert when any board mission includes the modifier

Opening mission select also feeds live button data so special/custom missions are covered.

## Authors

- Sparroh

## License

MIT — see LICENSE
