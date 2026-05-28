# 构建与发行

本项目通过 MSBuild target 自动完成复制、FFmpeg 准备、部署和打包。开发构建和正式发行推荐使用根目录的 bat 脚本。

## 开发构建

```bat
build-dev.bat
```

等价于：

```powershell
dotnet build "ADOFAI.EditorTweaks.csproj" -c Debug /p:CreateModPackage=true /p:BumpModVersion=false
```

开发构建会：

- 使用 Debug 配置。
- 不修改 `Info.json` 版本号。
- 自动准备 `tools/ffmpeg.exe`。
- 清空并重建 `out/`。
- 部署到游戏目录 `Mods/ADOFAI.EditorTweaks/`。
- 生成 `Build/<ModId>-<Version>/`。
- 生成 `Build/<ModId>-<Version>.zip`。

## 正式发行

```bat
build-release.bat
```

默认执行 Minor 版本递增，例如：

```text
1.2.0 -> 1.3.0
```

也可以指定递增类型：

```bat
build-release.bat Patch
build-release.bat Minor
build-release.bat Major
```

等价于：

```powershell
dotnet build "ADOFAI.EditorTweaks.csproj" -c Release /p:CreateModPackage=true /p:BumpModVersion=true /p:ModVersionBumpKind=Minor
```

正式发行会：

- 先递增 `Info.json` 里的版本号。
- 使用 Release 配置。
- 重新生成 `out/`。
- 重新生成 `Build/<ModId>-<Version>/`。
- 重新生成 `Build/<ModId>-<Version>.zip`。

## 直接使用 dotnet build

普通构建：

```powershell
dotnet build
```

普通构建不会修改版本号，但仍会打包。

显式关闭打包：

```powershell
dotnet build /p:CreateModPackage=false
```

显式递增版本：

```powershell
dotnet build /p:BumpModVersion=true
dotnet build /p:BumpModVersion=true /p:ModVersionBumpKind=Patch
```

`ModVersionBumpKind` 可选：

- `Major`
- `Minor`
- `Patch`

## 产物目录

`out/` 是临时部署目录，每次构建前会清空。

`Build/` 是最终产物目录：

```text
Build/
├── ADOFAI.EditorTweaks-1.2.0/
└── ADOFAI.EditorTweaks-1.2.0.zip
```

`Build/` 已加入 `.gitignore`，不会提交到仓库。

## FFmpeg

如果 `tools/ffmpeg.exe` 不存在，构建会运行：

```powershell
scripts/EnsureFfmpeg.ps1
```

脚本会下载指定版本的 gyan.dev FFmpeg essentials build，校验 SHA-256，并生成分发说明：

- `Tools/FFmpeg-BUILD.txt`
- `Tools/FFmpeg-SOURCE.txt`
- `Tools/FFmpeg-NOTICE.txt`

`tools/ffmpeg.exe` 不提交到 git，但会进入本地构建产物和最终 zip。

## 发行前检查

- `Info.json` 版本号正确。
- `CHANGELOG.md` 已补充本版本改动。
- `Build/<ModId>-<Version>.zip` 能正常解压。
- zip 内包含 `ADOFAI.EditorTweaks.dll`、`Info.json`、`Resources/`、`Tools/`、`ThirdParty/`。
- 游戏内 UMM 可以加载新版本。
- 谱面渲染至少用一个短谱测试成功。
