using TMPro;
using UnityEngine;
using Overlink.Core;

namespace Overlink.Cards
{
    /// <summary>Visual hierarchy and field binding for one card face.</summary>
    public sealed class CardFaceView : MonoBehaviour
    {
        public SpriteRenderer ArtImage;
        public SpriteRenderer FrameImage;
        public SpriteMask ArtMask;
        public TextMeshPro CostValue;
        public TextMeshPro NameValue;
        public TextMeshPro TypeValue;
        public TextMeshPro AtkValue;
        public TextMeshPro HpValue;
        public TextMeshPro ShieldValue;
        public TextMeshPro AbilityValue;
        public TextMeshPro IdValue;
        public TextMeshPro AtkLabel;
        public GameObject CombatStatsRoot;

        [Header("Card material")]
        public CardTierMaterialSet TierMaterials;

        public CardFaceData Current { get; private set; }
        Vector3 faceSize = new Vector3(1f, 1.4615f, 0.01f);

        // Every supplied frame is authored on the same 832x1216 portrait canvas, but Standard and
        // Legendary do NOT share an internal layout. Both zone sets below were measured
        // pixel-for-pixel from the delivered art (see docs/CARD-FRAME-SPEC.md), so the face follows
        // each frame instead of forcing Legendary into the Standard rectangle or stretching art.
        const float FrameWidthPixels = 832f;
        const float FrameHeightPixels = 1216f;

        /// <summary>Horizontal anchor of a label inside its zone.</summary>
        public enum ZoneAlign
        {
            Center,
            Left,
            Right,
            /// <summary>Left-aligned and hung from the top of the zone, for wrapped ability text.</summary>
            TopLeft
        }

        /// <summary>
        /// One text slot, entirely in 832x1216 canvas pixels: centre, bounding box, the desired cap
        /// height and the horizontal anchor. Deriving world scale from Em keeps glyph size
        /// proportional to the frame at any card resolution.
        /// </summary>
        public struct LabelZone
        {
            public readonly float X, Y, W, H, Em;
            public readonly ZoneAlign Align;
            public LabelZone(float x, float y, float w, float h, float em,
                ZoneAlign align = ZoneAlign.Center)
            {
                X = x; Y = y; W = w; H = h; Em = em; Align = align;
            }
        }

        /// <summary>
        /// Zone set for one frame tier, expressed on the 832x1216 canvas. Public so the template
        /// generator and the runtime face share one source of truth instead of drifting.
        /// </summary>
        public sealed class FaceZones
        {
            public float ArtX, ArtY, ArtW, ArtH;
            // Vertical bias for the cover crop: 0 keeps the top of the art, 1 keeps the bottom.
            // Portrait art in a wide window otherwise crops straight through the subject's head.
            public float ArtFocusY = 0.5f;
            /// <summary>
            /// True when the frame is a translucent overlay meant to sit on top of edge-to-edge
            /// art (Full Art). The art then fills the whole canvas and needs no mask.
            /// </summary>
            public bool FullArt;
            // No faction label: every delivered frame bakes its faction icon into the top-right
            // corner, so a text mark there duplicated the art and fought the gem for space.
            // AtkLabel is left unset (zero W) on frames that bake the word "Attack" into the bar
            // themselves, so the face does not spell it out twice. See HasZone.
            public LabelZone Cost, Name, Type, Hp, Shield, Atk, AtkLabel, Ability, Id;

            /// <summary>True when a zone describes a real slot rather than an absent one.</summary>
            public static bool HasZone(LabelZone zone)
            {
                return zone.W > 0f && zone.Em > 0f;
            }

            // Measured from FSTI_Common.png against the non-full-art reference sheet. The frame is
            // opaque below y460, so the art window is a genuine hole: x 66..765, y 66..450.
            // Plate geometry (baked ink in brackets):
            //   cost gem     x20..165  y16..165   [numeral cx 92]
            //   Health plate x101..226 y453..536  [icon x115..142 y484..511, caption y533..547]
            //   Name plate   x257..573 y454..566
            //   Shield plate x605..730 y453..536  [icon x619..646, caption y533..547]
            //   Attack bar   x108..730 y776..854  [icon only, x126..155 y800..829 - no label]
            //   Ability      x108..723 y896..1087 [no caption]
            //   ID rule      x341..490 y1164..1168
            // The captions occupy the lower half of each stat plate, so the values sit beside their
            // icons on the icon row rather than underneath, matching the sheet. The Attack bar
            // spells its word out as text (AtkLabel) because this frame only bakes the icon.
            public static readonly FaceZones Standard = new FaceZones
            {
                ArtX = 66f, ArtY = 66f, ArtW = 700f, ArtH = 385f, ArtFocusY = 0.4f,
                Cost = new LabelZone(92f, 92f, 110f, 130f, 80f),
                Name = new LabelZone(415f, 512f, 300f, 90f, 56f),
                Type = new LabelZone(415f, 638f, 300f, 50f, 32f),
                Hp = new LabelZone(185f, 497f, 70f, 55f, 46f),
                Shield = new LabelZone(690f, 497f, 70f, 55f, 46f),
                Atk = new LabelZone(510f, 815f, 420f, 58f, 44f, ZoneAlign.Right),
                // This frame's Attack bar carries only the arrow icon (x126..155), unlike Legendary's
                // which bakes the word in, so the face spells it out and then right-anchors the
                // value exactly as the full-art frame does.
                AtkLabel = new LabelZone(236f, 815f, 110f, 48f, 28f, ZoneAlign.Left),
                Ability = new LabelZone(412f, 1010f, 580f, 140f, 34f, ZoneAlign.TopLeft),
                Id = new LabelZone(200f, 1168f, 165f, 40f, 26f, ZoneAlign.Left)
            };

            // Measured from FSTI_Legendary.png against Assets/Cards/Example/Full Art Example.png.
            // Full Art: the frame is a translucent overlay (alpha ~1 outside its plates, ~47 on
            // them) and the art runs edge-to-edge beneath it, so the art region is the whole canvas
            // and no crop window or mask exists. The plates were still measured, because they are
            // what the text reads against:
            //   cost gem      x 28..153  y 14..160
            //   faction hex   x700..796  y 26..98
            //   Health plate  x 99..228  y720..794  [icon x115..142 y742..769, caption y791..805]
            //   Name plate    x244..586  y723..823
            //   Shield plate  x603..732  y720..794  [icon x619..646, caption y791..805]
            //   Attack bar    x 99..732  y869..928  [icon AND baked "Attack" label, x123..242]
            //   Ability panel x 99..732  y949..1130 [baked "Ability" caption x123..182 y969..987]
            // Unlike Standard, this frame's Attack bar already spells out "Attack", so the value is
            // right-aligned against the far rail exactly as the sheet shows it. The captions sit
            // below the icons, so the stat values go beside the icons on the icon row. "Unit" falls
            // outside the name plate, on the art, as in the sheet.
            public static readonly FaceZones Legendary = new FaceZones
            {
                ArtX = 0f, ArtY = 0f, ArtW = 832f, ArtH = 1216f, ArtFocusY = 0.5f, FullArt = true,
                Cost = new LabelZone(95f, 90f, 110f, 130f, 84f),
                Name = new LabelZone(415f, 770f, 330f, 62f, 50f),
                Type = new LabelZone(415f, 846f, 330f, 40f, 30f),
                Hp = new LabelZone(188f, 755f, 70f, 55f, 46f),
                Shield = new LabelZone(690f, 755f, 70f, 55f, 46f),
                Atk = new LabelZone(485f, 898f, 470f, 56f, 42f, ZoneAlign.Right),
                Ability = new LabelZone(422f, 1060f, 585f, 130f, 30f, ZoneAlign.TopLeft),
                Id = new LabelZone(200f, 1152f, 200f, 40f, 26f, ZoneAlign.Left)
            };
        }

        /// <summary>Canonical card footprint used when authoring templates outside Play Mode.</summary>
        public static readonly Vector3 CanonicalFaceSize = new Vector3(1f, 1.4615f, 0.01f);

        // Local layer offsets inside one card's sorting slice. The hand multiplies these out per
        // card, so a whole card (art, frame, labels) always sits above or below a neighbour card.
        public const int ArtOffset = -10;
        public const int FrameOffset = 0;
        public const int MaskFrontOffset = -9;
        public const int MaskBackOffset = -11;
        public const int TextOffset = 20;

        /// <summary>Base sorting order for this card; 0 for a lone card.</summary>
        int sortBaseline;

        /// <summary>
        /// TextMeshPro is told to render orthographically, so a glyph's world height is
        /// fontSize x transform scale. Rendering the atlas at a large point size and shrinking the
        /// transform is what keeps labels sharp; the old 3-9pt sizes rasterised a few pixels tall.
        /// </summary>
        public const float TextAtlasPointSize = 96f;

        FaceZones Zones
        {
            get { return ZonesFor(Current != null ? Current.Rarity : PreviewRarity); }
        }

        /// <summary>
        /// Rarity used for layout when no card is bound yet. The template generator sets this so
        /// Prefab Mode lays out Legendary exactly as it renders at runtime.
        /// </summary>
        public Rarity PreviewRarity = Rarity.Common;

        public static FaceZones ZonesFor(Rarity? rarity)
        {
            return rarity == Rarity.Legendary ? FaceZones.Legendary : FaceZones.Standard;
        }

        /// <summary>Converts an 832x1216 pixel coordinate into local card space.</summary>
        public static Vector3 PixelPosition(Vector3 faceSize, float x, float y)
        {
            return new Vector3(
                faceSize.x * (x / FrameWidthPixels - 0.5f),
                faceSize.y * (0.5f - y / FrameHeightPixels),
                -0.04f);
        }

        /// <summary>
        /// Places and sizes one card label from its pixel-space zone. Because the card keeps the
        /// frame canvas aspect, the rect in atlas units reduces to TextAtlasPointSize x (boxPx / Em),
        /// independent of the card's world size.
        /// </summary>
        public static void StyleLabel(TextMeshPro text, Vector3 faceSize, Vector3 localPosition,
            LabelZone zone, bool multiLine)
        {
            if (text == null) return;
            float worldEm = faceSize.y * zone.Em / FrameHeightPixels;
            float scale = worldEm / TextAtlasPointSize;
            text.transform.localPosition = localPosition;
            text.transform.localScale = new Vector3(scale, scale, scale);
            text.fontSize = TextAtlasPointSize;
            text.fontSizeMax = TextAtlasPointSize;
            text.fontSizeMin = TextAtlasPointSize * 0.6f;
            // Shrink-to-fit is only for the multi-line panel. A single-line zone keeps one em for
            // every card and condenses horizontally instead - see CondenseToFitWidth.
            text.enableAutoSizing = multiLine;
            text.enableWordWrapping = multiLine;
            // TMP 3.0.7 names: Left/Center/Right are vertically centred; TopLeft hangs from the top.
            switch (zone.Align)
            {
                case ZoneAlign.Left:
                    text.alignment = TextAlignmentOptions.Left;
                    break;
                case ZoneAlign.Right:
                    text.alignment = TextAlignmentOptions.Right;
                    break;
                case ZoneAlign.TopLeft:
                    text.alignment = TextAlignmentOptions.TopLeft;
                    break;
                default:
                    text.alignment = TextAlignmentOptions.Center;
                    break;
            }
            text.overflowMode = TextOverflowModes.Overflow;
            text.characterSpacing = 0f;
            text.isOrthographic = true;
            text.rectTransform.sizeDelta = new Vector2(
                TextAtlasPointSize * zone.W / zone.Em,
                TextAtlasPointSize * zone.H / zone.Em);
            if (!multiLine) CondenseToFitWidth(text, scale);
        }

        /// <summary>
        /// Fits a single-line label into its plate by squeezing it sideways, never by shrinking it.
        /// The fan is a row of congruent plates, so the cap height and the baseline of a name are
        /// what the eye compares from one card to the next; point-size shrink-to-fit made those
        /// differ by a third across a single hand (measured on the shipped pool: 71pt, 76.7pt,
        /// 85.5pt and 96pt side by side), which reads as the whole layout sliding along the row.
        /// A horizontal squeeze leaves the em box, the baseline and the plate height identical, and
        /// name LENGTH was already visible as a width change, so it costs nothing to keep it there.
        /// Runs under the card's roll, and the scale is in the label's own frame, so the roll
        /// composes outside it and the glyphs stay square-on-the-card rather than sheared.
        /// </summary>
        static void CondenseToFitWidth(TextMeshPro text, float baseScale)
        {
            if (text.rectTransform == null) return;
            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            if (info == null || info.lineCount <= 0) return;

            float widest = 0f;
            for (int i = 0; i < info.lineCount; i++)
            {
                Extents extents = info.lineInfo[i].lineExtents;
                float width = extents.max.x - extents.min.x;
                if (width > widest) widest = width;
            }

            // Rect units and glyph-mesh units are the same units here: the rect above is authored as
            // pointSize x (plate px / em px), and the mesh comes back in pointSize units too.
            float plate = text.rectTransform.rect.width;
            if (plate <= 0f || widest <= plate) return;
            text.transform.localScale = new Vector3(
                baseScale * plate / widest, baseScale, baseScale);
        }

        public void SetSize(Vector3 size)
        {
            faceSize = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 0.01f);
            ApplyLayout();
        }

        public void Bind(CardFaceData face, CardFrameCatalog frameCatalog)
        {
            Bind(face, frameCatalog, TierMaterials);
        }

        public void Bind(CardFaceData face, CardFrameCatalog frameCatalog, CardTierMaterialSet tierMaterials)
        {
            Current = face;
            TierMaterials = tierMaterials;
            EnsureFields();

            FrameImage.sprite = frameCatalog != null
                ? frameCatalog.Get(face.Type, face.Faction, face.Rarity)
                : NeutralSprite();
            FrameImage.color = frameCatalog != null ? Color.white : new Color(0.12f, 0.16f, 0.22f, 1f);
            ApplyCardMaterial(face);

            ArtImage.sprite = face.ArtSprite != null ? face.ArtSprite : NeutralSprite();
            ArtImage.color = face.ArtSprite != null ? Color.white : new Color(0.24f, 0.24f, 0.24f, 1f);
            SetText(CostValue, face.Cost.ToString());
            SetText(NameValue, face.DisplayName);
            SetText(TypeValue, face.Type.ToString());
            SetText(AbilityValue, face.AbilityText);
            // The sheet shows only the card id on the bottom rule; the rarity is already carried by
            // the frame itself, and appending it overran the measured 200px zone.
            SetText(IdValue, face.Id);

            bool hasStats = face.HasCombatStats;
            if (CombatStatsRoot != null) CombatStatsRoot.SetActive(hasStats);
            SetText(AtkValue, hasStats ? face.Atk.Value.ToString() : string.Empty);
            SetText(AtkLabel, "Attack");
            SetText(HpValue, hasStats ? face.Hp.Value.ToString() : string.Empty);
            SetText(ShieldValue, hasStats ? face.Shield.Value.ToString() : string.Empty);
            ApplyLayout();
        }

        void ApplyLayout()
        {
            ApplyRenderLayers();
            FitSprite(FrameImage, faceSize);
            ApplyTextLayout();
            if (ArtImage == null) return;

            // Standard tiers cover-fit their art into a measured window and clip it with the mask,
            // so it can never spill over the frame rails. Full Art has no window: the art fills the
            // canvas and the translucent frame overlay draws on top of it, so masking is off.
            FaceZones zones = Zones;
            float artWidth = faceSize.x * zones.ArtW / FrameWidthPixels;
            float artHeight = faceSize.y * zones.ArtH / FrameHeightPixels;
            Vector2 center = new Vector2(
                faceSize.x * ((zones.ArtX + zones.ArtW * 0.5f) / FrameWidthPixels - 0.5f),
                faceSize.y * (0.5f - (zones.ArtY + zones.ArtH * 0.5f) / FrameHeightPixels));
            Vector2 artSize = new Vector2(artWidth, artHeight);

            if (zones.FullArt)
            {
                if (ArtMask != null) ArtMask.gameObject.SetActive(false);
                ArtImage.maskInteraction = SpriteMaskInteraction.None;
            }
            else
            {
                ConfigureArtMask(center, artSize);
            }

            ArtImage.transform.localPosition = new Vector3(center.x, center.y, 0.02f);
            FitSpriteCover(ArtImage, artSize);
            if (!zones.FullArt) BiasCoverCrop(center.y, artHeight, zones.ArtFocusY);
        }

        // After a cover fit the sprite overflows its window. Slide it along Y so the crop keeps
        // the subject rather than cutting through the middle of the composition.
        void BiasCoverCrop(float centerY, float windowHeight, float focusY)
        {
            if (ArtImage == null || ArtImage.sprite == null) return;
            float scale = ArtImage.transform.localScale.y;
            float artHeight = ArtImage.sprite.bounds.size.y * scale;
            float excess = artHeight - windowHeight;
            if (excess <= 0f) return;
            float offset = excess * (Mathf.Clamp01(focusY) - 0.5f);
            ArtImage.transform.localPosition = new Vector3(
                ArtImage.transform.localPosition.x, centerY + offset, ArtImage.transform.localPosition.z);
        }

        void ApplyCardMaterial(CardFaceData face)
        {
            if (FrameImage == null || TierMaterials == null || face == null) return;
            Material material = TierMaterials.Resolve(face.Rarity, Shader.Find("Overlink/CardTier/" + face.Rarity) != null);
            if (material != null)
            {
                FrameImage.sharedMaterial = material;
                FrameImage.color = Color.white;
            }
        }

        static Sprite NeutralSprite()
        {
            return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        void EnsureFields()
        {
            if (ArtImage == null) ArtImage = CreateSprite("ArtImage", Vector3.zero, -10);
            if (FrameImage == null) FrameImage = CreateSprite("FrameImage", Vector3.zero, 0);
            if (ArtMask == null) ArtMask = CreateArtMask();
            if (CostValue == null) CostValue = CreateText("CostValue");
            if (NameValue == null) NameValue = CreateText("NameValue");
            if (TypeValue == null) TypeValue = CreateText("TypeValue");
            if (AbilityValue == null) AbilityValue = CreateText("AbilityValue");
            if (IdValue == null) IdValue = CreateText("IdValue");

            if (CombatStatsRoot == null)
            {
                CombatStatsRoot = new GameObject("CombatStats");
                CombatStatsRoot.transform.SetParent(transform, false);
                AtkValue = CreateText("AtkValue", CombatStatsRoot.transform);
                AtkLabel = CreateText("AtkLabel", CombatStatsRoot.transform);
                HpValue = CreateText("HpValue", CombatStatsRoot.transform);
                ShieldValue = CreateText("ShieldValue", CombatStatsRoot.transform);
            }
        }

        SpriteMask CreateArtMask()
        {
            GameObject go = new GameObject("ArtMask");
            go.transform.SetParent(transform, false);
            SpriteMask mask = go.AddComponent<SpriteMask>();
            mask.sprite = NeutralSprite();
            mask.transform.localScale = Vector3.one;
            mask.frontSortingOrder = -9;
            mask.backSortingOrder = -11;
            return mask;
        }

        void ConfigureArtMask(Vector2 center, Vector2 size)
        {
            if (ArtMask == null) return;
            if (ArtMask.sprite == null) ArtMask.sprite = NeutralSprite();
            ArtMask.gameObject.SetActive(true);
            // The mask is a 1x1 world-unit sprite, so its scale is the window size verbatim.
            ArtMask.transform.localPosition = new Vector3(center.x, center.y, 0.01f);
            ArtMask.transform.localScale = new Vector3(size.x, size.y, 1f);
            // Keep the mask inside this card's own sorting slice; ApplyRenderLayers may have run
            // before this, and a hardcoded order here would let a neighbour card's art leak in.
            ArtMask.frontSortingOrder = sortBaseline + MaskFrontOffset;
            ArtMask.backSortingOrder = sortBaseline + MaskBackOffset;
            ArtImage.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        SpriteRenderer CreateSprite(string name, Vector3 position, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        // Placeholder construction only. Position, scale and rect are all owned by StyleLabel,
        // which runs from ApplyTextLayout on every Bind/SetSize, so nothing here encodes layout.
        TextMeshPro CreateText(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var text = go.AddComponent<TextMeshPro>();
            text.color = Color.white;
            // Derived from the card's own baseline like every other child. A literal here is the
            // exact mistake section 12 warns about: it happens to work only because ApplyRenderLayers
            // runs after it on every bind, so any label created later would keep the constant and
            // draw beneath every neighbouring card.
            text.sortingOrder = sortBaseline + TextOffset;
            return text;
        }

        void ApplyRenderLayers()
        {
            // Every renderer on a card is offset from one per-card baseline. Without that offset the
            // hand stacks incorrectly: sorting order is global, so card A's labels (20) draw over
            // card B's frame (0) and art (-10), which reads as text and rails bleeding across the
            // fan. Within a baseline the local order still does the right thing: art under frame,
            // frame under text.
            int b = sortBaseline;
            SetSpriteOrder(ArtImage, b + ArtOffset);
            SetSpriteOrder(FrameImage, b + FrameOffset);
            if (ArtMask != null)
            {
                ArtMask.frontSortingOrder = b + MaskFrontOffset;
                ArtMask.backSortingOrder = b + MaskBackOffset;
            }
            SetTextOrder(CostValue, b + TextOffset);
            SetTextOrder(NameValue, b + TextOffset);
            SetTextOrder(TypeValue, b + TextOffset);
            SetTextOrder(AtkValue, b + TextOffset);
            SetTextOrder(HpValue, b + TextOffset);
            SetTextOrder(ShieldValue, b + TextOffset);
            SetTextOrder(AbilityValue, b + TextOffset);
            SetTextOrder(IdValue, b + TextOffset);
            SetTextOrder(AtkLabel, b + TextOffset);
        }

        /// <summary>
        /// Moves this card to its own slice of the global sorting space. The hand raises this for
        /// hovered, dragged and inspected cards so a lifted card covers every neighbour.
        /// </summary>
        public void SetSortBaseline(int baseline)
        {
            if (baseline == sortBaseline) return;
            sortBaseline = baseline;
            ApplyRenderLayers();
        }

        static void SetSpriteOrder(SpriteRenderer renderer, int order)
        {
            if (renderer != null) renderer.sortingOrder = order;
        }

        static void SetTextOrder(TextMeshPro text, int order)
        {
            Renderer renderer = text != null ? text.GetComponent<Renderer>() : null;
            if (renderer != null) renderer.sortingOrder = order;
        }

        void ApplyTextLayout()
        {
            // Labels come from the active tier's measured pixel zones, so Legendary text lands in
            // its own banner/panels instead of over the taller art window.
            FaceZones z = Zones;
            StyleLabel(CostValue, faceSize, PixelPosition(faceSize, z.Cost.X, z.Cost.Y), z.Cost, false);
            StyleLabel(NameValue, faceSize, PixelPosition(faceSize, z.Name.X, z.Name.Y), z.Name, false);
            StyleLabel(TypeValue, faceSize, PixelPosition(faceSize, z.Type.X, z.Type.Y), z.Type, false);
            StyleLabel(HpValue, faceSize, PixelPosition(faceSize, z.Hp.X, z.Hp.Y), z.Hp, false);
            StyleLabel(ShieldValue, faceSize, PixelPosition(faceSize, z.Shield.X, z.Shield.Y), z.Shield, false);
            StyleLabel(AtkValue, faceSize, PixelPosition(faceSize, z.Atk.X, z.Atk.Y), z.Atk, false);
            StyleLabel(AbilityValue, faceSize, PixelPosition(faceSize, z.Ability.X, z.Ability.Y), z.Ability, true);
            StyleLabel(IdValue, faceSize, PixelPosition(faceSize, z.Id.X, z.Id.Y), z.Id, false);

            // Optional slot: only frames that do not bake the word in supply an AtkLabel zone.
            if (AtkLabel != null)
            {
                bool show = FaceZones.HasZone(z.AtkLabel);
                AtkLabel.gameObject.SetActive(show);
                if (show)
                {
                    StyleLabel(AtkLabel, faceSize,
                        PixelPosition(faceSize, z.AtkLabel.X, z.AtkLabel.Y), z.AtkLabel, false);
                }
            }
        }

        static void SetText(TextMeshPro target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }

        void FitSpriteCover(SpriteRenderer renderer, Vector2 targetSize)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
            float scale = Mathf.Max(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        void FitSprite(SpriteRenderer renderer, Vector3 targetSize)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
            renderer.transform.localScale = new Vector3(
                targetSize.x / spriteSize.x, targetSize.y / spriteSize.y, 1f);
        }

    }
}
