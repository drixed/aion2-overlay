// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Processors
{
    /// <summary>Hands the field boss list (wire bytes 01 91) to the overlay's timers. Registered by upstream's
    /// assembly scan like every other processor; with no listener registered it does nothing.</summary>
    [PacketOpcode(0x9101)]
    internal sealed class FieldBossListForwarder(IEnumerable<IFieldBossListListener> listeners) : IOpcodeProcessor
    {
        public void Process(Packet packet)
        {
            foreach (var listener in listeners) listener.OnFieldBossList(packet);
        }
    }
}
