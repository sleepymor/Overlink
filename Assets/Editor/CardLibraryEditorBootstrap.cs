using Overlink.Cards;
using UnityEditor;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Registers the card pool in the Editor on every domain load, so EditMode tests and Editor tools see the same
    /// 150 cards the runtime loads from Resources.
    /// </summary>
    [InitializeOnLoad]
    public static class CardLibraryEditorBootstrap
    {
        static CardLibraryEditorBootstrap()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CardLibraryBootstrap.CatalogAssetPath);
            if (catalog != null)
            {
                CardLibraryBootstrap.Register(catalog);
            }
        }
    }
}
