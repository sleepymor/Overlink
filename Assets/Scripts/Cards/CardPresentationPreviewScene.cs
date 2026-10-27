using TMPro;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    /// <summary>
    /// Marker and small runtime affordance for the standalone card presentation preview.
    /// This scene intentionally has no ProtectedSceneGuard or AuthFlow dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardPresentationPreviewScene : MonoBehaviour
    {
        [SerializeField] HandController hand;
        [SerializeField] bool legendaryPreview;
        [SerializeField] bool galleryPreview;
        // Where the TOP EDGE of the header and subtitle sit, as a fraction of the camera's ortho
        // half-height. Both blocks grow downward from there, sized by their own line count, so the
        // subtitle has to start below the header's two lines: at the default 1.35 ortho a two-line
        // header occupies 0.243 world units, which puts its foot at 0.945 and rules out anything
        // above 0.70 for the subtitle. The faction matrix packs three rows into one view, so its
        // cards reach higher and the header lifts clear of them.
        [SerializeField] float headerLift = 0.88f;
        [SerializeField] float subtitleLift = 0.68f;

        public void ConfigurePreview(bool legendary)
        {
            legendaryPreview = legendary;
        }

        void Start()
        {
            string title = galleryPreview
                ? (GallerySpansMultipleFactions()
                    ? "LOGIN BYPASS  •  FRAME MATRIX  " + GalleryType() + "  (faction x tier)"
                    : "LOGIN BYPASS  •  CARD COLLECTION PREVIEW  (" + GalleryFaction() + " " + GalleryType() + ")")
                : legendaryPreview
                    ? "LOGIN BYPASS  •  LEGENDARY CARD PRESENTATION"
                    : "LOGIN BYPASS  •  PLACEHOLDER CARD PRESENTATION";

            // Anchor the header to the camera's actual orthographic bounds so it stays on screen
            // whatever framing the preview scene is built with, instead of hard-coded world Y.
            Camera camera = Camera.main;
            float top = camera != null && camera.orthographic ? camera.orthographicSize : 3.1f;
            float aspect = camera != null && camera.aspect > 0.01f ? camera.aspect : 16f / 9f;
            // Leave a margin so the title never runs under the LOG button at the screen edge.
            float usableWidth = 2f * top * aspect * 0.80f;

            string hint = galleryPreview
                ? "Hover to enlarge   Hold to inspect   (no draw or discard: this is a fixed gallery)"
                : "Space: draw   Backspace: discard   Drag: interact";

            CreateLabel("LoginBypassLabel", new Vector3(0f, top * Mathf.Clamp(headerLift, 0.5f, 0.995f), 0f),
                usableWidth, top * 0.062f, 2, title + "\n" + hint);
            CreateLabel("PreviewSubtitle", new Vector3(0f, top * Mathf.Clamp(subtitleLift, 0.4f, 0.95f), 0f),
                usableWidth, top * 0.040f, 1,
                "Dedicated smoke-test scene — no authentication or protected-scene routing");

            if (hand != null)
            {
                hand.autoDraw = !galleryPreview;
            }

            if (galleryPreview)
            {
                // HandController populates the gallery one frame after its own Start, so the cards do
                // not exist yet here. Waiting for them keeps the captions anchored to real positions
                // instead of a row of zeros.
                StartCoroutine(CreateGalleryCaptionsWhenReady(top));
            }
        }

        System.Collections.IEnumerator CreateGalleryCaptionsWhenReady(float top)
        {
            yield return new WaitUntil(() => hand != null && hand.CardCount > 0);
            // One more frame so the gallery's slots are applied before the captions read them.
            yield return null;
            CreateGalleryCaptions(top);
        }

        /// <summary>
        /// Names each gallery card with its faction and tier, so a screenshot is a per-tier record
        /// rather than four lookalike rectangles. The captions sit in the gap under each cell, which
        /// is why the gallery's cell height is deliberately taller than the card.
        /// </summary>
        void CreateGalleryCaptions(float top)
        {
            if (hand == null || hand.Cards == null) return;
            var fixtures = hand.galleryFixtures;
            bool multiFaction = GallerySpansMultipleFactions();
            for (int i = 0; i < hand.Cards.Count; i++)
            {
                CardView card = hand.Cards[i];
                if (card == null) continue;
                CardPresentationFixture fixture = fixtures != null && i < fixtures.Length
                    ? fixtures[i] : null;
                string rarityName = fixture != null ? fixture.rarity.ToString() : card.Data != null
                    ? card.Data.title : "Card";
                // In a single-faction row the header already names the faction, so repeating it on
                // every card is noise; in the matrix it is the row identity and has to be on screen.
                string caption = fixture != null && multiFaction
                    ? fixture.faction.ToString().ToUpperInvariant() + "  •  " + rarityName.ToUpperInvariant()
                    : rarityName.ToUpperInvariant();
                string effect = fixture == null ? string.Empty : EffectCaption(fixture.rarity);
                // Recompute the cell from the same layout the gallery uses rather than reading
                // card.transform: PopulateGallery tweens each card out from the hand origin, so a
                // caption snapshotted a frame or two in lands wherever the tween has reached. That
                // put the top row's caption below its card and the bottom row's on top of it, while
                // the middle row — whose slot is near the origin — looked right by luck.
                HandController.ComputeGallerySlot(i, hand.Cards.Count, hand.galleryColumns,
                    hand.galleryCellWidth, hand.galleryCellHeight, hand.galleryTopY,
                    out Vector3 slot, out Quaternion _);
                // Top-anchored just under the card, so the block grows downward into the cell gap
                // that galleryCellHeight deliberately leaves for it.
                bool twoLine = !string.IsNullOrEmpty(effect);
                CreateLabel("GalleryCaption_" + i,
                    new Vector3(slot.x, slot.y - hand.cardSize.y * 0.5f - 0.06f, 0f),
                    hand.galleryCellWidth * 0.95f, top * 0.030f, twoLine ? 2 : 1,
                    caption + (twoLine ? "\n" + effect : string.Empty));
            }
        }

        /// <summary>The faction every gallery fixture shares, or the first one if they differ.</summary>
        string GalleryFaction()
        {
            var fixtures = hand != null ? hand.galleryFixtures : null;
            if (fixtures == null || fixtures.Length == 0 || fixtures[0] == null) return "FSTI";
            return fixtures[0].faction.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// The card type every gallery fixture shares. The matrix scenes are one type each, so this is
        /// constant within a scene; deriving it from the fixtures rather than storing it on the
        /// preview component means the header cannot disagree with what the gallery actually holds.
        /// </summary>
        string GalleryType()
        {
            var fixtures = hand != null ? hand.galleryFixtures : null;
            if (fixtures == null || fixtures.Length == 0 || fixtures[0] == null) return "UNIT";
            return fixtures[0].type.ToString().ToUpperInvariant();
        }

        bool GallerySpansMultipleFactions()
        {
            var fixtures = hand != null ? hand.galleryFixtures : null;
            if (fixtures == null || fixtures.Length < 2) return false;
            Faction first = fixtures[0].faction;
            for (int i = 1; i < fixtures.Length; i++)
            {
                if (fixtures[i] != null && fixtures[i].faction != first) return true;
            }
            return false;
        }

        static string EffectCaption(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return "no effect";
                case Rarity.Rare: return "foil band";
                case Rarity.Epic: return "prismatic facets";
                default: return "foil sweep + dust";
            }
        }

        // Same technique as the card labels: render the TMP atlas at a large point size and shrink
        // the transform, so header text stays crisp instead of rasterising at a few points.
        //
        // <paramref name="topEdge"/> is the TOP-CENTRE of the block, and the rect is exactly
        // <paramref name="lines"/> tall with top-centre alignment, so a label's vertical footprint is
        // predictable from its line count. The previous version centred the text in a fixed 3.2 em
        // box, which reserved a third line's worth of space for a single-line subtitle and none at
        // all for a two-line caption below the last grid row: the header and subtitle overlapped each
        // other and the bottom captions ran off the viewport as soon as auto-sizing stopped shrinking
        // them to hide it.
        static void CreateLabel(string name, Vector3 topEdge, float worldWidth,
            float worldEm, int lines, string value)
        {
            const float atlasSize = 96f;
            const float lineHeightFactor = 1.45f;
            float blockHeight = worldEm * lineHeightFactor * Mathf.Max(1, lines);
            float scale = worldEm / atlasSize;
            GameObject labelObject = new GameObject(name);
            labelObject.transform.localScale = new Vector3(scale, scale, scale);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = value;
            label.fontSize = atlasSize;
            // Auto-sizing is deliberately off. It makes TMP resize the rect to the text's preferred
            // width, which throws away the sizeDelta below and leaves enableWordWrapping with nothing
            // to wrap against, so a long caption simply ran into the neighbouring cell. It also
            // fought the scale: worldEm/atlasSize at fontSize=atlasSize already renders the exact
            // intended em, so auto-sizing could only shrink the font and make a caption smaller than
            // designed. The rect is the authority on width, the scale on size.
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.alignment = TextAlignmentOptions.Top;
            // Overflow rather than Truncate: a caption that wraps to an unexpected third line must
            // stay legible, and the block is sized to the caller's line count, not to the text.
            label.overflowMode = TextOverflowModes.Overflow;
            label.isOrthographic = true;
            // Above every card slice: HandController tops out at 10300, and the play-slot card can
            // reach into the header band, so a low order here would let a card cover the caption.
            label.sortingOrder = 20000;
            label.rectTransform.sizeDelta = new Vector2(worldWidth / scale, blockHeight / scale);
            labelObject.transform.position =
                new Vector3(topEdge.x, topEdge.y - blockHeight * 0.5f, topEdge.z);
            label.color = Color.white;
        }
    }
}
