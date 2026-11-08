"""Import a video recorded some other way (the phone's own screen recorder, OBS on the PC, a reference clip)
as a review session.

Usage:
    python import_video.py <file.mp4> --name angrybirds    # a reference game
    python import_video.py <file.mp4> --farmfury           # FarmFury, no markers
Then: python analyze_video.py
"""

import argparse
import json
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path

from common import SESSIONS_DIR

HERE = Path(__file__).resolve().parent


def main():
    p = argparse.ArgumentParser()
    p.add_argument("file")
    p.add_argument("--name", default="reference")
    p.add_argument("--farmfury", action="store_true")
    args = p.parse_args()
    src = Path(args.file).resolve()
    if not src.exists():
        sys.exit(f"{src} not found")
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    session_dir = SESSIONS_DIR / (f"ff-{stamp}" if args.farmfury else f"ref-{args.name}-{stamp}")
    session_dir.mkdir(parents=True)
    raw = session_dir / f"raw{src.suffix.lower()}"
    shutil.copy2(src, raw)
    session = {"kind": "farmfury" if args.farmfury else "reference",
               "game": "FarmFury" if args.farmfury else args.name,
               "video": raw.name, "source": str(src)}
    (session_dir / "session.json").write_text(json.dumps(session, indent=2), encoding="utf-8")
    subprocess.run([sys.executable, str(HERE / "normalize_video.py"), str(session_dir)])
    print(f'Imported to {session_dir}\nNext: python analyze_video.py "{session_dir}"')


if __name__ == "__main__":
    main()
