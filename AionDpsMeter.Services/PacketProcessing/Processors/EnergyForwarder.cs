// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Processors
{
    /// <summary>Hands energy updates (wire bytes 0C 61) to the overlay. With no listener registered it does nothing.</summary>
    [PacketOpcode(0x610C)]
    internal sealed class EnergyForwarder(IEnumerable<IEnergyListener> listeners) : IOpcodeProcessor
    {
        public void Process(Packet packet)
        {
            foreach (var listener in listeners) listener.OnEnergyPacket(packet);
        }
    }
}
