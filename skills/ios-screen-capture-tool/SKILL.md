---
name: ios-screen-capture-tool
description: 'Capture iPhone iPad iOS screen screenshot from Windows PC over USB. Use for: grabbing device screenshot, mirroring iOS display, taking iPhone screenshot from command line, AI visual verification, iOS UI testing, capture frame from iPad, mobile screen grab, CLI screenshot tool, pymobiledevice3 screen capture, visual QA automation, headless iOS screenshot.'
---

# iOS Screen Capture Tool — Agent Contract

Use this skill for one job: capture a real iOS screenshot and analyze that image.

## Mandatory Execution Rules

1. **Check running state first (MUST):** before launching, check whether `IosScreenCaptureTool` is already running.
2. **Do not start a second copy (MUST NOT):** if running, reuse it and continue.
3. **Only launch when not running (MUST):** if not running, start it once and wait until ready.
4. **Capture with `--capture-frame` (MUST):** write to a unique `.png` path.
5. **Validate capture (MUST):** require success exit code and fresh file (exists, size > 10 KB, recent timestamp).
6. **Two-agent sequence (MUST):** Agent A captures; Agent B analyzes the same PNG as image context input.
7. **No OCR/text handoff (MUST NOT):** do not pass text extraction, summaries, or interpretations as primary input.

## Recommended Agent A Script (PowerShell)

```powershell
$exe = ".\IosScreenCaptureTool.exe"
$out = ".\screenshots\frame-$((Get-Date).ToString('yyyyMMdd-HHmmss')).png"

# 1) MUST check existing process before launch
$running = Get-Process -Name "IosScreenCaptureTool" -ErrorAction SilentlyContinue
if (-not $running) {
	# Launch only when not already running
	Start-Process -FilePath $exe
	Start-Sleep -Seconds 2
}

# 2) Capture one frame
& $exe --capture-frame $out
if ($LASTEXITCODE -ne 0) { throw "capture failed (exit $LASTEXITCODE)" }

# 3) Validate output freshness
$img = Get-Item $out -ErrorAction Stop
if ($img.Length -lt 10000) { throw "capture looks invalid: $($img.FullName)" }

"IMAGE_READY $($img.FullName)"
```

## Agent B Input Contract

- Input must be the PNG produced by Agent A (`IMAGE_READY <path>`).
- Analyze pixels from the image context directly.
- Do not substitute OCR/text output for the image.
