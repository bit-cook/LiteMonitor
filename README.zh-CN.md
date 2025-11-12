[English](./README.md)

# ⚡ LiteMonitor
轻量、可定制的桌面硬件监控工具 — 实时监测 CPU、GPU、内存、磁盘、网络等系统性能。

![LiteMonitor 主界面](./screenshots/overview.png)

LiteMonitor 是一款基于 **.NET 8 / WinForms** 的现代化桌面系统监控工具。  
支持多语言界面、主题切换、平滑动画、透明圆角显示，界面简洁且高度可配置。

---

## 🖥️ 系统监控功能

| 分类 | 监控指标 |
|------|-----------|
| **CPU** | 使用率、温度 |
| **GPU** | 使用率、温度、显存占用 |
| **内存** | 占用率 |
| **磁盘** | 读取速度、写入速度 |
| **网络** | 上传速度、下载速度 |

---

## ⚙️ 产品功能

| 功能 | 说明 |
|------|------|
| 🌍 多语言界面 | 支持简体中文、英语、日语、韩语、法语、德语、西班牙语、俄语 |
| 🎨 自定义主题 | 通过 JSON 文件定义颜色、字体、间距、圆角，支持即时切换 |
| 🪟 窗口与界面 | 圆角窗口、透明度调节、鼠标穿透、总在最前 |
| 📏 面板宽度 | 右键菜单可自由调整宽度，实时生效 |
| 💫 平滑动画 | 可调节数值变化速度，避免跳动突变 |
| 🧩 即时切换 | 主题与语言切换后即时生效，无需重启 |
| 🔠 DPI 缩放 | 字体自动适配高分屏显示比例 |
| ⚙️ 自动保存 | 设置更改即时写入 `settings.json` |
| 🚀 开机自启 | 通过计划任务方式实现管理员权限自启 |
| 🔄 自动更新 | 一键检测 GitHub 最新版本 |
| ℹ️ 关于窗口 | 显示版本号、作者与项目链接 |

---

## 📦 安装与使用

1. 前往 [Releases 页面](https://github.com/Diorser/LiteMonitor/releases) 下载最新版压缩包  
2. 解压后运行 `LiteMonitor.exe`  
3. 程序会自动根据系统语言加载对应语言文件

---

## 🎨 主题系统

![主题切换示例](./screenshots/theme_switch.png)

主题文件位于 `/themes/` 目录。

示例：
```json
{
  "name": "DarkFlat_Classic",
  "layout": { "rowHeight": 40, "cornerRadius": 10 },
  "color": {
    "background": "#202225",
    "textPrimary": "#EAEAEA",
    "barLow": "#00C853"
  }
}
```

---

## 🔄 自动更新

程序会访问以下地址检测版本更新：
```
https://raw.githubusercontent.com/Diorser/LiteMonitor/main/version.json
```

示例文件：
```json
{
  "version": "1.0.1",
  "changelog": "改进界面动画与关于窗口设计"
}
```

检测到新版本时，会提示前往 GitHub 下载。

---

## ⚙️ 设置文件（settings.json）

| 字段 | 说明 |
|------|------|
| `Skin` | 当前主题 |
| `PanelWidth` | 界面宽度 |
| `Opacity` | 透明度 |
| `Language` | 当前语言 |
| `TopMost` | 是否置顶 |
| `AutoStart` | 是否开机启动 |
| `AutoHide` | 靠边自动隐藏 |
| `ClickThrough` | 启用鼠标穿透 |
| `AnimationSpeed` | 数值平滑速度 |
| `Enabled` | 各项显示开关 |

---

## 🧩 架构概览

| 文件 | 功能 |
|------|------|
| `MainForm_Transparent.cs` | 主窗体与菜单逻辑 |
| `UIController.cs` | 界面与主题控制器 |
| `UIRenderer.cs` | 绘制组件与进度条 |
| `UILayout.cs` | 动态布局计算 |
| `ThemeManager.cs` | 加载与解析主题文件 |
| `LanguageManager.cs` | 语言管理与本地化 |
| `HardwareMonitor.cs` | 硬件数据采集 |
| `AutoStart.cs` | 计划任务自启管理 |
| `UpdateChecker.cs` | GitHub 更新检查 |
| `AboutForm.cs` | 关于窗口 |

---

## 🛠️ 编译说明

### 环境要求
- Windows 10 / 11  
- .NET 8 SDK  
- Visual Studio 2022 或 Rider

### 编译命令
```bash
git clone https://github.com/Diorser/LiteMonitor.git
cd LiteMonitor
dotnet build -c Release
```

输出文件：
```
/bin/Release/net8.0-windows/LiteMonitor.exe
```

---

## 📄 开源协议
本项目基于 **MIT License** 开源，可自由使用、修改与分发。

---

## 📬 联系方式
**作者**：Diorser  
**项目主页**：[https://github.com/Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor)
