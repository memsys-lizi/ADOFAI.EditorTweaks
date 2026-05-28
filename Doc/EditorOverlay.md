# EditorOverlay 模块

`src/Features/EditorOverlay` 提供编辑器内快捷设置浮窗，以及渲染时的模态进度窗和输入遮罩。

## 文件职责

| 文件 | 职责 |
| --- | --- |
| `EditorTweaksOverlayWindow.cs` | IMGUI 浮窗、设置同步、渲染按钮、进度窗口、鼠标捕获判断。 |
| `EditorOverlayInputBlockPatches.cs` | 防止浮窗和渲染进度窗点击穿透到编辑器或游戏。 |

## 浮窗生命周期

`Main.OnToggle(true)` 调用：

```text
EditorTweaksOverlayWindow.Ensure()
```

它创建：

```text
GameObject("ADOFAI.EditorTweaks.Overlay")
DontDestroyOnLoad(host)
host.AddComponent<EditorTweaksOverlayWindow>()
```

禁用 Mod 时 `Destroy()` 销毁对象，并清理鼠标捕获状态。

## 显示条件

`ShouldDraw()` 返回 true 时绘制：

- `Main.Settings.ShowEditorOverlay` 为 true。
- 并且满足以下任一条件：
  - 正在编辑关卡：`ADOBase.isEditingLevel && ADOBase.editor != null`
  - 当前场景有可渲染关卡：`ChartRenderSession.IsPlayableLevelLoaded()`
  - 正在渲染：`ChartRenderSession.IsRendering`

这让浮窗不仅能在编辑器出现，也能在官谱或 `scnGame` 中出现渲染入口。

## 浮窗内容

展开状态下：

- 装饰移动吸附精度。
- 小数每像素步进。
- 整数每像素步进。
- 小数最大位数。
- 渲染状态。
- 当前渲染规格。
- 判定文字显示开关。
- 渲染视频按钮。

折叠状态下只保留标题栏和折叠按钮。

位置和折叠状态写入：

- `Settings.EditorOverlayX`
- `Settings.EditorOverlayY`
- `Settings.EditorOverlayCollapsed`

首次位置不再使用旧默认 `(16, 96)`，而是偏右上，避免挡住编辑器主要操作区域。

## 渲染进度窗

渲染时 `OnGUI()` 会先绘制普通浮窗背景，但把 `GUI.enabled` 暂时设为 false，避免背景浮窗还能被点。随后绘制居中的 `DrawRenderOverlay()`。

进度窗显示：

- 当前阶段。
- 输出文件名或当前详情。
- 进度条。
- 模式和编码器。
- 已写入帧 / 总帧。
- 处理速度。
- 重复帧数量和简要评价。
- ETA。
- 取消按钮。

取消按钮调用：

```text
activeSession.Cancel()
```

## 鼠标捕获

普通浮窗不应该让点击、拖动、滚轮穿透到背景。`ShouldBlockMouseInput()` 的核心状态：

- `mouseCapturedByOverlay`
- `mouseCaptureReleaseFrame`

逻辑：

1. 鼠标按下时，如果光标在浮窗内，就捕获鼠标。
2. 捕获期间，即使拖出浮窗外，鼠标拖动和松开也继续被拦。
3. 鼠标松开后延迟到下一帧释放，避免松开事件穿透。
4. 鼠标滚轮在浮窗内也会被拦。

渲染进度窗不走这个普通鼠标判断，而是由 `IsRenderOverlayActive` 触发模态输入拦截。

## 输入遮罩 Patch

### `scnEditor.Update`

Prefix：

```text
return !EditorTweaksOverlayWindow.ShouldBlockEditorInput()
```

普通浮窗鼠标捕获或渲染模态窗口活跃时，跳过编辑器 Update。这样可以阻止：

- 背景点击。
- 滚轮缩放。
- 键盘快捷键。
- Escape 返回编辑模式等用户输入。

### `scnEditor.ZoomCamera`

Prefix 同样使用 `ShouldBlockEditorInput()`。这是滚轮缩放的兜底，防止某些路径绕过 `Update` 直接调用 `ZoomCamera`。

### `scrController.Update`

Prefix 只使用 `ShouldBlockMouseInput()`，不使用 `ShouldBlockGameplayInput()`。

原因：渲染期间不能跳过 `scrController.Update`。控制器更新会推进游戏状态、相机、玩家和谱面逻辑，如果渲染模态窗口把它停掉，视频就可能卡住。

### `scrController.TogglePauseGame`

渲染模态窗口活跃时 Prefix 返回当前 paused 状态并跳过原方法，防止用户按键暂停。

### `scrPlayerManager.AnyValidInputWasTriggered`

渲染模态窗口活跃时返回 false，防止 Press To Start、结算、切场景等玩家输入触发。

### `scrPlayer.ValidInputWasTriggered`

渲染模态窗口活跃时返回 false，防止用户键盘或鼠标影响命中。

### `scrPlayer.ValidInputWasReleased`

渲染模态窗口活跃时返回 false，防止用户松键影响 hold 逻辑。

### `scrPlayer.CountValidKeysPressed`

渲染模态窗口活跃时返回 0，防止 multipress 计数和有效按键数污染自动播放。

### `StandaloneInputModule.Process`

普通浮窗鼠标捕获或渲染模态窗口活跃时跳过 Unity UI 事件处理。IMGUI 浮窗本身不依赖这个输入模块，所以取消按钮仍可点击。

## 踩坑记录

- 渲染进度窗必须最后绘制，否则背景浮窗仍可能响应按钮或拖动。
- 渲染进度窗必须禁用背景浮窗，否则折叠按钮、输入框仍可点。
- 不要在渲染时整体拦 `scrController.Update`。
- 普通浮窗需要捕获拖动全过程，不能只判断鼠标当前是否还在窗口内。
- `StandaloneInputModule.Process` 只能阻止 Unity UI，不会阻止 IMGUI，需要两边都处理。
