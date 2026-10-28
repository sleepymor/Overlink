using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    // No [RequireComponent(typeof(BoxCollider))], and not for want of it: Unity refuses to remove a component from
    // an object whose script declares it required, so the attribute would make every card solid whether the caller
    // asked for that or not - silently, with a log line. A card in the fan is picked up by a physics raycast and has
    // to be solid, and <see cref="Build"/> adds that collider explicitly and sizes it. A card standing on the
    // tabletop must not be, because the board resolves clicks geometrically against its socket and a slab in front
    // of one is a raycast away from stealing an attack or a retreat.
    [RequireComponent(typeof(CardTween))]
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
        /// The footprint this card was built or sized to, in its own local units.
        /// <para>
        /// The card's size lives in the face's layout rather than in a Transform scale - every layer is placed
        /// from <see cref="CardFaceView.PixelPosition"/> against this rectangle - so without this the only way to
        /// ask a card how big it is would be to measure its renderers in world space, which is a pitched board's
        /// business rather than the card's.
        /// </para>
        /// </summary>
        public Vector3 Size { get; private set; } = CardSize;

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
            return Build(data, parent, size, frameCatalog, templateCatalog, fixture, tierMaterials, true);
        }

        /// <summary>
        /// Builds a card that stands on the tabletop instead of in the fan: the same face from the same
        /// catalogs, no collider, and parked in a sorting lane the caller owns.
        /// <para>
        /// The collider is the difference that matters. A hand card is picked up by a physics raycast
        /// (<c>HandController.RaycastCard</c>), so it has to be solid; a board card is not clicked at all —
        /// the pointer layer resolves a socket geometrically against the tile's own rectangle, and the card is
        /// decoration on top of it. Giving a board card a collider would put a solid slab in front of the socket
        /// it describes, which is one raycast hit away from stealing a click meant for an attack or a retreat.
        /// </para>
        /// <para>
        /// The sorting lane is the caller's business for the same reason: sprite order is global and cards do not
        /// write depth, so a board card that borrowed the hand's baseline arithmetic would interleave with the
        /// fan it is supposed to sit behind.
        /// </para>
        /// </summary>
        public static CardView CreateBoard(CardData data, Transform parent, Vector3 size,
            CardFrameCatalog frameCatalog, CardTemplateCatalog templateCatalog,
            CardPresentationFixture fixture, CardTierMaterialSet tierMaterials, int sortBaseline)
        {
            CardView view = Build(data, parent, size, frameCatalog, templateCatalog, fixture,
                tierMaterials, false);
            view.SetSortBaseline(sortBaseline);
            return view;
        }

        static CardView Build(CardData data, Transform parent, Vector3 size,
            CardFrameCatalog frameCatalog, CardTemplateCatalog templateCatalog,
            CardPresentationFixture fixture, CardTierMaterialSet tierMaterials, bool interactive)
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

            if (interactive)
            {
                var col = root.AddComponent<BoxCollider>();
                col.size = size;
            }

            // Only the interactive path gives a card a collider, and it does so itself, sized: a card in the fan is
            // picked up by a physics raycast and has to be solid. A card standing on the tabletop gets none, which
            // is why CardView no longer declares BoxCollider required - Unity will not let a required component be
            // removed, so the attribute would make every board card solid no matter what its factory did, and board
            // clicks are resolved geometrically against the socket instead.
            var view = root.AddComponent<CardView>();
            view.BaseScale = Vector3.one;
            view.Setup(data, size, frameCatalog, templateCatalog, fixture, tierMaterials);
            return view;
        }

        // Kept for Refresh: the face is a projection of the data through these, so re-binding later has to go
        // through the same four. A board card and a hand card differ in where they stand, not in what they are
        // drawn from, and that is only true if the second bind uses the first bind's catalogs.
        CardFrameCatalog frameCatalog;
        CardTemplateCatalog templateCatalog;
        CardPresentationFixture fixture;
        CardTierMaterialSet tierMaterials;

        /// <summary>The projection currently painted on this card, or null before the first bind.</summary>
        CardFaceData boundFace;

        void Setup(CardData data, Vector3 size, CardFrameCatalog frameCatalog,
            CardTemplateCatalog templateCatalog, CardPresentationFixture fixture,
            CardTierMaterialSet tierMaterials)
        {
            Data = data;
            Size = size;
            this.frameCatalog = frameCatalog;
            this.templateCatalog = templateCatalog;
            this.fixture = fixture;
            this.tierMaterials = tierMaterials;
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

            boundFace = faceData;
            Face.Bind(faceData, frameCatalog, tierMaterials);
            Face.SetSize(size);
            if (bodyRenderer != null && (frameCatalog != null || templateCatalog != null || fixture != null))
            {
                bodyRenderer.enabled = false;
            }
        }

        public void SetSize(Vector3 size)
        {
            Size = size;
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

        /// <summary>
        /// Re-projects this card's face from its bound data and repaints it, but only when the projection has
        /// actually moved. Returns true when it repainted.
        /// <para>
        /// A card in a hand is a fixed thing: its face was read once at creation and nothing afterwards can
        /// change it. A card standing on the tabletop is the same object with live numbers underneath, and the
        /// engine moves those on every exchange. The <see cref="CardData"/> it was built from is bound to the
        /// running <c>CardInstance</c>, so re-projecting through <see cref="CardFacePresenter.Build"/> reads what
        /// the engine holds now — which is what lets the board show "the same card you were just holding"
        /// without either view keeping a second copy of the card's appearance to keep in step.
        /// </para>
        /// <para>
        /// The equality guard is the reason this is affordable from a repaint pass that runs sixty times a second
        /// for a card whose numbers move a few times a match: a re-bind lays out every label on the face.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Only the projection is revisited. The frame template this card was instantiated from is keyed on type
        /// and rarity, both of which live on the immutable definition, so a refresh cannot legitimately need a
        /// different one; were it somehow to need one, the repaint would land on the template already in place
        /// and read as a wrong border rather than as a wrong rule.
        /// </remarks>
        public bool Refresh()
        {
            if (Face == null)
            {
                return false;
            }

            CardFaceData face = CardFacePresenter.Build(Data, fixture);
            if (face == null || (boundFace != null && boundFace.SameAs(face)))
            {
                return false;
            }

            boundFace = face;
            Face.Bind(face, frameCatalog, tierMaterials);
            return true;
        }

        /// <summary>
        /// Multiplies one colour over this card's whole face. This is the channel the board uses for spent, for
        /// summoned-this-step and for a nomination that is not a body yet, and the same one a card that has just
        /// been hit flashes through. See <c>CardFaceView.SetShade</c>.
        /// </summary>
        public void SetShade(Color shade)
        {
            if (Face != null) Face.SetShade(shade);
        }

        public void SetHovered(bool hovered) => IsHovered = hovered;
        public void SetDragging(bool dragging) => IsDragging = dragging;
    }
}
