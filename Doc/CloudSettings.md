# CloudSettings

## 功能说明

利用 Steam Cloud 的原生 API（`SteamRemoteStorage`）实现 mod 设置的多设备同步，与游戏的 `data.sav` 完全隔离。

## 存储位置

- Steam Cloud 文件名: `ado_fai_editor_tweaks_settings`
- 本地文件: `Settings.xml`（保持原有 UMM 机制，作为离线 fallback）

## 数据流

```
mod 加载
  └→ Settings.Load(modEntry)        ← 本地 Settings.xml 反序列化
  └→ CloudSettingsManager.TryReadFromCloud()
      ├→ SteamRemoteStorage.FileRead()   ← Steam Cloud 读取
      ├→ 版本检查
      └→ 覆盖 settings 字段               ← 云端数据优先

用户修改设置 → Settings.Save()
  └→ UMM base.Save()                 ← 写本地 Settings.xml
  └→ CloudSettingsManager.WriteToCloud()
      └→ SteamRemoteStorage.FileWrite()  ← 写 Steam Cloud
```

## 序列化策略

- 格式: JSON（通过 `GDMiniJSON` 序列化，与游戏内部一致）
- 包含字段: 所有 Enable* toggle、数值参数、ChartRender* 参数、EditorOverlay 位置参数
- 不包含字段: UI 折叠状态、文本输入缓存（这些不需要跨设备同步）
- 版本标记: 每个云端记录带有 `cloud_version` 字段，用于未来格式迁移

## 降级策略

- Steam 未初始化（离线模式 / 非 Steam 版）→ 读写均静默跳过，保持纯本地模式
- 云端数据版本不兼容 → 不覆盖本地，下次存盘时自动上传新版本
- `FileWrite` 失败 → 不抛异常，本地 `Settings.xml` 仍有效

## 文件清单

- `src/Features/CloudSettings/CloudSettingsManager.cs` — 云同步核心逻辑
- `src/Settings.cs` — `ToCloudDict()` / `FromCloudDict()` 序列化方法，`Save()` 末尾调用云写入
- `src/Main.cs` — `Load()` 后调用云读取

## 依赖

- `com.rlabrecque.steamworks.net` (Steamworks.NET)，通过 `$(ManagedAssembliesDir)` 引用游戏自带的 DLL