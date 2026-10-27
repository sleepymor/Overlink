using Overlink.Core;

namespace Overlink.Cards
{
    /// <summary>Builds a presentation-only face without owning a Unity hierarchy.</summary>
    public static class CardFacePresenter
    {
        public static CardFaceData Build(CardData data, CardPresentationFixture fixture = null)
        {
            if (data != null && data.Instance != null && data.Definition != null)
            {
                CardDefinition definition = data.Definition;
                CardInstance instance = data.Instance;
                bool unit = definition.Type == CardType.Unit;
                return new CardFaceData(
                    definition.Id,
                    definition.Name,
                    definition.Type,
                    definition.Faction,
                    definition.Rarity,
                    definition.Cost,
                    null,
                    unit ? instance.CurrentAtk : (int?)null,
                    unit ? instance.CurrentHp : (int?)null,
                    unit ? instance.CurrentShield : (int?)null,
                    definition.AbilityText);
            }

            if (fixture != null)
            {
                return fixture.ToFaceData();
            }

            CardType fallbackType = CardType.Ability;
            return new CardFaceData(
                string.IsNullOrEmpty(data != null ? data.id : null) ? "placeholder_card" : data.id,
                string.IsNullOrEmpty(data != null ? data.title : null) ? "Card Name" : data.title,
                fallbackType,
                Faction.Fsti,
                Rarity.Common,
                data != null ? data.cost : 2,
                null,
                null,
                null,
                null,
                data != null && !string.IsNullOrEmpty(data.description) ? data.description : "Ability placeholder.");
        }
    }
}
