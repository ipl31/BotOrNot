# BotOrNot UI Test Harness

Automated GUI test for the BotOrNot Avalonia desktop app on macOS.
Uses pyautogui for mouse/keyboard automation and AppleScript for macOS file dialog interaction.

## Prerequisites

- macOS with Accessibility permissions granted to your terminal app
- .NET 9 SDK installed at `~/.dotnet/dotnet`
- Python 3.10+
- ffmpeg (optional, for video generation)

## Setup

```bash
pip install -r UITests/requirements.txt
```

Grant your terminal (Terminal.app, iTerm2, etc.) Accessibility access in
**System Settings > Privacy & Security > Accessibility**.

## Running

```bash
python3 UITests/run_ui_test.py
```

To use a custom replay file:

```bash
python3 UITests/run_ui_test.py /path/to/replay.replay
```

## What it does

1. Builds the solution with `dotnet build`
2. Launches the Avalonia app with `dotnet run`
3. Waits for the "Bot or Not?" window to appear
4. Clicks "Select Replay File" to open the file dialog
5. Uses Cmd+Shift+G to navigate to the replay file path
6. Waits for the replay to load
7. Clicks column headers (Name, Level, Kills, Placement) to test sorting
8. Captures screenshots at each step in `UITests/screenshots/`
9. Stitches screenshots into `UITests/test_run.mp4` if ffmpeg is available
10. Exits cleanly

## Output

- `UITests/screenshots/` — PNG screenshots at each step (gitignored)
- `UITests/test_run.mp4` — optional video of the test run (gitignored)
