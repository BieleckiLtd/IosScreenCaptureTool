---
name: ios-safari-address-bar-visibility-check
description: Deterministically classify iOS Safari address-bar visibility/occlusion from a single screenshot, including full-width dark overlay behind the address bar and horizontal cutoff cases (for example y=2050..2100 on 1170x2532 captures). Use when visual judgment is disputed and you need reproducible PASS/FAIL output from pixel data.
---

# iOS Safari Address Bar Visibility Check

Use this skill to replace subjective screenshot review with deterministic pixel metrics.

## Mandatory Capture Rule
- Always capture a new screenshot immediately before running detection.
- Never reuse an existing screenshot file, even if it looks recent.
- Use `ios-screen-capture-tool` to create a unique timestamped PNG per check.

## Workflow
1. Capture a fresh frame to a new unique PNG path (for example `screenshots/frame-YYYYMMDD-HHMMSS.png`).
2. Run `scripts/detect_address_bar_visibility.py` for that newly captured image.
3. Use exit code as the decision:
- `0` means detection condition met.
- `1` means not detected.
- `2` means invalid input or processing error.

## Decision Mode
- Preferred mode for address-bar QA is `address_bar_overlay_or_cutoff`.
- In `address_bar_overlay_or_cutoff`, detection turns true (FAIL) when either:
- the address-bar ROI is dark across both left and right (`full_dark_detected=true`), or
- a strong horizontal cutoff crosses the bar zone (`cutoff_detected=true`).
- `horizontal_only_allow_vertical` and `horizontal_or_vertical` are available for compatibility/testing.

## Commands
Run:
```powershell
$out = ".\screenshots\frame-$((Get-Date).ToString('yyyyMMdd-HHmmss')).png"
.\IosScreenCaptureTool.exe --capture-frame $out
python skills/ios-safari-address-bar-visibility-check/scripts/detect_address_bar_visibility.py $out --y-start 2050 --y-end 2100 --decision-mode address_bar_overlay_or_cutoff
```

## Parameter Guidance
- Keep `--y-start` and `--y-end` fixed for the target device resolution.
- Increase `--min-abs-step` for stricter edge detection.
- Increase `--min-coverage` to require a wider full-row boundary.
- `--min-dark-coverage`, `--max-dark-mean`, and `--min-dark-ratio` affect vertical dark-overlay detection.
- `--address-dark-mean-threshold` controls when the address-bar ROI is treated as dark.
- `--cutoff-min-coverage` and `--cutoff-min-edge-strength` control cutoff sensitivity in `address_bar_overlay_or_cutoff`.
- Keep parameters constant across runs to preserve comparability.

## Output Contract
- Return JSON metrics from `detect_address_bar_visibility.py`.
- Always report `decision_mode`, `detected`, `result_label`, `sidebar_state`, and `overlay_behavior`.
- Use these semantic fields for human-readable interpretation:
- `result_label`: `pass` or `fail`.
- `sidebar_state`: `open`, `closed`, or `unknown`.
- `overlay_behavior`:
- `no_dark_overlay_behind_address_bar`
- `dark_overlay_right_side_only`
- `dark_overlay_full_width_behind_address_bar`
- `overlay_cutoff_crosses_address_bar`
- `dark_overlay_full_width_with_cutoff`
- `summary`: concise sentence combining the above classification.
