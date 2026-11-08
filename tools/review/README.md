# FarmFury video review pipeline

Ported from FarmFury_Arcade's `Tools/content-pipeline` (scrcpy recording + `[Highlight]` logcat markers), with
the clip-making replaced by **measurement**: every recording is turned into numbers in screen units and contact
sheets, so FarmFury can be compared against a reference game. The numbers feed
`docs/REFERENCE_FRAMEWORK.md`.

Units: **sh** = fraction of the screen height, **sw** = of the screen width, seconds. These make different games
comparable regardless of resolution or world scale.

## One-time setup
- scrcpy and ffmpeg: reused from `FarmFury_Arcade/Tools/content-pipeline/bin/` automatically (or unzip them into
  `tools/review/bin/`). Python needs `opencv-python` and `numpy` (already installed here).
- Phone: USB debugging on, plugged in, `adb devices` shows it.
- FarmFury with markers: `.\Run-Unity.ps1 build-android-dev` then
  `adb install -r unity\Builds\Android\FarmFury_dev.apk`. Markers only exist in Development Builds and the Editor.

## Record
```
python record_session.py --reference angrybirds --show   # the reference game: open it on the phone first
python record_session.py --show                          # FarmFury (opens the game, captures markers)
python import_video.py clip.mp4 --name angrybirds        # or import a video made any other way
python import_video.py clip.mp4 --farmfury
```
Sessions land in `tools/review/sessions/ref-<name>-<time>/` or `ff-<time>/` (gitignored).

## Analyse and compare
```
python analyze_video.py                  # newest session (or pass the folder / a video file)
python compare.py                        # every analysed session side by side -> comparison.md
```
`analyze_video.py` writes `<session>/analysis/`: `report.md`/`report.json`, `sheet_overview.jpg`,
`sheet_seg_NN.jpg` (16 frames of each flight/collapse with a 10% grid) and `still_NN.jpg` (full-resolution rest
frames with a 5% grid for reading sizes by eye).

What is measured from pixels alone: camera pans (duration, distance, speed), the parallax ratio of the sky and
middle bands during pans, zoom events, projectile flights (parabola fit: gravity, launch speed, angle, flight time,
range, projectile size), collapses (duration, settle time, debris fall speed via optical flow). Checked against a
synthetic clip with known values: gravity, launch speed and angle within ~2%, pan speed/parallax within ~8%,
debris speed within ~10%.

FarmFury sessions also get the game's own numbers (`HighlightMarkers.cs`): `layout` per level (zoom, structure
box, robot/block/animal heights, cannon-to-structure distance), `shot`, `impact` (flight time, range, apex) and
`settle` (time to rest, peak fall speed, blocks destroyed).

## Recording protocol (what to capture)
Reference game, 3-5 levels, phone sideways, Do Not Disturb on:
1. One level from its very start (the intro pan across the structure back to the launcher).
2. Several full-power long shots and a few short lobs (flight shape, camera follow).
3. At least two tall-tower collapses left to settle completely before the next shot.
4. One pinch-zoom out and back, and one manual drag-pan across the level.

FarmFury, same session shape: W1 L01, L05, L10, L18, then the first level of each of W2-W9.

## Tips
- Keep a session to 2-4 levels; shorter sessions are easier to review.
- Don't rotate the phone mid-recording (the capture is locked to the starting orientation).
- If a phone silences app logs, the script already sets `log.tag.Unity V` each session.
