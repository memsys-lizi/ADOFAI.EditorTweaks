# 项目架构

ADOFAI Editor Tweaks 是一个 UnityModManager Mod，核心是 Harmony Patch + 少量常驻 MonoBehaviour。项目不注入自定义场景，不替换官方资源，尽量通过小范围 Patch 修正官方编辑器和播放流程中的问题。

## 加载流程

入口在 `src/Main.cs`：

```text
UnityModManager -> ADOFAI.EditorTweaks.Main.Load
```

`Load` 做以下初始化：

1. 保存 `UnityModManager.ModEntry` 到 `Main.Mod`。
2. 调用 `Localization.Load(modEntry)` 读取 `Resources/localization.json`。
3. 调用 `Settings.Load(modEntry)` 读取 UMM 设置。
4. 调用 `Settings.EnsureDefaults(modEntry)` 补全路径等默认值。
5. 注册：
   - `modEntry.OnToggle = OnToggle`
   - `modEntry.OnGUI = Settings.OnGUI`
   - `modEntry.OnSaveGUI = Settings.OnSaveGUI`
6. 创建 `Harmony` 实例，ID 使用 `modEntry.Info.Id`。

启用 Mod 时：

- `Harmony.PatchAll(Assembly.GetExecutingAssembly())`
- `EditorTweaksOverlayWindow.Ensure()`

禁用 Mod 时：

- `Harmony.UnpatchAll(modEntry.Info.Id)`
- `EditorTweaksOverlayWindow.Destroy()`

## 模块边界

`src/Features` 下每个目录代表一个相对独立的功能域：

- `ChartRendering`：谱面视频渲染。负责播放启动、定帧、自动打击、画面捕获、音频捕获、编码、日志。
- `DecorationSelection`：装饰选择、拖动、轴心和吸附修复。
- `EditorOverlay`：编辑器内浮窗和输入遮罩。
- `EditorPreferences`：官方偏好设置即时保存。
- `NumericDrag`：数值输入框拖动。
- `VideoBackgroundSync`：视频背景时间校正。

公共基础：

- `Settings.cs`：UMM 设置对象、设置 UI、默认值、渲染参数范围校验。
- `Localization.cs`：JSON 本地化加载和语言选择。
- `Resources/localization.json`：用户可见文本。
- `ADOFAIMod.targets`：构建后复制、FFmpeg 下载、部署到游戏目录。

## 状态管理

Mod 的状态主要来自三个地方：

- `Main.Settings`：用户配置。
- Harmony Patch 的静态状态：例如视频同步状态、渲染诊断状态。
- 运行期对象：例如 `EditorTweaksOverlayWindow` 和一次性的 `ChartRenderSession`。

渲染器有一个全局静态标记：

```text
ChartRenderSession.IsRendering
```

这个标记被多个模块使用：

- `ChartRenderVisualClock` 判断是否强制 conductor 视觉时间。
- `ChartRenderAutoPlayer` 判断是否自动补打。
- `ChartRenderAudioPatches` 判断是否屏蔽界面音。
- `ChartRenderJudgmentPatches` 判断是否隐藏判定文字。
- `EditorOverlayInputBlockPatches` 判断是否启用模态输入遮罩。

维护时要注意：`IsRendering` 的生命周期必须覆盖从播放启动到最终清理的整个过程，且失败、取消、FFmpeg 后台线程错误都要能走到 `Finish()` 或 `Cleanup()`。

## 设置与本地化

设置对象继承 `UnityModManager.ModSettings`。UMM UI 直接写 `Main.Settings` 字段，并在重要设置变化时调用 `Save(modEntry)`。

渲染设置分为：

- 基础设置：玩家常用，默认显示。
- 高级设置：排查和特殊导出用，默认隐藏。

`Settings.NormalizeChartRenderSettings()` 会在保存前规范化：

- 宽高限制在安全范围并变成偶数。
- FPS 限制在 1 到 240。
- CRF 限制在 0 到 51。
- preset 空值回到 `veryfast`。
- 结尾尾巴秒数不允许小于 0。

## 构建和部署

项目目标框架是 `net481`。`ADOFAI.EditorTweaks.csproj` 通过 `GameExePath` 推导游戏 managed assemblies 路径，并引用 `Assembly-CSharp.dll`、UnityEngine 模块、UMM、Harmony 等 DLL。

构建命令：

```powershell
dotnet build
```

`ADOFAIMod.targets` 做：

1. `ValidateGameExePath`：游戏 exe 不存在则构建失败。
2. `EnsureFfmpegTool`：Windows 下如果 `tools/ffmpeg.exe` 缺失，调用 `scripts/EnsureFfmpeg.ps1` 下载。
3. `CopyToOut`：复制 DLL、`Info.json`、`Resources`、`Tools` 到 `out/`。
4. `DeployAndLaunch`：部署到游戏的 `Mods/ADOFAI.EditorTweaks/`。
5. `AutoLaunchGame=true` 时才启动游戏。

## 维护原则

- Patch 尽量小，优先在 Prefix/Postfix 中短路特定情况。
- 需要访问私有字段时，用 `AccessTools.Field`，并在文档中写清楚字段名和用途。
- 避免在渲染期间暂停或跳过核心游戏 Update，除非确认不会影响画面推进。
- 修改渲染器时要做取消、失败、成功三条路径的状态恢复检查。
- 用户可见行为变化必须同步更新 README、模块文档和本地化说明。
