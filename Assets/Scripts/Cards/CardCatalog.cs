using System.Collections.Generic;
using UnityEngine;

namespace Overlink.Cards
{
    [CreateAssetMenu(menuName = "Overlink/Card Catalog", fileName = "CardCatalog")]
    public sealed class CardCatalog : ScriptableObject
    {
        public List<CardAsset> cards = new List<CardAsset>();
        public Sprite cardBack;
    }
}
