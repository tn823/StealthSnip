<div align="center">

# 📸 StealthSnip

**Discreet, ultra-lightweight Windows screen capture & annotation utility with zero screen dimming.**

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?style=flat-square&logo=windows)](https://github.com/tn823/screenshot-app)
[![Framework](https://img.shields.io/badge/.NET-9.0--windows-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Memory](https://img.shields.io/badge/memory-~6MB%20RAM-success?style=flat-square)](#-performance--resource-efficiency)
[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)

[Features](#-key-features) • [Keyboard Shortcuts](#-keyboard-shortcuts) • [Markup Editor](#-windows-11-style-markup-editor) • [Getting Started](#-getting-started) • [Building from Source](#-building-from-source)

</div>

---

## 💡 Overview

Traditional snipping tools freeze or darken the entire display when activated, drawing unwanted attention in open offices and shared environments. 

**StealthSnip** solves this with a **100% Zero-Dimming screen capture engine** coupled with a modern, feature-rich **Windows 11-style Markup Editor**. Capture regions, whole screens, or specific windows seamlessly without visual disruption, annotate immediately, and paste straight into your favorite apps.

---

## ✨ Key Features

### 🥷 100% Stealth & Zero-Dimming
- **Zero Visual Flicker**: Preserves 100% native screen brightness and color fidelity during selection.
- **Discreet Overlay**: Only a subtle, crisp border and precision crosshair appear during selection—invisible to casual glances.

### 🎨 Windows 11-Style Markup Editor
- **✏️ Precision Pen**: Smooth, anti-aliased freehand drawing.
- **🖌️ Translucent Highlighter**: Semi-transparent alpha-blended marker for highlighting code and text without obscuring background content.
- **↗️ Directional Arrows**: Clean, sharp arrowheads for pointing out critical UI elements or bugs.
- **🔲 / ⭕ Geometric Shapes**: Box or circle elements with adjustable borders.
- **🔤 In-Place Text**: Click anywhere to type high-contrast text annotations.
- **🔢 Step Badges (1, 2, 3...)**: Auto-incrementing numbered badge bubbles for step-by-step guides and documentation. Right-click resets back to `1`.
- **🌫️ Privacy Blur / Mosaic**: Redact passwords, tokens, emails, and sensitive user data with a single drag.
- **🧹 Non-Destructive Eraser**: Effortlessly remove strokes and shapes.
- **🎨 24-Color Palette & Size Slider**: Modern popout panel featuring a curated color palette and a real-time sine wave stroke preview.

### ⚡ Rapid Clipboard & Export Pipeline
- **Always-Accessible Copy**:
  - Prominent **`📋 Copy (Ctrl+C)`** in the top toolbar (anchored so it never gets clipped).
  - **Floating Action Pill** in the bottom-right corner for quick access.
  - **Right-Click Context Menu** anywhere on the canvas.
  - Global hotkey **`Ctrl + C`** commits all markup and loads the image directly into the Windows Clipboard.
- **Save to Disk (`Ctrl + S`)**: Export directly to PNG, JPEG, or BMP.

### 🪶 Performance & Resource Efficiency
- Native **Win32 GDI+ & C#** implementation.
- Uses only **~5 MB to 8 MB RAM** and **0.0% CPU** while idling in the tray.
- Automatic memory trimming (`SetProcessWorkingSetSize`) after every capture operation.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Scope | Action | Description |
| :--- | :--- | :--- | :--- |
| **`Alt + A`** | Global | **Region Snip** | Drag to select capture region $\rightarrow$ Discreet preview toast (click to edit). |
| **`Alt + S`** | Global | **Fullscreen Snip** | Captures the entire virtual desktop instantaneously. |
| **`Alt + W`** | Global | **Active Window Snip** | Automatically detects and clips foreground window bounds. |
| **`Ctrl + Shift + A`** | Global | **Backup Snip** | Secondary hotkey if `Alt + A` is intercepted by other software. |
| **`Ctrl + C`** | Editor | **Copy to Clipboard** | Renders all annotations and copies image to clipboard. |
| **`Ctrl + S`** | Editor | **Save Image** | Opens file save dialog (PNG / JPG / BMP). |
| **`Ctrl + Z`** / **`Ctrl + Y`** | Editor | **Undo / Redo** | Unlimited history stack for all drawing actions. |
| **`Esc`** / Right-Click | Overlay / Editor | **Cancel / Dismiss** | Safely exits selection mode or editor. |

---

## 🖥️ System Tray Options

Right-click the camera icon in the Windows Notification Area (bottom-right near clock) to configure:

- **🖼️ Show preview popup after capture**: Displays a sleek floating thumbnail toast in the bottom-right corner with auto-dismiss (4s), pause-on-hover, and click-to-edit.
- **✏️ Open Markup Editor directly**: Skips the preview toast and immediately opens the full editor.
- **💾 Auto-save screenshots to disk**: Automatically preserves every capture into the `Screenshots/` directory.
- **🔔 Play notification sound**: Subtle chime confirming successful capture.
- **💬 Show balloon notification**: Windows balloon tip with screenshot status.
- **🚀 Start with Windows**: Registers or unregisters user startup entry in Windows Registry.

---

## 🚀 Getting Started

### Quick Run (1-Click Build & Auto-Start)
1. Nhấp đúp vào **`build_and_start.bat`**:
   - Tự động biên dịch bản Release tối ưu.
   - Tự động thiết lập khởi động cùng Windows (Registry `HKCU\Run`).
   - Tự động khởi chạy ứng dụng ngầm ở System Tray.
2. Bấm **`Alt + A`** để bắt đầu chụp vùng kín đáo!

---

## 🛠️ Building from Source

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Windows 10 (1809+) or Windows 11
- Visual Studio 2022 / VS Code / JetBrains Rider

### Build & Run
```bash
# Clone the repository
git clone https://github.com/tn823/screenshot-app.git
cd screenshot-app

# Restore dependencies & compile
dotnet build -c Release

# Run the app
dotnet run -c Release
```

### Self-Contained Native Publish
To create a standalone, optimized single-file executable:
```bash
dotnet publish -c Release -r win-x64 --self-contained false -o ./dist
```

---

## 🏗️ Architecture Overview

```
screenshot-app/
├── AnnotationModels.cs       # Vector annotation hierarchy (Pen, Arrow, Shapes, Blur, Badges)
├── ColorPalettePopup.cs      # Modern popout color picker with dynamic curve size preview
├── SnipEditorForm.cs         # Windows 11-style double-buffered markup editor & canvas
├── StealthSnipForm.cs        # Full-screen zero-dimming selection overlay
├── CaptureHelper.cs          # Win32 GDI bit-block transfer (BitBlt) & multi-monitor routines
├── HotkeyManager.cs          # Low-level Win32 RegisterHotKey pipeline
├── NativeMethods.cs          # Windows API P/Invoke signatures & DWM window boundary calculations
├── TrayApplicationContext.cs # System tray lifecycle, context menu, and orchestrator
├── AppSettings.cs            # JSON configuration & Windows startup registry integration
└── Program.cs                # Single-instance mutex enforcement & entry point
```

---

## 📄 License

Distributed under the **MIT License**. Feel free to use, modify, and distribute for personal or commercial projects.