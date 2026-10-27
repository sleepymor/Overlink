using UnityEngine;
using Overlink.Core;

namespace Overlink.Cards
{
    /// <summary>Temporary, explicitly named face data for presentation previews.</summary>
    [CreateAssetMenu(menuName = "Overlink/Cards/Presentation Fixture", fileName = "CardPresentationFixture")]
    public sealed class CardPresentationFixture : ScriptableObject
    {
        public string displayName = "Card Name";
        [Range(0, 10)] public int cost = 2;
        [TextArea(2, 6)] public string abilityText = "Ability text placeholder.";
        public Sprite artSprite;
        public Faction faction = Faction.Fsti;
        public Rarity rarity = Rarity.Common;
        public CardType type = CardType.Unit;
        public int atk = 3;
        public int hp = 5;
        public int shield = 1;
        public string id = "placeholder_card";

        public CardFaceData ToFaceData()
        {
            bool unit = type == CardType.Unit;
            return new CardFaceData(id, displayName, type, faction, rarity, cost, artSprite,
                unit ? atk : (int?)null,
                unit ? hp : (int?)null,
                unit ? shield : (int?)null,
                abilityText);
        }
    }
}
