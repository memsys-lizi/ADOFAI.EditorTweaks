# 更新日志

本文档记录 ADOFAI Editor Tweaks 的用户可见改动。版本号来自 `Info.json`。

## 未发布

### 新增

- 普通渲染设置新增分辨率快捷预设：1080p、2K、4K。
- 普通渲染设置新增帧率快捷预设：30、60、120。
- 新增"仅渲染选中段落"开关。开启后可在编辑器中框选连续砖块，只导出该段落。
- 高级渲染设置新增视频码率限制，默认 `0` 表示按当前分辨率和帧率自动推荐。

### 优化

- FFmpeg 视频编码改为带目标码率、最大码率和缓冲区限制，减少 4K 视频体积过大和播放器峰值码率卡顿。
- 浮窗渲染规格现在显示实际目标码率。
- 片段渲染会限制自动打击终点，避免尾巴录制期间继续打到选区后面的砖块。
- 片段渲染会等官方 checkpoint 切入实际游玩状态后再开始采集，避免录到 checkpoint 静音淡入和黑场准备阶段。
- 片段渲染不再使用"结束后延迟停止秒数"，到选区终点即停止。

### 修复

- 修复片段渲染时球停在选区起点、音频淡入、进度条反复到 100% 后回退的问题。

## 1.2.4

### 新增

- 高级渲染设置新增音频格式选项：AAC（有损，默认）、FLAC（无损）、ALAC（无损，MP4 原生支持）。
- 普通渲染设置新增视频输出格式：MP4、MKV、MOV。
- 新增「专业 FFmpeg 设置」折叠面板（默认隐藏，需先展开高级设置后再展开）：
  - 自定义 FFmpeg 合成参数字段。填写后完全接管 mux 阶段的输出参数（`-map`、`-c:v`、`-c:a`、`-movflags` 等）。留空则按常规音频格式选择器自动生成。
- 高级设置展开时显示详细安全警告文本，明确提醒非专业用户不要修改。
- 新增「打开 FFmpeg 参数参考帮助」按钮，点击打开 `Resources/FFmpegReference.html`。
- 新增 `Resources/FFmpegReference.html`：深色主题全中文 FFmpeg 参数参考文档，涵盖全局选项、视频编码器、码率控制、预设、滤镜、像素格式、音频编码器、容器格式、Mod 管道说明、自定义参数字段用法和 18 个实用配方示例。

### 修复

- 修复 `UpdateScreenClamp` Postfix 对 CameraAspect 装饰无条件设置 `clampToScreen = true`，导致部分装饰在播放时一直显示。改为仅在编辑器环境且装饰已启用屏幕裁剪时修正位置，不再强制覆写裁剪状态。

## 1.2.2 - 2026-05-29

### 新增

- 新增离线谱面视频渲染性能档位：
  - 自动均衡：默认，优先使用 NVENC GPU 编码，失败时回退 x264。
  - 最快、均衡、质量、CPU 兼容、自定义。
- 新增渲染预览模式：
  - 完整预览。
  - 暗色预览。
  - 极简预览。
- 新增实验性 BGRA GPU readback 模式，默认仍使用稳定 RGBA。
- 渲染进度窗口新增中文性能信息：
  - 单帧内存占用。
  - 缓存上限。
  - GPU 回读队列上限。
  - 编码缓存队列上限。
- 新增构建和发行脚本：
  - `build-dev.bat`：开发构建，不修改版本号。
  - `build-release.bat`：正式发行构建，自动递增版本号。
- 每次构建都会生成：
  - `Build/<ModId>-<Version>/`
  - `Build/<ModId>-<Version>.zip`
- 新增可选自动递增版本号：
  - `dotnet build /p:BumpModVersion=true`
  - `dotnet build /p:BumpModVersion=true /p:ModVersionBumpKind=Patch`

### 优化

- 重构谱面渲染器结构，把原本集中的 `ChartRenderSession` 拆分为：
  - `ChartRenderPlaybackController`
  - `ChartRenderFramePipeline`
  - `ChartRenderMemoryBudget`
  - `ChartRenderProgressModel`
  - `ChartRenderOptionValues`
- FFmpeg 写入队列改为按分辨率和内存预算计算，不再固定缓存大量帧。
- GPU readback pending 数改为按分辨率动态限制：
  - 1080p 最多 8 帧。
  - 1440p 最多 6 帧。
  - 4K 最多 4 帧。
  - 8K 最多 2 帧。
- 4K/8K 渲染时更保守地控制内存峰值，避免高分辨率下突然卡死或爆内存。
- 渲染窗口布局加高加宽，避免取消按钮遮挡状态信息。
- 构建前会清空 `out/`，防止历史残留文件混进最终压缩包。

### 修复

- 保留并强化谱面从第 0 格稳定开始的保护逻辑。
- 保留视觉时钟锚定后才启用自动播放的保护逻辑，避免开头跳砖块和球抽搐。
- 修复渲染性能信息未翻译、过长且被按钮遮挡的问题。
- 修复构建打包脚本在 Windows 路径末尾反斜杠下参数被吞的问题。

### 文档

- 更新根目录 `README.md` 的渲染、构建、发行说明。
- 新增 `Doc/BuildAndRelease.md`。
- 更新 `Doc/ChartRendering.md`，记录新的结构拆分、内存预算、队列策略和编码档位。
- 更新 `Doc/SettingsAndLocalization.md`，记录新增设置字段。

## 1.1.0 - 2026-05-28

### 新增

- 新增编辑器内谱面视频渲染浮窗。
- 支持离线定帧导出 MP4。
- 支持 Unity `AudioRenderer` 捕获原曲、打拍音、长按音效和 `PlaySound`。
- 支持隐藏或显示 Perfect / Early / Late 等判定文字。
- 支持谱面结束后额外录制尾巴秒数。
- 支持构建时自动下载并部署 FFmpeg。

### 修复

- 修复从中途播放谱面时视频背景延迟或漂移的问题。
- 修复渲染开局概率性跳到后面砖块的问题。
- 修复渲染期间球抽搐、自动播放追块的问题。
- 修复渲染音频和画面相位偏移的问题。
