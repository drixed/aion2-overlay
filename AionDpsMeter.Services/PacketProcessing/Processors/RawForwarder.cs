// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Processors
{
    /// <summary>Hands frames upstream does not read to the fork (the opcode is read back from the frame). No listener, nothing done.</summary>
    [PacketOpcode(0x5611)] // 11 56: the character's equipment list at login
    internal sealed class RawForwarder(IEnumerable<IRawPacketListener> listeners) : IOpcodeProcessor
    {
        public void Process(Packet packet)
        {
            var data = packet.Data;
            var i = 0;
            while (i < data.Length && (data[i] & 0x80) != 0) i++;
            i++; // past the length varint
            if (i + 2 > data.Length) return;
            var opcode = (ushort)(data[i] | (data[i + 1] << 8));
            foreach (var listener in listeners) listener.OnRawPacket(opcode, packet);
        }
    }
}
