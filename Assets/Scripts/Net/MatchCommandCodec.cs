using System;
using System.IO;
using Overlink.Core;

namespace Overlink.Net
{
    /// <summary>
    /// Bounded command wire format. The sender's seat is supplied by the authenticated
    /// receiver, never by the packet. Rules legality remains the engine's responsibility.
    /// </summary>
    public static class MatchCommandCodec
    {
        const byte Version = 1;
        public const int MaxPayloadBytes = 64;

        public static byte[] Encode(long revision, GameCommand command)
        {
            if (revision < 0 || command == null) throw new ArgumentException("Invalid command envelope.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(revision);
                writer.Write((byte)command.Type);
                switch (command)
                {
                    case DrawCardCommand _: case PassPhaseCommand _: case EndTurnCommand _:
                    case ConcedeCommand _: case RerollLineupCommand _: break;
                    case PlayCardCommand c:
                        WriteSlot(writer, c.CardInstanceId, c.TargetSlotType, c.SlotIndex, c.NodeIndex); break;
                    case SwitchPositionCommand c:
                        WriteSlot(writer, c.CardInstanceId, c.TargetSlotType, c.SlotIndex, c.NodeIndex); break;
                    case DeclareAttackCommand c: writer.Write(c.AttackerInstanceId); writer.Write(c.DefenderInstanceId); break;
                    case DeclareBlockCommand c: writer.Write(c.BlockerInstanceId); writer.Write(c.AttackerInstanceId); break;
                    case ActivateAbilityCommand c: writer.Write(c.CardInstanceId); break;
                    case RetreatCommand c: writer.Write(c.ActiveInstanceId); writer.Write(c.BenchInstanceId); break;
                    case MulliganCommand c: WriteIds(writer, c.CardInstanceIds, MatchState.StartingHandSize, false); break;
                    case ConfirmLineupCommand c: WriteIds(writer, c.ActiveInstanceIds, ConfirmLineupCommand.RequiredLength, true); break;
                    default: throw new ArgumentException("Unsupported command.", nameof(command));
                }
                return stream.ToArray();
            }
        }

        static void WriteSlot(BinaryWriter writer, int id, SlotType slot, int index, int node)
        {
            if (!Enum.IsDefined(typeof(SlotType), slot)) throw new ArgumentException("Invalid slot type.");
            writer.Write(id); writer.Write((byte)slot); writer.Write(index); writer.Write(node);
        }

        static void WriteIds(BinaryWriter writer, int[] ids, int max, bool exact)
        {
            if (ids == null || ids.Length > max || (exact && ids.Length != max))
                throw new ArgumentException("Invalid card ID count.");
            writer.Write((byte)ids.Length);
            foreach (int id in ids) writer.Write(id);
        }

        public static bool TryDecode(byte[] payload, PlayerIndex authenticatedSeat, out long revision, out GameCommand command)
        {
            revision = 0;
            command = null;
            if (payload == null || payload.Length < 10 || payload.Length > MaxPayloadBytes ||
                (authenticatedSeat != PlayerIndex.Player && authenticatedSeat != PlayerIndex.Opponent)) return false;
            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadByte() != Version) return false;
                    long value = reader.ReadInt64();
                    if (value < 0) return false;
                    CommandType type = (CommandType)reader.ReadByte();
                    GameCommand decoded;
                    switch (type)
                    {
                        case CommandType.DrawCard: decoded = new DrawCardCommand(authenticatedSeat); break;
                        case CommandType.PassPhase: decoded = new PassPhaseCommand(authenticatedSeat); break;
                        case CommandType.EndTurn: decoded = new EndTurnCommand(authenticatedSeat); break;
                        case CommandType.Concede: decoded = new ConcedeCommand(authenticatedSeat); break;
                        case CommandType.RerollLineup: decoded = new RerollLineupCommand(authenticatedSeat); break;
                        case CommandType.PlayCard:
                        case CommandType.SwitchPosition:
                            int id = reader.ReadInt32();
                            SlotType slot = (SlotType)reader.ReadByte();
                            if (!Enum.IsDefined(typeof(SlotType), slot)) return false;
                            int index = reader.ReadInt32(), node = reader.ReadInt32();
                            decoded = type == CommandType.PlayCard
                                ? (GameCommand)new PlayCardCommand(authenticatedSeat, id, slot, index, node)
                                : new SwitchPositionCommand(authenticatedSeat, id, slot, index, node);
                            break;
                        case CommandType.DeclareAttack: decoded = new DeclareAttackCommand(authenticatedSeat, reader.ReadInt32(), reader.ReadInt32()); break;
                        case CommandType.DeclareBlock: decoded = new DeclareBlockCommand(authenticatedSeat, reader.ReadInt32(), reader.ReadInt32()); break;
                        case CommandType.ActivateAbility: decoded = new ActivateAbilityCommand(authenticatedSeat, reader.ReadInt32()); break;
                        case CommandType.Retreat: decoded = new RetreatCommand(authenticatedSeat, reader.ReadInt32(), reader.ReadInt32()); break;
                        case CommandType.Mulligan:
                        case CommandType.ConfirmLineup:
                            int count = reader.ReadByte();
                            if (count > MatchState.StartingHandSize ||
                                (type == CommandType.ConfirmLineup && count != ConfirmLineupCommand.RequiredLength)) return false;
                            var ids = new int[count];
                            for (int i = 0; i < count; i++) ids[i] = reader.ReadInt32();
                            decoded = type == CommandType.Mulligan
                                ? (GameCommand)new MulliganCommand(authenticatedSeat, ids)
                                : new ConfirmLineupCommand(authenticatedSeat, ids);
                            break;
                        default: return false;
                    }
                    if (stream.Position != stream.Length) return false;
                    revision = value;
                    command = decoded;
                    return true;
                }
            }
            catch (EndOfStreamException) { return false; }
            catch (IOException) { return false; }
        }
    }
}
