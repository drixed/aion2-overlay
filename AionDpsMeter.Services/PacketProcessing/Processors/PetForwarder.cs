// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Processors
{
    /// <summary>Hands pet packets to the overlay (the opcode is read back from the frame). No listener, nothing done.</summary>
    [PacketOpcode(0x9000)]
    [PacketOpcode(0x900D)]
    [PacketOpcode(0x900E)]
    [PacketOpcode(0x9014)]
    internal sealed class PetForwarder(IEnumerable<IPetListener> listeners) : IOpcodeProcessor
    {
        public void Process(Packet packet)
        {
            var data = packet.Data;
            var i = 0;
            while (i < data.Length && (data[i] & 0x80) != 0) i++;
            i++; // past the length varint
            if (i + 2 > data.Length) return;
            var opcode = (ushort)(data[i] | (data[i + 1] << 8));
            foreach (var listener in listeners) listener.OnPetPacket(opcode, packet);
        }
    }
}
