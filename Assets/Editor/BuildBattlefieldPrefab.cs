using System.IO;
using UnityEditor;
using UnityEngine;
using Overlink.Match;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Builds the <see cref="BattlefieldPresenter"/>'s board geometry in <b>edit mode</b> and saves it as
    /// <c>Assets/Prefabs/Battlefield.prefab</c>, so the board is a real hierarchy object you can select,
    /// tweak and reuse instead of geometry that only exists once you press Play.
    /// <para>
    /// The prefab contains no behaviour - just the tile/label/caption hierarchy with the layout already
    /// baked in. At runtime <see cref="BattlefieldPresenter.BuildBoard"/> finds this child by name and
    /// <i>binds</i> to it, so the editable hierarchy is exactly what gets painted. If the child is missing
    /// (e.g. the legacy SampleScene setup) the presenter still builds the board procedurally, so nothing
    /// regresses.
    /// </para>
    /// </summary>
    public static class BuildBattlefieldPrefab
    {
        const string PrefabPath = "Assets/Prefabs/Battlefield.prefab";

        [MenuItem("Overlink/Battlefields/Build Battlefield Prefab")]
        public static void BuildFromMenu()
        {
            string message = Build() + "\n\nRebuild the Gameplay scene afterwards so its Match root uses " +
                "the prefab instance (Overlink > Scenes > Rebuild Gameplay Scene (No Prompt)).";
            EditorUtility.DisplayDialog("Battlefield Prefab", message, "OK");
        }

        // Prompt-free variant so menu automation and CI can rebuild without a modal dialog wedging the
        // editor. The interactive entry point above keeps its confirmation.
        [MenuItem("Overlink/Battlefields/Build Battlefield Prefab (No Prompt)")]
        public static void BuildFromMenuWithoutPrompt()
        {
            Debug.Log("[Overlink] " + Build());
        }

        static string Build()
        {
            // The presenter builds its board under a child named "Battlefield". Build that hierarchy on a
            // throwaway host, keep only the child, and save it - the host never survives to the prefab.
            var host = new GameObject("__BattlefieldBuild");
            try
            {
                var presenter = host.AddComponent<BattlefieldPresenter>();
                presenter.BuildBoard();

                Transform board = host.transform.Find("Battlefield");
                if (board == null)
                {
                    return "FAILED: BuildBoard() produced no 'Battlefield' child.";
                }

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

                // Strip the temporary host's identity so the prefab root is the board itself.
                board.SetParent(null, true);
                Object.DestroyImmediate(host);
                host = null;

                PrefabUtility.SaveAsPrefabAsset(board.gameObject, PrefabPath);
                Object.DestroyImmediate(board.gameObject);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                int children = CountDescendants(FindLoadedPrefab());
                return "Built " + PrefabPath + " (" + children + " objects in hierarchy).";
            }
            finally
            {
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                }
            }
        }

        static GameObject FindLoadedPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return prefab;
        }

        static int CountDescendants(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }
            return root.GetComponentsInChildren<Transform>(true).Length;
        }
    }
}
