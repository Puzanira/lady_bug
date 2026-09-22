using System;
using System.Reflection;
using UnityEngine;

// ---------------------------------------------------------------------------
// An EDITOR-ONLY stand-in for the arcade cabinet's shared controls package.
//
// Why it exists: several editor tools (the screenshot still, the arcade guard
// tests) have to observe the game the way the cabinet runs it — inside the
// launcher. The game decides that for itself, honestly and at runtime, by
// probing for the launcher's facade:
//
//     Type.GetType("AiGameStudio.ArcadeControls.ArcadeInput, AiGameStudio.ArcadeControls")
//
// (see LadyBug.ArcadeControlsReader). Standalone that type is absent, so
// ArcadeControlsReader.Available/InsideArcadeLauncher are false and the game
// behaves exactly as the author wrote it: the loader's attract screen runs, the
// flower intro runs, the serial readers own the boards.
//
// This file supplies a type of that exact name/shape from an editor assembly
// and hands it to the runtime through AppDomain.AssemblyResolve, but ONLY after
// a tool calls Install(). Nothing here runs on load, nothing is [InitializeOnLoad],
// and the whole assembly is Editor-only — so pressing Play by hand, a player
// build, and the game running on the real cabinet are all untouched. Inside the
// hub the REAL package is present, the resolve event never fires, and the game
// binds to the real facade.
//
// The stub reports neutral values (stick centred, no buttons, hands far away):
// it is here to answer "am I inside the launcher?", not to fake input.
// ---------------------------------------------------------------------------

namespace AiGameStudio.ArcadeControls
{
    /// <summary>
    /// Neutral stand-in for one logical cabinet control. One type carries all three
    /// member shapes ArcadeControlsReader probes for (Vector / IsHeld / Value), so the
    /// stub stays a single small class instead of a mirror of the whole package.
    /// </summary>
    public sealed class StubControl
    {
        // Written by editor tools/tests through LadyBug.ArcadeLauncherStub; read by the
        // game through its own reflection probe, live, exactly like the real facade.
        public Vector2 VectorValue;
        public bool HeldValue;
        public float RawValue;

        public Vector2 Vector { get { return VectorValue; } }
        public bool IsHeld { get { return HeldValue; } }
        public float Value { get { return RawValue; } }

        public void Reset()
        {
            VectorValue = Vector2.zero;
            HeldValue = false;
            RawValue = 0f;
        }
    }

    /// <summary>
    /// Same name, namespace and static surface as the launcher's real
    /// AiGameStudio.ArcadeControls.ArcadeInput — that is the whole point: the game's
    /// reflection probe must find it and bind without a single special case in game code.
    /// </summary>
    public static class ArcadeInput
    {
        public static StubControl Joystick { get; private set; }
        public static StubControl RedButton { get; private set; }
        public static StubControl GreenButton { get; private set; }
        public static StubControl HeightA { get; private set; }
        public static StubControl HeightB { get; private set; }

        static ArcadeInput()
        {
            Joystick = new StubControl();
            RedButton = new StubControl();
            GreenButton = new StubControl();
            HeightA = new StubControl();
            HeightB = new StubControl();
        }
    }
}

namespace LadyBug
{
    /// <summary>
    /// Installs the stub facade above for the current editor session.
    /// Editor tools call <see cref="Install"/>; game code never does and never can
    /// (this assembly is Editor-only and nothing in Scripts/ references it).
    /// </summary>
    public static class ArcadeLauncherStub
    {
        private const string FacadeAssemblyName = "AiGameStudio.ArcadeControls";
        private const string ReaderTypeName = "LadyBug.ArcadeControlsReader";

        private static bool _installed;

        /// <summary>
        /// Make the game see itself as running inside the arcade launcher, for the rest of
        /// this editor session. Throws if the game still cannot see the facade afterwards —
        /// a silent no-op here would mean a tool quietly photographing / testing the wrong
        /// thing, which is the bug this whole file exists to kill.
        /// </summary>
        public static void Install()
        {
            if (_installed)
                return;
            _installed = true;

            AppDomain.CurrentDomain.AssemblyResolve += ResolveFacadeAssembly;

            // The reader caches its probe in a static the first time anything asks.
            // If something already asked during this session (before the resolver was
            // installed) it cached "absent" — clear that so the probe runs again.
            ResetReaderProbe();

            if (GameSeesTheLauncher())
            {
                Debug.Log("[ArcadeLauncherStub] the game bound to the stub facade through "
                          + "AppDomain.AssemblyResolve — its own probe, its own code path.");
                return;
            }

            // Fallback for runtimes that refuse to satisfy an assembly reference from a
            // differently-named assembly: hand the reader its compiled getters directly.
            // Same end state (Available == true, every control neutral), reached without
            // the assembly-resolve hop.
            SeedReaderDelegates();

            if (!GameSeesTheLauncher())
                throw new InvalidOperationException(
                    "[ArcadeLauncherStub] The game still does not see the arcade facade. "
                    + "ArcadeControlsReader's probe or its private fields changed shape — "
                    + "re-point this stub at them, otherwise every editor tool that relies on "
                    + "'running inside the launcher' silently observes the standalone game instead.");
        }

        /// <summary>True when LadyBug.ArcadeControlsReader.Available reports the facade.</summary>
        public static bool GameSeesTheLauncher()
        {
            PropertyInfo available = ReaderType().GetProperty(
                "Available", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (available == null)
                throw new InvalidOperationException(
                    "[ArcadeLauncherStub] ArcadeControlsReader.Available is gone — the game's "
                    + "single 'am I in the cabinet?' answer moved. Editor tools depend on it.");
            return (bool)available.GetValue(null, null);
        }

        private static Assembly ResolveFacadeAssembly(object sender, ResolveEventArgs args)
        {
            string requested = new AssemblyName(args.Name).Name;
            if (requested != FacadeAssemblyName)
                return null;
            return typeof(AiGameStudio.ArcadeControls.ArcadeInput).Assembly;
        }

        private static Type ReaderType()
        {
            // The reader is internal to the game's runtime assembly; reach it through a
            // public type that lives beside it rather than by assembly name string.
            Type reader = typeof(JoystickSerial).Assembly.GetType(ReaderTypeName);
            if (reader == null)
                throw new InvalidOperationException(
                    "[ArcadeLauncherStub] " + ReaderTypeName + " not found in the game assembly.");
            return reader;
        }

        private static void ResetReaderProbe()
        {
            FieldInfo probed = ReaderType().GetField("_probed", BindingFlags.NonPublic | BindingFlags.Static);
            if (probed != null)
                probed.SetValue(null, false);
        }

        private static void SeedReaderDelegates()
        {
            Debug.Log("[ArcadeLauncherStub] assembly-resolve route refused by this runtime — "
                      + "handing the reader its getters directly instead (same stub values).");
            Type reader = ReaderType();
            SetStatic(reader, "_probed", true);
            SetStatic(reader, "_joystickVector",
                (Func<Vector2>)(() => AiGameStudio.ArcadeControls.ArcadeInput.Joystick.Vector));
            SetStatic(reader, "_redHeld",
                (Func<bool>)(() => AiGameStudio.ArcadeControls.ArcadeInput.RedButton.IsHeld));
            SetStatic(reader, "_greenHeld",
                (Func<bool>)(() => AiGameStudio.ArcadeControls.ArcadeInput.GreenButton.IsHeld));
            SetStatic(reader, "_heightA",
                (Func<float>)(() => AiGameStudio.ArcadeControls.ArcadeInput.HeightA.Value));
            SetStatic(reader, "_heightB",
                (Func<float>)(() => AiGameStudio.ArcadeControls.ArcadeInput.HeightB.Value));
        }

        /// <summary>
        /// Put every cabinet control back to neutral: stick centred, no button held,
        /// both hands far from the height sensors. Tests call this between cases.
        /// </summary>
        public static void ResetControls()
        {
            AiGameStudio.ArcadeControls.ArcadeInput.Joystick.Reset();
            AiGameStudio.ArcadeControls.ArcadeInput.RedButton.Reset();
            AiGameStudio.ArcadeControls.ArcadeInput.GreenButton.Reset();
            AiGameStudio.ArcadeControls.ArcadeInput.HeightA.Reset();
            AiGameStudio.ArcadeControls.ArcadeInput.HeightB.Reset();
        }

        /// <summary>Deflect the cabinet stick (each axis -1..1, +y = up, +x = right).</summary>
        public static void SetJoystick(Vector2 deflection)
        {
            AiGameStudio.ArcadeControls.ArcadeInput.Joystick.VectorValue = deflection;
        }

        private static void SetStatic(Type type, string field, object value)
        {
            FieldInfo info = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static);
            if (info != null)
                info.SetValue(null, value);
        }
    }
}
