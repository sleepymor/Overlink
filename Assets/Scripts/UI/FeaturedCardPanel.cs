using Overlink.Cards;
using Overlink.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>Shows one randomly chosen featured card, with its menu image, on the main menu.</summary>
    public sealed class FeaturedCardPanel : MonoBehaviour
    {
        [System.Serializable]
        public sealed class FeaturedEntry
        {
            public CardAsset card;
            public Sprite backdrop;
        }

        [SerializeField] FeaturedEntry[] entries = new FeaturedEntry[0];
        [SerializeField] Image backdropImage;
        [SerializeField] TextMeshProUGUI eyebrowText;
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] TextMeshProUGUI flavourText;

        void Start()
        {
            if (entries == null || entries.Length == 0)
            {
                Debug.LogWarning("[MainMenu] FeaturedCardPanel has no entries assigned.");
                return;
            }

            int index = FeaturedCardPicker.PickIndex(entries.Length, n => Random.Range(0, n));
            Show(entries[index]);
        }

        void Show(FeaturedEntry entry)
        {
            if (entry == null || entry.card == null) return;

            var card = entry.card;
            if (backdropImage != null && entry.backdrop != null)
            {
                backdropImage.sprite = entry.backdrop;
                backdropImage.color = Color.white;
            }
            if (eyebrowText != null)
                eyebrowText.text = FeaturedCardPicker.ShortFactionToken(card.faction) + "  /  "
                    + card.type.ToString().ToUpperInvariant() + "  /  " + card.id.ToUpperInvariant();
            if (nameText != null) nameText.text = card.cardName.ToUpperInvariant();
            if (flavourText != null) flavourText.text = card.flavourText;
        }
    }
}
