"""Shared helpers for the review tools: finding adb/scrcpy/ffmpeg and session folders."""

import os
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SESSIONS_DIR = HERE / "sessions"
# FarmFury_Arcade already has portable copies; reuse them rather than downloading a second set.
BIN_DIRS = [HERE / "bin", Path.home() / "Desktop/FarmFury_Arcade/Tools/content-pipeline/bin"]
ADB_FALLBACKS = [
    Path(os.environ.get("LOCALAPPDATA", "")) / "Android/Sdk/platform-tools/adb.exe",
    Path("C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"),
]


def _bundled(pattern):
    for d in BIN_DIRS:
        found = sorted(d.glob(pattern))
        if found:
            return str(found[-1])
    return None


def find_ffmpeg(name="ffmpeg"):
    path = _bundled(f"ffmpeg-*/bin/{name}.exe") or shutil.which(name)
    if not path:
        sys.exit(f"{name} not found. Unzip the gyan.dev ffmpeg essentials build into tools/review/bin/.")
    return path


def find_scrcpy():
    path = _bundled("scrcpy-*/scrcpy.exe") or shutil.which("scrcpy")
    if not path:
        sys.exit("scrcpy not found. Unzip scrcpy-win64 into tools/review/bin/ (see README).")
    return path


def find_adb():
    path = shutil.which("adb")
    if path:
        return path
    for candidate in ADB_FALLBACKS:
        if candidate.exists():
            return str(candidate)
    # scrcpy ships its own adb.
    path = _bundled("scrcpy-*/adb.exe")
    if path:
        return path
    sys.exit("adb not found. Install Android platform-tools or put adb on PATH.")


def session_arg(argv, index=1):
    """Session folder from the command line, else the newest one."""
    if len(argv) > index and not argv[index].startswith("-"):
        return Path(argv[index]).resolve()
    sessions = sorted(SESSIONS_DIR.glob("*/session.json"), key=lambda p: p.stat().st_mtime)
    if not sessions:
        sys.exit("No sessions found in tools/review/sessions.")
    return sessions[-1].parent
