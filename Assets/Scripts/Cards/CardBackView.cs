using UnityEngine;

namespace Overlink.Cards
{
    /// <summary>
    /// A face-down card: the shared card back, sized like a hand card. Used for the deck pile, where cards sit
    /// unrevealed. It has no collider and no CardData, so it cannot be picked up or bound.
    /// </summary>
    public sealed class CardBackView : MonoBehaviour
    {
        SpriteRenderer backRenderer;

        public static CardBackView Create(Transform parent, Vector3 size, int sortingOrder)
        {
            var root = new GameObject("CardBack");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<CardBackView>();
            view.backRenderer = root.AddComponent<SpriteRenderer>();
            view.backRenderer.sortingOrder = sortingOrder;
            view.SetSize(size);
            return view;
        }

        public void SetSize(Vector3 size)
        {
            Sprite sprite = CardLibraryBootstrap.CardBack;
            if (backRenderer == null) return;
            backRenderer.sprite = sprite;
            if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                transform.localScale = Vector3.one;
                return;
            }

            transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
        }
    }
}
