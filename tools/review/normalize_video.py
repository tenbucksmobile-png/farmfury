"""Re-encode a session's raw recording to a constant-frame-rate, landscape MP4 with a keyframe every second
(scrcpy's raw .mkv is variable-rate and has few keyframes; frame timing matters for speed measurements).
Timestamps are kept, so markers still line up.

Usage:  python normalize_video.py [sessions/<id>]
"""

import json
import subprocess
import sys

from common import find_ffmpeg, session_arg

FPS = 30


def normalize(session_dir):
    session_path = session_dir / "session.json"
    session = json.loads(session_path.read_text(encoding="utf-8"))
    raw = session_dir / session.get("raw_video", session["video"])
    out = session_dir / "video.mp4"
    w, h = session.get("screen_size") or [0, 0]
    scale = f"scale={max(w, h) - max(w, h) % 2}:{min(w, h) - min(w, h) % 2}," if w and h else ""
    print(f"Converting {raw.name} -> {out.name} ({FPS} fps, keyframe every second)...")
    cmd = [find_ffmpeg(), "-v", "error", "-y", "-i", str(raw),
           "-vf", f"{scale}fps={FPS}", "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
           "-force_key_frames", "expr:gte(t,n_forced*1)", "-c:a", "aac", "-b:a", "160k",
           "-movflags", "+faststart", str(out)]
    if subprocess.run(cmd).returncode != 0 or not out.exists():
        sys.exit("ffmpeg failed; the raw recording is still in the session folder.")
    session["raw_video"] = raw.name
    session["video"] = out.name
    session_path.write_text(json.dumps(session, indent=2), encoding="utf-8")
    print(f"Done: {out}")


if __name__ == "__main__":
    normalize(session_arg(sys.argv))
