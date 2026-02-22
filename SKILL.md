---
name: IosScreenCaptureTool
description: 'Capture iPhone iPad iOS screen screenshot from Windows PC over USB. Use for: grabbing device screenshot, mirroring iOS display, taking iPhone screenshot from command line, AI visual verification, iOS UI testing, capture frame from iPad, mobile screen grab, CLI screenshot tool, pymobiledevice3 screen capture, visual QA automation, headless iOS screenshot.'
argument-hint: 'output file path for the captured screenshot, e.g. .\screenshots\frame.png'
---

# iOS Screen Capture Tool — Usage Guide

Capture your iPhone or iPad screen on a Windows PC over USB using `IosScreenCaptureTool.exe`.

## Prerequisites

Before any capture method works, make sure:

1. **iTunes is installed** (provides Apple USB drivers).
2. **Developer Mode is enabled** on the iOS device (iOS 16+: Settings → Privacy & Security → Developer Mode → On).
3. The device is **connected via USB cable** and **unlocked**. Tap **Trust This Computer** if prompted.
4. **`IosScreenCaptureTool.exe` is already running** (as a window or in the system tray). The first launch requires a UAC Administrator prompt, so start it manually at least once.

## Capture Methods

### 1. GUI — Click the Camera Button

1. Open `IosScreenCaptureTool.exe`. The live iOS screen appears on the left.
2. Click the **camera button** (📷) on the right panel.
3. The screenshot is saved to the configured capture folder (default: `Pictures\iOSLiveStream`).
4. It appears in the **Captured Frames** list. Double-click to open, right-click to copy the path.

### 2. System Tray — Right-Click Menu

1. Minimize or close the window — the app stays in the notification area (system tray).
2. Right-click the tray icon → **Grab Screenshot**.
3. The screenshot is saved to the configured capture folder.

### 3. Command Line — `--capture-frame`

While the app is running (window or tray), open **any terminal** (even non-Administrator):

```powershell
.\IosScreenCaptureTool.exe --capture-frame .\screenshots\frame.png
```

- The image is written within milliseconds.
- The output folder is created automatically if it doesn't exist.
- Supports `.png` output.
- Exit code `0` = success, `1` = failure.

### 4. AI Agent / Automation — Headless Screenshot

Any AI tool that can run a process and read a file can use this to *see* the device screen:

```powershell
.\IosScreenCaptureTool.exe --capture-frame .\check.png
# → feed check.png to the AI and ask what it sees
```

**Typical automation scenarios:**

- Verify a UI change looks correct before merging a PR.
- Check a web page renders properly on a real iOS device.
- Visual regression: capture before/after a build, compare with AI or diff tool.
- Hands-free QA: the agent flags anything unexpected without human review.

> **Important:** The GUI app must be running first. AI agents (Copilot, Codex, Claude) cannot start it on their own due to the UAC prompt. Start it manually once, or enable **Start on startup** in Settings so it launches minimized to tray on every Windows boot.

### 5. Health Check — `--self-test`

Confirm the device connection works in a single command, then exit:

```powershell
.\IosScreenCaptureTool.exe --self-test .\screenshots\test.png
```

Exit code `0` means a real frame was captured. Useful as a CI pipeline health check before longer automation.

## All Command-Line Options

| Command | What it does |
|---|---|
| `IosScreenCaptureTool.exe` | Launch the GUI window. |
| `IosScreenCaptureTool.exe --start-minimized` | Launch directly to the system tray (no window). |
| `IosScreenCaptureTool.exe --capture-frame <path>` | Save one screenshot and exit. Requires the GUI to be running already. |
| `IosScreenCaptureTool.exe --self-test <path>` | Capture one frame as a connection test and exit. |
| `IosScreenCaptureTool.exe -h` or `--help` | Print usage info. |

## Settings Reference

| Setting | Location | Effect |
|---|---|---|
| **Capture folder** | Settings → Browse… | Where screenshots are saved. Default: `Pictures\iOSLiveStream`. |
| **Start on startup** | Settings checkbox | Launch minimized to tray on Windows boot — always ready for CLI/AI captures. |
| **Keep minimized after closing** | Settings checkbox | Closing the window hides to tray instead of quitting (enabled by default). |
| **FPS** | Stream panel dropdown | Preview refresh rate: 1 / 5 / 25 / 30 / 60 fps. |

## Troubleshooting Quick Reference

| Symptom | Fix |
|---|---|
| "Stream failed - Developer Mode may not be active" | Enable Developer Mode: Settings → Privacy & Security → Developer Mode → On, then restart device. |
| Device not detected | Install iTunes for USB drivers. |
| Blank screen / stuck on "Idle" | Unlock device and tap **Trust**. |
| `--capture-frame` says app not found | Start the GUI first; wait for the live stream to appear. |
| First-run install fails | Install Python 3.12 manually, then restart the app. |
