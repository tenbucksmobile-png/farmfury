using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FarmFury.Core
{
    /// <summary>
    /// Development-build / Editor-only log lines for the video review pipeline (tools/review), ported from
    /// FarmFury_Arcade's HighlightMarkers. tools/review/record_session.py records the phone screen with scrcpy while
    /// capturing logcat; parse_markers.py places these lines on the video timeline so analyze_video.py can measure
    /// each shot (flight, impact, collapse, settle) and compare it against a reference recording.
    ///
    /// Line format: [Highlight] type=shot ms=1790000000000 animal=Cluck angle=41.2 speed=8.10
    /// ms = device Unix time. Every Mark() call (and its argument evaluation) is compiled out of release builds.
    ///
    /// Besides events, the watcher writes measurements in SCREEN units (fractions of the visible screen height,
    /// "sh"), so FarmFury can be compared directly with any other game's recording regardless of world scale:
    ///   layout  - per level: zoom, structure box, robot/block/animal sizes, cannon-to-structure distance
    ///   impact  - per shot: flight time, range, apex
    ///   settle  - per shot: time from impact until every block is still, peak block fall speed, blocks destroyed
    /// </summary>
    public static class HighlightMarkers
    {
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Mark(string type, FormattableString details = null)
        {
            long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Debug.Log(details == null
                ? $"[Highlight] type={type} ms={ms}"
                : $"[Highlight] type={type} ms={ms} {FormattableString.Invariant(details)}");
        }

        /// <summary>Called by CatapultLauncher.Fire() with the animal it just launched.</summary>
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Shot(AnimalBase animal, AnimalType type, Vector2 velocity, float powerFrac)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            Mark("shot", $"animal={type} angle={angle:F1} speed={velocity.magnitude:F2} power={powerFrac:F2}");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            HighlightWatcher.Instance?.TrackShot(animal, type);
#endif
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void BlockDestroyed(string blockType)
        {
            Mark("block_destroyed", $"block={blockType}");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            HighlightWatcher.DestroyedSinceShot++;
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWatcher()
        {
            var go = new GameObject("HighlightWatcher") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<HighlightWatcher>();
        }
#endif
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    /// <summary>Writes the level/state markers and the per-shot measurements described on HighlightMarkers.</summary>
    public class HighlightWatcher : MonoBehaviour
    {
        public static HighlightWatcher Instance { get; private set; }
        public static int DestroyedSinceShot;

        private const float SettleSpeed = 0.15f;      // world units/s: below this every block counts as still
        private const float SettleHold = 0.5f;        // ...for this long
        private const float SettleTimeout = 12f;

        private GameManager _gm;
        private Coroutine _shotRoutine;

        private void Awake() => Instance = this;

        private void Update()
        {
            if (_gm != null) return;
            _gm = GameManager.Instance;
            if (_gm == null) return;
            _gm.OnLevelStarted += HandleLevelStarted;
            _gm.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (_gm == null) return;
            _gm.OnLevelStarted -= HandleLevelStarted;
            _gm.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.LevelComplete:
                    HighlightMarkers.Mark("level_complete", $"level={LevelId()}");
                    break;
                case GameState.LevelFailed:
                    HighlightMarkers.Mark("level_failed", $"level={LevelId()}");
                    break;
            }
        }

        private string LevelId()
        {
            var level = _gm != null ? _gm.CurrentLevel : null;
            return level != null ? level.levelId : "none";
        }

        private void HandleLevelStarted(LevelData level)
        {
            HighlightMarkers.Mark("level_start", $"level={level.levelId} world={level.world} birds={level.birds.Length}");
            StartCoroutine(MeasureLayout(level));
        }

        /// <summary>Waits for the loader to spawn the level and the launcher to set the zoom, then writes the
        /// layout in screen-height units.</summary>
        private IEnumerator MeasureLayout(LevelData level)
        {
            yield return null;
            yield return new WaitForSeconds(0.3f);
            while (LevelReveal.IsRevealing) yield return null;   // hidden renderers have no bounds
            var cam = Camera.main;
            if (cam == null) yield break;
            float sh = cam.orthographicSize * 2f;

            var structure = new Bounds();
            bool any = false;
            var robotHeights = new List<float>();
            var blockThin = new List<float>();
            foreach (var r in FindObjectsByType<RobotEnemy>())
            {
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                robotHeights.Add(sr.bounds.size.y);
                Grow(ref structure, ref any, sr.bounds);
            }
            foreach (var b in FindObjectsByType<BlockBase>())
            {
                var sr = b.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                blockThin.Add(Mathf.Min(sr.bounds.size.x, sr.bounds.size.y));
                Grow(ref structure, ref any, sr.bounds);
            }
            if (!any) yield break;

            var cannon = GameObject.Find("FarmCannon");
            float cannonX = cannon != null ? cannon.transform.position.x : -3.72f;
            var vpMin = cam.WorldToViewportPoint(structure.min);
            var vpMax = cam.WorldToViewportPoint(structure.max);
            float sw = sh * cam.aspect;

            HighlightMarkers.Mark("layout",
                $"level={level.levelId} world={level.world} ortho={cam.orthographicSize:F2} aspect={cam.aspect:F3} struct_x0={vpMin.x:F3} struct_x1={vpMax.x:F3} struct_y0={vpMin.y:F3} struct_y1={vpMax.y:F3} struct_h_sh={structure.size.y / sh:F3} struct_w_sw={structure.size.x / sw:F3} cannon_to_struct_sw={(structure.min.x - cannonX) / sw:F3} robot_h_sh={Median(robotHeights) / sh:F3} block_thin_sh={Median(blockThin) / sh:F3} animal_h_sh={0.72f / sh:F3} robots={robotHeights.Count} blocks={blockThin.Count}");
        }

        private static void Grow(ref Bounds b, ref bool any, Bounds add)
        {
            if (!any) { b = add; any = true; }
            else b.Encapsulate(add);
        }

        private static float Median(List<float> v)
        {
            if (v.Count == 0) return 0f;
            v.Sort();
            return v[v.Count / 2];
        }

        public void TrackShot(AnimalBase animal, AnimalType type)
        {
            if (animal == null) return;
            if (_shotRoutine != null) StopCoroutine(_shotRoutine);
            DestroyedSinceShot = 0;
            _shotRoutine = StartCoroutine(FollowShot(animal, type));
        }

        /// <summary>Flight (launch to first real impact), then collapse (impact until every block is still).</summary>
        private IEnumerator FollowShot(AnimalBase animal, AnimalType type)
        {
            var cam = Camera.main;
            float sh = cam != null ? cam.orthographicSize * 2f : 9f;
            float t0 = Time.time;
            Vector2 start = animal.transform.position;
            float apex = start.y;
            bool impacted = false;
            animal.OnAnimalImpact += _ => impacted = true;

            while (animal != null && !animal.IsDestroyed && !impacted && Time.time - t0 < 15f)
            {
                apex = Mathf.Max(apex, animal.transform.position.y);
                yield return null;
            }
            float flight = Time.time - t0;
            Vector2 end = animal != null ? (Vector2)animal.transform.position : start;
            HighlightMarkers.Mark("impact",
                $"animal={type} flight_s={flight:F2} range_sh={(end.x - start.x) / sh:F3} apex_sh={(apex - start.y) / sh:F3} impact_y_vp={(cam != null ? cam.WorldToViewportPoint(end).y : 0f):F3}");

            float tImpact = Time.time;
            float stillFor = 0f;
            float peakFall = 0f;
            float peakSpeed = 0f;
            while (Time.time - tImpact < SettleTimeout)
            {
                float maxSpeed = 0f;
                foreach (var b in FindObjectsByType<BlockBase>())
                {
                    var rb = b.GetComponent<Rigidbody2D>();
                    if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) continue;
                    Vector2 v = rb.linearVelocity;
                    maxSpeed = Mathf.Max(maxSpeed, v.magnitude);
                    peakFall = Mathf.Max(peakFall, -v.y);
                }
                peakSpeed = Mathf.Max(peakSpeed, maxSpeed);
                stillFor = maxSpeed < SettleSpeed ? stillFor + Time.deltaTime : 0f;
                if (stillFor >= SettleHold) break;
                yield return null;
            }
            HighlightMarkers.Mark("settle",
                $"animal={type} settle_s={Time.time - tImpact - stillFor:F2} peak_fall_shps={peakFall / sh:F3} peak_speed_shps={peakSpeed / sh:F3} destroyed={DestroyedSinceShot}");
            _shotRoutine = null;
        }
    }
#endif
}
