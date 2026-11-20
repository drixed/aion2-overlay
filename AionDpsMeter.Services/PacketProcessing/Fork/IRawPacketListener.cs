// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>Receives the raw frames the fork reads itself (the character's equipment at login: 11 56).</summary>
    public interface IRawPacketListener
    {
        void OnRawPacket(ushort opcode, Packet packet);
    }
}
