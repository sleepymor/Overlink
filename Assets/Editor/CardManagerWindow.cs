using System.Collections.Generic;
using System.IO;
using Overlink.Cards;
using Overlink.Core;
using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    /// <summary>
    /// M2 Card Manager: one window to browse, filter, validate, author and export <see cref="CardAsset"/> content.
    /// <para>
    /// Inline authoring edits go through <see cref="SerializedObject"/>/<see cref="SerializedProperty"/>, so a
    /// change captures an Undo step, marks the asset dirty, and re-runs validation live. "Save" persists the
    /// dirty assets with <see cref="AssetDatabase.SaveAssets"/> and refreshes the browser. Export is refused
    /// while any card has blocking (Error severity) issues.
    /// </para>
    /// <para>
    /// Limitation: <see cref="EffectDescriptor"/> exposes no keyword payload field, so an <c>AddKeyword</c>
    /// effect cannot name the keyword it grants. The editor documents this rather than inventing a convention;
    /// see the note drawn above the effects list.
    /// </para>
    /// </summary>
    public sealed class CardManagerWindow : EditorWindow
    {
        /// <summary>Project-relative folder the "Create Card" button places new assets in.</summary>
        private const string CardFolder = "Assets/Cards";

        private static readonly string[] FactionOptions = { "Any Faction", "FSTI", "FRTI", "FPB" };
        private static readonly string[] RarityOptions = { "Any Rarity", "Common", "Rare", "Epic", "Legendary" };
        private static readonly string[] TypeOptions = { "Any Type", "Unit", "Ability", "Relay", "Protocol" };

        private readonly List<CardAsset> cards = new List<CardAsset>();
        private readonly List<CardDefinition> definitions = new List<CardDefinition>();
        private readonly List<ValidationResult> results = new List<ValidationResult>();
        private readonly List<int> visible = new List<int>();

        private string idFilter = string.Empty;
        private string nameFilter = string.Empty;
        private int factionFilter;
        private int rarityFilter;
        private int typeFilter;
        private int selected = -1;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private bool showWarnings = true;
        private bool showInfo;

        private SerializedObject serializedCard;
        private CardAsset serializedCardAsset;

        [MenuItem("Overlink/Cards/Card Manager", false, 20)]
        public static void Open()
        {
            var window = GetWindow<CardManagerWindow>();
            window.titleContent = new GUIContent("Card Manager");
            window.minSize = new Vector2(860f, 520f);
            window.Refresh();
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            cards.Clear();
            cards.AddRange(CardExporter.FindAllCards());

            // The asset set was re-queried; drop the cached SerializedObject so it re-binds to the selected asset.
            serializedCard = null;
            serializedCardAsset = null;

            RebuildDefinitions();

            if (selected >= cards.Count)
            {
                selected = cards.Count - 1;
            }

            RebuildVisible();
            Repaint();
        }

        /// <summary>
        /// Regenerates every <see cref="CardDefinition"/> and its validation from the in-memory asset list,
        /// without re-querying the AssetDatabase. Called by <see cref="Refresh"/> and after each inline edit so
        /// validation, list highlighting and composition counts stay live.
        /// </summary>
        private void RebuildDefinitions()
        {
            definitions.Clear();
            results.Clear();

            for (int i = 0; i < cards.Count; i++)
            {
                CardAsset asset = cards[i];
                definitions.Add(asset != null ? asset.ToDefinition() : null);
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                results.Add(CardValidator.Validate(definitions[i], definitions));
            }
        }

        private void RebuildVisible()
        {
            visible.Clear();

            for (int i = 0; i < cards.Count; i++)
            {
                if (MatchesFilters(i))
                {
                    visible.Add(i);
                }
            }

            if (selected < 0 || !visible.Contains(selected))
            {
                selected = visible.Count > 0 ? visible[0] : -1;
            }
        }

        private bool MatchesFilters(int index)
        {
            CardAsset asset = cards[index];
            CardDefinition definition = definitions[index];
            if (asset == null || definition == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(idFilter) && (definition.Id == null || definition.Id.IndexOf(idFilter, System.StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(nameFilter) && (definition.Name == null || definition.Name.IndexOf(nameFilter, System.StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            if (factionFilter > 0 && (int)definition.Faction != factionFilter - 1)
            {
                return false;
            }

            if (rarityFilter > 0 && (int)definition.Rarity != rarityFilter - 1)
            {
                return false;
            }

            if (typeFilter > 0 && (int)definition.Type != typeFilter - 1)
            {
                return false;
            }

            return true;
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (cards.Count == 0)
            {
                EditorGUILayout.HelpBox("No CardAsset found in the project. Create one below, or use Assets > Create > Overlink > Card, then press Refresh.", MessageType.Info);
                EditorGUILayout.Space();
                if (GUILayout.Button("Create Card", GUILayout.Height(32f)))
                {
                    CreateNewCard();
                }

                return;
            }

            DrawComposition();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Id", GUILayout.Width(28f));
            string newId = EditorGUILayout.TextField(idFilter, EditorStyles.toolbarTextField, GUILayout.MinWidth(80f));
            GUILayout.Label("Name", GUILayout.Width(42f));
            string newName = EditorGUILayout.TextField(nameFilter, EditorStyles.toolbarTextField, GUILayout.MinWidth(80f));
            factionFilter = EditorGUILayout.Popup(factionFilter, FactionOptions, EditorStyles.toolbarPopup, GUILayout.Width(105f));
            rarityFilter = EditorGUILayout.Popup(rarityFilter, RarityOptions, EditorStyles.toolbarPopup, GUILayout.Width(105f));
            typeFilter = EditorGUILayout.Popup(typeFilter, TypeOptions, EditorStyles.toolbarPopup, GUILayout.Width(95f));

            if (newId != idFilter || newName != nameFilter)
            {
                idFilter = newId;
                nameFilter = newName;
                RebuildVisible();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create Card", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                CreateNewCard();
            }

            int errorCount = CountCardsWithErrors();
            string exportLabel = errorCount > 0 ? "Export Blocked (" + errorCount + ")" : "Export JSON";
            if (GUILayout.Button(exportLabel, EditorStyles.toolbarButton, GUILayout.Width(160f)))
            {
                Export();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                Refresh();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawComposition()
        {
            CardComposition composition = CardComposition.FromCards(definitions);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Pool: " + composition.Total + " card(s) FSTI " + composition.Fsti + " FRTI " + composition.Frti + " FPB " + composition.Fpb);
            EditorGUILayout.LabelField("Common " + composition.Common + " Rare " + composition.Rare + " Epic " + composition.Epic + " Legendary " + composition.Legendary);
            EditorGUILayout.EndVertical();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(360f));
            EditorGUILayout.LabelField("Cards (" + visible.Count + " of " + cards.Count + ")", EditorStyles.boldLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
            for (int v = 0; v < visible.Count; v++)
            {
                int index = visible[v];
                CardDefinition definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                bool hasError = results[index] != null && results[index].HasErrors;
                string label = (definition.Id == null ? "(no id)" : definition.Id) + " " + (definition.Name == null ? "(no name)" : definition.Name) + " [" + definition.Rarity + "/" + definition.Type + "]";

                Color previous = GUI.backgroundColor;
                if (hasError)
                {
                    GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                }
                else if (index == selected)
                {
                    GUI.backgroundColor = new Color(0.65f, 0.8f, 1f);
                }

                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(20f)))
                {
                    selected = index;
                }

                GUI.backgroundColor = previous;
            }

            if (visible.Count == 0)
            {
                EditorGUILayout.HelpBox("No card matches the current filters.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical();
            if (selected < 0 || selected >= cards.Count)
            {
                EditorGUILayout.HelpBox("Select a card to inspect or edit it.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            CardAsset asset = cards[selected];
            CardDefinition definition = definitions[selected];
            if (asset == null || definition == null)
            {
                EditorGUILayout.HelpBox("The selected asset could not be loaded. Press Refresh.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EnsureSerializedCard(asset);

            bool saveRequested = false;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(definition.Name ?? "(no name)", EditorStyles.boldLabel);
            if (GUILayout.Button("Select Asset", GUILayout.Width(100f)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            if (GUILayout.Button("Save", GUILayout.Width(70f)))
            {
                saveRequested = true;
            }

            EditorGUILayout.EndHorizontal();

            // SaveSelected calls Refresh, which drops the cached SerializedObject; bail out of this pass so the
            // remainder of the editor does not touch the now-null serialized object.
            if (saveRequested)
            {
                SaveSelected(asset);
                EditorGUILayout.EndVertical();
                return;
            }

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));

            serializedCard.Update();

            EditorGUILayout.PropertyField(serializedCard.FindProperty("id"), new GUIContent("ID"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("cardName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("faction"), new GUIContent("Faction"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("rarity"), new GUIContent("Rarity"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("type"), new GUIContent("Type"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("cost"), new GUIContent("Cost"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("atk"), new GUIContent("ATK"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("hp"), new GUIContent("HP"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("shield"), new GUIContent("Shield"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Keywords", EditorStyles.boldLabel);
            DrawKeywordsEditor(serializedCard.FindProperty("keywords"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "AddKeyword limitation: EffectDescriptor has no keyword payload field, so an AddKeyword effect cannot name the keyword it grants. " +
                "ReferenceId here is a card-id reference used by the validator, not a keyword selector. This is a schema gap, not an editor omission.",
                MessageType.None);
            DrawEffectsEditor(serializedCard.FindProperty("effects"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedCard.FindProperty("abilityText"), new GUIContent("Ability Text"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("designNote"), new GUIContent("Design Note"));
            EditorGUILayout.PropertyField(serializedCard.FindProperty("artPath"), new GUIContent("Art Path"));

            // Undo is registered before the staged edits are committed: RecordObject snapshots the asset's
            // pre-edit state, then the changes are applied without a second (duplicate) undo entry, the asset
            // is marked dirty, and validation is regenerated so errors, list highlighting and composition
            // counts update live.
            if (serializedCard.hasModifiedProperties)
            {
                Undo.RecordObject(asset, "Edit Card");
                serializedCard.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                RebuildDefinitions();
                Repaint();
            }

            EditorGUILayout.Space();
            DrawValidation(results[selected]);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            showWarnings = EditorGUILayout.ToggleLeft("Warnings", showWarnings, GUILayout.Width(90f));
            showInfo = EditorGUILayout.ToggleLeft("Info", showInfo, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawKeywordsEditor(SerializedProperty keywords)
        {
            if (keywords == null)
            {
                return;
            }

            for (int i = 0; i < keywords.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(keywords.GetArrayElementAtIndex(i), GUIContent.none);
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    DeleteArrayElement(keywords, i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Keyword", GUILayout.Width(120f)))
            {
                int index = keywords.arraySize;
                keywords.InsertArrayElementAtIndex(index);
                keywords.GetArrayElementAtIndex(index).enumValueIndex = (int)Keyword.Piercing;
            }
        }

        private void DrawEffectsEditor(SerializedProperty effects)
        {
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.arraySize; i++)
            {
                SerializedProperty effect = effects.GetArrayElementAtIndex(i);
                if (effect == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Effect " + (i + 1), EditorStyles.boldLabel);

                SerializedProperty typeProperty = effect.FindPropertyRelative("type");
                EditorGUILayout.PropertyField(typeProperty, new GUIContent("Type"));
                EditorGUILayout.PropertyField(effect.FindPropertyRelative("target"), new GUIContent("Target"));
                EditorGUILayout.PropertyField(effect.FindPropertyRelative("amount"), new GUIContent("Amount"));
                EditorGUILayout.PropertyField(effect.FindPropertyRelative("amount2"), new GUIContent("Amount2"));
                EditorGUILayout.PropertyField(effect.FindPropertyRelative("duration"), new GUIContent("Duration"));
                EditorGUILayout.PropertyField(effect.FindPropertyRelative("referenceId"), new GUIContent("ReferenceId"));
                EditorGUILayout.PropertyField(effect.FindPropertyRelative("fast"), new GUIContent("Fast"));

                if (typeProperty != null && (EffectType)typeProperty.enumValueIndex == EffectType.AddKeyword)
                {
                    EditorGUILayout.HelpBox("AddKeyword: no keyword payload field exists on EffectDescriptor, so the granted keyword cannot be authored here.", MessageType.Info);
                }

                if (GUILayout.Button("Remove Effect"))
                {
                    DeleteArrayElement(effects, i);
                    break;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (GUILayout.Button("Add Effect", GUILayout.Width(120f)))
            {
                int index = effects.arraySize;
                effects.InsertArrayElementAtIndex(index);
                SerializedProperty added = effects.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("type").enumValueIndex = (int)EffectType.DealDamage;
                added.FindPropertyRelative("target").enumValueIndex = (int)TargetKind.EnemyActive;
                added.FindPropertyRelative("amount").intValue = 1;
                added.FindPropertyRelative("referenceId").stringValue = string.Empty;
            }
        }

        /// <summary>
        /// Removes an array element. For arrays of reference types, the first delete clears the slot to null
        /// rather than shrinking the array, so a second delete is required; the size check keeps value-type
        /// arrays (for example <see cref="Keyword"/>) to a single delete.
        /// </summary>
        private static void DeleteArrayElement(SerializedProperty array, int index)
        {
            int previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == previousSize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }

        private void DrawValidation(ValidationResult result)
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            bool anyShown = false;
            if (result != null && result.Issues != null)
            {
                for (int i = 0; i < result.Issues.Count; i++)
                {
                    ValidationIssue issue = result.Issues[i];
                    if (issue.Severity == Severity.Info && !showInfo)
                    {
                        continue;
                    }

                    if (issue.Severity == Severity.Warning && !showWarnings)
                    {
                        continue;
                    }

                    MessageType messageType = issue.Severity == Severity.Error ? MessageType.Error : (issue.Severity == Severity.Warning ? MessageType.Warning : MessageType.Info);
                    EditorGUILayout.HelpBox(issue.ToString(), messageType);
                    anyShown = true;
                }
            }

            if (!anyShown)
            {
                EditorGUILayout.HelpBox("No issues at the current visibility settings.", MessageType.Info);
            }
        }

        private void EnsureSerializedCard(CardAsset asset)
        {
            if (serializedCardAsset == asset && serializedCard != null)
            {
                return;
            }

            serializedCardAsset = asset;
            serializedCard = new SerializedObject(asset);
        }

        private void SaveSelected(CardAsset asset)
        {
            // Edits are committed as they are made (see DrawDetail). Save only persists them.
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Refresh();
        }

        private void CreateNewCard()
        {
            EnsureFolder(CardFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(CardFolder + "/card_new.asset");

            var card = CreateInstance<CardAsset>();
            card.id = string.Empty;
            card.cardName = "New Card";
            card.faction = Faction.Fsti;
            card.rarity = Rarity.Common;
            card.type = CardType.Unit;
            card.cost = 1;
            card.atk = 1;
            card.hp = 1;
            card.shield = 0;
            card.keywords = new List<Keyword>();
            card.effects = new List<CardEffectEntry>();

            AssetDatabase.CreateAsset(card, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Refresh();

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == card)
                {
                    selected = i;
                    break;
                }
            }

            Selection.activeObject = card;
            EditorGUIUtility.PingObject(card);
            Repaint();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder);
            if (!string.IsNullOrEmpty(parent))
            {
                parent = parent.Replace("\\", "/");
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private int CountCardsWithErrors()
        {
            int count = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] != null && results[i].HasErrors)
                {
                    count++;
                }
            }

            return count;
        }

        private void Export()
        {
            CardExportPreview preview = CardExporter.BuildPreview(cards);
            if (preview.HasErrors)
            {
                for (int i = 0; i < preview.BlockingIssues.Count; i++)
                {
                    Debug.LogError("[CardExport] " + preview.BlockingIssues[i]);
                }

                EditorUtility.DisplayDialog("Export Card JSON", "Export refused: " + preview.BlockingIssues.Count + " blocking validation error(s). See the Console.", "OK");
                return;
            }

            CardExporter.ExportInteractive(cards);
        }
    }
}
