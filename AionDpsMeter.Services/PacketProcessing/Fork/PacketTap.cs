// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>Every frame before it is routed, for the packet recorder in the settings (diagnostics only).</summary>
    public static class PacketTap
    {
        public static event Action<ushort, Packet>? Frame;

        public static void Publish(ushort opcode, Packet packet)
        {
            var handler = Frame;
            if (handler is null) return;
            try { handler(opcode, packet); }
            catch (Exception) { /* a recorder must never break the meter */ }
        }
    }
}
