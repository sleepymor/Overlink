using System;
using System.Collections.Generic;
using System.IO;
using Overlink.Cards;
using Overlink.Core;
using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    public sealed class CardExportPreview
    {
        public bool HasErrors;
        public string Json;
        public int CardCount;
        public int WarningCount;
        public readonly List<ValidationIssue> BlockingIssues = new List<ValidationIssue>();
        public readonly List<ValidationIssue> Warnings = new List<ValidationIssue>();
    }

    public static class CardExporter
    {
        public const string DefaultExportFolder = "Assets/Overlink/Generated";
        public const string DefaultExportFileName = "cards.json";

        [MenuItem("Overlink/Cards/Export Card JSON %#e", false, 21)]
        public static void ExportFromMenu()
        {
            ExportInteractive(FindAllCards());
        }

        public static List<CardAsset> FindAllCards()
        {
            var cards = new List<CardAsset>();
            string[] guids = AssetDatabase.FindAssets("t:CardAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<CardAsset>(path);
                if (asset != null)
                {
                    cards.Add(asset);
                }
            }

            cards.Sort(CompareById);
            return cards;
        }

        public static int CompareById(CardAsset left, CardAsset right)
        {
            string leftId = left != null && left.id != null ? left.id : string.Empty;
            string rightId = right != null && right.id != null ? right.id : string.Empty;
            return string.CompareOrdinal(leftId, rightId);
        }

        public static List<CardDefinition> ToDefinitions(List<CardAsset> cards)
        {
            var definitions = new List<CardDefinition>();
            if (cards == null)
            {
                return definitions;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    definitions.Add(cards[i].ToDefinition());
                }
            }

            definitions.Sort(CompareDefinitionsById);
            return definitions;
        }

        public static int CompareDefinitionsById(CardDefinition left, CardDefinition right)
        {
            string leftId = left != null && left.Id != null ? left.Id : string.Empty;
            string rightId = right != null && right.Id != null ? right.Id : string.Empty;
            return string.CompareOrdinal(leftId, rightId);
        }

        public static CardExportPreview BuildPreview(List<CardAsset> cards)
        {
            var preview = new CardExportPreview();
            List<CardDefinition> definitions = ToDefinitions(cards);
            preview.CardCount = definitions.Count;

            ValidationResult validation = CardValidator.ValidatePool(definitions);
            for (int i = 0; i < validation.Issues.Count; i++)
            {
                ValidationIssue issue = validation.Issues[i];
                if (issue.Severity == Severity.Error)
                {
                    preview.BlockingIssues.Add(issue);
                }
                else if (issue.Severity == Severity.Warning)
                {
                    preview.Warnings.Add(issue);
                }
            }

            preview.HasErrors = preview.BlockingIssues.Count > 0;
            preview.WarningCount = preview.Warnings.Count;

            if (!preview.HasErrors)
            {
                preview.Json = CardJson.Serialize(definitions, CardComposition.FromCards(definitions));
            }

            return preview;
        }

        public static void ExportInteractive(List<CardAsset> cards)
        {
            CardExportPreview preview = BuildPreview(cards);

            if (preview.HasErrors)
            {
                for (int i = 0; i < preview.BlockingIssues.Count; i++)
                {
                    Debug.LogError("[CardExport] " + preview.BlockingIssues[i]);
                }

                EditorUtility.DisplayDialog("Export Card JSON", "Export refused: " + preview.BlockingIssues.Count + " blocking validation error(s). See the Console.", "OK");
                return;
            }

            EnsureFolder(DefaultExportFolder);
            string path = EditorUtility.SaveFilePanel("Export Card JSON", DefaultExportFolder, DefaultExportFileName, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            WriteExport(path, preview);
        }

        public static void WriteExport(string absolutePath, CardExportPreview preview)
        {
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            if (preview.HasErrors || string.IsNullOrEmpty(preview.Json))
            {
                throw new InvalidOperationException("Refusing to export while validation errors are present.");
            }

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolutePath, preview.Json);
            AssetDatabase.Refresh();

            Debug.Log("[CardExport] Wrote " + preview.CardCount + " card(s), " + preview.WarningCount + " warning(s) to " + absolutePath);
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
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}