#!/usr/bin/env python3
"""
Deterministically classify Safari address-bar visibility and occlusion states.

Example:
  python skills/ios-safari-address-bar-visibility-check/scripts/detect_address_bar_visibility.py screenshots/frame.png --y-start 2050 --y-end 2100 --decision-mode address_bar_overlay_or_cutoff
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


def _mad(values: np.ndarray) -> float:
    median = float(np.median(values))
    return float(np.median(np.abs(values - median)))


def _moving_average(values: np.ndarray, window: int) -> np.ndarray:
    if window <= 1:
        return values
    kernel = np.ones(window, dtype=np.float32) / float(window)
    return np.convolve(values, kernel, mode="same")


def detect_vertical_dark_overlay(
    gray: np.ndarray,
    y_start: int,
    min_abs_step: float,
    min_dark_coverage: float,
    max_dark_mean: float,
    min_dark_ratio: float,
) -> dict:
    """Detect split-screen dim overlays (for example side-menu backdrop dimmer)."""
    height, width = gray.shape
    if width < 10 or height < 10:
        return {
            "detected": False,
            "edge_x": 0,
            "edge_strength": 0.0,
            "dark_side": "none",
            "dark_coverage": 0.0,
            "left_mean": 0.0,
            "right_mean": 0.0,
        }

    probe_top = max(0, y_start - 480)
    probe_bottom = max(probe_top + 2, min(height, y_start - 40))
    band = gray[probe_top:probe_bottom, :]
    col_profile = np.mean(band, axis=0)

    smooth_window = max(3, (width // 80) | 1)  # keep odd for stable center alignment
    smooth_profile = _moving_average(col_profile, smooth_window)
    col_step = np.abs(np.diff(smooth_profile))
    prefix = np.cumsum(smooth_profile, dtype=np.float64)
    min_side = max(1, int(np.ceil(min_dark_coverage * width)))

    best: dict | None = None
    for edge_x in range(min_side - 1, width - min_side - 1):
        left_count = edge_x + 1
        right_count = width - left_count
        if left_count <= 0 or right_count <= 0:
            continue

        left_sum = float(prefix[edge_x])
        right_sum = float(prefix[-1] - left_sum)
        left_mean = left_sum / left_count
        right_mean = right_sum / right_count

        dark_side = "left" if left_mean < right_mean else "right"
        dark_mean = min(left_mean, right_mean)
        bright_mean = max(left_mean, right_mean)
        dark_width = left_count if dark_side == "left" else right_count
        dark_coverage = float(dark_width / width)
        ratio = (dark_mean / bright_mean) if bright_mean > 0 else 1.0
        edge_strength = float(col_step[edge_x])

        valid = bool(
            edge_strength >= min_abs_step
            and dark_coverage >= min_dark_coverage
            and dark_mean <= max_dark_mean
            and ratio <= min_dark_ratio
        )
        if not valid:
            continue

        candidate = {
            "detected": True,
            "edge_x": int(edge_x),
            "edge_strength": edge_strength,
            "dark_side": dark_side,
            "dark_coverage": dark_coverage,
            "left_mean": float(left_mean),
            "right_mean": float(right_mean),
            "dark_to_bright_ratio": float(ratio),
            "probe": {"y_start": int(probe_top), "y_end": int(probe_bottom)},
        }
        if best is None or candidate["edge_strength"] > best["edge_strength"]:
            best = candidate

    if best is not None:
        return best

    edge_x = int(np.argmax(col_step))
    left_mean = float(np.mean(smooth_profile[: edge_x + 1]))
    right_mean = float(np.mean(smooth_profile[edge_x + 1 :]))
    dark_side = "left" if left_mean < right_mean else "right"
    dark_width = (edge_x + 1) if dark_side == "left" else (width - edge_x - 1)
    dark_coverage = float(dark_width / width)
    ratio = (min(left_mean, right_mean) / max(left_mean, right_mean)) if max(left_mean, right_mean) > 0 else 1.0

    return {
        "detected": False,
        "edge_x": int(edge_x),
        "edge_strength": float(col_step[edge_x]),
        "dark_side": dark_side,
        "dark_coverage": dark_coverage,
        "left_mean": left_mean,
        "right_mean": right_mean,
        "dark_to_bright_ratio": float(ratio),
        "probe": {"y_start": int(probe_top), "y_end": int(probe_bottom)},
    }


def analyze_address_bar_region(
    gray: np.ndarray,
    dark_mean_threshold: float,
) -> dict:
    """Measure brightness in the bottom address-bar zone."""
    height, width = gray.shape

    y1 = int(round(height * 0.904))
    y2 = int(round(height * 0.945))
    x1 = int(round(width * 0.07))
    x2 = int(round(width * 0.93))
    x_mid = int(round(width * 0.62))

    y1 = max(0, min(y1, height - 2))
    y2 = max(y1 + 1, min(y2, height))
    x1 = max(0, min(x1, width - 2))
    x2 = max(x1 + 1, min(x2, width))
    x_mid = max(x1 + 1, min(x_mid, x2 - 1))

    roi = gray[y1:y2, x1:x2]
    left = gray[y1:y2, x1:x_mid]
    right = gray[y1:y2, x_mid:x2]

    roi_mean = float(np.mean(roi))
    left_mean = float(np.mean(left))
    right_mean = float(np.mean(right))
    dark_fraction = float(np.mean(roi <= dark_mean_threshold))
    full_dark = bool(left_mean <= dark_mean_threshold and right_mean <= dark_mean_threshold)

    return {
        "roi": {"x_start": int(x1), "x_end": int(x2), "y_start": int(y1), "y_end": int(y2)},
        "roi_mean": roi_mean,
        "left_mean": left_mean,
        "right_mean": right_mean,
        "dark_fraction": dark_fraction,
        "dark_mean_threshold": float(dark_mean_threshold),
        "full_dark_detected": full_dark,
    }


def classify_sidebar_state(vertical_overlay: dict, width: int) -> str:
    if not vertical_overlay.get("detected", False):
        return "closed"

    edge_x = int(vertical_overlay.get("edge_x", 0))
    dark_side = str(vertical_overlay.get("dark_side", "none"))
    dark_coverage = float(vertical_overlay.get("dark_coverage", 0.0))
    edge_ratio = edge_x / float(max(width, 1))

    looks_like_sidebar = bool(
        dark_side == "right"
        and 0.25 <= dark_coverage <= 0.60
        and 0.40 <= edge_ratio <= 0.80
    )
    return "open" if looks_like_sidebar else "unknown"


def classify_overlay_behavior(
    address_bar: dict,
    vertical_overlay: dict,
    cutoff_detected: bool,
) -> str:
    full_dark = bool(address_bar.get("full_dark_detected", False))
    vertical_detected = bool(vertical_overlay.get("detected", False))

    if full_dark and cutoff_detected:
        return "dark_overlay_full_width_with_cutoff"
    if full_dark:
        return "dark_overlay_full_width_behind_address_bar"
    if cutoff_detected:
        return "overlay_cutoff_crosses_address_bar"
    if vertical_detected:
        return "dark_overlay_right_side_only"
    return "no_dark_overlay_behind_address_bar"


def analyze(
    image_path: Path,
    y_start: int,
    y_end: int,
    sigma_multiplier: float,
    min_coverage: float,
    min_abs_step: float,
    min_dark_coverage: float,
    max_dark_mean: float,
    min_dark_ratio: float,
    decision_mode: str,
    address_dark_mean_threshold: float,
    cutoff_min_coverage: float,
    cutoff_min_edge_strength: float,
) -> dict:
    gray = np.asarray(Image.open(image_path).convert("L"), dtype=np.float32)
    height, width = gray.shape

    if height < 2:
        raise ValueError("image must be at least 2 pixels tall")

    y_start = max(0, y_start)
    y_end = min(height - 1, y_end)
    if y_end <= y_start:
        raise ValueError(f"invalid search range: [{y_start}, {y_end}] for image height {height}")

    # Row-to-row absolute difference averaged across width.
    # Index y in row_strength corresponds to transition between rows y and y+1.
    row_strength = np.mean(np.abs(gray[1:, :] - gray[:-1, :]), axis=1)

    band = row_strength[y_start:y_end]
    if band.size == 0:
        raise ValueError("selected range has no row transitions")

    # Baseline is computed from all transitions outside the inspected band.
    outside = np.concatenate([row_strength[:y_start], row_strength[y_end:]])
    if outside.size == 0:
        outside = row_strength

    base_median = float(np.median(outside))
    base_mad = _mad(outside)
    robust_sigma = 1.4826 * base_mad
    threshold = max(min_abs_step, base_median + sigma_multiplier * robust_sigma)

    local_idx = int(np.argmax(band))
    edge_y = y_start + local_idx
    edge_strength = float(band[local_idx])

    # Coverage check: how much of the row width exhibits a strong jump.
    per_col = np.abs(gray[edge_y + 1, :] - gray[edge_y, :])
    col_median = float(np.median(per_col))
    col_mad = _mad(per_col)
    col_threshold = max(min_abs_step, col_median + 3.0 * (1.4826 * col_mad))
    coverage = float(np.mean(per_col >= col_threshold))

    is_hard_change = bool(edge_strength >= threshold and coverage >= min_coverage)
    vertical_overlay = detect_vertical_dark_overlay(
        gray=gray,
        y_start=y_start,
        min_abs_step=min_abs_step,
        min_dark_coverage=min_dark_coverage,
        max_dark_mean=max_dark_mean,
        min_dark_ratio=min_dark_ratio,
    )
    address_bar = analyze_address_bar_region(
        gray=gray,
        dark_mean_threshold=address_dark_mean_threshold,
    )
    cutoff_detected = bool(edge_strength >= cutoff_min_edge_strength and coverage >= cutoff_min_coverage)
    if decision_mode == "horizontal_only_allow_vertical":
        # Vertical dim overlay by itself is allowed.
        detected = bool(is_hard_change)
    elif decision_mode == "horizontal_or_vertical":
        # Any strong blocking boundary (horizontal cutoff or vertical dim overlay) is a fail.
        detected = bool(is_hard_change or vertical_overlay.get("detected", False))
    elif decision_mode == "address_bar_overlay_or_cutoff":
        # Fail if the whole address-bar region is darkened, or if an obvious cutoff appears above it.
        detected = bool(address_bar.get("full_dark_detected", False) or cutoff_detected)
    else:
        raise ValueError(f"unsupported decision mode: {decision_mode}")

    result_label = "fail" if detected else "pass"
    sidebar_state = classify_sidebar_state(vertical_overlay=vertical_overlay, width=width)
    overlay_behavior = classify_overlay_behavior(
        address_bar=address_bar,
        vertical_overlay=vertical_overlay,
        cutoff_detected=cutoff_detected,
    )
    summary = (
        f"{result_label.upper()}: sidebar {sidebar_state}; "
        f"overlay behavior = {overlay_behavior}."
    )

    return {
        "image": str(image_path),
        "image_width": int(width),
        "image_height": int(height),
        "search_range": {"y_start": int(y_start), "y_end": int(y_end)},
        "detected": detected,
        "result_label": result_label,
        "sidebar_state": sidebar_state,
        "overlay_behavior": overlay_behavior,
        "summary": summary,
        "horizontal_detected": is_hard_change,
        "edge_y": int(edge_y),
        "edge_strength": edge_strength,
        "threshold": threshold,
        "coverage": coverage,
        "coverage_threshold": float(min_coverage),
        "baseline_median": base_median,
        "baseline_mad": base_mad,
        "min_abs_step": float(min_abs_step),
        "sigma_multiplier": float(sigma_multiplier),
        "vertical_overlay": vertical_overlay,
        "address_bar": address_bar,
        "cutoff_detected": cutoff_detected,
        "cutoff_min_coverage": float(cutoff_min_coverage),
        "cutoff_min_edge_strength": float(cutoff_min_edge_strength),
        "min_dark_coverage": float(min_dark_coverage),
        "max_dark_mean": float(max_dark_mean),
        "min_dark_ratio": float(min_dark_ratio),
        "decision_mode": decision_mode,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", type=Path, help="input image path")
    parser.add_argument("--y-start", type=int, default=2050, help="start row (inclusive)")
    parser.add_argument("--y-end", type=int, default=2100, help="end row (exclusive)")
    parser.add_argument(
        "--sigma-multiplier",
        type=float,
        default=6.0,
        help="robust sigma multiplier used for detection threshold",
    )
    parser.add_argument(
        "--min-coverage",
        type=float,
        default=0.70,
        help="minimum fraction of columns that must show strong row jump",
    )
    parser.add_argument(
        "--min-abs-step",
        type=float,
        default=8.0,
        help="minimum absolute grayscale step (0..255) treated as hard change",
    )
    parser.add_argument(
        "--min-dark-coverage",
        type=float,
        default=0.25,
        help="minimum width fraction of darker side for vertical dim-overlay detection",
    )
    parser.add_argument(
        "--max-dark-mean",
        type=float,
        default=120.0,
        help="maximum mean grayscale (0..255) allowed for darker side in dim-overlay detection",
    )
    parser.add_argument(
        "--min-dark-ratio",
        type=float,
        default=0.75,
        help="maximum darker/brighter mean ratio for vertical dim-overlay detection",
    )
    parser.add_argument(
        "--decision-mode",
        choices=[
            "horizontal_only_allow_vertical",
            "horizontal_or_vertical",
            "address_bar_overlay_or_cutoff",
        ],
        default="horizontal_only_allow_vertical",
        help="final decision rule for detected=true",
    )
    parser.add_argument(
        "--address-dark-mean-threshold",
        type=float,
        default=120.0,
        help="max grayscale mean (0..255) that is considered dark in address-bar region",
    )
    parser.add_argument(
        "--cutoff-min-coverage",
        type=float,
        default=0.30,
        help="minimum coverage for cutoff detection in address-bar-focused mode",
    )
    parser.add_argument(
        "--cutoff-min-edge-strength",
        type=float,
        default=40.0,
        help="minimum row edge strength for cutoff detection in address-bar-focused mode",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        result = analyze(
            image_path=args.image,
            y_start=args.y_start,
            y_end=args.y_end,
            sigma_multiplier=args.sigma_multiplier,
            min_coverage=args.min_coverage,
            min_abs_step=args.min_abs_step,
            min_dark_coverage=args.min_dark_coverage,
            max_dark_mean=args.max_dark_mean,
            min_dark_ratio=args.min_dark_ratio,
            decision_mode=args.decision_mode,
            address_dark_mean_threshold=args.address_dark_mean_threshold,
            cutoff_min_coverage=args.cutoff_min_coverage,
            cutoff_min_edge_strength=args.cutoff_min_edge_strength,
        )
    except Exception as exc:  # pylint: disable=broad-except
        print(json.dumps({"error": str(exc)}))
        return 2

    print(json.dumps(result, indent=2))
    return 0 if result["detected"] else 1


if __name__ == "__main__":
    sys.exit(main())
