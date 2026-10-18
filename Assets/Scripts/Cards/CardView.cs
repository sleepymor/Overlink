using TMPro;
using UnityEngine;

namespace Overlink.Cards
{
    [RequireComponent(typeof(CardTween))]
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour
    {
        // Card face is a 5:7 portrait (width:height), the standard trading-card look. Keep the X:Y ratio at
        // 5:7 when retuning the size so the Title/Cost/Desc layout below stays aligned.
        public static readonly Vector3 CardSize = new Vector3(1.0f, 1.4f, 0.06f);

        public CardData Data { get; private set; }
        public int SlotIndex { get; set; }
        public bool IsDragging { get; private set; }
        public bool IsHovered { get; private set; }

        /// <summary>
        /// Resting scale of this card's root transform. The root is only an animation container: the card's
        /// real footprint lives on the <c>Body</c> child (see <see cref="SetSize"/>), so the root rests at
        /// <see cref="Vector3.one"/> and every tween scales it uniformly. Keeping the root uniform is also
        /// what stops the Title/Cost/Desc labels (children of the root) from being stretched.
        /// </summary>
        public Vector3 BaseScale { get; private set; } = Vector3.one;

        CardTween tween;
        Renderer bodyRenderer;

        public CardTween Tween => tween;

        public static CardView Create(CardData data, Transform parent)
        {
            return Create(data, parent, CardSize);
        }

        public static CardView Create(CardData data, Transform parent, Vector3 size)
        {
            var root = new GameObject($"Card_{data.id}");
            root.transform.SetParent(parent, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = size;
            var bodyCollider = body.GetComponent<BoxCollider>();
            if (bodyCollider != null) Destroy(bodyCollider);

            var col = root.AddComponent<BoxCollider>();
            col.size = size;

            var view = root.AddComponent<CardView>();
            view.BaseScale = Vector3.one;
            view.Setup(data);
            return view;
        }

        /// <summary>
        /// Resizes the card face. The visible size lives on the <c>Body</c> child and the root collider, not
        /// on the root transform's scale, so <see cref="BaseScale"/> (the animation scale) is left alone.
        /// </summary>
        public void SetSize(Vector3 size)
        {
            var body = transform.Find("Body");
            if (body != null) body.localScale = size;
            var col = GetComponent<BoxCollider>();
            if (col != null) col.size = size;
        }

        void Setup(CardData data)
        {
            Data = data;
            tween = GetComponent<CardTween>();

            var body = transform.Find("Body");
            bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.material.color = data.accent;

            MakeLabel("Title", new Vector3(0f, 0.36f, -0.045f), new Vector2(40f, 13f), 10, data.title, false);
            MakeLabel("Cost", new Vector3(-0.30f, 0.52f, -0.045f), new Vector2(11f, 11f), 14, data.cost.ToString(), false);
            MakeLabel("Desc", new Vector3(0f, -0.16f, -0.045f), new Vector2(40f, 26f), 20, data.description, true);
        }

        TextMeshPro MakeLabel(string labelName, Vector3 localPos, Vector2 rect,
            int baseSize, string text, bool autoFit)
        {
            var go = new GameObject(labelName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.02f;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = baseSize;
            tmp.enableAutoSizing = autoFit;
            tmp.fontSizeMin = 8;
            tmp.fontSizeMax = 36;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = autoFit;
            tmp.overflowMode = autoFit ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            tmp.isOrthographic = true;
            tmp.rectTransform.sizeDelta = rect;
            tmp.color = Color.white;
            return tmp;
        }

        public void SetHovered(bool hovered)
        {
            IsHovered = hovered;
        }

        public void SetDragging(bool dragging)
        {
            IsDragging = dragging;
        }
    }
}
