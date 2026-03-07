---
name: ios-visual-verification-workflow
description: Capture fresh iOS screenshots from IosScreenCaptureTool and verify real-device UI behavior with reproducible PASS/FAIL evidence. Use for mobile web/app visual QA, regression checks, safe-area and browser-chrome overlap checks, before/after deployment verification, and any task where the latest on-device state must be proven from timestamped captures.
---

# iOS Visual Verification Workflow

## Overview

Use this skill to turn screenshot-based QA into a deterministic workflow.
Always capture a new frame right before analysis and report evidence from that newest frame.

## Core Workflow

1. Confirm tool state first:
- Check running process: `Get-Process -Name IosScreenCaptureTool -ErrorAction SilentlyContinue`
- Reuse the already-running app if present.
- Do not start a second instance.

2. Capture one fresh timestamped frame (default behavior):
```powershell
$exe = ".\IosScreenCaptureTool.exe"
$out = ".\screenshots\frame-$((Get-Date).ToString('yyyyMMdd-HHmmss')).png"
$p = Start-Process -FilePath $exe -ArgumentList @('--capture-frame', $out) -NoNewWindow -PassThru -Wait
if ($p.ExitCode -ne 0) { throw "capture failed (exit $($p.ExitCode))" }
```

3. Validate capture freshness and integrity:
- File exists.
- File size is greater than 10 KB.
- Last-write time is recent.
- If any check fails, capture again and do not analyze stale output.

4. Validate screen context before conclusions:
- Confirm screenshot actually shows the target app/page/state.
- If user asked for a specific page/modal and it is not visible, request screen switch and recapture.

5. Analyze and report:
- Provide strict `PASS` or `FAIL` for each requested check.
- Include absolute screenshot path(s).
- Include brief visual evidence tied to the newest capture only.

## Reporting Contract

When asked to verify UI:
- Use a single latest capture unless user explicitly requests a sequence.
- Keep findings concrete ("element visible above bottom bar", "overlay cutoff present at bottom", etc.).
- Do not claim success without a fresh capture proving it.

Suggested output shape:
```text
PASS/FAIL
Screenshot: <absolute path>
Evidence:
1) <criterion 1 result + visual proof>
2) <criterion 2 result + visual proof>
...
```

## Optional Sequence Capture

Use only when user asks for timed progression (for example auto-refresh or transient overlay windows):
- Capture a short sequence with explicit intervals.
- Still report against the final/latest relevant frame unless user requests otherwise.

## Failure Handling

- `capture command failed`: verify running instance, then retry once.
- `wrong screen captured`: ask user to open target screen, then recapture.
- `stale timestamp`: discard and capture a new frame immediately.
- `uncertain visual state`: capture one more fresh frame before final verdict.

## Practical Notes

- Prefer `Start-Process ... -Wait` for deterministic command completion.
- Keep screenshot filenames timestamped for traceability.
- For dispute-prone checks, pair visual review with deterministic pixel scripts when available.
