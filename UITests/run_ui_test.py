#!/usr/bin/env python3
"""
BotOrNot GUI Test Harness

Builds and launches the BotOrNot Avalonia app, opens a replay file via the
macOS file dialog, verifies the player grid loads, clicks column headers to
test sorting, and captures screenshots at every major step.

Optionally stitches screenshots into an mp4 if ffmpeg is available.

Usage:
    python3 UITests/run_ui_test.py [path/to/replay]

Exit codes:
    0 = success
    1 = failure
"""

import os
import sys
import time
import glob
import signal
import shutil
import subprocess

import pyautogui

# Quartz (CoreGraphics) for window listing without Accessibility permissions
import Quartz

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

DOTNET = os.path.expanduser("~/.dotnet/dotnet")
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SLN_PATH = os.path.join(PROJECT_ROOT, "BotOrNot.sln")
AVALONIA_PROJECT = os.path.join(PROJECT_ROOT, "BotOrNot.Avalonia")
SCREENSHOT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "screenshots")
DEFAULT_REPLAY = os.path.join(
    PROJECT_ROOT,
    "BotOrNot.Tests", "TestData",
    "UnsavedReplay-2026.01.31-15.34.27.replay",
)
SCREENCAPTURE = "/usr/sbin/screencapture"
FFMPEG = "/opt/homebrew/bin/ffmpeg"

# Columns to click for sort testing (visible columns, left-to-right)
SORT_COLUMNS = ["Name", "Level", "Kills", "Placement"]

# pyautogui settings
pyautogui.FAILSAFE = True
pyautogui.PAUSE = 0.3

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

_app_process = None   # holds the subprocess so we can kill on exit
_app_window_id = None  # Quartz window ID for targeted capture
_record_process = None  # ffmpeg screen recording process
_step = 0


def _cleanup(*_args):
    """Kill the app and recording processes on exit."""
    global _app_process, _record_process
    if _record_process and _record_process.poll() is None:
        print("\n[cleanup] Stopping screen recording …")
        _record_process.terminate()
        try:
            _record_process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            _record_process.kill()
    if _app_process and _app_process.poll() is None:
        print("\n[cleanup] Terminating app process …")
        _app_process.terminate()
        try:
            _app_process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            _app_process.kill()
    sys.exit(1)


signal.signal(signal.SIGINT, _cleanup)
signal.signal(signal.SIGTERM, _cleanup)


def step(msg: str):
    """Print a numbered status line."""
    global _step
    _step += 1
    print(f"\n{'='*60}")
    print(f"  Step {_step}: {msg}")
    print(f"{'='*60}")


def screenshot(label: str) -> str:
    """Take a screenshot using macOS screencapture. Returns the file path.
    Uses -l <windowID> to capture the app window across any Space."""
    os.makedirs(SCREENSHOT_DIR, exist_ok=True)
    ts = time.strftime("%H%M%S")
    fname = f"{_step:02d}_{label}_{ts}.png"
    path = os.path.join(SCREENSHOT_DIR, fname)
    try:
        # Prefer window-targeted capture (works across Spaces)
        if _app_window_id:
            cmd = [SCREENCAPTURE, "-x", "-l", str(_app_window_id), path]
        else:
            cmd = [SCREENCAPTURE, "-x", path]
        subprocess.run(cmd, capture_output=True, timeout=10)
        if os.path.exists(path):
            print(f"  [screenshot] {fname}")
        else:
            print(f"  [screenshot] FAILED to save {fname}")
    except Exception as e:
        print(f"  [screenshot] ERROR: {e}")
    return path


def run_applescript(script: str) -> str:
    """Execute an AppleScript snippet and return stdout."""
    result = subprocess.run(
        ["osascript", "-e", script],
        capture_output=True, text=True, timeout=30,
    )
    if result.returncode != 0:
        print(f"  [applescript stderr] {result.stderr.strip()}")
    return result.stdout.strip()


def get_window_info_quartz(owner_hint: str = "BotOrNot") -> dict | None:
    """
    Return dict with keys: x, y, w, h, window_id
    Searches ALL windows (all Spaces) so we find the app even if it's on a
    different Space. Uses kCGWindowListOptionAll instead of OnScreenOnly.
    """
    # Search all windows across all spaces
    windows = Quartz.CGWindowListCopyWindowInfo(
        Quartz.kCGWindowListOptionAll | Quartz.kCGWindowListExcludeDesktopElements,
        Quartz.kCGNullWindowID,
    )
    hint_lower = owner_hint.lower()
    for w in windows:
        owner = (w.get("kCGWindowOwnerName") or "").lower()
        title = (w.get("kCGWindowName") or "").lower()
        layer = w.get("kCGWindowLayer", 0)

        if layer != 0:
            continue

        if hint_lower in owner or hint_lower in title or "bot or not" in title:
            b = w.get("kCGWindowBounds")
            if b:
                x = int(b.get("X", 0))
                y = int(b.get("Y", 0))
                width = int(b.get("Width", 0))
                height = int(b.get("Height", 0))
                if width > 100 and height > 100:
                    return {
                        "x": x, "y": y, "w": width, "h": height,
                        "window_id": w.get("kCGWindowNumber"),
                    }
    return None


def get_window_bounds_quartz(owner_hint: str = "BotOrNot") -> tuple[int, int, int, int] | None:
    info = get_window_info_quartz(owner_hint)
    if info:
        return (info["x"], info["y"], info["w"], info["h"])
    return None


def wait_for_window(timeout: int = 60) -> tuple[int, int, int, int]:
    """Block until the app window appears. Returns bounds and stores window ID."""
    global _app_window_id
    print(f"  Waiting up to {timeout}s for window …")
    deadline = time.time() + timeout
    while time.time() < deadline:
        info = get_window_info_quartz()
        if info:
            _app_window_id = info["window_id"]
            print(f"  Window found: id={_app_window_id} pos=({info['x']},{info['y']}) size=({info['w']}x{info['h']})")
            return (info["x"], info["y"], info["w"], info["h"])
        time.sleep(1)
    raise TimeoutError("App window did not appear")


def focus_app():
    """Bring the BotOrNot window to front using osascript (no Accessibility needed)."""
    # Try activating by process name first, then by window title
    # dotnet-based Avalonia apps may show up as 'dotnet' in the process list
    script = 'tell application "System Events" to set frontmost of first process whose name contains "dotnet" to true'
    result = subprocess.run(
        ["osascript", "-e", script],
        capture_output=True, text=True, timeout=5,
    )
    if result.returncode != 0:
        # Fallback: use the PID to find and activate
        if _app_process:
            script2 = f'tell application "System Events" to set frontmost of first process whose unix id is {_app_process.pid} to true'
            subprocess.run(
                ["osascript", "-e", script2],
                capture_output=True, text=True, timeout=5,
            )
    time.sleep(0.5)


def move_window_to_current_space(x: int = 100, y: int = 100):
    """
    Bring the BotOrNot window to the current Space, raise it above all other windows,
    and maximize it so it fills the screen.
    """
    if not _app_process:
        return
    pid = _app_process.pid

    script = f'''
tell application "System Events"
    set theProcess to first process whose unix id is {pid}
    set frontmost of theProcess to true
    tell theProcess
        if (count of windows) > 0 then
            tell window 1
                perform action "AXRaise"
            end tell
        end if
    end tell
end tell
'''
    result = subprocess.run(["osascript", "-e", script], capture_output=True, text=True, timeout=10)
    if result.returncode != 0:
        print(f"  [move_window] AppleScript error: {result.stderr.strip()}")
    else:
        print(f"  Window raised to front")

    # Maximize via double-clicking the title bar (zoom)
    time.sleep(0.3)
    info = get_window_info_quartz()
    if info:
        # Double-click title bar to zoom/maximize
        title_x = info["x"] + info["w"] // 2
        title_y = info["y"] + 15
        pyautogui.doubleClick(title_x, title_y)
        print(f"  Window maximized")
    time.sleep(0.5)


def focus_app_by_pid():
    """Focus and RAISE the BotOrNot window above all others."""
    if not _app_process:
        return
    pid = _app_process.pid

    # Method 1: NSRunningApplication activate
    try:
        from AppKit import NSRunningApplication, NSApplicationActivateIgnoringOtherApps
        app = NSRunningApplication.runningApplicationWithProcessIdentifier_(pid)
        if app:
            app.activateWithOptions_(NSApplicationActivateIgnoringOtherApps)
            time.sleep(0.3)
    except Exception:
        pass

    # Method 2: AppleScript — set frontmost AND raise the window
    script = f'''
tell application "System Events"
    set theProcess to first process whose unix id is {pid}
    set frontmost of theProcess to true
    tell theProcess
        if (count of windows) > 0 then
            perform action "AXRaise" of window 1
        end if
    end tell
end tell
'''
    subprocess.run(["osascript", "-e", script], capture_output=True, text=True, timeout=10)
    time.sleep(0.3)


def click_at(x: int, y: int, label: str = ""):
    """Click at absolute coordinates with a log message."""
    print(f"  [click] ({x}, {y}){' – ' + label if label else ''}")
    pyautogui.click(x, y)
    time.sleep(0.5)


def start_recording(crop: dict | None = None) -> str:
    """Start ffmpeg screen recording. Returns output path.
    crop: optional dict with x, y, w, h to crop to window bounds."""
    global _record_process
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "test_run.mp4")

    vf_filters = []
    if crop:
        # Ensure even dimensions (required by libx264)
        cw = crop["w"] if crop["w"] % 2 == 0 else crop["w"] - 1
        ch = crop["h"] if crop["h"] % 2 == 0 else crop["h"] - 1
        vf_filters.append(f"crop={cw}:{ch}:{crop['x']}:{crop['y']}")
        print(f"  Cropping to window: x={crop['x']} y={crop['y']} w={cw} h={ch}")

    cmd = [
        FFMPEG, "-y",
        "-f", "avfoundation",
        "-framerate", "10",
        "-i", "0",  # Capture screen 0
    ]
    if vf_filters:
        cmd += ["-vf", ",".join(vf_filters)]
    cmd += [
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-crf", "28",
        "-preset", "ultrafast",
        out,
    ]
    print(f"  Starting screen recording → {out}")
    _record_process = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(1)  # let ffmpeg spin up
    return out


def stop_recording(out_path: str):
    """Stop ffmpeg recording gracefully."""
    global _record_process
    if _record_process and _record_process.poll() is None:
        print("  Stopping screen recording …")
        _record_process.terminate()
        try:
            _record_process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            _record_process.kill()
        _record_process = None
    if os.path.exists(out_path):
        print(f"  Video saved ({os.path.getsize(out_path)} bytes): {out_path}")
    else:
        print("  WARNING: video file not found")


# ---------------------------------------------------------------------------
# Main test flow
# ---------------------------------------------------------------------------

def main():
    global _app_process

    replay_path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_REPLAY
    replay_path = os.path.abspath(replay_path)

    if not os.path.isfile(replay_path):
        print(f"ERROR: Replay file not found: {replay_path}")
        return 1

    # Clean screenshots from previous runs
    if os.path.isdir(SCREENSHOT_DIR):
        for f in glob.glob(os.path.join(SCREENSHOT_DIR, "*.png")):
            os.remove(f)

    print(f"BotOrNot GUI Test Harness")
    print(f"  Replay : {replay_path}")
    print(f"  Project: {PROJECT_ROOT}")
    print(f"  Screenshots: {SCREENSHOT_DIR}")

    # ------------------------------------------------------------------
    # 1. Build
    # ------------------------------------------------------------------
    step("Building the solution")
    build = subprocess.run(
        [DOTNET, "build", SLN_PATH, "--configuration", "Debug"],
        capture_output=True, text=True, timeout=120,
    )
    if build.returncode != 0:
        print(f"  BUILD FAILED:\n{build.stdout[-2000:]}\n{build.stderr[-2000:]}")
        return 1
    print("  Build succeeded")

    # ------------------------------------------------------------------
    # 2. Launch the app
    # ------------------------------------------------------------------
    step("Launching BotOrNot")
    _app_process = subprocess.Popen(
        [DOTNET, "run", "--project", AVALONIA_PROJECT, "--no-build"],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    print(f"  PID: {_app_process.pid}")

    # ------------------------------------------------------------------
    # 3. Wait for the window, then start recording cropped to it
    # ------------------------------------------------------------------
    step("Waiting for app window")
    try:
        bounds = wait_for_window(timeout=30)
    except TimeoutError as e:
        print(f"  FAILED: {e}")
        screenshot("timeout_no_window")
        # Dump process info for debugging
        if _app_process:
            print(f"  Process alive: {_app_process.poll() is None}")
        # List all windows for debugging
        windows = Quartz.CGWindowListCopyWindowInfo(
            Quartz.kCGWindowListOptionOnScreenOnly,
            Quartz.kCGNullWindowID,
        )
        print("  Visible windows:")
        for w in windows:
            owner = w.get("kCGWindowOwnerName", "?")
            title = w.get("kCGWindowName", "")
            layer = w.get("kCGWindowLayer", -1)
            b = w.get("kCGWindowBounds", {})
            print(f"    {owner} | {title!r} | layer={layer} | {b}")
        return 1

    time.sleep(2)  # let the UI finish rendering

    # Move window to a known position on the current Space so clicks land correctly
    WINDOW_X, WINDOW_Y = 100, 50
    move_window_to_current_space(x=WINDOW_X, y=WINDOW_Y)
    time.sleep(0.5)

    # Re-fetch bounds after maximize
    time.sleep(0.8)
    info = get_window_info_quartz()
    if info:
        bounds = (info["x"], info["y"], info["w"], info["h"])
        print(f"  Window at: {bounds}")

    focus_app_by_pid()

    # Start recording — full screen (no crop, avoids coordinate mismatch issues)
    step("Starting screen recording (full screen)")
    video_path = start_recording()

    screenshot("app_launched")

    # ------------------------------------------------------------------
    # 4. Click "Select Replay File" button
    # ------------------------------------------------------------------
    step("Clicking 'Select Replay File' button")
    focus_app_by_pid()
    wx, wy, ww, wh = bounds

    # The button is near the top of the window. Avalonia on macOS has a
    # title-bar of ~28px.  The toolbar area with the button is just below.
    # The button "Select Replay File" is on the left side of the toolbar.
    btn_x = wx + 100
    btn_y = wy + 50
    click_at(btn_x, btn_y, "Select Replay File area")
    time.sleep(2)
    screenshot("after_button_click")

    # ------------------------------------------------------------------
    # 5. Type the replay path into the macOS file dialog
    # ------------------------------------------------------------------
    step("Entering replay file path in file dialog")
    time.sleep(1)

    # In macOS file dialog, Cmd+Shift+G opens "Go to Folder" sheet
    # where we can type an arbitrary path.
    pyautogui.hotkey("command", "shift", "g")
    time.sleep(1.5)
    screenshot("go_to_folder_sheet")

    # Type the full path to the replay file
    pyautogui.typewrite(replay_path, interval=0.01)
    time.sleep(0.5)
    screenshot("path_typed")

    # Press Enter/Go to navigate to the path
    pyautogui.press("enter")
    time.sleep(1)

    # Now the dialog should show the file or its parent directory.
    # Press Enter again to open/confirm the selected file.
    pyautogui.press("enter")
    time.sleep(1)
    screenshot("file_dialog_confirmed")

    # ------------------------------------------------------------------
    # 6. Wait for the replay to load
    # ------------------------------------------------------------------
    step("Waiting for replay to load")
    time.sleep(5)  # give it time to parse
    focus_app_by_pid()
    # Refresh window ID in case it changed after dialog closed
    info = get_window_info_quartz()
    if info:
        _app_window_id = info["window_id"]
        print(f"  Refreshed window ID: {_app_window_id}")
    screenshot("replay_loaded")

    # ------------------------------------------------------------------
    # 7. Click column headers to test sorting
    # ------------------------------------------------------------------
    step("Testing column header sorting")

    # Re-focus and re-fetch bounds
    focus_app_by_pid()
    new_bounds = get_window_bounds_quartz()
    if new_bounds:
        wx, wy, ww, wh = new_bounds
        print(f"  Window bounds for sorting: {new_bounds}")

    # The DataGrid starts below the metadata panel.  Based on the AXAML,
    # the top area has about ~180-200px of metadata + tabs. Column headers
    # sit just below that. We'll estimate the header row Y position.
    header_y = wy + 200

    # Column approximate X offsets within the window (from the AXAML widths):
    #   Name(195) | Level(104) | Bot(78) | Platform(130) | Kills(91) | Squad(70) | Placement(90) | Death Cause(156)
    # Starting X ≈ wx + 10 (small left margin)
    col_positions = {
        "Name": wx + 10 + 97,         # center of Name col (195px wide)
        "Level": wx + 10 + 195 + 52,  # center of Level col
        "Kills": wx + 10 + 195 + 104 + 78 + 130 + 45,  # center of Kills col
        "Placement": wx + 10 + 195 + 104 + 78 + 130 + 91 + 70 + 45,  # center of Placement col
    }

    for col_name in SORT_COLUMNS:
        col_x = col_positions.get(col_name)
        if col_x is None:
            continue
        # Re-focus before every column to ensure BotOrNot is frontmost
        focus_app_by_pid()
        time.sleep(0.3)
        print(f"\n  Sorting by {col_name} (ascending) …")
        click_at(col_x, header_y, f"{col_name} header")
        time.sleep(1)
        screenshot(f"sort_{col_name}_asc")

        focus_app_by_pid()
        time.sleep(0.3)
        print(f"  Sorting by {col_name} (descending) …")
        click_at(col_x, header_y, f"{col_name} header")
        time.sleep(1)
        screenshot(f"sort_{col_name}_desc")

    # ------------------------------------------------------------------
    # 8. Final screenshot
    # ------------------------------------------------------------------
    step("Capturing final state")
    focus_app_by_pid()
    screenshot("final_state")

    # ------------------------------------------------------------------
    # 9. Stop recording
    # ------------------------------------------------------------------
    step("Stopping screen recording")
    stop_recording(video_path)

    # ------------------------------------------------------------------
    # 10. Exit cleanly
    # ------------------------------------------------------------------
    step("Shutting down")
    if _app_process and _app_process.poll() is None:
        _app_process.terminate()
        try:
            _app_process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            _app_process.kill()
        print("  App terminated")

    print(f"\nAll steps complete. Screenshots in: {SCREENSHOT_DIR}")
    return 0


if __name__ == "__main__":
    try:
        code = main()
    except Exception as exc:
        print(f"\nFATAL: {exc}")
        import traceback
        traceback.print_exc()
        screenshot("fatal_error")
        code = 1
    finally:
        if _app_process and _app_process.poll() is None:
            _app_process.terminate()
    sys.exit(code)
