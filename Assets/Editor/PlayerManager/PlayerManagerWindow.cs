using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Overlink.Cards;
using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    public sealed class PlayerManagerWindow : EditorWindow
    {
        // Intentionally not serialized: player data and admin credentials must not enter layouts/assets.
        TestingPlayerManagerClient client;
        CancellationTokenSource lifetime;
        PlayerManagerState loaded;
        JObject data;
        readonly Dictionary<string, CardAsset> cards = new Dictionary<string, CardAsset>(StringComparer.Ordinal);
        int[] balances;
        string playerId = "";
        string reason = "";
        string filter = "";
        string status = "Enter a Testing Player ID and load its Cloud Save progression.";
        bool busy;
        bool stale;
        bool playerStopped;
        int tab;
        int addBalanceIndex;
        int addAmount = 1000;
        Vector2 scroll;

        [MenuItem("Overlink/Tools/Player Manager")]
        public static void Open()
        {
            var window = GetWindow<PlayerManagerWindow>("Player Manager");
            window.minSize = new Vector2(700, 520);
        }

        void OnEnable()
        {
            client = new TestingPlayerManagerClient();
            lifetime = new CancellationTokenSource();
            loaded = null;
            data = null;
            busy = false;
            ReindexCards();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            client?.Dispose();
            client = null;
            loaded = null;
            data = null;
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                lifetime?.Cancel();
                stale = true;
                playerStopped = false;
                status = "Play Mode invalidated this snapshot. Refresh after leaving Play Mode. Any interrupted save must be checked on the server.";
            }
            if (state == PlayModeStateChange.EnteredEditMode && !busy)
            {
                lifetime?.Dispose();
                lifetime = new CancellationTokenSource();
            }
            Repaint();
        }

        bool CorrectProject => CloudProjectSettings.projectId == TestingPlayerManagerClient.ProjectId;

        void OnGUI()
        {
            EditorGUILayout.LabelField("PLAYER MANAGER — TESTING ONLY", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel("Environment: testing / " + TestingPlayerManagerClient.EnvironmentId,
                GUILayout.Height(18));
            EditorGUILayout.LabelField("Project: " + TestingPlayerManagerClient.ProjectId, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("Administrative tool. Sign the target player out before changing balances. "
                + "This window never edits card definitions, ownership, decks, purchase counters, or receipts.", MessageType.Warning);
            if (!CorrectProject)
                EditorGUILayout.HelpBox("The Unity project is not linked to the expected Overlink project. Network actions are disabled.", MessageType.Error);
            if (!TestingPlayerManagerClient.HasCredentials)
                EditorGUILayout.HelpBox("Set " + TestingPlayerManagerClient.KeyIdVariable + " and "
                    + TestingPlayerManagerClient.SecretVariable + " outside the project. Windows user variables are read directly; no restart required. See docs/PlayerManager.md. "
                    + "Never paste service-account secrets into the project or chat.", MessageType.Info);
            EditorGUILayout.LabelField("Credential source: " + TestingPlayerManagerClient.CredentialSource, EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(busy || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                playerId = EditorGUILayout.TextField("Testing Player ID", playerId);
                if (GUILayout.Button("Recheck credentials"))
                {
                    client.ResetAuthentication();
                    status = TestingPlayerManagerClient.HasCredentials
                        ? "Credentials found. Enter the in-game Player ID, then click Load / Refresh from Testing. API permissions are checked when loading."
                        : "No complete credential pair found. Set both Windows user variables; do not paste secrets into this window.";
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!CorrectProject || !TestingPlayerManagerClient.HasCredentials || string.IsNullOrWhiteSpace(playerId)))
                        if (GUILayout.Button("Load / Refresh from Testing")) LoadPlayer();
                    if (GUILayout.Button("Reload local card names", GUILayout.Width(180))) ReindexCards();
                }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Stop Play Mode to use Player Manager.", MessageType.Warning);
            else if (string.IsNullOrWhiteSpace(playerId))
                EditorGUILayout.HelpBox("Enter an in-game Player ID to enable Load / Refresh. This is not your Unity developer account ID.", MessageType.Info);
            EditorGUILayout.HelpBox(busy ? "Contacting Unity Testing…" : status, stale ? MessageType.Warning : MessageType.Info);
            if (loaded == null || data == null) return;

            EditorGUILayout.LabelField("Loaded Player: " + loaded.PlayerId, EditorStyles.boldLabel);
            bool targetChanged = !string.Equals(playerId.Trim(), loaded.PlayerId, StringComparison.Ordinal);
            if (targetChanged) EditorGUILayout.HelpBox("The ID input differs from the loaded player. Load it before editing.", MessageType.Warning);
            tab = GUILayout.Toolbar(tab, new[] { "Balances", "Owned cards", "Decks & progression" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawBalances(targetChanged);
            else if (tab == 1) DrawCollection();
            else DrawProgression();
            EditorGUILayout.EndScrollView();
        }

        void DrawBalances(bool targetChanged)
        {
            string[] labels = { "Signal Credits", "Data Fragments", "Unopened standard packs", "Unopened loyalty packs" };
            using (new EditorGUI.DisabledScope(busy || stale || targetChanged || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.LabelField("Add to balance", EditorStyles.boldLabel);
                addBalanceIndex = EditorGUILayout.Popup("Balance to increase", addBalanceIndex, labels);
                addAmount = EditorGUILayout.IntField("Amount to add", addAmount);
                long resultingTotal = (long)balances[addBalanceIndex] + addAmount;
                bool canAdd = addAmount > 0 && balances[addBalanceIndex] >= 0 && resultingTotal <= int.MaxValue;
                if (canAdd)
                    EditorGUILayout.LabelField("Resulting total", balances[addBalanceIndex] + " + " + addAmount + " = " + resultingTotal);
                else
                    EditorGUILayout.HelpBox("Enter a positive amount that keeps the total within 0–2,147,483,647.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(!canAdd))
                    if (GUILayout.Button("Add amount to pending total"))
                    {
                        balances[addBalanceIndex] = PlayerManagerState.AddToBalance(balances[addBalanceIndex], addAmount);
                        status = "Amount added to the pending total only. Enter a reason, confirm the player is signed out, then review and save below.";
                    }
                EditorGUILayout.HelpBox("Add stages the change below; it does not send anything to Unity until you review and save.", MessageType.Info);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Pending totals (editable)", EditorStyles.boldLabel);
                for (int i = 0; i < balances.Length; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        balances[i] = EditorGUILayout.IntField(labels[i], balances[i]);
                        EditorGUILayout.LabelField("Server: " + loaded.Balance(i), GUILayout.Width(140));
                    }
                }
                reason = EditorGUILayout.TextField("Reason (required)", reason);
                playerStopped = EditorGUILayout.ToggleLeft("The target player is signed out / not playing on any client", playerStopped);
                bool changed = Enumerable.Range(0, balances.Length).Any(i => balances[i] != loaded.Balance(i));
                using (new EditorGUI.DisabledScope(!CorrectProject || !TestingPlayerManagerClient.HasCredentials || !playerStopped
                    || !changed || balances.Any(value => value < 0) || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 512))
                    if (GUILayout.Button("Review and save to Testing…", GUILayout.Height(28))) SavePlayer();
            }
            if (balances.Any(value => value < 0)) EditorGUILayout.HelpBox("Balances cannot be negative.", MessageType.Error);
            EditorGUILayout.HelpBox("A write conflict requires a refresh; no force overwrite or automatic retry is offered. "
                + "After saving, sign in again to load the new balances in-game. Loyalty/standard pack grants do not advance paid-purchase loyalty progress.", MessageType.Info);
            EditorGUILayout.LabelField("Local adjustment audit (not a server-side audit):", EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(TestingPlayerManagerClient.AuditDirectory, GUILayout.Height(35));
        }

        void DrawCollection()
        {
            filter = EditorGUILayout.TextField("Filter name / ID / rarity", filter);
            var owned = data["Owned"] as JObject;
            if (owned == null) { EditorGUILayout.HelpBox("No Owned dictionary in this record.", MessageType.Info); return; }
            EditorGUILayout.LabelField(owned.Count + " card IDs in the server collection", EditorStyles.boldLabel);
            foreach (JProperty entry in owned.Properties().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                cards.TryGetValue(entry.Name, out CardAsset card);
                string name = card != null ? card.cardName : "[Local card asset missing]";
                string rarity = card != null ? card.rarity.ToString() : "Unknown";
                string searchable = name + " " + entry.Name + " " + rarity;
                if (!string.IsNullOrWhiteSpace(filter) && searchable.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(entry.Value + " ×", GUILayout.Width(50));
                    EditorGUILayout.LabelField(name, GUILayout.MinWidth(180));
                    EditorGUILayout.LabelField(entry.Name, GUILayout.Width(140));
                    EditorGUILayout.LabelField(rarity, GUILayout.Width(80));
                    using (new EditorGUI.DisabledScope(card == null))
                        if (GUILayout.Button("Asset", GUILayout.Width(48))) EditorGUIUtility.PingObject(card);
                }
            }
        }

        void DrawProgression()
        {
            foreach (string key in new[] { "SchemaVersion", "StarterGranted", "StarterFaction", "Level", "Xp", "Wins", "Losses", "Draws",
                "PurchasedPacks", "LoyaltyGranted", "ActiveDeckId", "BattleDeckId" })
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(key), data[key]?.ToString() ?? "—");
            var decks = data["Decks"] as JArray;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Saved decks (read-only)", EditorStyles.boldLabel);
            if (decks == null || decks.Count == 0) EditorGUILayout.LabelField("No saved decks.");
            else foreach (JObject deck in decks.OfType<JObject>())
            {
                EditorGUILayout.LabelField((string)deck["Name"] + " / " + (string)deck["Id"] + " / " + (string)deck["Faction"], EditorStyles.boldLabel);
                if (deck["Cards"] is JObject deckCards)
                    foreach (JProperty entry in deckCards.Properties())
                    {
                        cards.TryGetValue(entry.Name, out CardAsset card);
                        EditorGUILayout.LabelField("    " + entry.Value + " × " + (card != null ? card.cardName : entry.Name) + "  [" + entry.Name + "]");
                    }
            }
        }

        void ReindexCards()
        {
            cards.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:CardAsset", new[] { "Assets/Cards/Data" }))
            {
                CardAsset card = AssetDatabase.LoadAssetAtPath<CardAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null && !string.IsNullOrEmpty(card.id) && !cards.ContainsKey(card.id)) cards.Add(card.id, card);
            }
        }

        async void LoadPlayer()
        {
            string target = playerId.Trim();
            await Run(async cancellation =>
            {
                loaded = null;
                data = null;
                Apply(await client.LoadAsync(target, cancellation));
                status = "Loaded from Testing. Collection metadata comes from local CardAssets.";
            });
        }

        async void SavePlayer()
        {
            PlayerManagerState snapshot = loaded;
            int[] edited = (int[])balances.Clone();
            string changeReason = reason.Trim();
            string changes = string.Join("\n", Enumerable.Range(0, edited.Length)
                .Where(i => edited[i] != snapshot.Balance(i))
                .Select(i => PlayerManagerState.BalanceFields[i] + ": " + snapshot.Balance(i) + " → " + edited[i]));
            if (!EditorUtility.DisplayDialog("Save TESTING balances?",
                "Player: " + snapshot.PlayerId + "\nEnvironment: testing\n\n" + changes + "\n\nReason: " + changeReason,
                "Save to Testing", "Cancel")) return;
            await Run(async cancellation =>
            {
                stale = true; // A failed/ambiguous write may have reached Unity. Require refresh even then.
                await client.SaveAsync(snapshot, edited, changeReason, cancellation);
                Apply(await client.LoadAsync(snapshot.PlayerId, cancellation));
                reason = "";
                status = "Saved to Testing and refreshed from the server. Sign in again to verify in-game.";
            });
        }

        void Apply(PlayerManagerState state)
        {
            int[] next = Enumerable.Range(0, PlayerManagerState.BalanceFields.Length).Select(state.Balance).ToArray();
            loaded = state;
            data = state.ReadData();
            balances = next;
            stale = false;
            playerStopped = false;
        }

        async Task Run(Func<CancellationToken, Task> action)
        {
            if (busy || !CorrectProject || EditorApplication.isPlayingOrWillChangePlaymode || lifetime == null) return;
            if (lifetime.IsCancellationRequested)
            {
                lifetime.Dispose();
                lifetime = new CancellationTokenSource();
            }
            CancellationToken cancellation = lifetime.Token;
            busy = true;
            try { await action(cancellation); }
            catch (OperationCanceledException) { status = "Request cancelled or timed out. Refresh before editing; an interrupted save may have reached Unity."; }
            catch (Exception e) { status = e.Message + " Refresh before retrying an adjustment."; }
            finally
            {
                busy = false;
                if (this != null) Repaint();
            }
        }
    }
}
