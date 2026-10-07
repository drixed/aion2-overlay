// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Processors
{
    /// <summary>Hands map loads (wire bytes 21 36) to the overlay. With no listener registered it does nothing.</summary>
    [PacketOpcode(0x3621)]
    internal sealed class MapLoadForwarder(IEnumerable<IMapLoadListener> listeners) : IOpcodeProcessor
    {
        public void Process(Packet packet)
        {
            foreach (var listener in listeners) listener.OnMapLoad(packet);
        }
    }
}
