[中文文档](./README.zh-CN.md)

# ⚡ LiteMonitor
A lightweight and customizable **Windows hardware monitor** — track your CPU, GPU, memory, disk, and network stats in real time.

![LiteMonitor Overview](./screenshots/overview.png)

LiteMonitor is a modern, minimal **desktop system monitor** built with **.NET 8 (WinForms)**.  
It offers smooth animations, theme customization, transparency control, and multilingual UI — a lightweight alternative to traditional **traffic and system monitor tools**.

---

## 🖥️ Monitoring Features

| Category | Metrics |
|-----------|----------|
| **CPU** | Usage %, Temperature |
| **GPU** | Usage %, Temperature, VRAM Usage |
| **Memory** | RAM Usage % |
| **Disk** | Read Speed, Write Speed |
| **Network** | Upload Speed, Download Speed |

---

## ⚙️ Product Features

| Feature | Description |
|----------|-------------|
| 🌍 Multilingual Interface | 8 languages supported (Chinese, English, Japanese, Korean, French, German, Spanish, Russian) |
| 🎨 Theme System | JSON-defined themes with customizable colors, fonts, padding, and corner radius |
| 🪟 Window & UI | Rounded corners, adjustable transparency, click-through support, and “Always on top” |
| 📏 Adjustable Width | Instantly change panel width via menu |
| 💫 Smooth Animation | Adjustable animation speed for smooth value transitions |
| 🧩 Real-time Theme & Language Switch | Changes apply immediately without restart |
| 🔠 DPI Scaling | Auto font scaling for high-resolution displays |
| ⚙️ Auto-Save Settings | All menu changes saved in real time to settings.json |
| 🚀 Auto Start | Launches via Windows Task Scheduler with admin privileges |
| 🔄 Update Check | Automatically detects new versions from GitHub |
| ℹ️ About Window | Displays version, author, and project information |

---

## 📦 Installation

1. Download the latest version from [GitHub Releases](https://github.com/Diorser/LiteMonitor/releases)
2. Extract and run `LiteMonitor.exe`
3. The app automatically loads the correct language and theme

---

## 🌐 Multilingual Support

Language files are stored in `/lang/`:

| Language | File |
|-----------|------|
| Chinese (Simplified) | `zh.json` |
| English | `en.json` |
| Japanese | `ja.json` |
| Korean | `ko.json` |
| French | `fr.json` |
| German | `de.json` |
| Spanish | `es.json` |
| Russian | `ru.json` |

---

## 🎨 Theme System

Themes are stored under `/themes/` as JSON files.

![Theme Switching Example](./screenshots/theme_switch.png)

Example:
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

## 🔄 Auto Update

LiteMonitor checks for updates from:
```
https://raw.githubusercontent.com/Diorser/LiteMonitor/main/version.json
```

Example version file:
```json
{
  "version": "1.0.1",
  "changelog": "Improved UI animation and About window design"
}
```

If a newer version is found, the app will prompt to open the GitHub Releases page.

---

## ⚙️ Settings (settings.json)

| Field | Description |
|--------|-------------|
| `Skin` | Current theme name |
| `PanelWidth` | Panel width |
| `Opacity` | Window opacity |
| `Language` | Current language |
| `TopMost` | Always on top |
| `AutoStart` | Run at startup |
| `AutoHide` | Auto-hide when near screen edge |
| `ClickThrough` | Enable mouse click-through |
| `AnimationSpeed` | Smooth animation speed |
| `Enabled` | Show/hide monitoring items |

---

## 🧩 Architecture Overview

| File | Responsibility |
|------|----------------|
| `MainForm_Transparent.cs` | Main window logic, right-click menu, and layout control |
| `UIController.cs` | Theme and update control |
| `UIRenderer.cs` | Rendering of bars, texts, and smooth transitions |
| `UILayout.cs` | Dynamic layout calculation |
| `ThemeManager.cs` | Load and parse theme JSON files |
| `LanguageManager.cs` | Manage language localization files |
| `HardwareMonitor.cs` | Collect system data using LibreHardwareMonitorLib |
| `AutoStart.cs` | Manage Windows Task Scheduler for startup |
| `UpdateChecker.cs` | GitHub version checker |
| `AboutForm.cs` | About window dialog |

---

## 🛠️ Build Instructions

### Requirements
- Windows 10 / 11  
- .NET 8 SDK  
- Visual Studio 2022 or JetBrains Rider

### Build Steps
```bash
git clone https://github.com/Diorser/LiteMonitor.git
cd LiteMonitor
dotnet build -c Release
```

Output:
```
/bin/Release/net8.0-windows/LiteMonitor.exe
```

---

## 📄 License
Released under the **MIT License** — free for commercial and personal use.

---

## 💬 Contact
**Author:** Diorser  
**GitHub:** [https://github.com/Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor)

---

<!-- SEO Keywords: Windows hardware monitor, system monitor, desktop performance widget, traffic monitor alternative, CPU GPU temperature monitor, open-source hardware monitor, lightweight system widget, memory and network usage tracker -->
