// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>Receives the in-game map's field boss list packets (wire bytes 01 91).</summary>
    public interface IFieldBossListListener
    {
        void OnFieldBossList(Packet packet);
    }
}
