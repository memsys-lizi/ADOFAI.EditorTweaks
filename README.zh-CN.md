# ADOFAI Editor Tweaks

语言：[English](README.md) | 中文

ADOFAI Editor Tweaks 是一个用于 **A Dance of Fire and Ice** 的 UnityModManager Mod。它专注于改善编辑器工作流、修复自定义谱面编辑中的一些边界问题，并提供一个紧凑的编辑器内快捷设置浮窗。

该 Mod 是一个基于 Harmony patch 的 `net481` 类库。

## 功能

### 数值拖动输入框

为原本只能手动输入的数值编辑器字段添加拖动调节能力。

支持的控件：

- 整数字段
- 小数字段
- 地板字段
- Vector2 字段

实现方式是复用游戏已有的 `DraggableNumberInputField` 组件，并将它附加到支持的 `PropertyControl_Text` 和 `PropertyControl_Vector2` 输入框上。拖动过程中会实时把数值应用到当前选中的事件，因此装饰位置、背景设置、滑条和事件指示器都会立即刷新。

相关设置：

- 小数每像素步进
- 整数每像素步进
- 小数最大位数

相关文件：

- `src/Features/NumericDrag/NumericDragFeature.cs`
- `src/Features/NumericDrag/NumericDragPatches.cs`
- `src/Features/NumericDrag/EditorTweaksNumericDragMarker.cs`

### 装饰选择修复

改善多个装饰编辑行为。

- 修复镜头相对装饰的拖动。
- 让 Camera 和 CameraAspect 相对装饰的移动符合屏幕空间直觉。
- 保留拖动时的 Shift 轴锁定行为。
- 修复镜头/视差装饰的轴心十字显示位置。
- 添加可配置的装饰移动吸附精度。

相关设置：

- 修复镜头相对装饰拖动
- 修复镜头/视差装饰轴心
- 装饰移动吸附精度，`0` 表示关闭吸附

相关文件：

- `src/Features/DecorationSelection/CameraRelativeDecorationDragPatches.cs`
- `src/Features/DecorationSelection/DecorationPivotPatches.cs`
- `src/Features/DecorationSelection/DecorationMoveSnapPatches.cs`

### 视频背景同步修复

修复从谱面中途预览时，视频背景漂移或延迟的问题。

原版游戏会在 `VideoPlayer.Prepare()` 完成后启动视频背景，并且只设置一次 `VideoPlayer.time`。对于较长视频，中途随机 seek 可能会因为解码延迟而落后，尤其是在从 checkpoint 或编辑器选中的地板开始播放时很明显。该 Mod 会在视频启动初期持续检查视频时间和谱面时间，并在不同步时进行校正。

目标视频时间遵循游戏原本的时间模型：

```text
songposition_minusi - countdownOffset + vidOffset
```

如果视频是循环播放，并且能读取到视频长度，则目标时间会按视频长度取模。

相关文件：

- `src/Features/VideoBackgroundSync/VideoBackgroundSyncPatches.cs`

### 编辑器偏好设置持久化

当官方编辑器偏好设置发生变化后立即保存，避免因为游戏或编辑器没有在预期时机保存而导致设置丢失。

相关文件：

- `src/Features/EditorPreferences/EditorPreferencesPersistencePatches.cs`

### 编辑器内快捷设置浮窗

在编辑谱面时显示一个可拖动的 IMGUI 浮窗，用于快速调整常用参数，不需要打开 UMM 设置面板。

浮窗包含：

- 装饰移动吸附精度
- 小数拖动每像素步进
- 整数拖动每像素步进
- 小数最大位数

浮窗会记住：

- 位置
- 折叠/展开状态

相关文件：

- `src/Features/EditorOverlay/EditorTweaksOverlayWindow.cs`

### UMM 设置面板

UnityModManager 设置面板暴露了所有功能开关和数值设置。数值输入框会保留编辑中的临时状态，因此可以清空或输入半截数值，而不会被每一帧自动格式化打断。

相关文件：

- `src/Settings.cs`

### 本地化

用户可见文本存放在 JSON 文件中，而不是硬编码在 C# 的 switch 里。

本地化文件：

- `Resources/localization.json`

运行时加载器：

- `src/Localization.cs`

当游戏语言是中文、简体中文或繁体中文时，加载器会选择中文文本；其他语言默认使用英文。缺失的 key 会回退为 key 本身。

## 项目结构

```text
.
├── ADOFAI.EditorTweaks.csproj
├── ADOFAI.EditorTweaks.slnx
├── ADOFAIMod.targets
├── Info.json
├── README.md
├── README.zh-CN.md
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

## 构建

项目需要本地安装 ADOFAI，以便引用游戏的 managed assemblies。

项目文件中的默认游戏路径：

```text
C:\Steam\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice.exe
```

构建命令：

```powershell
dotnet build
```

`ADOFAIMod.targets` 会在构建后执行：

- 将 DLL 和 `Info.json` 复制到 `out/`。
- 将 `Resources/**` 复制到 `out/Resources/`。
- 将 Mod 部署到游戏的 `Mods/ADOFAI.EditorTweaks/` 目录。
- 除非将 `AutoLaunchGame` 设置为 `true`，否则不会自动启动游戏。

## 运行入口

UnityModManager 会加载：

```text
ADOFAI.EditorTweaks.Main.Load
```

`Main.Load` 会初始化设置、本地化、UMM 回调和 Harmony 实例。启用 Mod 时应用 patch，禁用 Mod 时移除 patch。

## 开发说明

- 功能相关的 Harmony patch 放在 `src/Features/<FeatureName>/` 下。
- 优先写小范围 patch，尽量保留原版游戏行为，只调整缺失或出错的部分。
- 用户可见文本放在 `Resources/localization.json`。
- 新增设置时，需要同步更新 `Settings.cs`、`Resources/localization.json`，以及相关的浮窗或 UI 代码。
- 项目启用了 nullable reference types，新代码应显式处理 null。

## 验证

至少运行：

```powershell
dotnet build
```

建议在游戏中手动检查：

- 打开 UMM 设置面板，确认本地化标签显示正常。
- 在编辑器中打开自定义谱面，确认快捷设置浮窗出现。
- 在事件检查器中拖动数值字段。
- 拖动镜头相对装饰。
- 从带视频背景的谱面中途开始播放，确认音画同步。
