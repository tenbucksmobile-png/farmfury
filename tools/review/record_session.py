"""Record a gameplay session from the Android phone for review (ported from FarmFury_Arcade's content pipeline).

Records the phone screen with scrcpy, captures logcat, and measures the phone-to-PC clock offset so the game's
[Highlight] markers (HighlightMarkers.cs; Development Builds only) land on the video timeline.

Two modes:
    python record_session.py                         # FarmFury: opens the game, captures markers
    python record_session.py --reference angrybirds  # any other game (e.g. the reference title): open it on the
                                                     # phone yourself first; video only, no markers
Options: --show (mirror window on the PC), --package <id> (FarmFury's Android package if it changes).
Stop with Enter. Then: python analyze_video.py   (newest session)
"""

import argparse
import json
import os
import signal
import subprocess
import sys
import threading
import time
from datetime import datetime

from common import SESSIONS_DIR, find_adb, find_scrcpy

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_PACKAGE = "com.DefaultCompany.2D-URP"  # bundle id not set yet (see CLAUDE.md "Identity not set")


def now_ms():
    return int(time.time() * 1000)


def run(cmd, **kwargs):
    return subprocess.run(cmd, capture_output=True, text=True, **kwargs)


def check_device(adb):
    lines = run([adb, "devices"]).stdout.strip().splitlines()[1:]
    devices = [line.split()[0] for line in lines if line.strip().endswith("device")]
    if len(devices) != 1:
        sys.exit(f"Expected exactly one connected device, found {len(devices)}. Check `adb devices`.")
    return devices[0], run([adb, "shell", "getprop", "ro.product.model"]).stdout.strip()


def screen_size(adb):
    text = run([adb, "shell", "wm", "size"]).stdout
    try:
        w, h = (int(v) for v in text.split(":")[-1].strip().split("x"))
    except ValueError:
        return None
    return [w - w % 16, h - h % 16]


def phone_time_ms(adb):
    for shell_cmd in ("echo $EPOCHREALTIME", "date +%s.%N"):
        text = run([adb, "shell", shell_cmd]).stdout.strip()
        try:
            value = float(text)
            if "." in text:
                return int(value * 1000)
        except ValueError:
            continue
    return int(run([adb, "shell", "date", "+%s"]).stdout.strip()) * 1000


def measure_clock_offset(adb, samples=7):
    best = None
    for _ in range(samples):
        t0 = now_ms()
        phone = phone_time_ms(adb)
        t1 = now_ms()
        if best is None or t1 - t0 < best[0]:
            best = (t1 - t0, phone - (t0 + t1) // 2)
    return best[1], best[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--show", action="store_true")
    parser.add_argument("--reference", metavar="NAME", help="record another game (no launch, no markers)")
    parser.add_argument("--package", default=DEFAULT_PACKAGE)
    args = parser.parse_args()

    adb, scrcpy = find_adb(), find_scrcpy()
    serial, model = check_device(adb)
    offset_ms, round_trip_ms = measure_clock_offset(adb)
    print(f"Device: {model} ({serial})  clock offset phone-PC: {offset_ms} ms (round trip {round_trip_ms} ms)")

    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    name = f"ref-{args.reference}-{stamp}" if args.reference else f"ff-{stamp}"
    session_dir = SESSIONS_DIR / name
    session_dir.mkdir(parents=True)
    video_path = session_dir / "raw.mkv"  # mkv stays playable if the recording is cut off
    logcat_path = session_dir / "logcat.txt"

    env = dict(os.environ, ADB=adb)  # stop scrcpy's own adb fighting ours
    creation = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0

    if not args.reference:
        if args.package not in run([adb, "shell", "pm", "list", "packages", args.package]).stdout:
            print(f"WARNING: {args.package} is not installed. Build it with .\\Run-Unity.ps1 build-android-dev")
        run([adb, "shell", "setprop", "log.tag.Unity", "V"])  # some phones silence app logs
        print("Opening FarmFury...")
        run([adb, "shell", "monkey", "-p", args.package, "-c", "android.intent.category.LAUNCHER", "1"])
        time.sleep(4)
    else:
        input(f"Open {args.reference} on the phone (sideways), then press Enter to start recording...")

    run([adb, "logcat", "-c"])
    logcat_file = open(logcat_path, "w", encoding="utf-8", errors="replace")
    logcat = subprocess.Popen([adb, "logcat", "-v", "epoch", "Unity:V", "*:S"],
                              stdout=logcat_file, stderr=subprocess.STDOUT)

    cmd = [scrcpy, f"--record={video_path}", "--stay-awake", "--capture-orientation=@",
           "--video-codec-options=i-frame-interval:int=1"]
    if not args.show:
        cmd.append("--no-window")
    launch_ms = now_ms()
    recorder = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                env=env, creationflags=creation)
    started = {"ms": None}
    scrcpy_log = []

    def watch():
        for line in recorder.stdout:
            scrcpy_log.append(line.rstrip())
            if started["ms"] is None and "Recording started" in line:
                started["ms"] = now_ms()

    threading.Thread(target=watch, daemon=True).start()
    print(f"Recording to {session_dir}\nPlay (keep the phone sideways). Press Enter here to stop.")
    try:
        input()
    except KeyboardInterrupt:
        pass

    if recorder.poll() is None:
        recorder.send_signal(signal.CTRL_BREAK_EVENT if os.name == "nt" else signal.SIGINT)
        try:
            recorder.wait(timeout=10)
        except subprocess.TimeoutExpired:
            recorder.kill()
    logcat.terminate()
    logcat.wait(timeout=5)
    logcat_file.close()

    if started["ms"] is None:
        print("WARNING: scrcpy never printed 'Recording started'; markers may be off by ~1s.")
    (session_dir / "scrcpy.log").write_text("\n".join(scrcpy_log), encoding="utf-8")
    session = {
        "kind": "reference" if args.reference else "farmfury",
        "game": args.reference or "FarmFury",
        "device_model": model,
        "video": video_path.name,
        "screen_size": screen_size(adb),
        "logcat": logcat_path.name,
        "recording_start_pc_ms": started["ms"] or launch_ms,
        "clock_offset_ms": offset_ms,
        "clock_round_trip_ms": round_trip_ms,
    }
    (session_dir / "session.json").write_text(json.dumps(session, indent=2), encoding="utf-8")
    if not video_path.exists():
        sys.exit("ERROR: no video was written. See scrcpy.log in the session folder.")

    print("Recording saved.")
    subprocess.run([sys.executable, os.path.join(HERE, "normalize_video.py"), str(session_dir)])
    if not args.reference:
        subprocess.run([sys.executable, os.path.join(HERE, "parse_markers.py"), str(session_dir)])
    print(f'Next: python analyze_video.py "{session_dir}"')


if __name__ == "__main__":
    main()
