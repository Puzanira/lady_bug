using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace LadyBug
{
    // Reads the arcade cabinet's shared logical controls (AiGameStudio.ArcadeControls.ArcadeInput)
    // WITHOUT a compile-time dependency on the arcade-controls package.
    //
    // Why reflection, not an asmdef reference: lady_bug ships in two contexts. Standalone (its own
    // UnityProject) the arcade-controls package is ABSENT — a hard reference would fail to compile
    // there. In the arcade-hub the package IS present and the launcher pumps ArcadeInput every frame.
    // Probing the type by name keeps lady_bug buildable/runnable in BOTH: when the type is missing
    // (standalone) every reader returns neutral, so the legacy keyboard / sensor paths are untouched;
    // in the hub the cabinet joystick + buttons drive the game through the same facade every other
    // cabinet game reads.
    //
    // Perf: these properties are read every frame from Update paths, so the one-time probe compiles
    // the property chains into strongly-typed delegates (expression trees -> Func<Vector2>/Func<bool>,
    // no boxing, no per-frame MethodInfo.Invoke). If expression compilation is unavailable on some
    // player backend, the probe falls back to cached MethodInfo.Invoke — slower but correct.
    //
    // This is the fix for the gate-2 "коровка не управляется в хабе" report: gameplay used to read
    // ONLY legacy Input / its own serial readers, so the cabinet's joystick (delivered through
    // ArcadeInput by the hub — serial on the real cabinet, keyboard-backed on a dev laptop) never
    // reached the ladybug. PlayerController OR-combines this reader with its keyboard reads.
    internal static class ArcadeControlsReader
    {
        private static bool _probed;

        // Fast path: compiled zero-boxing getters (null when the package is absent).
        private static Func<Vector2> _joystickVector;
        private static Func<bool> _redHeld;
        private static Func<bool> _greenHeld;
        private static Func<float> _heightA;
        private static Func<float> _heightB;

        // Fallback path: cached reflection members, used only if delegate compilation failed.
        private static MethodInfo _joystickGet, _vectorGet;
        private static MethodInfo _redGet, _redHeldGet;
        private static MethodInfo _greenGet, _greenHeldGet;
        private static MethodInfo _heightAGet, _heightAValueGet;
        private static MethodInfo _heightBGet, _heightBValueGet;

        /// <summary>True when the arcade-controls facade is present (i.e. running inside the hub).</summary>
        public static bool Available
        {
            get { Probe(); return _joystickVector != null || _vectorGet != null; }
        }

        /// <summary>
        /// True when this game was started from the shared arcade-cabinet launcher rather than run
        /// on its own. Same probe as <see cref="Available"/> — the facade only exists when the
        /// launcher put it there — named for the one thing it is used to decide besides input:
        /// whether the game's own attract flow should stand down.
        ///
        /// The cabinet launcher already IS an attract screen: it shows the 7-game control panel and
        /// plays a hold-to-launch fill animation (adapted from IntroSequence) for whichever control
        /// the player holds, then loads that game. lady_bug playing its own loader and intro on top
        /// of that would show the player the same two screens twice in a row, so inside the launcher
        /// LoaderScreenController and the IntroSequences stand aside and the menu comes up straight
        /// away. Run standalone the facade is absent, this is false, and the loader, the slot
        /// select and the flower intro all behave exactly as they always have.
        /// </summary>
        public static bool InsideArcadeLauncher
        {
            get { return Available; }
        }

        /// <summary>Cabinet joystick vector, each axis -1..1. Zero when the facade is absent.</summary>
        public static Vector2 Joystick
        {
            get
            {
                Probe();
                if (_joystickVector != null) return _joystickVector();
                if (_vectorGet == null) return Vector2.zero;
                object js = _joystickGet.Invoke(null, null);
                if (js == null) return Vector2.zero;
                return (Vector2)_vectorGet.Invoke(js, null);
            }
        }

        /// <summary>Cabinet RedButton held. False when the facade is absent.</summary>
        public static bool RedHeld
        {
            get
            {
                Probe();
                if (_redHeld != null) return _redHeld();
                return ReadButtonSlow(_redGet, _redHeldGet);
            }
        }

        /// <summary>Cabinet GreenButton held. False when the facade is absent.</summary>
        public static bool GreenHeld
        {
            get
            {
                Probe();
                if (_greenHeld != null) return _greenHeld();
                return ReadButtonSlow(_greenGet, _greenHeldGet);
            }
        }

        /// <summary>
        /// Cabinet height sensor A, 0..1, where BIGGER means the hand is CLOSER to the sensor
        /// (arcade-controls' own convention — see SerialParsers.HeightMmToNormalized: 100mm -> 1.0,
        /// 600mm -> 0.0). Zero when the facade is absent. This is the ladybug's actual steering
        /// input in the cabinet: HeightA/HeightB are the two hands the game already knows how to
        /// read, just delivered through the hub instead of this game's own sensor board.
        /// </summary>
        public static float HeightA
        {
            get
            {
                Probe();
                if (_heightA != null) return _heightA();
                return ReadHeightSlow(_heightAGet, _heightAValueGet);
            }
        }

        /// <summary>Cabinet height sensor B, same convention as <see cref="HeightA"/>.</summary>
        public static float HeightB
        {
            get
            {
                Probe();
                if (_heightB != null) return _heightB();
                return ReadHeightSlow(_heightBGet, _heightBValueGet);
            }
        }

        /// <summary>
        /// Span the normalized height is stretched over when handed to code that speaks the
        /// hand-sensor protocol's millimetres. arcade-controls reports 0..1 with BIGGER meaning
        /// CLOSER; GestureInput reads millimetres with SMALLER meaning closer and thresholds at
        /// 100mm ("hand down") / 200mm ("hand up"), so a 0..300mm span both inverts the sense and
        /// lands those two thresholds on even thirds of the sensor's travel.
        /// </summary>
        public const float HeightSensorSpanMm = 300f;

        /// <summary>
        /// Cabinet height (0..1, bigger = closer) -> the millimetres the game's own sensor
        /// readers publish. Lives here, not in either serial reader, so the two of them cannot
        /// drift apart on the cabinet's single pair of sensors.
        ///
        /// Zero is NOT a distance — it is "there is nothing in the band". arcade-controls
        /// produces it for a sensor with no target at all (SerialParsers.HeightMmToNormalized
        /// returns 0 for a negative reading, and the boards report a negative reading when the
        /// beam comes back empty) and, indistinguishably, for a hand at or past the far edge
        /// of the tuned band. This bridge therefore answers it the way the game's own protocol
        /// answers an empty sensor: -1, which GestureInput.HandStateForDistance reads as
        /// Neutral, and which SanitizeDistanceMm already passes through untouched.
        ///
        /// It used to answer 300mm — the far end of the span — and 300mm is past the 200mm
        /// "hand up" cutoff, so on the cabinet an EMPTY sensor read as a RAISED HAND. Both
        /// sensors empty meant "both hands up" forever; one hand over one sensor with nothing
        /// over the other was a fully formed lean, held for as long as the hand stayed there.
        /// That is not a corner case here: game.json declares this game's controls as
        /// HeightA/HeightB, so the hub launches it by holding a hand over a sensor, and the
        /// menu opened mid-phantom-lean every single time (it toggled a row value by itself
        /// before the player touched anything).
        ///
        /// The cost, stated plainly: a hand raised at or beyond HeightFarMm now reads Neutral
        /// instead of Up. Where the band ends is a tuning knob of the physical build
        /// (SerialTuning.HeightFarMm), not something this bridge should be guessing around —
        /// and the author's own boards have always called an out-of-range hand "no reading"
        /// (GestureInput.NoTargetMm), so this restores his semantics rather than inventing new.
        /// </summary>
        public static int HeightToSensorMm(float normalized)
        {
            if (normalized <= 0f)
                return -1;

            return Mathf.RoundToInt((1f - Mathf.Clamp01(normalized)) * HeightSensorSpanMm);
        }

        private static float ReadHeightSlow(MethodInfo controlGet, MethodInfo valueGet)
        {
            if (controlGet == null || valueGet == null) return 0f;
            object ctrl = controlGet.Invoke(null, null);
            if (ctrl == null) return 0f;
            return (float)valueGet.Invoke(ctrl, null);
        }

        private static bool ReadButtonSlow(MethodInfo controlGet, MethodInfo heldGet)
        {
            if (controlGet == null || heldGet == null) return false;
            object ctrl = controlGet.Invoke(null, null);
            if (ctrl == null) return false;
            return (bool)heldGet.Invoke(ctrl, null);
        }

        private static void Probe()
        {
            if (_probed) return;
            _probed = true;
            Type t = Type.GetType("AiGameStudio.ArcadeControls.ArcadeInput, AiGameStudio.ArcadeControls");
            if (t == null) return; // standalone: package absent — stay a no-op

            PropertyInfo joystick = t.GetProperty("Joystick", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo red = t.GetProperty("RedButton", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo green = t.GetProperty("GreenButton", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo heightA = t.GetProperty("HeightA", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo heightB = t.GetProperty("HeightB", BindingFlags.Public | BindingFlags.Static);

            // Fast path: compile static property chains into typed delegates.
            try
            {
                if (joystick != null)
                {
                    PropertyInfo vector = joystick.PropertyType.GetProperty("Vector");
                    if (vector != null && vector.PropertyType == typeof(Vector2))
                        _joystickVector = Expression.Lambda<Func<Vector2>>(
                            Expression.Property(Expression.Property(null, joystick), vector)).Compile();
                }
                _redHeld = CompileHeld(red);
                _greenHeld = CompileHeld(green);
                _heightA = CompileValue(heightA);
                _heightB = CompileValue(heightB);
            }
            catch (Exception)
            {
                // Some player backends can't compile expressions — fall back to cached Invoke below.
                _joystickVector = null;
                _redHeld = null;
                _greenHeld = null;
                _heightA = null;
                _heightB = null;
            }

            if (_joystickVector != null && _redHeld != null && _greenHeld != null
                && _heightA != null && _heightB != null)
                return; // fully compiled — no need for the fallback members

            // Fallback: cache the MethodInfos once (still no per-frame GetProperty lookups).
            if (joystick != null)
            {
                _joystickGet = joystick.GetGetMethod();
                PropertyInfo vec = _joystickGet.ReturnType.GetProperty("Vector");
                _vectorGet = vec != null ? vec.GetGetMethod() : null;
            }
            BindButton(red, ref _redGet, ref _redHeldGet);
            BindButton(green, ref _greenGet, ref _greenHeldGet);
            BindHeight(heightA, ref _heightAGet, ref _heightAValueGet);
            BindHeight(heightB, ref _heightBGet, ref _heightBValueGet);
        }

        private static Func<float> CompileValue(PropertyInfo heightProp)
        {
            if (heightProp == null) return null;
            PropertyInfo value = heightProp.PropertyType.GetProperty("Value");
            if (value == null || value.PropertyType != typeof(float)) return null;
            return Expression.Lambda<Func<float>>(
                Expression.Property(Expression.Property(null, heightProp), value)).Compile();
        }

        private static void BindHeight(PropertyInfo p, ref MethodInfo ctrlGet, ref MethodInfo valueGet)
        {
            if (p == null) return;
            ctrlGet = p.GetGetMethod();
            PropertyInfo value = ctrlGet.ReturnType.GetProperty("Value");
            valueGet = value != null ? value.GetGetMethod() : null;
        }

        private static Func<bool> CompileHeld(PropertyInfo buttonProp)
        {
            if (buttonProp == null) return null;
            PropertyInfo held = buttonProp.PropertyType.GetProperty("IsHeld");
            if (held == null || held.PropertyType != typeof(bool)) return null;
            return Expression.Lambda<Func<bool>>(
                Expression.Property(Expression.Property(null, buttonProp), held)).Compile();
        }

        private static void BindButton(PropertyInfo p, ref MethodInfo ctrlGet, ref MethodInfo heldGet)
        {
            if (p == null) return;
            ctrlGet = p.GetGetMethod();
            PropertyInfo held = ctrlGet.ReturnType.GetProperty("IsHeld");
            heldGet = held != null ? held.GetGetMethod() : null;
        }
    }
}
