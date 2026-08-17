# naibao 桌面宠物 🐾

一个运行在 **Windows 10** 上的轻量级桌面宠物程序。它悬浮在桌面最上层，单击宠物可以快速打开预设网页、调用系统截图工具，并在每个**北京时间整点**弹出报时消息。

- **程序名**：naibao
- **当前版本**：v1.0.0
- **技术栈**：C# + WPF（.NET 8，`net8.0-windows`）
- **宠物形象**：奶娃（`assets/naibao.png`，程序图标为同图缩小版）

---

## 目录

- [功能特性](#功能特性)
- [系统适配](#系统适配)
- [安装与使用](#安装与使用)
  - [方式一：安装包](#方式一安装包推荐)
  - [方式二：免安装绿色版](#方式二免安装绿色版)
- [下载源码后的开发引导](#下载源码后的开发引导)
  - [环境准备](#1-环境准备)
  - [克隆与运行](#2-克隆与运行)
  - [编译与打包](#3-编译与打包)
  - [项目结构](#4-项目结构)
  - [常用修改点速查](#5-常用修改点速查)
  - [修改后提交与发版](#6-修改后提交与发版)
- [配置说明](#配置说明)
- [常见问题 FAQ](#常见问题-faq)
- [版本记录](#版本记录)
- [许可证](#许可证)

---

## 功能特性

### 1. 显示模式

- **默认模式：悬浮在最上层**
  - 宠物为无边框透明窗口，始终保持在其他窗口之上。
  - 按住宠物即可**拖拽移动**，位置会自动保存，下次启动恢复到最后的位置。
- **可选模式：隐藏到菜单栏（系统托盘）**
  - 在设置中选择后，宠物窗口隐藏，托盘图标保留。
  - 托盘图标**双击**显示宠物，**右键**弹出菜单：显示宠物 / 隐藏宠物 / 设置 / 退出。
- 两种模式下托盘图标都常驻，方便随时找回宠物。
- 启动时的显示方式可单独配置：`悬浮在最上层` 或 `隐藏到菜单栏`。

### 2. 开机自启动

- 设置中勾选“开机自启动”后，立即写入用户级注册表：
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`。
- 启动项为：`"安装路径\naibao.exe" --autostart`，静默启动不弹设置窗口。
- 自启动时按“启动时显示方式”决定宠物是显示还是只进托盘。
- 卸载程序时会自动删除该启动项。

### 3. 网页跳转

- **单击宠物** → 弹出菜单 → **网页跳转** → 展开预设的网页列表。
- **有几个预设就显示几个跳转选项**；没有预设时显示“暂无预设，请到设置中添加”。
- 点击任一选项，用系统默认浏览器打开对应网址。
- 设置中可以：
  - 新增预设（名称 + 网址）；
  - 修改、删除、上移、下移预设；
  - 用浏览器测试网址；
  - 网址会做合法性校验（只允许 `http://` / `https://`）。

### 4. 截图

- **单击宠物** → **截图**，程序自动执行：
  1. 先隐藏宠物窗口（避免宠物出现在截图里）；
  2. 等待约 0.35 秒，确保窗口完全隐藏；
  3. 启动 Windows 10 自带的 `C:\Windows\System32\SnippingTool.exe`；
  4. 截图工具退出后，宠物自动恢复显示（至少隐藏 3 秒，防止工具秒退导致提前出现）。
- 兼容策略：若系统中找不到 `SnippingTool.exe`，自动回退启动 Windows 10 新版截图应用（`ms-screenclip:`）。
- 如果宠物原本就藏在托盘里，截图后仍然保持隐藏，不会被强行弹出来。

### 5. 整点报时（北京时间）

- 每个**北京时间整点**，宠物旁弹出消息气泡：
  `叮咚～ 现在是北京时间 15:00`。
- 时间基准使用系统时区 `China Standard Time`（UTC+8），与电脑当前设置的时区无关。
- 调度方式：每 250ms 检查一次，在整点后 **30 秒内**触发一次，不会重复报时。
- 系统**睡眠/唤醒**后自动恢复检测；已错过的整点不补报。
- 三种报时模式：
  1. **有声音 + 消息提示（非静音）**：播放自定义音效并显示消息气泡；
  2. **音效静音（仅消息提示）**：只显示消息气泡，不播放声音；
  3. **完全静音**：不显示消息、不播放声音。
- 自定义音效：
  - 支持 `mp3` / `wav` 格式；
  - 可在设置中试听、调节音量（0–100%）；
  - 未选择音效文件时，“非静音”模式自动降级为仅消息提示，不报错。
- 宠物藏在托盘里时，整点也会**临时弹出宠物和消息气泡**，7 秒后自动重新隐藏。

### 6. 其他特性

- **单实例运行**：宠物已在运行时再次启动程序，会自动唤起已有宠物而不是开两个。
- **位置记忆**：拖拽后的位置自动保存，重启恢复。
- **配置持久化**：所有设置保存在 `%APPDATA%\naibao\config.json`，卸载/升级不清除（除非手动删除）。
- **安装卸载**：提供 NSIS 安装包，含开始菜单、可选桌面快捷方式、标准卸载入口。
- **无需联网**：程序本身不联网、不上传任何数据；只有点击网页跳转时才会调用默认浏览器。

---

## 系统适配

| 项目 | 说明 |
|---|---|
| 操作系统 | Windows 10（64 位）。基于 .NET 8 构建，官方最低支持 Win10 1607+，建议 1909 及以上 |
| 架构 | 当前发布版为 **win-x64**；如需 32 位版本可重新发布 win-x86 |
| .NET 运行时 | **不需要预装**。发布产物为自包含单文件（self-contained） |
| 安装权限 | 无需管理员权限，按当前用户安装（HKCU 注册表 + `%LOCALAPPDATA%`） |
| 显示缩放 | 支持常见 100% / 125% / 150% DPI；宠物尺寸默认 160px，可在配置文件中调整 |
| 多显示器 | 支持，位置保存时会校验是否仍在屏幕可见范围内 |
| 截图工具 | 依赖 Windows 10 自带截图工具；缺失时自动回退新版截图 |
| 音效 | 依赖 Windows 自带媒体解码能力，支持 mp3 / wav |
| 已实测 | 已在 Windows 10 上完成安装、网页跳转、托盘、设置等基本功能测试 |

> 说明：目前只发布 64 位版本；在 Windows 11 上通常也可以运行，但未做完整兼容性测试。

---

## 安装与使用

### 方式一：安装包（推荐）

1. 下载 `naibao-setup-1.0.0.exe`。
2. 双击运行，按向导安装（默认安装到 `%LOCALAPPDATA%\Programs\naibao`）。
3. 安装完成后宠物默认出现在屏幕右下角。

> 首次运行若出现 Windows SmartScreen 提示，是因为程序未做代码签名，点“更多信息 → 仍要运行”即可。

### 方式二：免安装绿色版

1. 下载 `naibao-portable-1.0.0.zip`。
2. 解压到任意目录。
3. 双击 `naibao.exe` 运行。

### 使用说明

| 操作 | 效果 |
|---|---|
| 单击宠物 | 弹出菜单：网页跳转 / 截图 / 设置 / 隐藏 / 退出 |
| 按住宠物拖动 | 移动位置（自动保存） |
| 双击托盘图标 | 显示宠物 |
| 右键托盘图标 | 显示宠物 / 隐藏宠物 / 设置 / 退出 |
| 设置 → 保存 | 立即应用显示模式、自启动、网页预设、报时设置 |

---

## 下载源码后的开发引导

### 1. 环境准备

| 工具 | 用途 | 是否必需 |
|---|---|---|
| Windows 10 / 11（64 位） | 编译运行 WPF 程序 | ✅ 必需 |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 编译、运行 | ✅ 必需 |
| Visual Studio 2022（勾选“.NET 桌面开发”工作负载）或 VS Code + C# 插件 | 编辑调试 | 推荐 |
| [NSIS 3](https://nsis.sourceforge.io/Download) | 生成安装包 `setup.exe` | 仅打安装包时需要 |
| Git | 拉取、提交代码 | ✅ 必需 |

### 2. 克隆与运行

```bash
git clone https://github.com/<你的用户名>/<仓库名>.git
cd <仓库名>

# 直接运行（调试模式，依赖本机 .NET 8 SDK）
dotnet run

# 或生成 Release 并运行
dotnet build -c Release
start bin\Release\net8.0-windows\naibao.exe
```

> 注意：`git clone` 下来的仓库**不包含** `bin/ obj/ publish/` 等编译产物，这些会在首次编译时自动生成。

### 3. 编译与打包

**Windows 上（推荐）：**

```powershell
# 发布 64 位自包含单文件版，输出到 publish\win-x64\
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1

# 需要安装包时：先执行上面的发布，再用 NSIS 打包
makensis .\installer\naibao.nsi
# 产物：publish\naibao-setup-1.0.0.exe
```

**WSL / Linux 上（交叉编译，本项目已在 WSL 验证）：**

```bash
bash build.sh
```

产物说明：

| 产物 | 路径 | 说明 |
|---|---|---|
| 单文件主程序 | `publish\win-x64\naibao.exe` | 自包含，直接双击运行 |
| 安装包 | `publish\naibao-setup-1.0.0.exe` | NSIS 安装包，需先有 win-x64 发布产物 |
| 绿色版压缩包 | `publish\naibao-portable-1.0.0.zip` | 手工打包或由脚本生成 |

### 4. 项目结构

```
naibao.csproj            项目文件（.NET 8 + WPF + WinForms 托盘）
App.xaml / App.xaml.cs   程序入口：单实例、托盘、报时调度、配置生命周期
PetWindow.xaml(.cs)      宠物窗口：透明置顶、拖拽、单击菜单、报时气泡
SettingsWindow.xaml(.cs) 设置界面：显示模式、自启、网页预设、报时与音效
Models/
  AppConfig.cs           配置模型（含 WebLinkItem）
Services/
  ConfigService.cs       配置读写（%APPDATA%\naibao\config.json）
  AutoStartService.cs    开机自启（HKCU Run 注册表）
  WebLinkService.cs      网址校验 + 默认浏览器打开
  ScreenshotService.cs   SnippingTool 调用 + 回退 + 隐藏/恢复宠物
  SoundService.cs        音效播放（WPF MediaPlayer）
  HourlyChimeService.cs  北京时间整点调度
  TrayService.cs         系统托盘图标与右键菜单
assets/
  naibao.png             宠物形象（原始素材 1024×1024）
  naibao.ico             程序/托盘/安装包图标（16–256px 多尺寸）
installer/
  naibao.nsi             NSIS 安装脚本
build.sh                 WSL/Linux 交叉编译脚本
build-windows.ps1        Windows 编译脚本
README.md                本文档
```

### 5. 常用修改点速查

| 想改什么 | 去哪里改 |
|---|---|
| 换宠物图片 | 替换 `assets/naibao.png`（建议保持透明背景 PNG），并重新生成 `naibao.ico` |
| 宠物默认大小 | `Models/AppConfig.cs` 中 `PetSize`（默认 160，允许 80–400） |
| 单击菜单内容 | `PetWindow.xaml` 中的 `ContextMenu` 部分 |
| 网页菜单生成逻辑 | `PetWindow.xaml.cs` → `RefreshWebLinkMenuItems()` |
| 设置界面 | `SettingsWindow.xaml` / `SettingsWindow.xaml.cs` |
| 报时文案 | `App.xaml.cs` → `OnHourlyChime()` |
| 报时触发规则 | `Services/HourlyChimeService.cs` |
| 截图行为 | `Services/ScreenshotService.cs` |
| 自启动逻辑 | `Services/AutoStartService.cs` |
| 版本号 | `naibao.csproj` 的 `<Version>` 和 `installer/naibao.nsi` 的 `APP_VERSION`（两处保持一致） |

### 6. 修改后提交与发版

```bash
# 修改代码 → 本地验证
dotnet build -c Release

# 提交
git add .
git commit -m "fix: 修复 xxx / feat: 新增 xxx"
git push

# 发版（把产物上传到 GitHub Releases，不放源码仓库）
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
makensis .\installer\naibao.nsi
gh release create v1.0.1 publish\naibao-setup-1.0.1.exe publish\naibao-portable-1.0.1.zip --notes "更新说明"
```

建议的提交信息风格：`feat:` 新功能、`fix:` 修复、`docs:` 文档、`chore:` 杂项。

---

## 配置说明

配置文件位置：`%APPDATA%\naibao\config.json`，示例：

```json
{
  "DisplayMode": "topmost",
  "StartupMode": "topmost",
  "AutoStart": false,
  "PetX": null,
  "PetY": null,
  "PetSize": 160,
  "WebLinks": [
    { "Name": "百度", "Url": "https://www.baidu.com" },
    { "Name": "GitHub", "Url": "https://github.com" }
  ],
  "ChimeMode": "sound_message",
  "SoundPath": "D:\\sounds\\chime.mp3",
  "Volume": 0.8
}
```

字段说明：

| 字段 | 取值 | 说明 |
|---|---|---|
| `DisplayMode` | `topmost` / `tray` | 当前显示模式：置顶悬浮 / 隐藏到托盘 |
| `StartupMode` | `topmost` / `tray` | 开机自启动时的显示方式 |
| `AutoStart` | `true` / `false` | 是否开机自启动 |
| `PetX` / `PetY` | 数字或 `null` | 宠物窗口位置，`null` 表示默认右下角 |
| `PetSize` | 80–400 | 宠物显示尺寸（像素） |
| `WebLinks` | 数组 | 网页预设列表 |
| `ChimeMode` | `sound_message` / `message_only` / `full_mute` | 报时模式 |
| `SoundPath` | 路径字符串 | 自定义音效文件 |
| `Volume` | 0.0–1.0 | 音量 |

---

## 常见问题 FAQ

**Q1：安装包提示 SmartScreen 已保护你的电脑？**
A：程序未购买代码签名证书，属正常现象。点“更多信息 → 仍要运行”。

**Q2：点了“截图”没反应？**
A：程序先隐藏宠物再启动截图工具，请观察任务栏是否出现截图窗口。若系统没有 `SnippingTool.exe`，会自动回退到新版截图应用；仍不行请反馈系统版本。

**Q3：选了音效但整点没有声音？**
A：请确认音效为 `mp3` 或 `wav` 格式，并在设置里点“试听”验证；若文件被移动/删除，程序会自动降级为仅消息提示。

**Q4：开机自启动没生效？**
A：检查设置中是否勾选并点了“保存”；部分安全软件会拦截注册表启动项，可把 `naibao.exe` 加入白名单。

**Q5：升级新版本需要卸载旧版吗？**
A：不需要。托盘右键退出宠物后，直接运行新安装包覆盖安装即可，配置会保留。

**Q6：想恢复默认配置？**
A：退出宠物后删除 `%APPDATA%\naibao\config.json`，再启动即可。

**Q7：为什么仓库里没有 exe / 安装包？**
A：按 Git 最佳实践，`publish/` 等编译产物在 `.gitignore` 中排除，二进制文件通过 **GitHub Releases** 分发。

---

## 版本记录

| 版本 | 日期 | 说明 |
|---|---|---|
| v1.0.0 | 2026-08 | 首个版本：置顶/托盘显示、开机自启、网页跳转、截图、北京时间整点报时、三种报时模式、自定义音效、NSIS 安装包 |

---

## 许可证

许可证待定。如需开源，建议在仓库中添加 `LICENSE` 文件（例如 MIT License）。
