using System.Collections;
using System.Collections.Generic;
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

        [Header("Fan layout")]
        public int handSizeStart = 5;
        public Vector3 cardSize = new Vector3(0.9f, 1.4f, 0.06f);
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

        public int CardCount => cards.Count;

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
            for (int i = 0; i < handSizeStart; i++)
            {
                DrawOne();
                yield return new WaitForSeconds(0.1f);
            }
        }

        void Update()
        {
            if (cam == null) return;

            if (Input.GetKeyDown(KeyCode.Space)) DrawOne();
            if (Input.GetKeyDown(KeyCode.Backspace)) DiscardLast();

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
            var card = CardView.Create(data, transform, cardSize);
            card.SlotIndex = cards.Count;
            cards.Add(card);
            card.transform.localPosition = deckPile.localPosition;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = cardSize * 0.6f;
            LayoutAll();
            ComputeSlot(card.SlotIndex, cards.Count, fanSpacing, arcHeight, maxYaw, handY,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, drawDuration, CardEase.OutCubic);
            Debug.Log($"[Hand] Drew {data.title} ({cards.Count} in hand).");
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
            dragPlane = new Plane(Vector3.forward, card.transform.position);
            grabOffset = card.transform.position - MouseOnPlane();
            lastDragX = card.transform.position.x;
            Vector3 p = card.transform.position;
            p.z = -1f;
            card.transform.position = p;
        }

        void UpdateDrag()
        {
            Vector3 target = MouseOnPlane() + grabOffset;
            target.z = -1f;
            float vx = (target.x - lastDragX) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastDragX = target.x;
            dragged.transform.position = target;
            float tilt = Mathf.Clamp(-vx * tiltFactor, -maxTilt, maxTilt);
            dragged.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        }

        void ReleaseDrag()
        {
            var card = dragged;
            dragged = null;
            card.SetDragging(false);
            if (card.transform.position.y > playLineY)
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
            cards.Remove(card);
            if (hovered == card) hovered = null;
            LayoutAll();
            StartCoroutine(ResolvePlay(card));
        }

        IEnumerator ResolvePlay(CardView card)
        {
            var done = false;
            card.Tween.Play(playSlot.position, Quaternion.identity,
                card.BaseScale * 1.15f, resolveDuration, CardEase.OutCubic, () => done = true);
            yield return new WaitUntil(() => done);
            Debug.Log($"[Hand] Played {card.Data.title} (cost {card.Data.cost}): {card.Data.description}");
            yield return new WaitForSeconds(0.45f);
            StartCoroutine(SendToDiscard(card));
        }

        IEnumerator SendToDiscard(CardView card)
        {
            deck.Discard(card.Data);
            var done = false;
            card.Tween.Play(discardPile.position, Quaternion.identity,
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
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float d)) return ray.GetPoint(d);
            return dragged != null ? dragged.transform.position : Vector3.zero;
        }
    }
}
