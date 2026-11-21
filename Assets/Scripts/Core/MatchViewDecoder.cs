using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Overlink.Core
{
    /// <summary>Strictly decodes v2 guest views. Authentication is the caller's responsibility.</summary>
    public static class MatchViewDecoder
    {
        public const int MaxPayloadBytes = 16384;
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static bool TryDecode(byte[] payload, out MatchViewSnapshot snapshot)
        {
            snapshot = null;
            if (payload == null || payload.Length == 0 || payload.Length > MaxPayloadBytes) return false;
            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, Utf8))
                {
                    if (reader.ReadByte() != 2) return false;
                    long revision = reader.ReadInt64();
                    if (revision < 0 || reader.ReadByte() != (byte)PlayerIndex.Opponent) return false;
                    var active = ReadEnum<PlayerIndex>(reader);
                    var phase = ReadEnum<TurnPhase>(reader);
                    int turn = reader.ReadInt32();
                    if (turn < 0) return false;
                    var result = ReadEnum<MatchResult>(reader);
                    string reason = ReadString(reader);
                    bool setup = ReadBool(reader), flipped = ReadBool(reader);
                    var first = ReadEnum<PlayerIndex>(reader);
                    var ids = new HashSet<int>();
                    var players = new MatchViewSnapshot.PlayerView[2];
                    for (int i = 0; i < 2; i++) players[i] = ReadPlayer(reader, i == 1, ids);
                    if (stream.Position != stream.Length) return false;
                    snapshot = new MatchViewSnapshot(revision, active, phase, turn, result, reason,
                        setup, flipped, first, players);
                    return true;
                }
            }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
            catch (FormatException) { return false; }
        }

        static MatchViewSnapshot.PlayerView ReadPlayer(BinaryReader r, bool own, HashSet<int> ids)
        {
            int np = r.ReadInt32(), core = r.ReadInt32(), deck = r.ReadInt32(), handCount = r.ReadInt32();
            int offerCount = r.ReadInt32(), discard = r.ReadInt32(), rerolls = r.ReadInt32();
            if (deck < 0 || handCount < 0 || offerCount < 0 || discard < 0 || rerolls < 0)
                throw new FormatException();
            bool confirmed = ReadBool(r);
            var hand = ReadCards(r, ids);
            var offer = ReadCards(r, ids);
            var discarded = ReadCards(r, ids);
            if (discarded.Count != discard) throw new FormatException();
            if (own ? hand.Count != handCount || offer.Count != offerCount : hand.Count != 0 || offer.Count != 0)
                throw new FormatException();
            int boardCount = r.ReadByte();
            if (boardCount > Board.CoreActiveSlots + Board.CoreBenchSlots +
                Board.NodeCount * (Board.NodeActiveSlots + Board.NodeBenchSlots) + 1) throw new FormatException();
            var board = new List<MatchViewSnapshot.BoardCard>(boardCount);
            var slots = new HashSet<string>();
            for (int i = 0; i < boardCount; i++)
            {
                var type = ReadEnum<SlotType>(r);
                int index = r.ReadInt32(), node = r.ReadInt32();
                bool valid = type == SlotType.CoreActive ? node == -1 && index >= 0 && index < Board.CoreActiveSlots :
                    type == SlotType.CoreBench ? node == -1 && index >= 0 && index < Board.CoreBenchSlots :
                    type == SlotType.NodeActive ? node >= 0 && node < Board.NodeCount && index >= 0 && index < Board.NodeActiveSlots :
                    type == SlotType.NodeBench ? node >= 0 && node < Board.NodeCount && index >= 0 && index < Board.NodeBenchSlots :
                    type == SlotType.Protocol && node == -1 && index == 0;
                if (!valid || !slots.Add($"{(int)type}:{node}:{index}")) throw new FormatException();
                board.Add(new MatchViewSnapshot.BoardCard(type, index, node, ReadCard(r, ids)));
            }
            return new MatchViewSnapshot.PlayerView(np, core, deck, handCount, offerCount, discard, rerolls,
                confirmed, hand, offer, discarded, board);
        }

        static List<MatchViewSnapshot.CardView> ReadCards(BinaryReader r, HashSet<int> ids)
        {
            int count = r.ReadByte();
            var cards = new List<MatchViewSnapshot.CardView>(count);
            for (int i = 0; i < count; i++) cards.Add(ReadCard(r, ids));
            return cards;
        }

        static MatchViewSnapshot.CardView ReadCard(BinaryReader r, HashSet<int> ids)
        {
            int id = r.ReadInt32();
            string definition = ReadString(r);
            if (id <= 0 || !ids.Add(id) || definition.Length == 0) throw new FormatException();
            int attack = r.ReadInt32(), health = r.ReadInt32(), shield = r.ReadInt32();
            var state = ReadEnum<UnitState>(r);
            bool delay = ReadBool(r), cannotBlock = ReadBool(r);
            int nodeAttack = r.ReadInt32(), nodeHealth = r.ReadInt32();
            return new MatchViewSnapshot.CardView(id, definition, attack, health, shield, state,
                delay, cannotBlock, nodeAttack, nodeHealth);
        }

        static string ReadString(BinaryReader r)
        {
            int length = r.ReadByte();
            if (length > 128) throw new FormatException();
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Utf8.GetString(bytes);
        }

        static bool ReadBool(BinaryReader r)
        {
            byte b = r.ReadByte();
            if (b > 1) throw new FormatException();
            return b == 1;
        }

        static T ReadEnum<T>(BinaryReader r) where T : struct
        {
            var value = (T)Enum.ToObject(typeof(T), r.ReadByte());
            if (!Enum.IsDefined(typeof(T), value)) throw new FormatException();
            return value;
        }
    }
}
