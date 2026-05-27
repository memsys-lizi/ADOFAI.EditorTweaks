# ADOFAI Editor Tweaks

Language: English | [中文](README.zh-CN.md)

ADOFAI Editor Tweaks is a UnityModManager mod for **A Dance of Fire and Ice**. It focuses on editor workflow improvements, fixes for custom-level editing edge cases, a compact in-editor quick settings overlay, and an offline chart video renderer.

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

### Chart Video Renderer

Exports the currently loaded editor chart to an MP4 video without recording the desktop or editor UI.

The renderer starts playback through the game's normal editor play path so chart setup, decorations, camera movement, filters, video backgrounds, hitsounds, hold sounds, and `PlaySound` events are initialized by the game itself. Rendering is offline and deterministic: the output profile is fixed to `1920x1080 @ 60fps`, with CRF 18 and the `veryfast` encoder preset applied internally.

Pipeline:

- Captures the chart camera output to a dedicated `RenderTexture`.
- Reads frames with `AsyncGPUReadback` to avoid the old blocking CPU readback path.
- Streams raw RGBA frames directly into FFmpeg instead of writing PNG sequences.
- Prefers hardware H.264 encoding when FFmpeg can use it, then falls back to software encoding.
- Builds the final audio mix from the song plus captured gameplay sounds, including hitsounds, hold loops, and scheduled sound events.
- Suppresses UI/menu sounds during rendering so UMM clicks and interface audio are not exported.
- Stops after the level reaches the end, then records a configurable tail duration. The default tail is 5 seconds.

Renderer controls:

- Workspace directory for temporary files.
- Export directory for final MP4 files.
- End-of-level tail duration.
- Whether hit judgment labels such as Perfect, early, and late are visible during export.

The renderer requires FFmpeg at `Tools/ffmpeg.exe` inside the deployed mod folder. The build target downloads it automatically when `tools/ffmpeg.exe` is missing from the repository checkout, so the binary does not need to be committed.

Files:

- `src/Features/ChartRendering/ChartRenderSession.cs`
- `src/Features/ChartRendering/ChartFrameCapture.cs`
- `src/Features/ChartRendering/ChartRenderAudioMix.cs`
- `src/Features/ChartRendering/ChartRenderAudioPatches.cs`
- `src/Features/ChartRendering/ChartRenderJudgmentPatches.cs`
- `src/Features/ChartRendering/FfmpegEncoder.cs`
- `scripts/EnsureFfmpeg.ps1`

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
- Chart renderer status and render button
- Hit judgment visibility toggle for video exports

The overlay remembers:

- Position
- Collapsed/expanded state

While rendering, the overlay shows a modal progress panel with written frames, output speed, duplicated-frame count, ETA, current stage, and a cancel button.

File:

- `src/Features/EditorOverlay/EditorTweaksOverlayWindow.cs`

### UMM Settings Panel

The UnityModManager settings panel exposes feature toggles, numeric editor settings, and chart renderer directories. Text input fields keep intermediate edit states, so values can be cleared or partially typed without being reformatted every frame.

Chart render width, height, FPS, CRF, and preset are intentionally fixed by the mod to the default `1920x1080 @ 60fps` profile. Users only configure where files are written and how the render behaves.

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
├── scripts/
│   └── EnsureFfmpeg.ps1
├── tools/
│   └── ffmpeg.exe        # downloaded locally, ignored by git
├── Resources/
│   └── localization.json
└── src/
    ├── Main.cs
    ├── Settings.cs
    ├── Localization.cs
    └── Features/
        ├── ChartRendering/
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

- Downloads FFmpeg from the Gyan.dev release archive when `tools/ffmpeg.exe` is missing.
- Copies the DLL and `Info.json` to `out/`.
- Copies `Resources/**` to `out/Resources/`.
- Copies `tools/ffmpeg.exe` to `out/Tools/ffmpeg.exe`.
- Deploys the mod to the game's `Mods/ADOFAI.EditorTweaks/` directory.
- Does not launch the game unless `AutoLaunchGame` is set to `true`.

`tools/ffmpeg.exe` is intentionally ignored by git because it is large. Keep the script and build target in source control, not the binary.

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
- When touching chart rendering, check the render session, overlay, audio mix, FFmpeg command, and localization together. Those pieces are tightly connected.
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
- Load a custom chart, render a video, and confirm the MP4 is `1920x1080 @ 60fps`.
- Confirm the exported video does not include the UMM panel, editor UI, render modal, or menu sounds.
- Toggle hit judgments off and confirm Perfect/early/late labels are hidden only during export.
- Confirm the render continues for the configured tail duration after the level ends.
- Confirm song audio, hitsounds, hold sounds, and `PlaySound` events stay aligned with the video.
