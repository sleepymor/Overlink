using System.Collections.Generic;
using Overlink.Core;

namespace Overlink.Persistence
{
    /// <summary>
    /// Runtime account-data seam. The legacy PlayerPrefs implementation remains available for isolated
    /// editor tests, but a signed-in build installs the Cloud Code implementation before protected scenes
    /// are opened. Persistence deliberately knows nothing about Unity Services or networking.
    /// </summary>
    public interface ICloudProgressionBackend
    {
        bool IsReady { get; }

        IReadOnlyList<DeckSummary> AllDecks();
        string ActiveDeckId { get; }
        string BattleDeckId { get; }
        DeckData LoadBattleDeck();
        DeckData LoadDeck(string id);
        string NameOf(string id);
        string CreateDeck(string name, Faction faction);
        void SaveDeck(string id, string name, DeckData data);
        void SetActiveDeck(string id);
        void SetBattleDeck(string id);
        void DeleteDeck(string id);
        void DeleteAllDecks();
        int CopiesInActive(string cardId);
        bool SetActiveCopies(string cardId, int copies);
    }
}
