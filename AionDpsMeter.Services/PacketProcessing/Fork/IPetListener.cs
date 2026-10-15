// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>Receives pet packets: login list (00 90), souls (0D 90), exact souls (0E 90), level up (5C 8D).</summary>
    public interface IPetListener
    {
        void OnPetPacket(ushort opcode, Packet packet);
    }
}
