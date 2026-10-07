// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>Receives energy update packets (wire bytes 0C 61).</summary>
    public interface IEnergyListener
    {
        void OnEnergyPacket(Packet packet);
    }
}
