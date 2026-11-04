using UnityEngine;

namespace Overlink.Vfx
{
    /// <summary>
    /// Single accessibility gate for every authored UI effect. Reads the three persisted motion
    /// preferences straight from PlayerPrefs (Unity keeps them cached in memory, so per-frame reads
    /// are cheap and a Settings-screen toggle takes effect on the very next frame without any
    /// event plumbing):
    ///   • REDUCE SHAKE   — camera/panel trauma and knockback-style offsets,
    ///   • REDUCE FLASH   — additive flashes, sheens and brightness pops,
    ///   • REDUCED MOTION — the offline pack's master switch: stills tweens, idles and sheens alike.
    ///
    /// Widgets ask the gate, never the setting: <see cref="Motion"/> for anything that moves or
    /// scales, <see cref="Flash"/> for anything additive or bright, <see cref="Ambient"/> for idle
    /// breathing loops that never settle on their own.
    /// </summary>
    public static class UiVfxGate
    {
        const string ReduceShakeKey = "Overlink.Settings.ReduceShake";
        const string ReduceFlashKey = "Overlink.Settings.ReduceFlash";
        const string ReducedMotionKey = "Overlink.OfflinePack.ReducedMotion";

        public static bool ReduceShake => PlayerPrefs.GetInt(ReduceShakeKey, 0) != 0;
        public static bool ReduceFlash => PlayerPrefs.GetInt(ReduceFlashKey, 0) != 0;
        public static bool ReducedMotion => PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;

        /// <summary>Transform/scale animation allowed (tweens, pops, slides, shake).</summary>
        public static bool Motion => !(ReduceShake || ReducedMotion);

        /// <summary>Additive brightness allowed (sheens, flashes, glow pulses).</summary>
        public static bool Flash => !(ReduceFlash || ReducedMotion);

        /// <summary>Idle loops allowed (breathing backs, slow sheen sweeps, bar shimmer).</summary>
        public static bool Ambient => !ReducedMotion;

        /// <summary>Scales a shake/punch magnitude to zero when motion is reduced.</summary>
        public static float MotionScale(float amount) => Motion ? amount : 0f;

        /// <summary>Scales a flash/sheen intensity to zero when flashing is reduced.</summary>
        public static float FlashScale(float amount) => Flash ? amount : 0f;
    }
}
