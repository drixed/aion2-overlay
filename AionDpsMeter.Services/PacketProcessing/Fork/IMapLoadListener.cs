// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>Receives map load packets (wire bytes 21 36).</summary>
    public interface IMapLoadListener
    {
        void OnMapLoad(Packet packet);
    }
}
