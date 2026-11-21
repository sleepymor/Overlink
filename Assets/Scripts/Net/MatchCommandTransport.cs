using System;
using System.IO;
using Overlink.Core;

namespace Overlink.Net
{
    /// <summary>Bounded request/response envelopes; transport supplies the authenticated sender.</summary>
    public static class MatchCommandTransport
    {
        public const int MaxRequestBytes = 8 + MatchCommandCodec.MaxPayloadBytes;
        public const int ResponseBytes = 18;

        public static byte[] CreateRequest(long requestId, long revision, GameCommand command)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            byte[] commandBytes = MatchCommandCodec.Encode(revision, command);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(requestId);
                writer.Write(commandBytes);
                return stream.ToArray();
            }
        }

        /// <summary>Malformed requests produce no response and do not reach the engine.</summary>
        public static bool TryExecute(byte[] request, ulong sender, AuthoritativeCommandBoundary boundary,
            out byte[] response, out GameCommand acceptedCommand)
        {
            response = null;
            acceptedCommand = null;
            if (boundary == null || request == null || request.Length < 18 || request.Length > MaxRequestBytes) return false;
            using (var stream = new MemoryStream(request, false))
            using (var reader = new BinaryReader(stream))
            {
                long requestId = reader.ReadInt64();
                byte[] commandBytes = reader.ReadBytes(request.Length - 8);
                if (requestId <= 0 || !MatchCommandCodec.TryDecode(commandBytes, PlayerIndex.Opponent,
                        out long expectedRevision, out GameCommand command)) return false;
                CommandResult result = boundary.Execute(sender, expectedRevision, command);
                if (result == CommandResult.Success) acceptedCommand = command;
                using (var output = new MemoryStream())
                using (var writer = new BinaryWriter(output))
                {
                    writer.Write((byte)1);
                    writer.Write(requestId);
                    writer.Write(boundary.Revision);
                    writer.Write((byte)result);
                    response = output.ToArray();
                }
                return true;
            }
        }

        public static bool TryReadResponse(byte[] payload, out long requestId, out long revision, out CommandResult result)
        {
            requestId = 0; revision = 0; result = CommandResult.InvalidCommand;
            if (payload == null || payload.Length != ResponseBytes) return false;
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadByte() != 1) return false;
                long id = reader.ReadInt64(), currentRevision = reader.ReadInt64();
                var code = (CommandResult)reader.ReadByte();
                if (id <= 0 || currentRevision < 0 || !Enum.IsDefined(typeof(CommandResult), code)) return false;
                requestId = id; revision = currentRevision; result = code;
                return true;
            }
        }
    }
}
