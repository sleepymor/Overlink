using System;
using System.Collections;
using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    [RequireComponent(typeof(DeckController))]
    public class HandController : MonoBehaviour
    {
        [Header("Refs (auto-created/found if empty)")]
        public DeckController deck;
        public Transform deckPile;
        public Transform playSlot;
        public Transform discardPile;

        [Header("External match driving (optional)")]
        [Tooltip("When false, Start() does not auto-draw from DeckController. An external owner calls SetHand/AddCard instead.")]
        public bool autoDraw = true;

        [Header("Fan layout")]
        public int handSizeStart = 5;
        // 5:7 portrait card face (matches CardView.CardSize).
        public Vector3 cardSize = new Vector3(1.0f, 1.4f, 0.06f);
        public float fanSpacing = 0.78f;
        public float arcHeight = 0.35f;
        public float maxYaw = 16f;
        public float handY = -2.6f;

        [Header("Motion")]
        public float layoutDuration = 0.28f;
        public float drawDuration = 0.38f;
        public float hoverDuration = 0.12f;
        public float resolveDuration = 0.25f;
        public float hoverLift = 0.45f;
        public float hoverScale = 1.12f;
        public float playLineY = 1.2f;
        public float tiltFactor = 0.6f;
        public float maxTilt = 18f;

        readonly List<CardView> cards = new List<CardView>();
        CardView hovered;
        CardView dragged;
        Camera cam;
        Plane dragPlane = new Plane(Vector3.forward, Vector3.zero);
        Vector3 grabOffset;
        float lastDragX;

        /// <summary>
        /// Optional external authority for plays. When set, a card released above the play line is offered
        /// here first; returning false bounces the card back into the hand untouched. The match engine owns
        /// cost and legality, so the hand never decides those itself.
        /// </summary>
        public Func<CardData, bool> playResolver;

        /// <summary>
        /// Optional drop-target probe supplied by the match owner, set together with
        /// <see cref="playResolverWithSlot"/>. Called every frame a card is dragged with the card's current
        /// <em>world</em> position; it returns the board slot the play would land in, or null when the card
        /// is over nothing usable. The owner also draws the highlight for that slot, so the hand stays
        /// ignorant of the board's geometry.
        /// <para>
        /// A non-null result also lets a release deploy the card <em>without</em> crossing
        /// <see cref="playLineY"/>. That matters because the player's own Active and Bench ranks sit below
        /// that line: with the height test as the only gate, dropping onto the Bench was physically
        /// impossible and every unit silently went to the first free slot instead.
        /// </para>
        /// The hand cannot see the board itself - <c>Overlink.Cards</c> does not reference
        /// <c>Overlink.Match</c> - so the owner injects this exactly like <see cref="playResolver"/>.
        /// </summary>
        public Func<CardData, Vector3, BoardSlot> dropTargetResolver;

        /// <summary>
        /// Play gate that receives the slot <see cref="dropTargetResolver"/> nominated. Takes precedence
        /// over <see cref="playResolver"/> when set; the slot may be null on a plain above-the-line fling.
        /// </summary>
        public Func<CardData, BoardSlot, bool> playResolverWithSlot;

        /// <summary>Clears the owner's drop highlight when a drag ends for any reason.</summary>
        public Action dropTargetCleared;

        // The slot under the card as of the last drag update, replayed to the gate on release.
        BoardSlot pendingDropSlot;

        /// <summary>Raised after a play was accepted by the resolver and has finished animating to the play slot.</summary>
        public event Action<CardData> CardPlayed;

        public int CardCount => cards.Count;

        /// <summary>Snapshot of the data currently shown in the hand.</summary>
        public IReadOnlyList<CardData> HandData
        {
            get
            {
                var data = new List<CardData>(cards.Count);
                foreach (CardView card in cards)
                {
                    if (card != null)
                    {
                        data.Add(card.Data);
                    }
                }

                return data;
            }
        }

        public static void ComputeSlot(int i, int n, float spacing, float arc,
            float yawDeg, float y, out Vector3 pos, out Quaternion rot)
        {
            float c = (n - 1) * 0.5f;
            float x = (i - c) * spacing;
            float t = n <= 1 ? 0f : (i - c) / Mathf.Max(1f, c);
            pos = new Vector3(x, y + arc * (1f - t * t), -0.002f * i);
            rot = Quaternion.Euler(0f, 0f, -t * yawDeg);
        }

        void Awake()
        {
            cam = Camera.main;
            if (deck == null) deck = GetComponent<DeckController>();
            deckPile = EnsureAnchor(deckPile, "DeckPile", new Vector3(-4.2f, 0f, 0f));
            playSlot = EnsureAnchor(playSlot, "PlaySlot", new Vector3(0f, 2.6f, 0f));
            discardPile = EnsureAnchor(discardPile, "DiscardPile", new Vector3(4.2f, 0f, 0f));
        }

        void OnValidate()
        {
            cardSize.x = Mathf.Max(0.1f, cardSize.x);
            cardSize.y = Mathf.Max(0.1f, cardSize.y);
            cardSize.z = Mathf.Max(0.01f, cardSize.z);
            fanSpacing = Mathf.Max(0.05f, fanSpacing);
        }

        public void ResizeHand(Vector3 newSize)
        {
            cardSize = new Vector3(
                Mathf.Max(0.1f, newSize.x),
                Mathf.Max(0.1f, newSize.y),
                Mathf.Max(0.01f, newSize.z));
            foreach (var card in cards)
            {
                if (card == null) continue;
                card.SetSize(cardSize);
            }
            LayoutAll();
        }

        Transform EnsureAnchor(Transform t, string anchorName, Vector3 pos)
        {
            if (t != null) return t;
            var go = new GameObject(anchorName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        IEnumerator Start()
        {
            yield return null;
            if (!autoDraw)
            {
                // An external match owner is driving this hand; it will call SetHand/AddCard itself.
                yield break;
            }

            for (int i = 0; i < handSizeStart; i++)
            {
                DrawOne();
                yield return new WaitForSeconds(0.1f);
            }
        }

        void Update()
        {
            if (cam == null) return;

            // Debug draw/discard only makes sense for the standalone presentation scene. When a match
            // owns the hand, these keys would desync the view from the authoritative state.
            if (autoDraw)
            {
                if (Input.GetKeyDown(KeyCode.Space)) DrawOne();
                if (Input.GetKeyDown(KeyCode.Backspace)) DiscardLast();
            }

            if (dragged != null)
            {
                UpdateDrag();
                if (Input.GetMouseButtonUp(0)) ReleaseDrag();
                return;
            }

            UpdateHover();
            if (hovered != null && Input.GetMouseButtonDown(0)) BeginDrag(hovered);
        }

        public void DrawOne()
        {
            if (deck == null) return;
            CardData data = deck.Draw();
            if (data == null)
            {
                Debug.Log("[Hand] Deck and discard are both empty — nothing to draw.");
                return;
            }
            CardView card = CreateView(data);
            card.transform.localPosition = deckPile.localPosition;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one * 0.6f;
            LayoutAll();
            ComputeSlot(card.SlotIndex, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, drawDuration, CardEase.OutCubic);
            Debug.Log($"[Hand] Drew {data.title} ({cards.Count} in hand).");
        }

        /// <summary>
        /// Replaces the hand contents with exactly <paramref name="data"/>, keeping already-shown views
        /// alive so their positions tween instead of popping. Intended for a match owner that treats the
        /// Core MatchState as authoritative.
        /// <para>
        /// Reuse depends on reference identity: the caller must pass the same <see cref="CardData"/>
        /// instances across calls (cache one per card instance), otherwise every sync rebuilds the hand.
        /// </para>
        /// </summary>
        public void SetHand(IReadOnlyList<CardData> data)
        {
            var wanted = new HashSet<CardData>();
            if (data != null)
            {
                foreach (CardData item in data)
                {
                    if (item != null)
                    {
                        wanted.Add(item);
                    }
                }
            }

            for (int i = cards.Count - 1; i >= 0; i--)
            {
                CardView card = cards[i];
                if (card == null || card.Data == null || !wanted.Contains(card.Data))
                {
                    RemoveView(card);
                }
            }

            var present = new HashSet<CardData>();
            foreach (CardView card in cards)
            {
                if (card != null) present.Add(card.Data);
            }

            if (data != null)
            {
                foreach (CardData item in data)
                {
                    if (item != null && !present.Contains(item))
                    {
                        CreateView(item);
                    }
                }
            }

            LayoutAll();
        }

        /// <summary>Adds a single card to the end of the hand with the normal draw animation.</summary>
        public void AddCard(CardData data)
        {
            if (data == null) return;
            CardView card = CreateView(data);
            card.transform.localPosition = deckPile.localPosition;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one * 0.6f;
            LayoutAll();
            ComputeSlot(card.SlotIndex, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, drawDuration, CardEase.OutCubic);
        }

        /// <summary>Immediately removes a card from the hand without the discard animation.</summary>
        public void RemoveCard(CardData data)
        {
            if (data == null) return;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if (cards[i] != null && cards[i].Data == data)
                {
                    RemoveView(cards[i]);
                    LayoutAll();
                    return;
                }
            }
        }

        CardView CreateView(CardData data)
        {
            var card = CardView.Create(data, transform, cardSize);
            card.SlotIndex = cards.Count;
            cards.Add(card);
            return card;
        }

        void RemoveView(CardView card)
        {
            if (card == null) return;
            cards.Remove(card);
            if (hovered == card) hovered = null;
            if (dragged == card) dragged = null;
            Destroy(card.gameObject);
        }

        public void DiscardLast()
        {
            if (cards.Count == 0) return;
            var card = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            if (hovered == card) hovered = null;
            LayoutAll();
            StartCoroutine(SendToDiscard(card));
        }

        void LayoutAll()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                card.SlotIndex = i;
                if (card == dragged || card == hovered) continue;
                ComputeSlot(i, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                    out Vector3 pos, out Quaternion rot);
                card.Tween.Play(pos, rot, card.BaseScale, layoutDuration, CardEase.OutCubic);
            }
        }

        void UpdateHover()
        {
            var hit = RaycastCard();
            if (hit != hovered)
            {
                if (hovered != null) Unhover(hovered);
                hovered = hit;
                if (hovered != null) Hover(hovered);
            }
        }

        void Hover(CardView card)
        {
            card.SetHovered(true);
            ComputeSlot(card.SlotIndex, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                out Vector3 pos, out Quaternion rot);
            pos.y += hoverLift;
            pos.z = -0.4f;
            card.Tween.Play(pos, rot, card.BaseScale * hoverScale, hoverDuration, CardEase.OutCubic);
        }

        void Unhover(CardView card)
        {
            card.SetHovered(false);
            ComputeSlot(card.SlotIndex, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, hoverDuration, CardEase.OutCubic);
        }

        void BeginDrag(CardView card)
        {
            if (hovered == card) hovered = null;
            card.SetHovered(false);
            card.SetDragging(true);
            card.Tween.Stop();
            dragged = card;
            dragPlane = new Plane(Vector3.forward, card.transform.localPosition);
            grabOffset = card.transform.localPosition - MouseOnPlane();
            lastDragX = card.transform.localPosition.x;
            pendingDropSlot = null;
            Vector3 p = card.transform.localPosition;
            p.z = -1f;
            card.transform.localPosition = p;
        }

        void UpdateDrag()
        {
            Vector3 target = MouseOnPlane() + grabOffset;
            target.z = -1f;
            float vx = (target.x - lastDragX) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastDragX = target.x;
            dragged.transform.localPosition = target;
            float tilt = Mathf.Clamp(-vx * tiltFactor, -maxTilt, maxTilt);
            dragged.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            // Ask the owner which socket this is over. It highlights the one it accepts, so a lit socket is
            // a promise the release can rely on, and a dark board means the play is not currently legal.
            pendingDropSlot = dropTargetResolver != null
                ? dropTargetResolver(dragged.Data, dragged.transform.position)
                : null;
        }

        void ReleaseDrag()
        {
            var card = dragged;
            dragged = null;
            card.SetDragging(false);
            dropTargetCleared?.Invoke();

            // Either a resolved socket or the classic fling past the play line. The socket route is what
            // makes the player's own ranks - which sit below that line - reachable at all.
            if (pendingDropSlot != null || card.transform.localPosition.y > playLineY)
            {
                PlayCard(card);
            }
            else
            {
                ComputeSlot(card.SlotIndex, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                    out Vector3 pos, out Quaternion rot);
                card.Tween.Play(pos, rot, card.BaseScale, layoutDuration, CardEase.OutCubic);
            }
        }

        void PlayCard(CardView card)
        {
            // Remove the card from the hand first: the resolver may sync the hand against the match state,
            // and a view still present here would be recreated as a duplicate.
            cards.Remove(card);
            if (hovered == card) hovered = null;

            // The match engine is the authority on legality (turn, phase, NP, slot). The slot-aware gate is
            // preferred when installed, because it is the only one that can honour a targeted socket; with
            // no resolver at all the hand keeps its original standalone behaviour and plays anything.
            BoardSlot slot = pendingDropSlot;
            pendingDropSlot = null;
            bool accepted = playResolverWithSlot != null
                ? playResolverWithSlot(card.Data, slot)
                : playResolver == null || playResolver(card.Data);

            if (!accepted)
            {
                Debug.Log($"[Hand] {card.Data.title} was rejected by the match engine; returning it to the hand.");
                cards.Add(card);
                ReturnToHand(card);
                return;
            }

            LayoutAll();
            StartCoroutine(ResolvePlay(card));
        }

        void ReturnToHand(CardView card)
        {
            card.SlotIndex = cards.Count;
            LayoutAll();
            ComputeSlot(card.SlotIndex, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, layoutDuration, CardEase.OutCubic);
        }

        IEnumerator ResolvePlay(CardView card)
        {
            var done = false;
            card.Tween.Play(playSlot.localPosition, Quaternion.identity,
                card.BaseScale * 1.15f, resolveDuration, CardEase.OutCubic, () => done = true);
            yield return new WaitUntil(() => done);
            Debug.Log($"[Hand] Played {card.Data.title} (cost {card.Data.cost}): {card.Data.description}");
            CardPlayed?.Invoke(card.Data);
            yield return new WaitForSeconds(0.45f);
            StartCoroutine(SendToDiscard(card));
        }

        IEnumerator SendToDiscard(CardView card)
        {
            if (deck != null) deck.Discard(card.Data);
            var done = false;
            card.Tween.Play(discardPile.localPosition, Quaternion.identity,
                card.BaseScale * 0.4f, resolveDuration, CardEase.OutCubic, () => done = true);
            yield return new WaitUntil(() => done);
            Destroy(card.gameObject);
        }

        CardView RaycastCard()
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var view = hit.collider.GetComponent<CardView>();
                if (view != null && cards.Contains(view) && view != dragged) return view;
            }
            return null;
        }

        Vector3 MouseOnPlane()
        {
            // Work in the hand's local space so the same drag math serves both the upright prototype hand
            // and an Underlords-style hand laid flat on a pitched table, where the hand root is rotated.
            Ray worldRay = cam.ScreenPointToRay(Input.mousePosition);
            Ray localRay = new Ray(
                transform.InverseTransformPoint(worldRay.origin),
                transform.InverseTransformDirection(worldRay.direction));
            if (dragPlane.Raycast(localRay, out float d)) return localRay.GetPoint(d);
            return dragged != null ? dragged.transform.localPosition : Vector3.zero;
        }
    }
}
