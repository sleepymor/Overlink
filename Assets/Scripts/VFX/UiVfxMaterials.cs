using UnityEngine;

namespace Overlink.UI
{
    /// <summary>
    /// Runtime resolution point for the authored VFX materials. They live under
    /// <c>Assets/Resources/VFX</c> (generated once by <c>Overlink/VFX/Ensure VFX Assets</c>) so both
    /// the Editor and player builds resolve them through <c>Resources.Load</c> with no scene wiring
    /// and no per-widget material instances.
    /// </summary>
    public static class UiVfxMaterials
    {
        static Material sheen;
        static Material sheenWarm;

        public static Material Sheen => sheen != null ? sheen : (sheen = Resources.Load<Material>("VFX/UiSheen"));
        public static Material SheenWarm => sheenWarm != null ? sheenWarm : (sheenWarm = Resources.Load<Material>("VFX/UiSheenWarm"));
    }
}
