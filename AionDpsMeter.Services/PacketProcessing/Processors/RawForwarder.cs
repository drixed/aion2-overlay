// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Processors
{
    /// <summary>Hands frames upstream does not read to the fork (the opcode is read back from the frame). No listener, nothing done.</summary>
    [PacketOpcode(0x5611)] // 11 56: the login list (equipment, inventory)
    [PacketOpcode(0x561B)] // 1B 56: an inventory change (kina, Abyss points)
    [PacketOpcode(0x371A)] [PacketOpcode(0x371B)] [PacketOpcode(0x371C)] [PacketOpcode(0x371E)] [PacketOpcode(0x3720)] // movements: the replay
    [PacketOpcode(0x3728)] [PacketOpcode(0x3729)] [PacketOpcode(0x372A)] [PacketOpcode(0x371F)] [PacketOpcode(0x372F)] [PacketOpcode(0x373C)]
    [PacketOpcode(0x3834)] // 34 38: the target the character selected (the pet line before the first hit)
    [PacketOpcode(0x3650)] // 50 36: another player's character window (inspect)
    [PacketOpcode(0x921C)] // 1C 92: a party member's place (names after a restart in the dungeon)
    [PacketOpcode(0x3809)] // 09 38: a skill being wound up (the boss cast overlay)
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
