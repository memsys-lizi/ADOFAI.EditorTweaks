# ADOFAI Editor Tweaks

Language: English | [中文](README.zh-CN.md)

ADOFAI Editor Tweaks is a UnityModManager mod for **A Dance of Fire and Ice**. It focuses on small editor workflow improvements, fixes for custom-level editing edge cases, and a compact in-editor quick settings overlay.

The mod is built as a Harmony patch library targeting `net481`.

## Features

### Numeric Drag Fields

Adds drag-to-adjust behavior to numeric editor fields that normally behave like plain text boxes.

Supported controls:

- Integer fields
- Float fields
- Tile fields
- Vector2 fields

The implementation reuses the game's existing `DraggableNumberInputField` component and attaches it to supported `PropertyControl_Text` and `PropertyControl_Vector2` inputs. During drag, values are applied live to the selected event so decoration positions, background settings, sliders, and indicators refresh immediately.

Settings:

- Float step per pixel
- Int step per pixel
- Maximum float decimals

Files:

- `src/Features/NumericDrag/NumericDragFeature.cs`
- `src/Features/NumericDrag/NumericDragPatches.cs`
- `src/Features/NumericDrag/EditorTweaksNumericDragMarker.cs`

### Decoration Selection Fixes

Improves several decoration editing behaviors.

- Fixes dragging camera-relative decorations.
- Keeps camera-relative and camera-aspect decoration movement aligned with screen-space expectations.
- Preserves axis locking behavior while dragging.
- Fixes pivot cross placement for camera/parallax decorations.
- Adds configurable decoration move snapping.

Settings:

- Fix camera-relative decoration dragging
- Fix camera/parallax decoration pivot
- Decoration move snap step, where `0` disables snapping

Files:

- `src/Features/DecorationSelection/CameraRelativeDecorationDragPatches.cs`
- `src/Features/DecorationSelection/DecorationPivotPatches.cs`
- `src/Features/DecorationSelection/DecorationMoveSnapPatches.cs`

### Video Background Sync Fix

Fixes video backgrounds drifting or appearing delayed when previewing a level from the middle of the chart.

The base game starts video backgrounds once after `VideoPlayer.Prepare()` completes and sets `VideoPlayer.time` only once. Random seeking into longer videos can finish late, which is most visible when playback starts from a checkpoint or an editor-selected floor. This mod checks the video time against the conductor time during the startup window and corrects the video if it falls out of sync.

The target video time follows the game's timing model:

```text
songposition_minusi - countdownOffset + vidOffset
```

Looping videos are wrapped by video length when available.

File:

- `src/Features/VideoBackgroundSync/VideoBackgroundSyncPatches.cs`

### Editor Preferences Persistence

Persists official editor preferences immediately after a preference control changes. This avoids preference changes being lost when the game/editor does not save them at the expected time.

File:

- `src/Features/EditorPreferences/EditorPreferencesPersistencePatches.cs`

### In-Editor Quick Settings Overlay

Adds a draggable IMGUI overlay while editing a level. It provides quick access to the most frequently tuned values without opening the UMM settings panel.

The overlay includes:

- Decoration move snap step
- Float drag step per pixel
- Int drag step per pixel
- Maximum float decimals

The overlay remembers:

- Position
- Collapsed/expanded state

File:

- `src/Features/EditorOverlay/EditorTweaksOverlayWindow.cs`

### UMM Settings Panel

The UnityModManager settings panel exposes all feature toggles and numeric settings. Text input fields keep intermediate edit states, so values can be cleared or partially typed without being reformatted every frame.

File:

- `src/Settings.cs`

### Localization

User-facing strings are stored in JSON instead of hardcoded switches.

File:

- `Resources/localization.json`

Runtime loader:

- `src/Localization.cs`

The loader chooses Chinese text when the game language is Chinese, Chinese Simplified, or Chinese Traditional. Otherwise it falls back to English. Missing keys fall back to the key name.

## Project Layout

```text
.
├── ADOFAI.EditorTweaks.csproj
├── ADOFAI.EditorTweaks.slnx
├── ADOFAIMod.targets
├── Info.json
├── Resources/
│   └── localization.json
└── src/
    ├── Main.cs
    ├── Settings.cs
    ├── Localization.cs
    └── Features/
        ├── DecorationSelection/
        ├── EditorOverlay/
        ├── EditorPreferences/
        ├── NumericDrag/
        └── VideoBackgroundSync/
```

## Build

The project expects a local ADOFAI installation so it can reference the game's managed assemblies.

Default path in the project file:

```text
C:\Steam\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice.exe
```

Build with:

```powershell
dotnet build
```

`ADOFAIMod.targets` performs these steps after build:

- Copies the DLL and `Info.json` to `out/`.
- Copies `Resources/**` to `out/Resources/`.
- Deploys the mod to the game's `Mods/ADOFAI.EditorTweaks/` directory.
- Does not launch the game unless `AutoLaunchGame` is set to `true`.

## Runtime Entry

UnityModManager loads:

```text
ADOFAI.EditorTweaks.Main.Load
```

`Main.Load` initializes settings, localization, UMM callbacks, and the Harmony instance. Patches are applied when the mod is enabled and removed when disabled.

## Development Notes

- Keep feature-specific Harmony patches under `src/Features/<FeatureName>/`.
- Prefer small patches that preserve the base game's behavior and only adjust the broken or missing part.
- Store user-facing text in `Resources/localization.json`.
- When adding a setting, update `Settings.cs`, `Resources/localization.json`, and any relevant overlay/UI code.
- The project enables nullable reference types, so new code should keep null handling explicit.

## Verification

At minimum, run:

```powershell
dotnet build
```

Manual checks worth doing in-game:

- Open the UMM settings panel and verify localized labels display correctly.
- Open a custom level in the editor and confirm the quick settings overlay appears.
- Drag numeric fields in the event inspector.
- Drag camera-relative decorations.
- Start playback from the middle of a chart with a video background and confirm video/audio sync.
