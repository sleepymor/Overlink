using System;
using System.IO;
using System.Text;
using Overlink.Core;

namespace Overlink.Net
{
    /// <summary>
    /// Versioned, bounded host-to-guest snapshot encoding. This is only an encoder:
    /// it must not be sent until a validating decoder and authenticated lifecycle exist.
    /// Player indices are the host's authoritative indices.
    /// </summary>
    public static class MatchViewEncoder
    {
        public const int MaxPayloadBytes = MatchViewDecoder.MaxPayloadBytes;
        const int MaxCardsPerZone = 255;
        const int MaxStringBytes = 128;
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] EncodeGuest(MatchViewSnapshot view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (view.Viewer != PlayerIndex.Opponent || view.Players.Count != 2 ||
                view.Players[0].Hand.Count != 0 || view.Players[0].LineupOffer.Count != 0)
                throw new ArgumentException("A redacted guest view is required.", nameof(view));

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Utf8))
            {
                writer.Write((byte)2);
                writer.Write(view.Revision);
                writer.Write((byte)view.Viewer);
                writer.Write((byte)view.ActivePlayer);
                writer.Write((byte)view.Phase);
                writer.Write(view.TurnNumber);
                writer.Write((byte)view.Result);
                WriteString(writer, view.ResultReason);
                writer.Write(view.SetupInProgress);
                writer.Write(view.CoinFlipped);
                writer.Write((byte)view.FirstPlayer);
                foreach (var player in view.Players)
                {
                    writer.Write(player.NP);
                    writer.Write(player.CoreDurability);
                    writer.Write(player.DeckCount);
                    writer.Write(player.HandCount);
                    writer.Write(player.OfferCount);
                    writer.Write(player.DiscardCount);
                    writer.Write(player.LineupRerolls);
                    writer.Write(player.LineupConfirmed);
                    WriteCards(writer, player.Hand);
                    WriteCards(writer, player.LineupOffer);
                    WriteCards(writer, player.Discard);
                    if (player.Board.Count > MaxCardsPerZone) throw new ArgumentException("Too many board cards.");
                    writer.Write((byte)player.Board.Count);
                    foreach (var slot in player.Board)
                    {
                        writer.Write((byte)slot.Slot);
                        writer.Write(slot.Index);
                        writer.Write(slot.NodeIndex);
                        WriteCard(writer, slot.Card);
                    }
                }
                if (stream.Length > MaxPayloadBytes) throw new ArgumentException("Snapshot is too large.", nameof(view));
                return stream.ToArray();
            }
        }

        static void WriteCards(BinaryWriter writer, System.Collections.Generic.IReadOnlyList<MatchViewSnapshot.CardView> cards)
        {
            if (cards.Count > MaxCardsPerZone) throw new ArgumentException("Too many cards in zone.");
            writer.Write((byte)cards.Count);
            foreach (var card in cards) WriteCard(writer, card);
        }

        static void WriteCard(BinaryWriter writer, MatchViewSnapshot.CardView card)
        {
            writer.Write(card.InstanceId);
            WriteString(writer, card.DefinitionId);
            writer.Write(card.Attack); writer.Write(card.Health); writer.Write(card.Shield);
            writer.Write((byte)card.State);
            writer.Write(card.SummoningDelay); writer.Write(card.CannotBeBlocked);
            writer.Write(card.NodeAtkBonus); writer.Write(card.NodeHpBonus);
        }

        static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Utf8.GetBytes(value ?? string.Empty);
            if (bytes.Length > MaxStringBytes) throw new ArgumentException("Snapshot string is too long.");
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
    }
}
