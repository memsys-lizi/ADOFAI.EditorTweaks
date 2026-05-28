# ChartRendering 模块

`src/Features/ChartRendering` 是谱面视频渲染器。它负责把当前游戏内关卡离线导出为 MP4，核心目标是：

- 不录桌面。
- 不录编辑器 UI 或 UMM UI。
- 支持编辑器自定义谱面、`scnGame` 自定义关卡、官谱和旧官谱场景。
- 成品帧率由设置决定，机器慢只影响等待时间，不影响视频时间轴。
- 音频直接来自 Unity mixer 离线渲染，尽量贴近游戏实际播放结果。

## 文件职责

| 文件 | 职责 |
| --- | --- |
| `ChartRenderSession.cs` | 一次渲染的主协程。负责准备目录、启动播放、定帧循环、结束检测、取消、恢复状态、调用 FFmpeg。 |
| `ChartFrameCapture.cs` | 使用官方 `scrCamera` 相机链捕获画面到 `RenderTexture`，并用 `AsyncGPUReadback` 异步读回。 |
| `ChartUnityAudioCapture.cs` | 使用 Unity `AudioRenderer` 离线捕获音频并写成 float32 WAV。 |
| `FfmpegEncoder.cs` | FFmpeg rawvideo pipe、GPU/CPU 编码选择、视频完成、音频 mux。 |
| `ChartRenderVisualClock.cs` | 强制视觉时间轴，Patch conductor 的 `songposition_minusi` 和 `calibration_i`。 |
| `ChartRenderAutoPlayer.cs` | 渲染期间自动补打砖块，并 suppress 异步输入角度修正。 |
| `ChartRenderAudioPatches.cs` | 屏蔽界面音效进入成品音频。 |
| `ChartRenderJudgmentPatches.cs` | 根据设置隐藏或显示判定文字。 |
| `ChartRenderDiagnostics.cs` | 写 `render.log`，记录球抽搐、跳块、失败、FFmpeg 等诊断信息。 |
| `ChartRenderPaths.cs` | FFmpeg 路径、工作区路径、导出路径、文件名清理。 |
| `ChartRenderResult.cs` | 渲染结果 DTO。 |

## 入口

渲染由 `EditorTweaksOverlayWindow.StartChartRender()` 启动：

```text
chartRenderSession = new ChartRenderSession(Main.Mod, Main.Settings)
StartCoroutine(chartRenderSession.Run(callback))
```

渲染是否可用由 `ChartRenderSession.IsPlayableLevelLoaded()` 和 `HasRenderableAudio()` 判断。

编辑器环境：

- `ADOBase.editor != null`
- `editor.customLevel != null`
- `editor.customLevel.levelData != null`
- `editor.floors.Count > 1`

非编辑器环境：

- `ADOBase.controller != null`
- `ADOBase.controller.gameworld == true`
- `ADOBase.lm.listFloors.Count > 1`
- 如果 `ADOBase.customLevel != null`，则要求它不在 loading 且有 `levelData`。

这个判断允许官谱直场景，因为官谱可能没有 `ADOBase.customLevel`，但仍有 `scrController`、`scrLevelMaker` 和 floors。

## 主流程

`ChartRenderSession.Run()` 的顺序：

1. 设置 `IsActive = true` 和 `IsRendering = true`。
2. `TryPrepare()`：
   - 补全设置默认值。
   - 创建 workspace 和 export 目录。
   - 清空并重建 `CurrentRender`。
   - 设置 `temp_video.mp4`、`audio.wav`、最终输出路径。
   - 开启 `ChartRenderDiagnostics.Begin(render.log)`。
3. `TryStartPlayback()`：
   - `RenderState.Capture()` 保存旧状态。
   - 设置 `Time.captureFramerate` 为目标 FPS。
   - `QualitySettings.vSyncCount = 0`。
   - `Application.targetFrameRate = max(1000, fps * 4)`。
   - 编辑器走 `StartEditorPlayback()`。
   - 非编辑器走 `StartGameScenePlayback()`。
4. 等待播放 schedule：
   - conductor 已存在。
   - `hasSongStarted` 为 true，或 controller state 是 `Countdown` / `PlayerControl`。
5. `BeginForcedVisualClock()`：
   - 记录当前 `songposition_minusi` 作为视觉锚点。
   - 记录 pitch、addoffset、当前 input offset 到日志。
6. 初始化：
   - 估算总时长。
   - 创建 `ChartFrameCapture`。
   - 创建并启动 `ChartUnityAudioCapture`。
   - 创建 `FfmpegEncoder` 并 `BeginVideo()`。
7. 主循环：
   - 等 GPU readback 队列有空位。
   - `yield return new WaitForEndOfFrame()`。
   - `audioCapture.CaptureFrame()`。
   - `frameCapture.RequestFrame(requestedFrames)`。
   - `SetForcedFrameTime(requestedFrames + 1)`。
   - `DrainReadyFrames(encoder)`。
   - 检测关卡结束并切换到尾巴帧数。
8. Drain 剩余 GPU 帧。
9. 完成音频、恢复状态。
10. 后台线程执行 `encoder.CompleteVideo()`。
11. 后台线程执行 `encoder.MuxAudioFile(capturedAudioPath)`。
12. 成功后保留临时目录，失败或取消时按路径删除。
13. `Finish()` 清理 `IsRendering`、视觉时钟、诊断日志并回调 UI。

## 播放启动策略

### 编辑器

`StartEditorPlayback()` 使用官方编辑器播放路径：

```text
editor.SelectFloor(editor.floors[0], cameraJump: false)
GCS.checkpointNum = 0
RDC.auto = false
editor.Play()
RDC.auto = true
```

先 `RDC.auto = false` 是为了让官方 `editor.Play()` 按正常路径初始化；播放启动后再打开 auto，由 Mod 的自动打击逻辑接管。

### 游戏 / 官谱 / scnGame

`StartGameScenePlayback()` 会记录场景信息到日志：

```text
scene=<ADOBase.sceneName> level=<controller.levelName> scnGame=<ADOBase.customLevel != null> state=<controller.state>
```

如果播放已经 schedule，就直接捕获当前时间线。否则：

- `GCS.checkpointNum = 0`
- `AbortWaitingForStartCoroutine(controller)`：通过私有字段 `waitForStartCoCallCount` 让等待开始协程失效。
- 隐藏 Press To Start。
- 显示 Get Ready。
- `ADOBase.conductor.Rewind()`
- `ADOBase.conductor.Start()`
- `controller.Start_Rewind(checkpoint)`
- 自定义 `scnGame` 再调用 `FinishCustomLevelLoading(checkpoint)`

官谱旧场景的关键是不要依赖 `ADOBase.customLevel`，而是依赖已有 controller 和 floors。

## 定帧与视觉时钟

只设置 `Time.captureFramerate` 不够。新版游戏的 conductor、输入校准、异步输入角度修正仍可能影响视觉结果，造成：

- 视觉和音频有很小相位差。
- 球在某些砖块突然抽搐。
- 自动播放一次追过多个砖块。

当前定帧设计：

```text
forcedSongPosition = startSongPosition + outputFrameIndex / fps * song.pitch
```

`startSongPosition` 必须在播放 schedule 后读取，因为此时官方 countdown、起点和场景初始化已经完成。

Patch 点：

- `scrConductor.set_songposition_minusi` Prefix：
  - 如果 `ChartRenderVisualClock.TryGetSongPosition()` 成功，把 setter 的 `value` 替换成 forced song position。
- `scrConductor.get_calibration_i` Prefix：
  - 渲染期间直接 `__result = 0f` 并 `return false`。

为什么要去掉 `calibration_i`：

- 玩家输入偏移是为了游玩手感，不应该把离线视频画面再整体挪一个相位。
- 音频来自 Unity mixer 的离线输出，不需要靠输入偏移修正。
- 之前视觉起点和音频起点很小相位差就是由这里暴露出来的。

## 自动打击与防球抽搐

`ChartRenderAutoPlayer.CatchUp()` 在 `scrConductor.Update` Postfix 中执行。

判断条件：

- 正在渲染。
- 视觉时钟活跃。
- controller 不为空。
- controller 没暂停。
- state 是 `PlayerControl`。
- playerManager 存在。

对每个 active player：

1. 读取当前 floor 和 nextfloor。
2. 刷新 chosen planet 角度。
3. 如果 `conductor.songposition_minusi + tolerance >= nextfloor.entryTime`，就应该命中。
4. 调用 `HitPerfect()`。

`HitPerfect()` 做：

- 暂时设置 `RDC.auto = true`。
- `controller.responsive = true`。
- 清理 paused、multipress penalty、multipress first press。
- 清空 `player.keyTimes`。
- 非 midspin 时把 planet `angle` 和 `cachedAngle` 对齐到 `targetExitAngle`。
- hold tile 先调用 `holdRenderer.Hit()`。
- 调用 `player.Hit(isAuto: true)`。
- finally 恢复旧的 `RDC.auto`。

防线：

- `MaxHitsPerFrame = 16`，防止异常情况下无限追块。
- 每次失败、跳块、达到 guard 都写入 `render.log`。
- Patch `AsyncInputUtils.AdjustAngle(scrPlayer, ulong)`：渲染时直接跳过，并记录 suppressed 次数。

如果球又抽搐，优先看 `render.log`：

- `AUTO_HIT ... FLOOR_JUMP`
- `AUTO_HIT_GUARD_REACHED`
- `PLAYER_FAILED`
- `SONG_MOVED_BACKWARD`
- 最后一行 diagnostics summary 的 `failedAutoHits` 和 `floorJumps`

## 画面捕获

`ChartFrameCapture` 不自己新建摄像机，而是使用官方 `scrCamera.instance`：

- `Bgcamstatic`
- `BGcam`
- `camobj`

它保存旧 target：

- `oldBgStaticTarget`
- `oldBgTarget`
- `oldMainTarget`
- `oldOverlayActive`
- `oldQuadActive`
- `oldQuadTexture`

然后把三台相机都指向同一个 `RenderTexture(width, height, 24, ARGB32)`。

为什么使用官方相机链：

- 官谱和旧官谱场景的摄像机层级不一定和自定义谱一致。
- 背景、滤镜、视频背景、overlay quad 都由官方 `scrCamera` 管。
- 自建相机容易漏掉后处理或 depth 顺序。

读回：

```text
AsyncGPUReadback.Request(captureTarget, 0, TextureFormat.RGBA32)
```

完成后 `request.GetData<byte>()` copy 到 byte[]，交给 FFmpeg writer。

注意：

- FFmpeg 侧用 `-vf vflip` 翻转画面。
- Dispose 必须恢复所有 targetTexture、quad texture 和 active 状态。

## 音频捕获

`ChartUnityAudioCapture` 使用 Unity `AudioRenderer`：

1. `Begin()`：
   - 创建 WAV 文件。
   - 写 placeholder WAV header。
   - `AudioRenderer.Start()`。
2. 每帧 `CaptureFrame()`：
   - `AudioRenderer.GetSampleCountForCaptureFrame()`。
   - 按 speaker mode 推导 channel count。
   - `AudioRenderer.Render(samples)`。
   - 写 float32 PCM 数据。
3. `Complete()`：
   - 回到文件头重写 RIFF/WAVE header。
4. `Dispose()`：
   - `AudioRenderer.Stop()`。
   - 释放 NativeArray 和 stream。

这个方式的优点：

- 不需要手工混合歌曲、打拍音、hold 音效、PlaySound。
- pitch、音量、mixer、游戏实际播放时序都由 Unity 负责。

Patch `scrSfx.PlaySfx(... InterfaceParent ...)` 的原因：

- UMM 点击、菜单、界面音也在 Unity mixer 里。
- 渲染成品不应该包含这些声音。
- 只屏蔽 `MixerGroup.InterfaceParent`，谱面音效不屏蔽。

## FFmpeg

`FfmpegEncoder` 的视频命令输入是 raw RGBA：

```text
-f rawvideo
-pixel_format rgba
-video_size <width>x<height>
-framerate <fps>
-i -
-an
-vf vflip
<encoder args>
-pix_fmt yuv420p
temp_video.mp4
```

编码器选择：

- 默认先探测 `h264_nvenc`。
- 探测成功使用 NVENC：
  - `-c:v h264_nvenc`
  - `-preset p1`
  - `-tune ll`
  - `-rc constqp`
  - `-qp <crf>`
- 如果 preset 是 `cpu`、`x264` 或 `x264:<preset>`，强制 CPU。
- CPU 使用：
  - `-c:v libx264`
  - `-preset <preset>`
  - `-crf <crf>`

音频 mux：

```text
-i temp_video.mp4
-i audio.wav
-map 0:v:0
-map 1:a:0
-c:v copy
-c:a aac
-b:a 320k
-ac 2
-movflags +faststart
final.mp4
```

如果高级设置里的音频同步偏移不为 0，mux 阶段会额外加 audio filter：

- 正数：音频提前。用 `atrim=start=<seconds>,asetpts=PTS-STARTPTS` 裁掉音频开头，让后面的声音更早对上画面。
- 负数：音频延后。用 `adelay=<ms>:all=1,asetpts=PTS-STARTPTS` 给音频补延迟。

这个设置是给固定偏移环境兜底的，不参与游戏时间轴，也不会影响球、滤镜或自动打击。

之前 FFmpeg mux exit code `-22` 的常见原因是输出文件名带非法字符或路径异常。`ChartRenderPaths.MakeSafeFileName()` 会：

- 去掉富文本 tag。
- 替换 Windows 非法文件名字符。
- 合并空白和下划线。
- trim 空格、点、下划线。
- 限制基础文件名长度。

## 取消和恢复

`Cancel()` 只设置 `cancelRequested = true`。主协程和后台线程会在安全点检查。

恢复状态由 `RenderState` 负责：

- `RDC.auto`
- `GCS.checkpointNum`
- `Time.captureFramerate`
- `Application.targetFrameRate`
- `QualitySettings.vSyncCount`
- 编辑器选中 floor

编辑器环境还会在必要时调用 `editor.SwitchToEditMode()`。这个恢复在 `Finish()` 清掉 `IsRendering` 后还会再执行一次，避免渲染模态输入遮罩阻止切回编辑模式。

## 踩坑记录

- 不要在渲染模态窗口期间跳过 `scrController.Update`。控制器更新是画面和状态推进的一部分。
- 不要在播放 schedule 之前锚定视觉时钟，否则起点相位可能错。
- 不要把玩家输入偏移叠到渲染视觉上，渲染不是实时游玩。
- 不要用旧版 `scrConductor` 兼容分支覆盖新版逻辑。
- 不要手工拼音频，Unity `AudioRenderer` 更稳定。
- 不要自建相机链，官方 `scrCamera` 三相机链对官谱更可靠。
- 不要忽略 `render.log`。球抽搐类问题先看日志再改逻辑。
