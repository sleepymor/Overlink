using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    [RequireComponent(typeof(CardTween))]
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour
    {
        // All supplied frame textures use the same 832x1216 portrait canvas, so every tier keeps
        // that aspect. CardFaceView selects the per-tier inner art window instead of resizing the
        // card; the old 5:7 Legendary size silently squashed the frame canvas by ~4.6%.
        public static readonly Vector3 CardSize = new Vector3(1.0f, 1.4615f, 0.06f);

        /// <summary>
        /// Name of the compatibility cube that <see cref="Create"/> parents under a non-presentation
        /// card. Presentation cards have no such child at all. It is a public const because the
        /// hierarchy is built by <see cref="Create"/> but read back by <see cref="Setup"/> and
        /// <see cref="SetSize"/>: spelling it out at each site is how the two halves lost track of
        /// one another, so every reference goes through this one name.
        /// </summary>
        public const string LegacyBodyName = "LegacyBody";

        // No per-tier size override lives here any more. Legendary used to resolve to its own
        // constant, which was harmless while every caller drew at CardSize but meant that in a
        // gallery laid out at any smaller cardSize the Legendary card alone ignored it and grew
        // back to full size, overlapping its neighbours. Every tier now honours the caller's
        // footprint, which is what the gallery's cell spacing assumes.

        public CardData Data { get; private set; }
        public int SlotIndex { get; set; }
        public bool IsDragging { get; private set; }
        public bool IsHovered { get; private set; }
        public Vector3 BaseScale { get; private set; } = Vector3.one;
        public CardTween Tween => tween;
        public CardFaceView Face { get; private set; }

        /// <summary>
        /// Assigns this card its own slice of the global sorting space. See
        /// <see cref="CardFaceView.SetSortBaseline"/>; the hand owns the policy.
        /// </summary>
        public void SetSortBaseline(int baseline)
        {
            if (Face != null) Face.SetSortBaseline(baseline);
        }

        CardTween tween;
        Renderer bodyRenderer;

        public static CardView Create(CardData data, Transform parent)
        {
            return Create(data, parent, CardSize, null, null, null, null);
        }

        public static CardView Create(CardData data, Transform parent, Vector3 size)
        {
            return Create(data, parent, size, null, null, null, null);
        }

        public static CardView Create(CardData data, Transform parent, Vector3 size,
            CardFrameCatalog frameCatalog, CardTemplateCatalog templateCatalog,
            CardPresentationFixture fixture)
        {
            return Create(data, parent, size, frameCatalog, templateCatalog, fixture, null);
        }

        public static CardView Create(CardData data, Transform parent, Vector3 size,
            CardFrameCatalog frameCatalog, CardTemplateCatalog templateCatalog,
            CardPresentationFixture fixture, CardTierMaterialSet tierMaterials)
        {
            var root = new GameObject($"Card_{(data != null ? data.id : "placeholder")}");
            root.transform.SetParent(parent, false);

            bool presentationMode = frameCatalog != null || templateCatalog != null || fixture != null;
            if (!presentationMode)
            {
                // Legacy compatibility path only. Presentation cards have no cube body at all;
                // the root collider below remains the interaction surface.
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = LegacyBodyName;
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = size;
                var bodyCollider = body.GetComponent<BoxCollider>();
                if (bodyCollider != null) Object.Destroy(bodyCollider);
                PlaceLegacyBody(size, body.transform);
            }

            var col = root.AddComponent<BoxCollider>();
            col.size = size;

            var view = root.AddComponent<CardView>();
            view.BaseScale = Vector3.one;
            view.Setup(data, size, frameCatalog, templateCatalog, fixture, tierMaterials);
            return view;
        }

        void Setup(CardData data, Vector3 size, CardFrameCatalog frameCatalog,
            CardTemplateCatalog templateCatalog, CardPresentationFixture fixture,
            CardTierMaterialSet tierMaterials)
        {
            Data = data;
            tween = GetComponent<CardTween>();
            var body = transform.Find(LegacyBodyName);
            bodyRenderer = body != null ? body.GetComponent<Renderer>() : null;
            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = data != null ? data.accent : Color.gray;
            }

            CardFaceData faceData = CardFacePresenter.Build(data, fixture);
            CardFaceView template = templateCatalog != null && faceData != null
                ? templateCatalog.Get(faceData.Type, faceData.Rarity)
                : null;
            if (template != null)
            {
                Face = Object.Instantiate(template, transform, false);
                Face.name = "CardFace";
            }
            else
            {
                var faceObject = new GameObject("CardFace");
                faceObject.transform.SetParent(transform, false);
                Face = faceObject.AddComponent<CardFaceView>();
            }

            Face.Bind(faceData, frameCatalog, tierMaterials);
            Face.SetSize(size);
            if (bodyRenderer != null && (frameCatalog != null || templateCatalog != null || fixture != null))
            {
                bodyRenderer.enabled = false;
            }
        }

        public void SetSize(Vector3 size)
        {
            var body = transform.Find(LegacyBodyName);
            if (body != null) PlaceLegacyBody(size, body);
            var col = GetComponent<BoxCollider>();
            if (col != null) col.size = size;
            if (Face != null) Face.SetSize(size);
        }

        /// <summary>
        /// Sizes the legacy placeholder cube and holds it behind the card's face plane.
        /// <para>
        /// Every layer of a real card face is co-planar at local z = 0, which is what stops perspective from
        /// pulling a wing card's text out of its plate (see <c>CardFaceView.PixelPosition</c>). This cube is
        /// the one opaque thing a card can carry, and it is opaque on the <em>legacy</em> path only - a
        /// presentation card disables it - so it has to be the thing that moves. Its near face therefore
        /// rests just behind the plane the text sits on, and reads as a backing slab under the placeholder
        /// frame instead of swallowing the label the way it would from in front of it.
        /// </para>
        /// </summary>
        static void PlaceLegacyBody(Vector3 size, Transform body)
        {
            body.localScale = size;
            body.localPosition = new Vector3(0f, 0f, size.z * 0.5f + 0.01f);
        }

        public void SetHovered(bool hovered) => IsHovered = hovered;
        public void SetDragging(bool dragging) => IsDragging = dragging;
    }
}
